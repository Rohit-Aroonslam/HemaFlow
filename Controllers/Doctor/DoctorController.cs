using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Project.Data;
using Project.Models.Doctor;
using Project.Models.Entities;
using Project.Services;
using DocResultRow = Project.Models.Doctor.TestResultRow;
using GenUser = Project.Models.Entities.User;

namespace Project.Controllers.Doctor
{
    // Everything on the doctor side now reads/writes the real database (no static lists any more).
    // A doctor only ever sees and changes THEIR OWN test requests; the only other patient data they can
    // see is a patient's medical record and, with the patient's consent, other doctors' released results.
    [Authorize(Roles = "Doctor")]
    public class DoctorController : Controller
    {
        // TestRequestStatus ids (see database/019_workflow_upgrade.sql)
        private const byte StSubmitted = 1, StInProgress = 2, StCompleted = 3, StCancelled = 4, StSamplesReceived = 5, StReleased = 6;

        private readonly ApplicationDbContext _ctx;
        private readonly EmailService _email;
        private readonly PdfService _pdf;
        private readonly ILogger<DoctorController> _log;

        public DoctorController(ApplicationDbContext ctx, EmailService email, PdfService pdf, ILogger<DoctorController> log)
        {
            _ctx = ctx;
            _email = email;
            _pdf = pdf;
            _log = log;
        }

        // ───────────────────────── who is logged in ─────────────────────────
        private int DoctorId => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        private DoctorProfile? _doc;
        private DoctorProfile? Doc => _doc ??= _ctx.DoctorProfiles.Include(d => d.User)
            .FirstOrDefault(d => d.UserId == DoctorId && !d.IsArchived);

        private string DoctorFullName => Doc == null ? "" : $"{Doc.User.FirstName} {Doc.User.Surname}";

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);
            if (User.Identity?.IsAuthenticated == true)
            {
                if (Doc == null)
                {
                    // Logged in as a Doctor but no (active) doctor profile exists - send them back to login.
                    context.Result = RedirectToAction("Login", "Account");
                    return;
                }
                ViewBag.DoctorName = DoctorFullName;
            }
        }

        // ───────────────────────── small helpers ─────────────────────────
        private static string StatusLabel(byte id) => id switch
        {
            StSubmitted => "Submitted",
            StSamplesReceived => "In Progress",   // lab has the samples
            StInProgress => "In Progress",
            StCompleted => "Awaiting Release",    // lab finished, the doctor has not released it yet
            StReleased => "Completed",            // released to the patient
            StCancelled => "Cancelled",
            _ => "Submitted"
        };

        private static string UrgencyLabel(string? code) =>
            string.Equals(code, "stat", StringComparison.OrdinalIgnoreCase) ? "STAT"
            : string.Equals(code, "urgent", StringComparison.OrdinalIgnoreCase) ? "Urgent" : "Routine";

        private static bool IsAbnormal(decimal v, decimal? min, decimal? max) =>
            (min.HasValue && v < min.Value) || (max.HasValue && v > max.Value);

        private static string Num(decimal v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        private static string H(string? s) => WebUtility.HtmlEncode(s ?? "");

        private static readonly Regex EmailRx = new(@"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9\-]+(\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}$", RegexOptions.Compiled);

        // "REQ-000123" style number shown everywhere
        private static string ReqNo(int id) => "REQ-" + id.ToString("D3");

        private void LogStaff(string action, string detail)
        {
            try
            {
                _ctx.StaffActivityLogs.Add(new StaffActivityLog
                {
                    ActorId = DoctorId,
                    ActorRole = Doc!.User.RoleId,
                    Action = action,
                    Detail = detail,
                    LogTypeId = 1
                });
                _ctx.SaveChanges();
            }
            catch (Exception ex) { _log.LogWarning(ex, "Could not write staff activity log"); }
        }

        private void LogPatient(int patientUserId, string action, string detail, string icon)
        {
            try
            {
                _ctx.PatientActivityLogs.Add(new PatientActivityLog
                {
                    PatientUserId = patientUserId, Action = action, Detail = detail, Icon = icon, LogTypeId = 1
                });
                _ctx.SaveChanges();
            }
            catch (Exception ex) { _log.LogWarning(ex, "Could not write patient activity log"); }
        }

        // Sends an e-mail; never throws. Returns an error text or null when it worked.
        private string? TrySend(string to, string subject, string html, byte[]? pdf = null, string? pdfName = null)
        {
            try { _email.SendEmail(to, subject, html, pdf, pdfName); return null; }
            catch (Exception ex)
            {
                _log.LogError(ex, "E-mail to {To} failed", to);
                return "the e-mail could not be sent";
            }
        }

        // Makes the doctor show on the patient's Doctor Access page (and the patient under "My Patients").
        //  - registering a brand-new patient: full-access consent (the patient was created by this doctor);
        //  - ordering for an EXISTING patient: access to just the tests this doctor ordered (their own requests
        //    anyway), so the patient's earlier results are never opened up without the patient's own choice.
        // If the patient has revoked this doctor, nothing is changed, so a revoke is never silently undone.
        private void EnsureTreatingDoctorConsent(int patientUserId, bool fullAccess, IEnumerable<TestRequestItem>? orderedItems = null)
        {
            var consent = _ctx.PatientDoctorConsents.FirstOrDefault(c => c.PatientUserId == patientUserId && c.DoctorUserId == DoctorId);
            bool created = false;
            if (consent == null)
            {
                if (_ctx.PatientDoctorConsents.Count(c => c.PatientUserId == patientUserId && c.RevokedOn == null) >= 5) return;   // the patient's own limit
                consent = new PatientDoctorConsent { PatientUserId = patientUserId, DoctorUserId = DoctorId, GrantedOn = DateTime.UtcNow };
                _ctx.PatientDoctorConsents.Add(consent);
                created = true;
            }
            else if (consent.RevokedOn != null) return;

            if (!fullAccess && orderedItems != null)
            {
                // Full access = no per-test rows. Only add rows when this consent is (or is being created as) per-test.
                bool hasRows = !created && _ctx.ConsentedTestRequests.Any(r => r.PatientDoctorConsentId == consent.PatientDoctorConsentId);
                if (created || hasRows)
                    foreach (var it in orderedItems)
                        _ctx.ConsentedTestRequests.Add(new ConsentedTestRequest { PatientDoctorConsent = consent, TestRequestItemId = it.TestRequestItemId, IsConsented = true });
            }
            _ctx.SaveChanges();
            if (created)
                LogPatient(patientUserId, "Doctor added", $"Dr {DoctorFullName} was added to your doctors. You can change this under Doctor Access.", "bi-person-check-fill");
        }

        private IQueryable<TestRequest> MyRequests() =>
            _ctx.TestRequests.AsNoTracking().Where(r => r.DoctorUserId == DoctorId);

        // Builds the list rows (one per request) for any set of requests.
        private List<DoctorRequestItem> LoadRequestRows(IQueryable<TestRequest> query, int? take = null)
        {
            var ordered = query.OrderByDescending(r => r.RequestedOn).ThenByDescending(r => r.TestRequestId);
            var page = (take.HasValue ? ordered.Take(take.Value) : ordered)
                .Select(r => new
                {
                    r.TestRequestId, r.PatientUserId, r.RequestedOn, r.TestRequestStatusId,
                    Urgency = r.Urgency.Code,
                    IdNumber = r.PatientUser.IdNumber,
                    First = r.PatientUser.User.FirstName, Sur = r.PatientUser.User.Surname, Email = r.PatientUser.User.Email,
                    r.Notes, r.DoctorNote, r.ReleasedOn, r.CancelReason
                }).ToList();
            if (page.Count == 0) return new();

            var ids = page.Select(p => p.TestRequestId).ToList();
            var items = _ctx.TestRequestItems.AsNoTracking().Where(i => ids.Contains(i.TestRequestId))
                .Select(i => new { i.TestRequestId, Test = i.TestType.Name, Cat = i.TestType.TestCategory.Name })
                .ToList().ToLookup(i => i.TestRequestId);
            var results = _ctx.TestResults.AsNoTracking().Where(t => ids.Contains(t.TestRequestItem.TestRequestId))
                .Select(t => new { t.TestRequestItem.TestRequestId, t.ResultValue, t.NormalMinAtTest, t.NormalMaxAtTest })
                .ToList().ToLookup(t => t.TestRequestId);

            return page.Select(p =>
            {
                var its = items[p.TestRequestId].ToList();
                var res = results[p.TestRequestId].ToList();
                var label = StatusLabel(p.TestRequestStatusId);
                return new DoctorRequestItem
                {
                    TestRequestId = p.TestRequestId,
                    PatientUserId = p.PatientUserId,
                    PatientName = $"{p.First} {p.Sur}".Trim(),
                    PatientEmail = p.Email,
                    PatientIdNumber = p.IdNumber,
                    TestName = string.Join(", ", its.Select(i => i.Test)),
                    Category = string.Join(", ", its.Select(i => i.Cat).Distinct()),
                    ItemCount = its.Count,
                    OrderDate = SastTime.ToSast(p.RequestedOn),
                    Urgency = UrgencyLabel(p.Urgency),
                    Status = label,
                    StatusKey = label.ToLowerInvariant(),
                    ResultCount = res.Count,
                    AbnormalCount = res.Count(r => IsAbnormal(r.ResultValue, r.NormalMinAtTest, r.NormalMaxAtTest)),
                    ReleasedOn = p.ReleasedOn.HasValue ? SastTime.ToSast(p.ReleasedOn.Value) : null,
                    ClinicalNotes = p.Notes,
                    DoctorNote = p.DoctorNote,
                    CancelReason = p.CancelReason,
                    CanCancel = p.TestRequestStatusId == StSubmitted || p.TestRequestStatusId == StSamplesReceived,
                    CanRelease = p.TestRequestStatusId == StCompleted
                };
            }).ToList();
        }

        // Abnormal results (on this doctor's requests) captured in a UTC window.
        private IQueryable<TestResult> AbnormalResults(DateTime fromUtc, DateTime toUtcExclusive) =>
            _ctx.TestResults.AsNoTracking().Where(t =>
                t.TestRequestItem.TestRequest.DoctorUserId == DoctorId
                && t.TestRequestItem.TestRequest.TestRequestStatusId != StCancelled
                && t.RecordedOn >= fromUtc && t.RecordedOn < toUtcExclusive
                && ((t.NormalMinAtTest != null && t.ResultValue < t.NormalMinAtTest)
                    || (t.NormalMaxAtTest != null && t.ResultValue > t.NormalMaxAtTest)));

        private static DateTime SastToday => SastTime.ToSast(DateTime.UtcNow).Date;

        // ═══════════════════════════ DASHBOARD ═══════════════════════════
        public IActionResult Index()
        {
            var counts = MyRequests().GroupBy(r => r.TestRequestStatusId)
                .Select(g => new { Id = g.Key, N = g.Count() }).ToList();
            int C(params byte[] ids) => counts.Where(c => ids.Contains(c.Id)).Sum(c => c.N);

            var today = SastToday;
            var alertCount = AbnormalResults(SastTime.SastDateToUtc(today.AddDays(-5)), SastTime.SastDateToUtc(today.AddDays(1))).Count();

            return View(new DoctorDashboardViewModel
            {
                DoctorSurname = Doc!.User.Surname,
                SubmittedCount = C(StSubmitted),
                InProgressCount = C(StInProgress, StSamplesReceived),
                AwaitingReleaseCount = C(StCompleted),
                AlertCount = alertCount,
                TotalPatientsCount = MyRequests().Select(r => r.PatientUserId).Distinct().Count(),
                RecentRequests = LoadRequestRows(MyRequests(), 6)
            });
        }

        // ═══════════════════════════ PATIENTS ═══════════════════════════
        // "My patients" = patients this doctor has ordered tests for, or who have given this doctor access.
        public IActionResult Patients()
        {
            var linked = MyRequests().Select(r => r.PatientUserId).Distinct().ToList()
                .Union(_ctx.PatientDoctorConsents.AsNoTracking()
                    .Where(c => c.DoctorUserId == DoctorId && c.RevokedOn == null).Select(c => c.PatientUserId).ToList())
                .ToList();

            var list = _ctx.PatientProfiles.AsNoTracking().Include(p => p.User)
                .Where(p => !p.IsArchived && linked.Contains(p.UserId))
                .OrderBy(p => p.User.Surname).ThenBy(p => p.User.FirstName).ToList();
            return View(list);
        }

        // Looks a patient up by SA ID number (any registered patient) and opens the record.
        [HttpGet]
        public IActionResult FindPatient(string? idNumber)
        {
            var id = (idNumber ?? "").Trim();
            var p = id.Length == 0 ? null : _ctx.PatientProfiles.AsNoTracking().FirstOrDefault(x => x.IdNumber == id && !x.IsArchived);
            if (p == null)
            {
                TempData["Error"] = "No patient was found with that ID number. Use Add New Patient to register them.";
                return RedirectToAction(nameof(Patients));
            }
            return RedirectToAction(nameof(PatientProfile), new { id = p.UserId });
        }

        public IActionResult PatientProfile(int id)
        {
            var patient = _ctx.PatientProfiles.AsNoTracking()
                .Include(p => p.User)
                .Include(p => p.PatientConditions).ThenInclude(c => c.Condition)
                .Include(p => p.PatientAllergies).ThenInclude(a => a.Allergy)
                .Include(p => p.PatientMedications).ThenInclude(m => m.Medication)
                .FirstOrDefault(p => p.UserId == id && !p.IsArchived);
            if (patient == null) { TempData["Error"] = "That patient could not be found."; return RedirectToAction(nameof(Patients)); }

            ViewBag.Requests = LoadRequestRows(MyRequests().Where(r => r.PatientUserId == id), 10);
            return View(patient);
        }

        // ═══════════════════════════ ADD PATIENT ═══════════════════════════
        [HttpGet]
        public IActionResult AddPatient() => View(new DoctorAddPatientModel());

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult AddPatient(DoctorAddPatientModel m)
        {
            IActionResult Fail(string msg) { TempData["Error"] = msg; return View(m); }

            m.FirstName = (m.FirstName ?? "").Trim(); m.Surname = (m.Surname ?? "").Trim();
            m.SaIdNumber = (m.SaIdNumber ?? "").Trim(); m.Email = (m.Email ?? "").Trim(); m.Phone = (m.Phone ?? "").Trim();

            if (m.FirstName.Length == 0 || m.Surname.Length == 0) return Fail("First name and surname are required.");
            if (!new Project.Models.LabManager.SouthAfricanIdAttribute().IsValid(m.SaIdNumber)) return Fail("Please enter a valid 13-digit South African ID number.");
            if (!EmailRx.IsMatch(m.Email)) return Fail("Enter a valid email address, for example name@example.com.");
            if (!Regex.IsMatch(m.Phone, @"^[0-9+ ()\-]{10,15}$")) return Fail("Enter a valid cellphone number, for example 082 123 4567.");
            if (!m.DateOfBirth.HasValue) return Fail("Date of birth is required.");

            // The date of birth must agree with the ID number (YYMMDD) and cannot be in the future.
            var dobPicked = m.DateOfBirth.Value.Date;
            if (dobPicked > SastToday) return Fail("Date of birth cannot be in the future.");
            if (dobPicked.ToString("yyMMdd") != m.SaIdNumber[..6]) return Fail("The date of birth does not match the ID number.");

            if (_ctx.Users.Any(u => u.Email == m.Email)) return Fail("An account with this email address already exists.");
            if (_ctx.PatientProfiles.Any(p => p.IdNumber == m.SaIdNumber)) return Fail("A patient with this ID number is already registered.");

            var role = _ctx.Roles.FirstOrDefault(r => r.Code == "Patient");
            if (role == null) return Fail("Patient registration is temporarily unavailable.");

            var suburbId = LocationHelper.ResolveOrCreate(_ctx, m.SuburbId, m.ProvinceId, m.City, m.Suburb, m.PostalCode, out var addrError);
            if (suburbId == null) return Fail(addrError ?? "Please check the address.");

            var street = (m.Street ?? "").Trim();
            if (street.Length == 0) return Fail("Street address is required.");
            var complex = (m.Complex ?? "").Trim();
            if (complex.Length > 100) return Fail("The complex / building name can be at most 100 characters.");

            var seq = _ctx.PatientProfiles.Count() + 1;
            var code = $"PAT-{seq:D4}";
            while (_ctx.Users.Any(u => u.UserCode == code) || _ctx.PatientProfiles.Any(p => p.PatientCode == code)) { seq++; code = $"PAT-{seq:D4}"; }

            var tempPassword = PasswordPolicy.GenerateTemporary();
            using var tx = _ctx.Database.BeginTransaction();
            try
            {
                var user = new GenUser
                {
                    RoleId = role.RoleId, FirstName = m.FirstName, Surname = m.Surname, Email = m.Email, Phone = m.Phone,
                    UserCode = code, Identifier = m.SaIdNumber, Status = "Active", IsArchived = false,
                    MustChangePassword = true, PasswordHash = ""
                };
                user.PasswordHash = new PasswordHasher<GenUser>().HashPassword(user, tempPassword);
                _ctx.Users.Add(user);
                _ctx.SaveChanges();

                _ctx.PatientProfiles.Add(new PatientProfile
                {
                    UserId = user.UserId, PatientCode = code, IdNumber = m.SaIdNumber,
                    DateOfBirth = DateOnly.FromDateTime(dobPicked), AddressLine1 = street.Length > 150 ? street[..150] : street, ComplexName = complex.Length == 0 ? null : complex, SuburbId = suburbId
                });
                _ctx.SaveChanges();
                EnsureTreatingDoctorConsent(user.UserId, true);

                var loginUrl = Url.Action("Login", "Account", null, Request.Scheme);
                var body = $@"<p>Dear {H(m.FirstName)},</p>
                    <p>Dr {H(DoctorFullName)} has registered you on <strong>HemaFlow</strong>, where you can view your test results.</p>
                    <p><strong>Login email:</strong> {H(m.Email)}<br/><strong>Temporary password:</strong> {H(tempPassword)}</p>
                    <p>You will be asked to choose your own password the first time you sign in.</p>
                    <p><a href=""{H(loginUrl)}"">Sign in to HemaFlow</a></p>";
                _email.SendEmail(m.Email, "Welcome to HemaFlow - your account details", body);

                tx.Commit();
                LogStaff("Patient registered", $"{m.FirstName} {m.Surname} ({code})");
                TempData["Success"] = $"{m.FirstName} {m.Surname} was registered and a temporary password was e-mailed to {m.Email}.";
                return RedirectToAction(nameof(CreateTestRequest), new { patientId = user.UserId });
            }
            catch (Exception ex)
            {
                tx.Rollback();
                _log.LogError(ex, "Doctor {Doctor} could not register patient {Email}", DoctorId, m.Email);
                return Fail("The patient could not be registered, because the welcome e-mail with the temporary password could not be sent. Nothing was saved - please check the e-mail address and try again.");
            }
        }

        // ═══════════════════════════ NEW TEST REQUEST ═══════════════════════════
        private static string TubeColour(string sampleType)
        {
            var n = (sampleType ?? "").ToLowerInvariant();
            if (n.Contains("edta") || n.Contains("whole blood")) return "#8B5CF6";
            if (n.Contains("citrat")) return "#3B82F6";
            if (n.Contains("serum") || n.Contains("sst")) return "#F59E0B";
            if (n.Contains("plasma") || n.Contains("heparin")) return "#10B981";
            if (n.Contains("marrow")) return "#EF4444";
            if (n.Contains("digital") || n.Contains("lead")) return "#EC4899";
            if (n.Contains("spirom") || n.Contains("breath") || n.Contains("airflow")) return "#0D9488";
            if (n.Contains("urine")) return "#EAB308";
            var palette = new[] { "#6366F1", "#F97316", "#84CC16", "#14B8A6", "#D97706" };
            int h = 17; foreach (var ch in n) h = unchecked(h * 31 + ch);   // stable (string.GetHashCode changes per run)
            return palette[(h & 0x7fffffff) % palette.Length];
        }

        private CreateTestRequestViewModel BuildRequestForm(int? patientId = null)
        {
            var patients = _ctx.PatientProfiles.AsNoTracking().Include(p => p.User)
                .Where(p => !p.IsArchived && !p.User.IsArchived)
                .OrderBy(p => p.User.Surname).ThenBy(p => p.User.FirstName).ToList()
                .Select(p => new PatientItemViewModel
                {
                    PatientId = p.UserId, Name = p.User.FirstName, Surname = p.User.Surname, SaIdNumber = p.IdNumber,
                    DateOfBirth = p.DateOfBirth?.ToString("yyyy-MM-dd") ?? "", ContactNumber = p.User.Phone ?? ""
                }).ToList();

            var categories = _ctx.TestCategories.AsNoTracking().Where(c => !c.IsArchived)
                .OrderBy(c => c.Name)
                .Select(c => new
                {
                    c.TestCategoryId, c.Name,
                    Tests = c.TestTypes.Where(t => !t.IsArchived).OrderBy(t => t.Name)
                        .Select(t => new { t.TestTypeId, t.Name, Sample = t.SampleType.Name }).ToList()
                }).ToList()
                .Where(c => c.Tests.Count > 0)
                .Select(c => new TestCategoryViewModel
                {
                    Name = c.Name, Slug = "cat-" + c.TestCategoryId,
                    TestTypes = c.Tests.Select(t => new TestTypeOptionViewModel
                    { Id = t.TestTypeId, Name = t.Name, SampleType = t.Sample, SampleColor = TubeColour(t.Sample) }).ToList()
                }).ToList();

            return new CreateTestRequestViewModel { Patients = patients, Categories = categories, PatientId = patientId ?? 0 };
        }

        [HttpGet]
        public IActionResult CreateTestRequest(int? patientId) => View(BuildRequestForm(patientId));

        // What the doctor may see about a patient while writing a request: the medical record, plus previous results.
        // Own requests are always visible; other doctors' RELEASED results only with the patient's consent.
        [HttpGet]
        public IActionResult PatientSummary(int id)
        {
            var p = _ctx.PatientProfiles.AsNoTracking()
                .Include(x => x.PatientConditions).ThenInclude(c => c.Condition)
                .Include(x => x.PatientAllergies).ThenInclude(a => a.Allergy)
                .Include(x => x.PatientMedications).ThenInclude(m => m.Medication)
                .FirstOrDefault(x => x.UserId == id && !x.IsArchived);
            if (p == null) return NotFound();

            var consentedItemIds = (from cr in _ctx.ConsentedTestRequests.AsNoTracking()
                                    join c in _ctx.PatientDoctorConsents.AsNoTracking() on cr.PatientDoctorConsentId equals c.PatientDoctorConsentId
                                    where c.PatientUserId == id && c.DoctorUserId == DoctorId && c.RevokedOn == null && cr.IsConsented
                                    select cr.TestRequestItemId).ToList();
            var hasConsent = _ctx.PatientDoctorConsents.AsNoTracking().Any(c => c.PatientUserId == id && c.DoctorUserId == DoctorId && c.RevokedOn == null);

            var rows = _ctx.TestResults.AsNoTracking()
                .Where(t => t.TestRequestItem.TestRequest.PatientUserId == id
                    && t.TestRequestItem.TestRequest.TestRequestStatusId != StCancelled
                    && (t.TestRequestItem.TestRequest.DoctorUserId == DoctorId
                        || (t.TestRequestItem.TestRequest.TestRequestStatusId == StReleased && consentedItemIds.Contains(t.TestRequestItemId))))
                .OrderByDescending(t => t.RecordedOn).Take(40)
                .Select(t => new
                {
                    t.TestRequestItem.TestRequestId, t.RecordedOn, Test = t.TestRequestItem.TestType.Name,
                    Cat = t.TestRequestItem.TestType.TestCategory.Name, t.ResultValue, t.Unit, t.NormalMinAtTest, t.NormalMaxAtTest,
                    St = t.TestRequestItem.TestRequest.TestRequestStatusId,
                    Doc = t.TestRequestItem.TestRequest.DoctorUser.User.Surname, DocId = t.TestRequestItem.TestRequest.DoctorUserId
                }).ToList();

            return Json(new
            {
                allergies = p.PatientAllergies.Select(a => a.Allergy.Name + (string.IsNullOrEmpty(a.Severity) ? "" : $" ({a.Severity})")).ToList(),
                conditions = p.PatientConditions.Select(c => c.Condition.Name).ToList(),
                medications = p.PatientMedications.Select(m => m.Medication.Name + (string.IsNullOrWhiteSpace(m.Strength) ? "" : " " + m.Strength)).ToList(),
                hasGivenConsent = hasConsent,
                testHistory = rows.Select(t => new
                {
                    testRequestId = t.TestRequestId,
                    orderDate = SastTime.ToSast(t.RecordedOn).ToString("yyyy-MM-dd"),
                    testName = t.Test, category = t.Cat,
                    resultValue = Num(t.ResultValue) + (string.IsNullOrWhiteSpace(t.Unit) ? "" : " " + t.Unit),
                    referenceRange = RangeText(t.NormalMinAtTest, t.NormalMaxAtTest),
                    status = StatusLabel(t.St),
                    isAbnormal = IsAbnormal(t.ResultValue, t.NormalMinAtTest, t.NormalMaxAtTest),
                    orderedByDoctor = "Dr " + t.Doc,
                    isOrderedByCurrentDoctor = t.DocId == DoctorId
                }).ToList()
            });
        }

        private static string RangeText(decimal? min, decimal? max) => (min, max) switch
        {
            (not null, not null) => $"{Num(min.Value)} - {Num(max.Value)}",
            (not null, null) => $"> {Num(min.Value)}",
            (null, not null) => $"< {Num(max.Value)}",
            _ => "-"
        };

        private string NextBarcode(HashSet<string> usedNow)
        {
            for (int i = 0; i < 20; i++)
            {
                var code = $"HF-{SastToday:yyMMdd}-{Random.Shared.Next(100000, 999999)}";
                if (usedNow.Contains(code) || _ctx.RequestSamples.Any(s => s.BarcodeLabel == code)) continue;
                usedNow.Add(code);
                return code;
            }
            throw new InvalidOperationException("Could not generate a unique barcode.");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult CreateTestRequest(CreateTestRequestViewModel model)
        {
            IActionResult Fail(string msg)
            {
                TempData["Error"] = msg;
                var again = BuildRequestForm(model.PatientId);
                again.Urgency = model.Urgency; again.ClinicalNotes = model.ClinicalNotes; again.TestTypeIds = model.TestTypeIds ?? new(); again.Barcodes = model.Barcodes ?? new();
                return View(again);
            }

            var patient = _ctx.PatientProfiles.Include(p => p.User)
                .FirstOrDefault(p => p.UserId == model.PatientId && !p.IsArchived && !p.User.IsArchived);
            if (patient == null) return Fail("Please select a patient.");

            var typeIds = (model.TestTypeIds ?? new()).Distinct().ToList();
            if (typeIds.Count == 0) return Fail("Select at least one test.");
            var types = _ctx.TestTypes.Include(t => t.TestCategory).Include(t => t.SampleType)
                .Where(t => typeIds.Contains(t.TestTypeId) && !t.IsArchived).ToList();
            if (types.Count != typeIds.Count) return Fail("One of the selected tests is no longer available. Please reselect your tests.");

            var urgency = _ctx.Urgencies.ToList()
                .FirstOrDefault(u => string.Equals(u.Code, model.Urgency, StringComparison.OrdinalIgnoreCase));
            if (urgency == null) return Fail("Select an urgency (Routine, Urgent or STAT).");

            var notes = (model.ClinicalNotes ?? "").Trim();
            if (notes.Length > 1000) return Fail("Clinical notes can be at most 1000 characters.");

            // The form shows one barcode per test, grouped by sample type. Hand them out per sample type in the
            // order they were posted; anything missing, malformed or already used gets a fresh server-side barcode.
            var posted = (model.Barcodes ?? new()).Where(b => b != null && !string.IsNullOrWhiteSpace(b.BarcodeNumber))
                .Select(b => new { Type = (b.SampleType ?? "").Trim(), Code = b.BarcodeNumber.Trim() }).ToList();
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var postedCodes = posted.Select(p => p.Code).Distinct().ToList();
            var existing = new HashSet<string>(
                _ctx.RequestSamples.AsNoTracking().Where(x => postedCodes.Contains(x.BarcodeLabel)).Select(x => x.BarcodeLabel).ToList(),
                StringComparer.OrdinalIgnoreCase);
            string TakeBarcode(string sampleTypeName)
            {
                var hit = posted.FirstOrDefault(p => string.Equals(p.Type, sampleTypeName, StringComparison.OrdinalIgnoreCase)
                    && p.Code.Length <= 40 && Regex.IsMatch(p.Code, @"^[A-Za-z0-9\-]+$")
                    && !used.Contains(p.Code) && !existing.Contains(p.Code));
                if (hit != null) { used.Add(hit.Code); posted.Remove(hit); return hit.Code; }
                return NextBarcode(used);
            }

            var request = new TestRequest
            {
                PatientUserId = patient.UserId,
                DoctorUserId = DoctorId,
                TestRequestStatusId = StSubmitted,
                UrgencyId = urgency.UrgencyId,
                RequestedOn = DateTime.UtcNow,
                Notes = notes.Length == 0 ? null : notes
            };
            foreach (var t in types.OrderBy(t => t.SampleType.Name).ThenBy(t => t.Name))
            {
                var item = new TestRequestItem { TestTypeId = t.TestTypeId, TestRequestItemStatusId = 1 };
                item.RequestSamples.Add(new RequestSample
                {
                    SampleTypeId = t.SampleTypeId,
                    BarcodeLabel = TakeBarcode(t.SampleType.Name),
                    CollectedOn = DateTime.UtcNow
                });
                request.TestRequestItems.Add(item);
            }
            _ctx.TestRequests.Add(request);
            _ctx.SaveChanges();
            EnsureTreatingDoctorConsent(patient.UserId, false, request.TestRequestItems);

            LogStaff("Test request created", $"{ReqNo(request.TestRequestId)} for {patient.User.FirstName} {patient.User.Surname} ({types.Count} test(s), {UrgencyLabel(urgency.Code)})");
            LogPatient(patient.UserId, "Test request submitted", $"Dr {DoctorFullName} requested: {string.Join(", ", types.Select(t => t.Name))}.", "bi-clipboard2-pulse-fill");

            var mailBody = $@"<p>Dear {H(patient.User.FirstName)},</p>
                <p>Dr {H(DoctorFullName)} has submitted a laboratory test request for you ({ReqNo(request.TestRequestId)}, {UrgencyLabel(urgency.Code)}):</p>
                <ul>{string.Join("", types.Select(t => $"<li>{H(t.Name)}</li>"))}</ul>
                <p>Please visit the laboratory to give your sample. You will be notified when your results are ready.</p>";
            var mailErr = TrySend(patient.User.Email, $"Test request {ReqNo(request.TestRequestId)} submitted", mailBody);

            TempData["SuccessMessage"] = mailErr == null
                ? $"{ReqNo(request.TestRequestId)} was submitted to the lab and {patient.User.FirstName} has been notified by e-mail."
                : $"{ReqNo(request.TestRequestId)} was submitted to the lab, but {mailErr} - please tell the patient yourself.";
            return RedirectToAction(nameof(TrackRequests));
        }

        // ═══════════════════════════ TRACK REQUESTS ═══════════════════════════
        public IActionResult TrackRequests() => View(LoadRequestRows(MyRequests()));

        private DoctorRequestItem? LoadRequestDetail(int id)
        {
            var row = LoadRequestRows(MyRequests().Where(r => r.TestRequestId == id)).FirstOrDefault();
            if (row == null) return null;

            var items = _ctx.TestRequestItems.AsNoTracking().Where(i => i.TestRequestId == id)
                .Select(i => new
                {
                    Test = i.TestType.Name, Cat = i.TestType.TestCategory.Name, Sample = i.TestType.SampleType.Name,
                    St = i.TestRequestItemStatus.Code,
                    Samples = i.RequestSamples.Select(s => new { s.BarcodeLabel, SampleType = s.SampleType.Name, s.CollectedByUserId, s.CollectedOn }).ToList()
                }).ToList();
            row.Tests = items.Select(i => new DoctorRequestTestLine { TestName = i.Test, Category = i.Cat, SampleType = i.Sample, Status = ItemStatusLabel(i.St) }).ToList();
            row.Samples = items.SelectMany(i => i.Samples.Select(s => new DoctorRequestSampleLine
            {
                Barcode = s.BarcodeLabel, SampleType = s.SampleType, TestName = i.Test,
                ReceivedOn = s.CollectedByUserId.HasValue ? SastTime.ToSast(s.CollectedOn) : null
            })).ToList();
            return row;
        }

        private static string ItemStatusLabel(string code) => code switch
        {
            "Awaiting" => "Awaiting sample",
            "SampleCollected" => "Sample received",
            "Testing" => "In progress",
            "Verified" => "Verified",
            "Rejected" => "Rejected",
            "Completed" => "Completed",
            "ToBeReviewed" => "To be reviewed",
            _ => code
        };

        public IActionResult RequestDetails(int id)
        {
            var r = LoadRequestDetail(id);
            if (r == null) { TempData["Error"] = "That request could not be found."; return RedirectToAction(nameof(TrackRequests)); }
            return View(r);
        }

        // A doctor may cancel only while the request is Submitted or Samples Received, and must give a reason.
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult CancelRequest(int id, string? reason)
        {
            var req = _ctx.TestRequests.Include(r => r.PatientUser).ThenInclude(p => p.User)
                .FirstOrDefault(r => r.TestRequestId == id && r.DoctorUserId == DoctorId);
            if (req == null) return NotFound();
            if (req.TestRequestStatusId != StSubmitted && req.TestRequestStatusId != StSamplesReceived)
            { TempData["Error"] = "This request can no longer be cancelled because the lab has already started on it."; return RedirectToAction(nameof(RequestDetails), new { id }); }
            reason = (reason ?? "").Trim();
            if (reason.Length < 3) { TempData["Error"] = "Please give a reason for cancelling the request."; return RedirectToAction(nameof(RequestDetails), new { id }); }
            if (reason.Length > 500) reason = reason[..500];

            req.TestRequestStatusId = StCancelled;
            req.CancelReason = reason;
            req.CancelledOn = DateTime.UtcNow;
            _ctx.SaveChanges();

            LogStaff("Test request cancelled", $"{ReqNo(id)}: {reason}");
            LogPatient(req.PatientUserId, "Test request cancelled", $"{ReqNo(id)} was cancelled by Dr {DoctorFullName}. Reason: {reason}", "bi-x-circle-fill");
            TrySend(req.PatientUser.User.Email, $"Test request {ReqNo(id)} cancelled",
                $"<p>Dear {H(req.PatientUser.User.FirstName)},</p><p>Your laboratory test request {ReqNo(id)} was cancelled by Dr {H(DoctorFullName)}.</p><p><strong>Reason:</strong> {H(reason)}</p>");
            TempData["Success"] = $"{ReqNo(id)} was cancelled.";
            return RedirectToAction(nameof(RequestDetails), new { id });
        }

        // ═══════════════════════════ RESULTS ═══════════════════════════
        private TestResultsViewModel? LoadResults(int id)
        {
            var req = MyRequests().Where(r => r.TestRequestId == id)
                .Select(r => new
                {
                    r.TestRequestId, r.TestRequestStatusId, Urgency = r.Urgency.Code, r.Notes, r.DoctorNote, r.ReleasedOn,
                    First = r.PatientUser.User.FirstName, Sur = r.PatientUser.User.Surname, Email = r.PatientUser.User.Email
                }).FirstOrDefault();
            if (req == null) return null;

            var rows = _ctx.TestResults.AsNoTracking().Where(t => t.TestRequestItem.TestRequestId == id)
                .OrderBy(t => t.TestRequestItem.TestType.TestCategory.Name).ThenBy(t => t.TestRequestItem.TestType.Name)
                .Select(t => new
                {
                    t.TestResultId, t.TestRequestItem.TestType.Name, Cat = t.TestRequestItem.TestType.TestCategory.Name,
                    t.ResultValue, t.Unit, t.NormalMinAtTest, t.NormalMaxAtTest, t.Notes, t.RecordedOn
                }).ToList();

            var resultIds = rows.Select(r => r.TestResultId).ToList();
            var verified = _ctx.Verifications.AsNoTracking().Where(v => resultIds.Contains(v.TestResultId) && v.Outcome == "Verified")
                .Select(v => new { v.VerifiedOn, By = v.VerifiedByUser.User.FirstName + " " + v.VerifiedByUser.User.Surname })
                .OrderByDescending(v => v.VerifiedOn).FirstOrDefault();
            var allVerified = resultIds.Count > 0 && _ctx.Verifications.AsNoTracking().Where(v => v.Outcome == "Verified" && resultIds.Contains(v.TestResultId)).Select(v => v.TestResultId).Distinct().Count() == resultIds.Count;

            return new TestResultsViewModel
            {
                TestRequestId = id,
                PatientName = req.First, PatientSurname = req.Sur, PatientEmail = req.Email,
                Status = StatusLabel(req.TestRequestStatusId),
                DoctorNote = req.DoctorNote,
                ReleasedOn = req.ReleasedOn.HasValue ? SastTime.ToSast(req.ReleasedOn.Value) : null,
                IsReleased = req.TestRequestStatusId == StReleased,
                CanRelease = req.TestRequestStatusId == StCompleted,
                CompletedAt = rows.Count > 0 ? SastTime.ToSast(rows.Max(r => r.RecordedOn)) : null,
                VerifiedBy = allVerified ? verified?.By : null,
                VerifiedAt = allVerified && verified != null ? SastTime.ToSast(verified.VerifiedOn) : null,
                Results = rows.Select(r =>
                {
                    var ab = IsAbnormal(r.ResultValue, r.NormalMinAtTest, r.NormalMaxAtTest);
                    return new DocResultRow
                    {
                        TestTypeName = r.Name, Category = r.Cat, ResultValue = Num(r.ResultValue), Units = r.Unit ?? "",
                        NormalRangeMin = r.NormalMinAtTest.HasValue ? Num(r.NormalMinAtTest.Value) : "",
                        NormalRangeMax = r.NormalMaxAtTest.HasValue ? Num(r.NormalMaxAtTest.Value) : "",
                        IsAbnormal = ab,
                        Direction = !ab ? "" : (r.NormalMinAtTest.HasValue && r.ResultValue < r.NormalMinAtTest.Value ? "Low" : "High"),
                        Notes = r.Notes
                    };
                }).ToList()
            };
        }

        public IActionResult ViewResults(int id)
        {
            var vm = LoadResults(id);
            if (vm == null) { TempData["Error"] = "That request could not be found."; return RedirectToAction(nameof(TrackRequests)); }
            if (vm.Results.Count == 0)
            { TempData["Error"] = "There are no results for this request yet."; return RedirectToAction(nameof(RequestDetails), new { id }); }
            return View(vm);
        }

        // Reached from an alert card: the whole request (all of its results) with the flagged test in focus.
        public IActionResult CompletedResults(int id, string? test)
        {
            if (id <= 0) return RedirectToAction(nameof(Alerts));
            var vm = LoadResults(id);
            if (vm == null || vm.Results.Count == 0)
            { TempData["Error"] = "That request has no results yet."; return RedirectToAction(nameof(Alerts)); }
            ViewBag.ClinicalNotes = MyRequests().Where(r => r.TestRequestId == id).Select(r => r.Notes).FirstOrDefault();
            ViewBag.Focus = !string.IsNullOrWhiteSpace(test) && vm.Results.Any(r => r.TestTypeName == test)
                ? test : (vm.Results.FirstOrDefault(r => r.IsAbnormal) ?? vm.Results[0]).TestTypeName;
            return View(vm);
        }

        // Release to the patient, with a note. Only a Completed request can be released.
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult ReleaseResults(int id, string? doctorNote)
        {
            var req = _ctx.TestRequests.Include(r => r.PatientUser).ThenInclude(p => p.User)
                .FirstOrDefault(r => r.TestRequestId == id && r.DoctorUserId == DoctorId);
            if (req == null) return NotFound();
            if (req.TestRequestStatusId != StCompleted)
            { TempData["Error"] = req.TestRequestStatusId == StReleased ? "These results were already released." : "Only a completed request can be released."; return RedirectToAction(nameof(ViewResults), new { id }); }
            doctorNote = (doctorNote ?? "").Trim();
            if (doctorNote.Length == 0) { TempData["Error"] = "Please add a short note for the patient before releasing the results."; return RedirectToAction(nameof(ViewResults), new { id }); }
            if (doctorNote.Length > 1000) doctorNote = doctorNote[..1000];

            req.TestRequestStatusId = StReleased;
            req.ReleasedOn = DateTime.UtcNow;
            req.DoctorNote = doctorNote;
            _ctx.SaveChanges();

            LogStaff("Results released", $"{ReqNo(id)} released to {req.PatientUser.User.FirstName} {req.PatientUser.User.Surname}");
            LogPatient(req.PatientUserId, "Results released", $"Dr {DoctorFullName} released the results of {ReqNo(id)}.", "bi-file-earmark-medical-fill");
            var err = TrySend(req.PatientUser.User.Email, $"Your test results are ready ({ReqNo(id)})",
                $@"<p>Dear {H(req.PatientUser.User.FirstName)},</p>
                   <p>Dr {H(DoctorFullName)} has released your test results ({ReqNo(id)}). You can view them by signing in to HemaFlow.</p>
                   <p><strong>Note from your doctor:</strong><br/>{H(doctorNote).Replace("\n", "<br/>")}</p>");
            TempData["Success"] = err == null ? "Results released and the patient was notified by e-mail." : $"Results released, but {err} - please let the patient know.";
            return RedirectToAction(nameof(ViewResults), new { id });
        }

        private byte[]? BuildResultsPdf(int id, out string fileName)
        {
            fileName = $"HemaFlow_{ReqNo(id)}_Results.pdf";
            var vm = LoadResults(id);
            if (vm == null || vm.Results.Count == 0) return null;
            var patient = MyRequests().Where(r => r.TestRequestId == id)
                .Select(r => new { r.PatientUser.IdNumber, r.PatientUser.DateOfBirth }).First();
            var date = vm.CompletedAt ?? SastToday;
            var data = new PatientResultsReportData
            {
                PatientName = $"{vm.PatientName} {vm.PatientSurname}",
                IdNumber = patient.IdNumber,
                DateOfBirth = patient.DateOfBirth?.ToDateTime(TimeOnly.MinValue) ?? default,
                DoctorName = "Dr " + DoctorFullName,
                DoctorNote = vm.DoctorNote,
                Results = vm.Results.Select(r => new Project.Services.TestResultRow
                {
                    RequestId = id, TestName = r.TestTypeName, Category = r.Category, ResultValue = r.ResultValue, Unit = r.Units,
                    NormalRangeMin = decimal.TryParse(r.NormalRangeMin, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var mn) ? mn : null,
                    NormalRangeMax = decimal.TryParse(r.NormalRangeMax, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var mx) ? mx : null,
                    Notes = r.Notes ?? "", IsAbnormal = r.IsAbnormal, ResultDate = date
                }).ToList()
            };
            return _pdf.GeneratePatientResultsReport(data);
        }

        public IActionResult ResultsPdf(int id)
        {
            var bytes = BuildResultsPdf(id, out var name);
            if (bytes == null) { TempData["Error"] = "There are no results to download for this request."; return RedirectToAction(nameof(TrackRequests)); }
            return File(bytes, "application/pdf", name);
        }

        // Doctor e-mails the results PDF (plus a message) to the patient. The patient's address comes from the
        // database - never from the browser.
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult SendTestResultsEmail(int requestId, string? subject, string? messageBody)
        {
            if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(messageBody))
                return Json(new { success = false, message = "Please ensure all fields are filled." });

            var vm = LoadResults(requestId);
            if (vm == null || vm.Results.Count == 0) return Json(new { success = false, message = "There are no results to send for this request." });
            var pdf = BuildResultsPdf(requestId, out var fileName);

            var html = $@"<p>Dear {H(vm.PatientName)},</p>
                <p>{H(messageBody.Trim()).Replace("\n", "<br/>")}</p>
                <p>Your test results report for {ReqNo(requestId)} is attached.</p>
                <p>Kind regards,<br/>Dr {H(DoctorFullName)}</p>";
            var err = TrySend(vm.PatientEmail, subject.Trim(), html, pdf, fileName);
            if (err != null) return Json(new { success = false, message = $"Failed to send: {err}. Please try again in a moment." });

            LogStaff("Results e-mailed", $"{ReqNo(requestId)} e-mailed to {vm.PatientName} {vm.PatientSurname}");
            return Json(new { success = true, message = $"Results e-mailed to {vm.PatientName} {vm.PatientSurname}." });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult BookAppointment(int requestId, DateTime appointmentDate, string? appointmentTime, string? notes)
        {
            if (string.IsNullOrWhiteSpace(appointmentTime) || appointmentDate == default || string.IsNullOrWhiteSpace(notes))
                return Json(new { success = false, message = "Please ensure all fields are filled." });
            if (appointmentDate.Date < SastToday)
                return Json(new { success = false, message = "The appointment date cannot be in the past." });

            var req = MyRequests().Where(r => r.TestRequestId == requestId)
                .Select(r => new { r.PatientUser.User.FirstName, r.PatientUser.User.Surname, r.PatientUser.User.Email, r.PatientUserId }).FirstOrDefault();
            if (req == null) return Json(new { success = false, message = "That request could not be found." });

            var html = $@"<h3>Appointment scheduled</h3>
                <p>Dear {H(req.FirstName)},</p>
                <p>Dr {H(DoctorFullName)} has booked an appointment with you to discuss your recent test results ({ReqNo(requestId)}).</p>
                <ul><li><strong>Date:</strong> {appointmentDate:yyyy-MM-dd}</li>
                    <li><strong>Time:</strong> {H(appointmentTime)}</li>
                    <li><strong>Notes:</strong> {H(notes.Trim())}</li></ul>
                <p>If this time does not suit you, please contact the practice.</p>";
            var err = TrySend(req.Email, $"Appointment confirmation - Dr {DoctorFullName}", html);
            if (err != null) return Json(new { success = false, message = $"The appointment could not be confirmed: {err}." });

            LogPatient(req.PatientUserId, "Appointment booked", $"Dr {DoctorFullName} booked an appointment on {appointmentDate:yyyy-MM-dd} ({appointmentTime}).", "bi-calendar-check-fill");
            LogStaff("Appointment booked", $"{ReqNo(requestId)}: {appointmentDate:yyyy-MM-dd} {appointmentTime} with {req.FirstName} {req.Surname}");
            return Json(new { success = true, message = $"Appointment booked for {req.FirstName} {req.Surname}. A confirmation e-mail was sent." });
        }

        // ═══════════════════════════ ALERTS ═══════════════════════════
        // Abnormal results on this doctor's requests; default window is the last 5 days, filterable.
        public IActionResult Alerts(DateTime? fromDate, DateTime? toDate)
        {
            var today = SastToday;
            var to = (toDate ?? today).Date; if (to > today) to = today;
            var from = (fromDate ?? to.AddDays(-5)).Date; if (from > to) from = to;

            var rows = AbnormalResults(SastTime.SastDateToUtc(from), SastTime.SastDateToUtc(to.AddDays(1)))
                .OrderByDescending(t => t.RecordedOn).Take(300)
                .Select(t => new
                {
                    t.TestRequestItem.TestRequestId, t.RecordedOn, Test = t.TestRequestItem.TestType.Name,
                    First = t.TestRequestItem.TestRequest.PatientUser.User.FirstName, Sur = t.TestRequestItem.TestRequest.PatientUser.User.Surname,
                    Email = t.TestRequestItem.TestRequest.PatientUser.User.Email,
                    t.ResultValue, t.Unit, t.NormalMinAtTest, t.NormalMaxAtTest,
                    Urgency = t.TestRequestItem.TestRequest.Urgency.Code
                }).ToList();

            var items = rows.Select(t =>
            {
                var urg = UrgencyLabel(t.Urgency);
                var unit = string.IsNullOrWhiteSpace(t.Unit) ? "" : " " + t.Unit;
                return new DoctorAlertItem
                {
                    TestRequestId = t.TestRequestId,
                    Test = t.Test,
                    Patient = $"{t.First} {t.Sur}".Trim(),
                    Email = t.Email,
                    Req = "#" + ReqNo(t.TestRequestId),
                    Reported = SastTime.ToSast(t.RecordedOn).ToString("yyyy-MM-dd HH:mm"),
                    Result = Num(t.ResultValue) + unit,
                    Range = RangeText(t.NormalMinAtTest, t.NormalMaxAtTest) + unit,
                    Urgency = urg,
                    UrgClass = urg == "STAT" ? "bg-danger" : urg == "Urgent" ? "bg-warning text-dark" : "bg-secondary"
                };
            }).ToList();
            ViewBag.DoctorName = DoctorFullName;
            return View(items);
        }

        // ═══════════════════════════ REPORTS ═══════════════════════════
        public IActionResult Reports()
        {
            var history = _ctx.StaffActivityLogs.AsNoTracking()
                .Where(l => l.ActorId == DoctorId && l.Action == "Report generated")
                .OrderByDescending(l => l.Timestamp).Take(20).ToList();
            var total = _ctx.StaffActivityLogs.AsNoTracking().Count(l => l.ActorId == DoctorId && l.Action == "Report generated");

            ViewBag.SubmittedRequests = MyRequests().Count();
            ViewBag.ReportsGenerated = total;
            ViewBag.LastGenerated = history.Count > 0 ? SastTime.ToSast(history[0].Timestamp).ToString("dd MMM yyyy") : "None yet";
            ViewBag.History = history.Select(l =>
            {
                var parts = (l.Detail ?? "").Split(" | ", 2);
                return new[] { parts[0], parts.Length > 1 ? parts[1] : "", SastTime.ToSast(l.Timestamp).ToString("yyyy-MM-dd HH:mm") };
            }).ToList();
            return View();
        }

        [HttpGet]
        public IActionResult GenerateReport() => RedirectToAction(nameof(Reports));

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult GenerateReport(string? priorityFilter, DateTime fromDate, DateTime toDate)
        {
            var today = SastToday;
            if (fromDate == default || toDate == default) { TempData["Error"] = "Please choose a from and to date."; return RedirectToAction(nameof(Reports)); }
            if (fromDate.Date > today || toDate.Date > today) { TempData["Error"] = "Dates cannot be in the future."; return RedirectToAction(nameof(Reports)); }
            if (toDate.Date < fromDate.Date) { TempData["Error"] = "The to date cannot be before the from date."; return RedirectToAction(nameof(Reports)); }

            var fromUtc = SastTime.SastDateToUtc(fromDate);
            var toUtc = SastTime.SastDateToUtc(toDate.Date.AddDays(1));
            var q = MyRequests().Where(r => r.RequestedOn >= fromUtc && r.RequestedOn < toUtc);
            string? urgencyName = null;
            if (!string.IsNullOrWhiteSpace(priorityFilter))
            {
                var u = _ctx.Urgencies.ToList().FirstOrDefault(x => string.Equals(x.Code, priorityFilter, StringComparison.OrdinalIgnoreCase));
                if (u != null) { q = q.Where(r => r.UrgencyId == u.UrgencyId); urgencyName = UrgencyLabel(u.Code); }
            }

            var rows = LoadRequestRows(q);
            var data = new DoctorRequestsReportData
            {
                DoctorName = DoctorFullName,
                HpcsaNumber = Doc!.HpcsaNumber,
                FromDate = fromDate.Date, ToDate = toDate.Date, UrgencyFilter = urgencyName,
                Rows = rows.Select(r => new DoctorRequestsReportRow
                {
                    RequestNumber = ReqNo(r.TestRequestId), Patient = r.PatientName, Tests = r.TestName,
                    Urgency = r.Urgency, Status = r.Status, RequestedOn = r.OrderDate
                }).ToList()
            };

            var bytes = _pdf.GenerateDoctorRequestsReport(data);
            var fileName = $"Doctor_Report_{fromDate:yyyyMMdd}_to_{toDate:yyyyMMdd}.pdf";
            LogStaff("Report generated", $"{fileName} | {fromDate:d MMM yyyy} to {toDate:d MMM yyyy}, {urgencyName ?? "all priorities"}, {rows.Count} request(s)");
            return File(bytes, "application/pdf", fileName);
        }

        // ═══════════════════════════ PROFILE ═══════════════════════════
        [HttpGet]
        public IActionResult Profile()
        {
            ViewBag.Security = BuildSecurity();
            return View(Doc);
        }

        // ── Real two-factor / device data (same tables the login check and the Admin/Patient pages use) ──
        private DoctorSecurityViewModel BuildSecurity()
        {
            var uid = DoctorId;
            var tf = _ctx.UserTwoFactors.AsNoTracking().FirstOrDefault(t => t.UserId == uid && t.IsEnabled);
            var sec = new DoctorSecurityViewModel
            {
                TwoFactorEnabled = tf != null,
                TwoFactorLastVerified = tf?.LastVerifiedOn == null ? null : SastTime.ToSast(tf.LastVerifiedOn.Value),
                BackupCodesLeft = _ctx.UserBackupCodes.Count(b => b.UserId == uid && b.UsedOn == null)
            };
            if (TempData["NewBackupCodes"] is string codes && codes.Length > 0)
                sec.NewBackupCodes = codes.Split(',').ToList();
            return sec;
        }

        [HttpGet]
        public IActionResult Setup2FA()
        {
            var user = _ctx.Users.FirstOrDefault(u => u.UserId == DoctorId);
            if (user == null) return RedirectToAction("Login", "Account");
            var row = _ctx.UserTwoFactors.FirstOrDefault(t => t.UserId == user.UserId);
            if (row != null && row.IsEnabled)
            {
                TempData["Success"] = "Two-factor authentication is already enabled.";
                return RedirectToAction(nameof(Profile));
            }
            if (row == null)
            {
                row = new UserTwoFactor { UserId = user.UserId, SecretKey = TotpService.GenerateSecretKey(), IsEnabled = false };
                _ctx.UserTwoFactors.Add(row);
                _ctx.SaveChanges();
            }
            return View(new Project.Models.TwoFactorSetupViewModel
            {
                SecretKey = row.SecretKey,
                OtpAuthUri = TotpService.BuildOtpAuthUri("HemaFlow", user.Email, row.SecretKey),
                AccountEmail = user.Email
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Setup2FAConfirm(string code)
        {
            var row = _ctx.UserTwoFactors.FirstOrDefault(t => t.UserId == DoctorId);
            if (row == null) { TempData["Error"] = "Start the setup again."; return RedirectToAction(nameof(Setup2FA)); }
            if (!TotpService.ValidateCode(row.SecretKey, code ?? ""))
            {
                TempData["Error"] = "That code didn't match. Check your authenticator app's clock and try the current code.";
                return RedirectToAction(nameof(Setup2FA));
            }
            row.IsEnabled = true;
            row.EnabledOn = DateTime.UtcNow;
            row.LastVerifiedOn = DateTime.UtcNow;
            var plain = ReplaceBackupCodes(DoctorId);
            _ctx.SaveChanges();
            LogStaff("2FA enabled", "Doctor enabled two-factor authentication");
            TempData["NewBackupCodes"] = string.Join(",", plain);
            TempData["Success"] = "Two-factor authentication is now enabled. Save your backup codes somewhere safe.";
            return RedirectToAction(nameof(Profile));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult DisableTwoFactor(string password)
        {
            var user = _ctx.Users.FirstOrDefault(u => u.UserId == DoctorId);
            if (user == null) return RedirectToAction("Login", "Account");
            var hasher = new PasswordHasher<GenUser>();
            if (string.IsNullOrEmpty(password) ||
                hasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed)
            {
                TempData["Error"] = "Incorrect password. Two-factor authentication was not disabled.";
                return RedirectToAction(nameof(Profile));
            }
            var row = _ctx.UserTwoFactors.FirstOrDefault(t => t.UserId == user.UserId);
            if (row != null) _ctx.UserTwoFactors.Remove(row);
            _ctx.UserBackupCodes.RemoveRange(_ctx.UserBackupCodes.Where(b => b.UserId == user.UserId));
            _ctx.SaveChanges();
            LogStaff("2FA disabled", "Doctor disabled two-factor authentication");
            TempData["Success"] = "Two-factor authentication has been disabled.";
            return RedirectToAction(nameof(Profile));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult RegenerateBackupCodes()
        {
            var tf = _ctx.UserTwoFactors.FirstOrDefault(t => t.UserId == DoctorId && t.IsEnabled);
            if (tf == null) { TempData["Error"] = "Enable two-factor authentication first."; return RedirectToAction(nameof(Profile)); }
            var plain = ReplaceBackupCodes(DoctorId);
            _ctx.SaveChanges();
            LogStaff("Backup codes regenerated", "Doctor generated new 2FA backup codes");
            TempData["NewBackupCodes"] = string.Join(",", plain);
            TempData["Success"] = "New backup codes generated. Your old codes no longer work.";
            return RedirectToAction(nameof(Profile));
        }

        private List<string> ReplaceBackupCodes(int userId)
        {
            _ctx.UserBackupCodes.RemoveRange(_ctx.UserBackupCodes.Where(b => b.UserId == userId));
            var plain = TotpService.GenerateBackupCodes();
            foreach (var c in plain)
                _ctx.UserBackupCodes.Add(new UserBackupCode { UserId = userId, CodeHash = TotpService.HashBackupCode(c) });
            return plain;
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(string? firstName, string? lastName, string? phone, string? email,
            string? currentPassword, string? newPassword, string? confirmPassword)
        {
            var user = Doc!.User;
            var u = _ctx.Users.First(x => x.UserId == user.UserId);

            // Password change (needs the current password)
            if (!string.IsNullOrWhiteSpace(currentPassword) || !string.IsNullOrWhiteSpace(newPassword) || !string.IsNullOrWhiteSpace(confirmPassword))
            {
                var hasher = new PasswordHasher<GenUser>();
                if (string.IsNullOrWhiteSpace(currentPassword) || hasher.VerifyHashedPassword(u, u.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
                { TempData["Error"] = "Your current password is incorrect."; return RedirectToAction(nameof(Profile)); }
                if (newPassword != confirmPassword)
                { TempData["Error"] = "The new password and confirmation do not match."; return RedirectToAction(nameof(Profile)); }
                if (!PasswordPolicy.IsValid(newPassword))
                { TempData["Error"] = PasswordPolicy.Message; return RedirectToAction(nameof(Profile)); }
                u.PasswordHash = hasher.HashPassword(u, newPassword!);
                _ctx.SaveChanges();
                LogStaff("Password changed", "Doctor changed their own password");
                TempData["Success"] = "Password updated successfully.";
                return RedirectToAction(nameof(Profile));
            }

            // Details (name, phone, e-mail = the login username)
            if (!string.IsNullOrWhiteSpace(firstName)) u.FirstName = firstName.Trim();
            if (!string.IsNullOrWhiteSpace(lastName)) u.Surname = lastName.Trim();
            if (!string.IsNullOrWhiteSpace(phone))
            {
                if (!Regex.IsMatch(phone.Trim(), @"^[0-9+ ()\-]{10,15}$")) { TempData["Error"] = "Enter a valid cellphone number."; return RedirectToAction(nameof(Profile)); }
                u.Phone = phone.Trim();
            }
            if (!string.IsNullOrWhiteSpace(email) && !string.Equals(email.Trim(), u.Email, StringComparison.OrdinalIgnoreCase))
            {
                var e = email.Trim();
                if (!EmailRx.IsMatch(e)) { TempData["Error"] = "Enter a valid email address."; return RedirectToAction(nameof(Profile)); }
                if (_ctx.Users.Any(x => x.UserId != u.UserId && x.Email == e)) { TempData["Error"] = "That email address is already used by another account."; return RedirectToAction(nameof(Profile)); }
                u.Email = e;
            }
            _ctx.SaveChanges();
            await AuthCookieHelper.RefreshNameAndEmailAsync(HttpContext, u);
            LogStaff("Profile updated", "Doctor updated their profile details");
            TempData["Success"] = "Profile updated successfully.";
            return RedirectToAction(nameof(Profile));
        }
    }
}
