using Microsoft.AspNetCore.Authorization;
using Project.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Models;
using Project.Services;
using System.Security.Claims;
using GenUser = Project.Models.Entities.User;
using GenPatientProfile = Project.Models.Entities.PatientProfile;
using GenPatientCondition = Project.Models.Entities.PatientCondition;
using GenPatientAllergy = Project.Models.Entities.PatientAllergy;
using GenPatientMedication = Project.Models.Entities.PatientMedication;
using GenPendingApproval = Project.Models.Entities.PendingApproval;
using GenUserTwoFactor = Project.Models.Entities.UserTwoFactor;
using GenUserBackupCode = Project.Models.Entities.UserBackupCode;
using Project.Services;

namespace Project.Controllers
{
    // Real login exists now, so every action here requires being signed in
    // as a Patient - each patient only ever sees/edits their OWN row, found
    // via the auth cookie's claims (see CurrentUserId/CurrentUserEmail below).
    [Authorize(Roles = "Patient")]
    public class PatientController : Controller
    {

        private readonly PdfService _pdfService;
        private readonly ApplicationDbContext _context;

        public PatientController(PdfService pdfService, ApplicationDbContext context)
        {
            _pdfService = pdfService;
            _context = context;
        }

        // ═══════════════════════════════════════════════════════════════════
        // CURRENT PATIENT: identity, medical data, requests and results
        // all come from the database, scoped to the signed-in patient.
        // ═══════════════════════════════════════════════════════════════════

        // E-mail of the signed-in patient, read from the login cookie.
        private string CurrentUserEmail => User.FindFirstValue(ClaimTypes.Email) ?? "";
        // Id of the signed-in patient, read from the login cookie.
        private int? CurrentUserId =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (int?)null;

        // The patient's user and profile rows, loaded once per request.
        private GenUser? _currentUserCache;
        // The patient's user record, loaded once per request.
        private GenUser? CurrentUser => _currentUserCache ??= _context.Users.FirstOrDefault(u => u.UserId == CurrentUserId);

        private GenPatientProfile? _currentProfileCache;
        // The patient's profile record, loaded once per request.
        private GenPatientProfile? CurrentProfile => _currentProfileCache ??=
            (CurrentUser != null ? _context.PatientProfiles.FirstOrDefault(p => p.UserId == CurrentUser.UserId) : null);

        // Short names for the signed-in patient's details.
        private string _firstName => CurrentUser?.FirstName ?? "";
        private string _surname => CurrentUser?.Surname ?? "";
        private string _patientName => $"{_firstName} {_surname}";
        private string _patientId => CurrentProfile?.PatientCode ?? "";
        private string _idNumber => CurrentProfile?.IdNumber ?? "";
        // PatientProfile.DateOfBirth is a DateOnly? in the generated model
        // (EF Core maps SQL DATE columns to DateOnly) - _dob is kept as
        // DateTime since that's what every existing read call site expects.
        private DateTime _dob => CurrentProfile?.DateOfBirth is DateOnly d ? d.ToDateTime(TimeOnly.MinValue) : default;
        private string _email => CurrentUser?.Email ?? "";
        private string _phone => CurrentUser?.Phone ?? "";
        private string _addressLine1 => CurrentProfile?.AddressLine1 ?? "";
        private (int? ProvinceId, string Town, string Suburb, string PostalCode)? _addrCache;
        private (int? ProvinceId, string Town, string Suburb, string PostalCode) _addr => _addrCache ??= LocationHelper.Describe(_context, CurrentProfile?.SuburbId);
        private string _suburb => _addr.Suburb;
        private string _city => _addr.Town;
        private string _postalCode => _addr.PostalCode;

        // Writes to the real PatientActivityLog table for the CURRENT logged-in
        // patient. Kept as a 3-arg call (same shape as the old in-memory
        // version) so none of the many call sites below needed to change.
        private void LogPatientActivity(string action, string detail, string icon = "bi-info-circle-fill")
        {
            if (CurrentUserId == null) return;
            _context.PatientActivityLogs.Add(new Project.Models.Entities.PatientActivityLog
            {
                PatientUserId = CurrentUserId.Value,
                Action = action,
                Detail = detail,
                Icon = icon,
                LogTypeId = 1 // 'info' - matches LogType seed (1=info,2=success,3=warning,4=danger)
            });
            _context.SaveChanges();
        }

        // A patient may have several primary doctors (each gets full access to results).
        private const int MaxPrimaryDoctors = 5;
        // Real consent rows (PatientDoctorConsent / ConsentedTestRequest).
        //   active consent + NO per-test rows  = full access (this is a "primary doctor")
        //   active consent + per-test rows     = access to just those tests
        private List<AuthorizedDoctorAccess>? _consentCache;
        // Doctors the patient has given access to.
        private List<AuthorizedDoctorAccess> _consentDoctors => _consentCache ??= LoadConsents();
        // Doctors with access to everything (the primary doctors).
        private List<DoctorSummary> _primaryDoctors => _consentDoctors.Where(d => d.IsFullAccess)
            .Select(d => new DoctorSummary { Name = d.DoctorName, HpcsaNumber = d.HpcsaNumber, Practice = d.Practice, Specialty = d.Specialty }).ToList();
        private DoctorSummary? _primaryDoctor => _primaryDoctors.FirstOrDefault();

        // Reads the patient's consent rows from the database.
        private List<AuthorizedDoctorAccess> LoadConsents()
        {
            if (CurrentUserId == null) return new();
            var uid = CurrentUserId.Value;
            var rows = (from c in _context.PatientDoctorConsents.AsNoTracking()
                        join d in _context.DoctorProfiles.AsNoTracking() on c.DoctorUserId equals d.UserId
                        join u in _context.Users.AsNoTracking() on d.UserId equals u.UserId
                        where c.PatientUserId == uid && c.RevokedOn == null
                        orderby c.GrantedOn descending
                        select new { c.PatientDoctorConsentId, c.GrantedOn, u.FirstName, u.Surname, d.HpcsaNumber, d.Specialization }).ToList();
            var ids = rows.Select(r => r.PatientDoctorConsentId).ToList();
            var perTest = (from cr in _context.ConsentedTestRequests.AsNoTracking()
                           join i in _context.TestRequestItems.AsNoTracking() on cr.TestRequestItemId equals i.TestRequestItemId
                           where ids.Contains(cr.PatientDoctorConsentId) && cr.IsConsented
                           select new { cr.PatientDoctorConsentId, i.TestRequestId }).Distinct().ToList();
            var withRows = _context.ConsentedTestRequests.AsNoTracking()
                .Where(cr => ids.Contains(cr.PatientDoctorConsentId)).Select(cr => cr.PatientDoctorConsentId).Distinct().ToList();
            return rows.Select(r => new AuthorizedDoctorAccess
            {
                DoctorName = $"Dr. {r.FirstName} {r.Surname}",
                HpcsaNumber = r.HpcsaNumber,
                Practice = "",
                Specialty = r.Specialization ?? "",
                GrantedDate = SastTime.ToSast(r.GrantedOn),
                IsFullAccess = !withRows.Contains(r.PatientDoctorConsentId),
                GrantedRequestIds = perTest.Where(t => t.PatientDoctorConsentId == r.PatientDoctorConsentId).Select(t => t.TestRequestId).ToList()
            }).ToList();
        }

        // Every (non-archived) doctor the patient could share results with.
        private List<DoctorSummary>? _allDoctorsCache;
        // Every active doctor, used by the search box.
        private List<DoctorSummary> _allDoctors => _allDoctorsCache ??=
            (from d in _context.DoctorProfiles.AsNoTracking()
             join u in _context.Users.AsNoTracking() on d.UserId equals u.UserId
             where !d.IsArchived
             orderby u.Surname, u.FirstName
             select new { u.FirstName, u.Surname, d.HpcsaNumber, d.Specialization }).ToList()
            .Select(d => new DoctorSummary { Name = $"Dr. {d.FirstName} {d.Surname}", HpcsaNumber = d.HpcsaNumber, Practice = "", Specialty = d.Specialization ?? "" }).ToList();

        // Conditions, allergies and medications come from the patient's own rows,
        // joined to the admin-managed master lists. Each is loaded once per request.
        private List<ConditionItem>? _conditionDetailsCache;
        // The patient's conditions with the date diagnosed.
        private List<ConditionItem> _conditionDetails => _conditionDetailsCache ??=
            CurrentUser == null ? new List<ConditionItem>() :
            (from pc in _context.PatientConditions.AsNoTracking()
             join c in _context.MedicalConditions on pc.ConditionId equals c.ConditionId
             where pc.PatientUserId == CurrentUser.UserId
             orderby c.Name
             select new ConditionItem { Name = c.Name, DiagnosedOn = pc.DiagnosedOn }).ToList();
        private List<string> _conditions => _conditionDetails.Select(c => c.Name).ToList();

        private List<AllergyItem>? _allergiesCache;
        // The patient's allergies with severity.
        private List<AllergyItem> _allergies => _allergiesCache ??=
            CurrentUser == null ? new List<AllergyItem>() :
            (from pa in _context.PatientAllergies.AsNoTracking()
             join a in _context.Allergies on pa.AllergyId equals a.AllergyId
             join cat in _context.Categories on a.CategoryId equals cat.CategoryId
             where pa.PatientUserId == CurrentUser.UserId
             orderby a.Name
             select new AllergyItem { Name = a.Name, Category = cat.Name, Severity = pa.Severity ?? "" }).ToList();

        private List<MedicationItem>? _medicationsCache;
        // The patient's medications with dose details.
        private List<MedicationItem> _medications => _medicationsCache ??=
            CurrentUser == null ? new List<MedicationItem>() :
            (from pm in _context.PatientMedications.AsNoTracking()
             join m in _context.Medications on pm.MedicationId equals m.MedicationId
             where pm.PatientUserId == CurrentUser.UserId
             orderby m.Name
             select new { m.Name, pm.Notes, pm.Strength, pm.Dose, pm.Form, pm.Frequency, pm.StartedOn })
             .ToList()
             .Select(x =>
             {
                 // Rows saved before the Strength/Dose/Form columns existed kept "dosage|frequency" in Notes.
                 var legacy = (x.Notes ?? "").Split('|');
                 var strength = x.Strength ?? "";
                 var dose = x.Dose ?? "";
                 var freq = x.Frequency ?? (legacy.Length > 1 ? legacy[1] : "");
                 var dosage = string.Join(", ", new[] { strength, dose }.Where(t => t != ""));
                 if (dosage == "" && legacy.Length > 0 && x.Strength == null && x.Dose == null) dosage = legacy[0];
                 return new MedicationItem
                 {
                     Name = x.Name, Strength = strength, Dose = dose, Form = x.Form ?? "",
                     Dosage = dosage, Frequency = freq, StartedOn = x.StartedOn
                 };
             }).ToList();

        // Helpers behind the add and remove buttons for conditions, allergies and medications.
        private static readonly string[] AllowedSeverities = { "Mild", "Moderate", "Severe", "Life-threatening" };
        private static readonly string[] AllowedForms = { "Tablet", "Capsule", "Syrup / liquid", "Injection", "Inhaler", "Cream / ointment", "Drops", "Patch", "Other" };
        private static readonly string[] AllowedFrequencies = { "Once daily", "Twice daily", "Three times daily", "Four times daily", "Every 8 hours", "Weekly", "Monthly", "As needed", "Other" };

        // A date picked by the patient: never in the future, never before 1900.
        private static DateOnly? CleanPastDate(DateTime? d)
        {
            if (d == null) return null;
            var date = DateOnly.FromDateTime(d.Value);
            if (date > DateOnly.FromDateTime(DateTime.Today) || date.Year < 1900) return null;
            return date;
        }

        // Saves a condition on the patient's record.
        private void AddPatientCondition(int conditionId, DateOnly? diagnosedOn = null)
        {
            if (CurrentUser == null) return;
            if (_context.PatientConditions.Any(pc => pc.PatientUserId == CurrentUser.UserId && pc.ConditionId == conditionId)) return;
            _context.PatientConditions.Add(new GenPatientCondition
            {
                PatientUserId = CurrentUser.UserId,
                ConditionId = conditionId,
                DiagnosedOn = diagnosedOn
            });
            _context.SaveChanges();
        }

        // Removes a condition from the patient's record.
        private void RemovePatientCondition(string conditionName)
        {
            if (CurrentUser == null) return;
            var row = (from pc in _context.PatientConditions
                       join c in _context.MedicalConditions on pc.ConditionId equals c.ConditionId
                       where pc.PatientUserId == CurrentUser.UserId && c.Name == conditionName
                       select pc).FirstOrDefault();
            if (row != null)
            {
                _context.PatientConditions.Remove(row);
                _context.SaveChanges();
            }
        }

        // Saves an allergy on the patient's record.
        private void AddPatientAllergy(int allergyId, string? severity = null)
        {
            if (CurrentUser == null) return;
            if (_context.PatientAllergies.Any(pa => pa.PatientUserId == CurrentUser.UserId && pa.AllergyId == allergyId)) return;
            _context.PatientAllergies.Add(new GenPatientAllergy
            {
                PatientUserId = CurrentUser.UserId,
                AllergyId = allergyId,
                Severity = AllowedSeverities.Contains(severity) ? severity : null
            });
            _context.SaveChanges();
        }

        // Removes an allergy from the patient's record.
        private void RemovePatientAllergy(string allergyName)
        {
            if (CurrentUser == null) return;
            var row = (from pa in _context.PatientAllergies
                       join a in _context.Allergies on pa.AllergyId equals a.AllergyId
                       where pa.PatientUserId == CurrentUser.UserId && a.Name == allergyName
                       select pa).FirstOrDefault();
            if (row != null)
            {
                _context.PatientAllergies.Remove(row);
                _context.SaveChanges();
            }
        }

        // Saves a medication on the patient's record.
        private void AddPatientMedication(int medicationId, string? strength, string? dose, string? form, string? frequency, DateOnly? startedOn)
        {
            if (CurrentUser == null) return;
            string? Clean(string? t, int max) => string.IsNullOrWhiteSpace(t) ? null : (t.Trim().Length > max ? t.Trim()[..max] : t.Trim());
            var existing = _context.PatientMedications.FirstOrDefault(pm => pm.PatientUserId == CurrentUser.UserId && pm.MedicationId == medicationId);
            if (existing == null)
            {
                existing = new GenPatientMedication { PatientUserId = CurrentUser.UserId, MedicationId = medicationId };
                _context.PatientMedications.Add(existing);
            }
            existing.Strength = Clean(strength, 50);
            existing.Dose = Clean(dose, 50);
            existing.Form = Clean(form, 30);
            existing.Frequency = Clean(frequency, 60);
            existing.StartedOn = startedOn;
            existing.Notes = null;
            _context.SaveChanges();
        }

        // Removes a medication from the patient's record.
        private void RemovePatientMedication(string medName)
        {
            if (CurrentUser == null) return;
            var row = (from pm in _context.PatientMedications
                       join m in _context.Medications on pm.MedicationId equals m.MedicationId
                       where pm.PatientUserId == CurrentUser.UserId && m.Name == medName
                       select pm).FirstOrDefault();
            if (row != null)
            {
                _context.PatientMedications.Remove(row);
                _context.SaveChanges();
            }
        }

        // ── Test requests, results and trends: real rows from TestRequest / TestRequestItem /
        //    TestResult for the logged-in patient only. A brand-new patient has none. ──────────

        // Turns a status code into the text the patient sees.
        private static string DisplayStatus(string code) => code switch
        {
            "Pending" or "Submitted" => "Submitted",
            "SamplesReceived" => "Samples Received",
            "InProgress" => "In Progress",
            "Completed" => "Completed",
            "Released" => "Released by Doctor",
            "Cancelled" => "Cancelled",
            _ => code
        };

        // True when a result is outside its normal range.
        private static bool IsAbnormalValue(decimal v, decimal? min, decimal? max) =>
            (min.HasValue && v < min.Value) || (max.HasValue && v > max.Value);

        // Shows a result without trailing zeros.
        private static string FormatValue(decimal v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        private List<TestResultRow>? _resultsCache;
        // Only results from requests the doctor has RELEASED are ever shown to the patient.
        private List<TestResultRow> _testResults => _resultsCache ??= LoadReleasedResults();

        // Only released results are ever shown to the patient.
        private List<TestResultRow> LoadReleasedResults()
        {
            if (CurrentUserId == null) return new();
            var uid = CurrentUserId.Value;
            return (from i in _context.TestRequestItems.AsNoTracking()
                    where i.TestRequest.PatientUserId == uid
                          && i.TestRequest.TestRequestStatus.Code == "Released"
                          && i.TestResult != null
                    select new
                    {
                        i.TestRequestId,
                        TestName = i.TestType.Name,
                        Category = i.TestType.TestCategory.Name,
                        Value = i.TestResult!.ResultValue,
                        Unit = i.TestResult.Unit ?? i.TestType.Unit,
                        Min = i.TestResult.NormalMinAtTest ?? i.TestType.NormalMin,
                        Max = i.TestResult.NormalMaxAtTest ?? i.TestType.NormalMax,
                        i.TestResult.Notes,
                        On = i.TestResult.RecordedOn
                    }).ToList()
                .Select(x => new TestResultRow
                {
                    RequestId = x.TestRequestId,
                    TestName = x.TestName,
                    Category = x.Category,
                    ResultValue = FormatValue(x.Value),
                    Unit = x.Unit ?? "",
                    NormalRangeMin = x.Min,
                    NormalRangeMax = x.Max,
                    IsAbnormal = IsAbnormalValue(x.Value, x.Min, x.Max),
                    Notes = x.Notes ?? "",
                    ResultDate = SastTime.ToSast(x.On)
                })
                .OrderByDescending(r => r.ResultDate).ThenBy(r => r.TestName).ToList();
        }

        private List<TestRequestSummary>? _requestsCache;
        private List<TestRequestSummary> _requests => _requestsCache ??= LoadRequests();

        // Loads the patient's requests with their status.
        private List<TestRequestSummary> LoadRequests()
        {
            if (CurrentUserId == null) return new();
            var uid = CurrentUserId.Value;
            var reqs = _context.TestRequests.AsNoTracking()
                .Where(r => r.PatientUserId == uid)
                .OrderByDescending(r => r.RequestedOn)
                .Select(r => new { r.TestRequestId, r.RequestedOn, Status = r.TestRequestStatus.Code })
                .ToList();
            var ids = reqs.Select(r => r.TestRequestId).ToList();
            var names = _context.TestRequestItems.AsNoTracking()
                .Where(i => ids.Contains(i.TestRequestId))
                .Select(i => new { i.TestRequestId, Name = i.TestType.Name })
                .ToList().GroupBy(x => x.TestRequestId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Name).OrderBy(n => n).ToList());
            var abnormal = _testResults.Where(r => r.IsAbnormal).Select(r => r.RequestId).ToHashSet();
            var released = _testResults.Select(r => r.RequestId).ToHashSet();

            return reqs.Select(r =>
            {
                var list = names.TryGetValue(r.TestRequestId, out var n) ? n : new List<string>();
                var title = list.Count == 0 ? "Test request"
                          : list.Count == 1 ? list[0]
                          : list.Count == 2 ? $"{list[0]}, {list[1]}"
                          : $"{list[0]}, {list[1]} +{list.Count - 2} more";
                return new TestRequestSummary
                {
                    RequestId = r.TestRequestId,
                    TestName = title,
                    RequestDate = SastTime.ToSast(r.RequestedOn),
                    Status = DisplayStatus(r.Status),
                    ResultFlag = !released.Contains(r.TestRequestId) ? "" : abnormal.Contains(r.TestRequestId) ? "Abnormal" : "Normal"
                };
            }).ToList();
        }

        // Trend lines: one per test name the patient has released results for.
        private class TrendSeries
        {
            public string Name = "";
            public string Unit = "";
            public decimal Min, Max;
            public List<TrendDataPoint> Points = new();
        }

        // Builds the chart lines for the Track Results page.
        private List<TrendSeries> BuildTrends(DateTime? from = null, DateTime? to = null)
        {
            return _testResults
                .Where(r => decimal.TryParse(r.ResultValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
                .Where(r => (from == null || r.ResultDate.Date >= from.Value.Date) && (to == null || r.ResultDate.Date <= to.Value.Date))
                .GroupBy(r => r.TestName)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var ordered = g.OrderBy(r => r.ResultDate).ToList();
                    var latest = ordered.Last();
                    return new TrendSeries
                    {
                        Name = g.Key,
                        Unit = latest.Unit,
                        Min = latest.NormalRangeMin ?? 0,
                        Max = latest.NormalRangeMax ?? 0,
                        Points = ordered.Select(r => new TrendDataPoint
                        {
                            Label = r.ResultDate.ToString("d MMM yy"),
                            Date = r.ResultDate,
                            Value = decimal.Parse(r.ResultValue, System.Globalization.CultureInfo.InvariantCulture)
                        }).ToList()
                    };
                }).ToList();
        }

        // Median of a list of values (used for the trend summary).
        private static decimal Median(IEnumerable<decimal> values)
        {
            var v = values.OrderBy(x => x).ToList();
            if (v.Count == 0) return 0;
            return v.Count % 2 == 1 ? v[v.Count / 2] : Math.Round((v[v.Count / 2 - 1] + v[v.Count / 2]) / 2, 2);
        }

        // ═══════════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════════

        // Gathers everything the dashboard shows.
        private PatientDashboardViewModel BuildDashboard(string? trackedTest = null)
        {
            var trends = BuildTrends();
            var series = trends.FirstOrDefault(t => t.Name == trackedTest) ?? trends.FirstOrDefault(t => t.Name == "Total Cholesterol") ?? trends.FirstOrDefault();

            return new PatientDashboardViewModel
            {
                PatientName = _patientName,
                PatientNumber = _patientId,
                ActiveRequestCount = _requests.Count(r => r.Status is "Submitted" or "In Progress" or "Samples Received"),
                RecentRequests = _requests.Take(5).ToList(),
                Conditions = _conditions.ToList(),
                Allergies = _allergies.Select(a => a.Name).ToList(),
                Medications = _medications.ToList(),
                AvailableTrackedTests = trends.Select(t => t.Name).ToList(),
                TrackedTest = series?.Name ?? "",
                TrackedTestUnit = series?.Unit ?? "",
                TrackedTestNormalMax = series?.Max ?? 0,
                TrackedTestDataPoints = series?.Points ?? new List<TrendDataPoint>()
            };
        }

        // Gathers everything the medical profile page shows.
        private MedicalProfileViewModel BuildProfile() => new()
        {
            PatientName = _patientName,
            FirstName = _firstName,
            Surname = _surname,
            PatientId = _patientId,
            IdNumber = _idNumber,
            DateOfBirth = _dob,
            Email = _email,
            Phone = _phone,
            AddressLine1 = _addressLine1,
            ComplexName = CurrentProfile?.ComplexName,
            SuburbId = CurrentProfile?.SuburbId,
            PendingSubmissions = CurrentUserId == null ? new List<PendingSubmissionItem>() :
                _context.PendingApprovals.AsNoTracking()
                    .Where(p => p.SubmittedByUserId == CurrentUserId.Value && p.ApprovalStatusId == 1)
                    .OrderBy(p => p.SubmittedOn).ToList()
                    .Select(p => new PendingSubmissionItem
                    {
                        Type = p.ApprovalTypeId == 1 ? "Condition" : p.ApprovalTypeId == 2 ? "Allergy" : "Medication",
                        Name = p.Name,
                        SubmittedOn = p.SubmittedOn
                    }).ToList(),
            Suburb = _suburb,
            City = _city,
            PostalCode = _postalCode,
            PrimaryDoctor = _primaryDoctor,
            PrimaryDoctors = _primaryDoctors.ToList(),
            Conditions = _conditions.ToList(),
            ConditionDetails = _conditionDetails.ToList(),
            Allergies = _allergies.ToList(),
            Medications = _medications.ToList(),
            AvailableConditions = AdminController.AllConditionsStatic(_context).Where(c => !c.IsArchived).ToList(),
            AvailableAllergies = AdminController.AllAllergiesStatic(_context).Where(a => !a.IsArchived).ToList(),
            AvailableMedications = AdminController.AllMedicationsStatic(_context).Where(m => !m.IsArchived).ToList(),
            ConditionCategories = GetCatsFromDb("Medical"),
            AllergyCategories = GetCatsFromDb("Allergy"),
            MedicationCategories = GetCatsFromDb("Medication"),
            TwoFactorEnabled = _twoFactor?.IsEnabled ?? false,
            TwoFactorEnabledOn = _twoFactor?.EnabledOn,
            TwoFactorLastVerifiedOn = _twoFactor?.LastVerifiedOn,
            BackupCodesRemaining = CurrentUser == null ? 0 :
                _context.UserBackupCodes.Count(b => b.UserId == CurrentUser.UserId && b.UsedOn == null)
        };

        // Only the ENABLED row matters for display - a row can exist but be
        // mid-setup (created by Setup2FA, not yet confirmed with a real code).
        private GenUserTwoFactor? _twoFactorCache;
        // The 2FA row, only counted once it has been confirmed.
        private GenUserTwoFactor? _twoFactor => _twoFactorCache ??=
            CurrentUser == null ? null :
            _context.UserTwoFactors.FirstOrDefault(t => t.UserId == CurrentUser.UserId);

        // Categories now come from the real database (same table AdminController writes to),
        // so whatever Admin adds/renames/archives shows up here immediately - no separate copy.
        private List<CategoryItem> GetCatsFromDb(string domain) =>
            _context.Categories
                .Where(c => c.DomainType == domain && !c.IsArchived)
                .Select(c => new CategoryItem { Id = c.CategoryId, Name = c.Name, SysCode = c.SysCode, DomainType = c.DomainType, IsArchived = c.IsArchived })
                .ToList();

        // Gathers everything the consent page shows.
        private ConsentViewModel BuildConsent() => new()
        {
            PrimaryDoctor = _primaryDoctor,
            PrimaryDoctors = _primaryDoctors.ToList(),
            AuthorizedDoctors = _consentDoctors.ToList(),
            AllTestRequests = _requests.ToList()
        };

        // Builds the PDF helper used by the downloads.
        private PdfService GetPdf() =>
            new PdfService(HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>());

        // ═══════════════════════════════════════════════════════════════════
        // GET ACTIONS
        // ═══════════════════════════════════════════════════════════════════

        // Dashboard.
        public IActionResult Index(string? trackedTest = null) => View(BuildDashboard(trackedTest));

        // Medical profile page.
        public IActionResult MedicalProfile() => View(BuildProfile());

        // Consent page where doctor access is managed.
        public IActionResult ConsentManagement()
        {
            SyncTreatingDoctors();
            return View(BuildConsent());
        }

        // Every doctor who has requested tests for this patient shows up on the Consent page.
        // Doctors the patient already revoked are left alone, and the 5 doctor limit is respected.
        private void SyncTreatingDoctors()
        {
            if (CurrentUserId == null) return;
            var uid = CurrentUserId.Value;
            var requested = _context.TestRequests.AsNoTracking()
                .Where(r => r.PatientUserId == uid)
                .Select(r => new { r.DoctorUserId, r.TestRequestId }).ToList();
            var known = _context.PatientDoctorConsents.Where(c => c.PatientUserId == uid).Select(c => c.DoctorUserId).ToList();
            int active = _context.PatientDoctorConsents.Count(c => c.PatientUserId == uid && c.RevokedOn == null);
            bool added = false;
            foreach (var docId in requested.Select(r => r.DoctorUserId).Distinct().Except(known))
            {
                if (active >= 5) break;
                var dn = _context.Users.Where(u => u.UserId == docId).Select(u => u.FirstName + " " + u.Surname).FirstOrDefault();
                _context.PatientActivityLogs.Add(new Project.Models.Entities.PatientActivityLog
                {
                    PatientUserId = uid, Action = "Doctor added",
                    Detail = $"Dr. {dn} was added to your doctors because they requested tests for you.", Icon = "bi-person-check-fill", LogTypeId = 1
                });
                var consent = new Project.Models.Entities.PatientDoctorConsent { PatientUserId = uid, DoctorUserId = docId, GrantedOn = DateTime.UtcNow };
                _context.PatientDoctorConsents.Add(consent);
                // Access only to the tests this doctor ordered, not to everything.
                var ids = requested.Where(r => r.DoctorUserId == docId).Select(r => r.TestRequestId).ToList();
                var items = _context.TestRequestItems.Where(i => ids.Contains(i.TestRequestId)).Select(i => i.TestRequestItemId).ToList();
                foreach (var itemId in items)
                    _context.ConsentedTestRequests.Add(new Project.Models.Entities.ConsentedTestRequest { PatientDoctorConsent = consent, TestRequestItemId = itemId, IsConsented = true });
                active++; added = true;
            }
            if (added) { _context.SaveChanges(); _consentCache = null; }
        }

        // The patient's own recent activity.
        public IActionResult ActivityLog()
        {
            var entries = CurrentUserId == null
                ? new List<PatientActivityLogEntry>()
                : _context.PatientActivityLogs
                    .Where(l => l.PatientUserId == CurrentUserId.Value)
                    .OrderByDescending(l => l.Timestamp)
                    .Select(l => new PatientActivityLogEntry
                    {
                        Timestamp = l.Timestamp,
                        Action = l.Action,
                        Detail = l.Detail,
                        Icon = l.Icon
                    })
                    .ToList()
                    .Select(e => { e.Timestamp = SastTime.ToSast(e.Timestamp); return e; }).ToList();
            return View(new PatientActivityLogViewModel { Entries = entries });
        }

        // Released results list with search and filters.
        public IActionResult MyResults(int? highlight = null)
        {
            // Only show results for requests the doctor has actually released
            var releasedRequestIds = _requests.Where(r => r.Status == "Released by Doctor").Select(r => r.RequestId).ToHashSet();
            var results = _testResults.Where(r => releasedRequestIds.Contains(r.RequestId))
                                       .OrderByDescending(r => r.ResultDate)
                                       .ToList();
            ViewBag.HighlightRequestId = highlight;
            return View(new MyResultsViewModel { Results = results, Requests = _requests.ToDictionary(r => r.RequestId) });
        }

        // One test request with ALL of its tests, plus a PDF download of the whole request.
        public IActionResult RequestResults(int id)
        {
            var request = _requests.FirstOrDefault(r => r.RequestId == id);
            if (request == null || request.Status != "Released by Doctor")
            {
                TempData["Error"] = "Results for that request are not available yet.";
                return RedirectToAction("MyResults");
            }
            var results = _testResults.Where(r => r.RequestId == id).OrderBy(r => r.TestName).ToList();
            LogPatientActivity("Results viewed", $"You opened the results for {request.TestName}.", "bi-eye-fill");
            return View(new RequestResultsViewModel { Request = request, Results = results });
        }

        // Trend chart for one test over a date range.
        public IActionResult TrackResults(string? selectedTest = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            // Dates in the future make no sense - clamp to today; swap if the range is back-to-front.
            var today = SastTime.ToSast(DateTime.UtcNow).Date;
            if (fromDate > today) fromDate = today;
            if (toDate > today) toDate = today;
            if (fromDate > toDate) (fromDate, toDate) = (toDate, fromDate);

            // The test-type list is built from the patient's own released results (ignoring the date filter).
            var allTrends = BuildTrends();
            var trends = BuildTrends(fromDate, toDate);
            var series = trends.FirstOrDefault(t => t.Name == selectedTest) ?? trends.FirstOrDefault(t => t.Name == "Total Cholesterol") ?? trends.FirstOrDefault();
            var points = series?.Points ?? new List<TrendDataPoint>();
            var latest = points.LastOrDefault()?.Value ?? 0;
            var prev = points.Count > 1 ? points[points.Count - 2].Value : latest;
            var change = prev != 0 ? Math.Round((latest - prev) / prev * 100, 1) : 0;

            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.Today = today.ToString("yyyy-MM-dd");
            ViewBag.HasAnyResults = allTrends.Any();

            return View(new TrackResultsViewModel
            {
                AvailableTests = allTrends.Select(t => t.Name).ToList(),
                SelectedTest = series?.Name ?? (selectedTest ?? ""),
                Unit = series?.Unit ?? "",
                NormalRangeMax = series?.Max ?? 0,
                NormalRangeMin = series?.Min ?? 0,
                MedianValue = Median(points.Select(p => p.Value)),
                DataPoints = points,
                LatestValue = latest,
                ChangePercent = change
            });
        }

        // Report list and the form to generate a new one.
        public IActionResult Reports()
        {
            ViewBag.ReleasedResultCount = _testResults.Count;
            ViewBag.Categories = _testResults.Select(r => r.Category).Distinct().OrderBy(c => c).ToList();
            // Per category: result count, the tests in it and the dates covered, so the page can explain itself.
            ViewBag.CategoryInfo = _testResults.GroupBy(r => r.Category).OrderBy(g => g.Key)
                .Select(g => new ReportCategoryInfo
                {
                    Category = g.Key,
                    Count = g.Count(),
                    Tests = g.GroupBy(r => r.TestName).OrderBy(t => t.Key).Select(t => $"{t.Key} ({t.Count()})").ToList(),
                    First = g.Min(r => r.ResultDate).ToString("yyyy-MM-dd"),
                    Last = g.Max(r => r.ResultDate).ToString("yyyy-MM-dd")
                }).ToList();
            ViewBag.FirstResult = _testResults.Any() ? _testResults.Min(r => r.ResultDate).ToString("yyyy-MM-dd") : "";
            ViewBag.Today = SastTime.ToSast(DateTime.UtcNow).ToString("yyyy-MM-dd");

            // Report history comes from this patient's own activity log (not a shared in-memory list).
            var generated = CurrentUserId == null ? new List<GeneratedReport>() :
                _context.PatientActivityLogs.AsNoTracking()
                    .Where(l => l.PatientUserId == CurrentUserId.Value && l.Action == "Report generated")
                    .OrderByDescending(l => l.Timestamp).Take(20).ToList()
                    .Select(l =>
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(l.Detail ?? "", @"^'(.+)' \((.+)\)$");
                        return new GeneratedReport
                        {
                            FileName = m.Success ? m.Groups[1].Value : (l.Detail ?? ""),
                            Category = m.Success ? m.Groups[2].Value : "",
                            GeneratedDate = SastTime.ToSast(l.Timestamp)
                        };
                    }).ToList();
            return View(new ReportsViewModel { GeneratedReports = generated });
        }

        // Live doctor search for the consent and primary doctor boxes.
        [HttpGet]
        public IActionResult SearchDoctors(string? q)
        {
            // An empty box lists everyone, so the patient can browse before typing.
            var query = (q ?? "").Trim().ToLower();
            var results = _allDoctors
                .Where(d => query.Length == 0 ||
                            d.Name.ToLower().Contains(query) ||
                            d.HpcsaNumber.ToLower().Contains(query) ||
                            d.Specialty.ToLower().Contains(query))
                .Take(30)
                .Select(d => new { d.Name, d.HpcsaNumber, d.Practice, d.Specialty })
                .ToList();
            return Json(results);
        }

        // ═══════════════════════════════════════════════════════════════════
        // POST - PRIMARY DOCTOR
        // ═══════════════════════════════════════════════════════════════════

        // Finds (or creates) this patient's consent row for a doctor and makes it active again.
        private Project.Models.Entities.PatientDoctorConsent? GetOrCreateConsent(string hpcsaNumber, out string doctorName)
        {
            doctorName = "";
            var doc = (from d in _context.DoctorProfiles
                       join u in _context.Users on d.UserId equals u.UserId
                       where !d.IsArchived && d.HpcsaNumber == hpcsaNumber.Trim()
                       select new { d.UserId, u.FirstName, u.Surname }).FirstOrDefault();
            if (doc == null || CurrentUserId == null) return null;
            doctorName = $"Dr. {doc.FirstName} {doc.Surname}";
            var uid = CurrentUserId.Value;
            var consent = _context.PatientDoctorConsents.FirstOrDefault(c => c.PatientUserId == uid && c.DoctorUserId == doc.UserId);
            if (consent == null)
            {
                consent = new Project.Models.Entities.PatientDoctorConsent { PatientUserId = uid, DoctorUserId = doc.UserId, GrantedOn = DateTime.UtcNow };
                _context.PatientDoctorConsents.Add(consent);
            }
            else if (consent.RevokedOn != null)
            {
                consent.RevokedOn = null;
                consent.GrantedOn = DateTime.UtcNow;
                _context.ConsentedTestRequests.RemoveRange(_context.ConsentedTestRequests.Where(c => c.PatientDoctorConsentId == consent.PatientDoctorConsentId));
            }
            return consent;
        }

        // Test-request-item ids for requests that belong to THIS patient (never trust ids from the form).
        private List<int> OwnItemIds(IEnumerable<int> requestIds)
        {
            if (CurrentUserId == null) return new();
            var uid = CurrentUserId.Value; var ids = requestIds.ToList();
            return _context.TestRequestItems.Where(i => ids.Contains(i.TestRequestId) && i.TestRequest.PatientUserId == uid)
                .Select(i => i.TestRequestItemId).ToList();
        }

        // E-mails the doctor when access is granted.
        private void NotifyDoctorOfConsent(string hpcsaNumber, string doctorName, bool full)
        {
            try
            {
                var email = (from d in _context.DoctorProfiles join u in _context.Users on d.UserId equals u.UserId
                             where d.HpcsaNumber == hpcsaNumber.Trim() select u.Email).FirstOrDefault();
                var mail = HttpContext.RequestServices.GetService<EmailService>();
                if (email == null || mail == null) return;
                mail.SendEmail(email, "A patient has shared their test results with you",
                    $"<p>Hello {System.Net.WebUtility.HtmlEncode(doctorName)},</p><p>{System.Net.WebUtility.HtmlEncode(_patientName)} has given you access to " +
                    (full ? "all of their" : "selected") + " previous test results on HemaFlow.</p><p>Regards,<br/>HemaFlow</p>");
            }
            catch (Exception ex)
            {
                HttpContext.RequestServices.GetRequiredService<ILogger<PatientController>>().LogWarning(ex, "Consent e-mail failed");
            }
        }

        // Makes a doctor primary (maximum of five).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetPrimaryDoctor(string hpcsaNumber)
        {
            if (string.IsNullOrWhiteSpace(hpcsaNumber)) { TempData["Error"] = "HPCSA number is required."; return RedirectToAction("MedicalProfile"); }
            if (_primaryDoctors.Any(d => string.Equals(d.HpcsaNumber, hpcsaNumber.Trim(), StringComparison.OrdinalIgnoreCase)))
            { TempData["Error"] = "That doctor is already one of your primary doctors."; return RedirectToAction("MedicalProfile"); }
            if (_primaryDoctors.Count >= MaxPrimaryDoctors)
            { TempData["Error"] = $"You can have up to {MaxPrimaryDoctors} primary doctors. Remove one first."; return RedirectToAction("MedicalProfile"); }

            var consent = GetOrCreateConsent(hpcsaNumber, out var name);
            if (consent == null) { TempData["Error"] = $"Doctor with HPCSA number '{hpcsaNumber}' not found."; return RedirectToAction("MedicalProfile"); }
            // Primary doctor = full access: drop any per-test limits.
            if (consent.PatientDoctorConsentId != 0)
                _context.ConsentedTestRequests.RemoveRange(_context.ConsentedTestRequests.Where(c => c.PatientDoctorConsentId == consent.PatientDoctorConsentId));
            _context.SaveChanges();

            NotifyDoctorOfConsent(hpcsaNumber, name, true);
            TempData["Success"] = $"{name} added as a primary doctor.";
            LogPatientActivity("Primary doctor added", $"Added {name} as a primary doctor.", "bi-person-check-fill");
            return RedirectToAction("MedicalProfile");
        }

        // Removes a primary doctor.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemovePrimaryDoctor(string hpcsaNumber)
        {
            var doc = _primaryDoctors.FirstOrDefault(d => d.HpcsaNumber == hpcsaNumber);
            if (doc == null) { TempData["Error"] = "That doctor is not one of your primary doctors."; return RedirectToAction("MedicalProfile"); }
            RevokeConsentRow(hpcsaNumber);
            TempData["Success"] = $"{doc.Name} removed as a primary doctor and no longer has access.";
            LogPatientActivity("Primary doctor removed", $"Removed {doc.Name} as a primary doctor.", "bi-person-dash-fill");
            return RedirectToAction("MedicalProfile");
        }

        // Deletes the consent row for one doctor.
        private void RevokeConsentRow(string hpcsaNumber)
        {
            if (CurrentUserId == null) return;
            var uid = CurrentUserId.Value;
            var consent = (from c in _context.PatientDoctorConsents
                           join d in _context.DoctorProfiles on c.DoctorUserId equals d.UserId
                           where c.PatientUserId == uid && d.HpcsaNumber == hpcsaNumber && c.RevokedOn == null
                           select c).FirstOrDefault();
            if (consent == null) return;
            consent.RevokedOn = DateTime.UtcNow;
            _context.ConsentedTestRequests.RemoveRange(_context.ConsentedTestRequests.Where(c => c.PatientDoctorConsentId == consent.PatientDoctorConsentId));
            _context.SaveChanges();
        }

        // ═══════════════════════════════════════════════════════════════════
        // ACTIONS - EDIT PROFILE (MODAL OR STANDALONE)
        // ═══════════════════════════════════════════════════════════════════

        // Edit profile form.
        [HttpGet]
        public IActionResult EditProfile()
        {
            // Start the form with the current e-mail and phone
            var model = new EditProfileViewModel
            {
                Email = _email,
                Phone = _phone
            };

            return View(model);
        }

        // Saves name, phone, e-mail and address. ID and date of birth stay locked.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditProfile(EditProfileViewModel model)
        {
            // Save onto the signed-in patient's own rows.
            var user = CurrentUser;
            if (user == null) return RedirectToAction("Login", "Account");

            if (!string.IsNullOrWhiteSpace(model.FirstName) && !InputRules.IsValidName(model.FirstName))
            { TempData["Error"] = InputRules.NameMessage; return RedirectToAction("MedicalProfile"); }
            if (!string.IsNullOrWhiteSpace(model.Surname) && !InputRules.IsValidName(model.Surname))
            { TempData["Error"] = InputRules.NameMessage; return RedirectToAction("MedicalProfile"); }
            if (!string.IsNullOrWhiteSpace(model.Phone) && !InputRules.IsValidPhone(model.Phone))
            { TempData["Error"] = InputRules.PhoneMessage; return RedirectToAction("MedicalProfile"); }
            if (!string.IsNullOrWhiteSpace(model.FirstName))
                user.FirstName = model.FirstName.Trim();
            if (!string.IsNullOrWhiteSpace(model.Surname))
                user.Surname = model.Surname.Trim();
            var emailChanged = false;
            if (!string.IsNullOrWhiteSpace(model.Email))
            {
                // The e-mail address is the patient's username, so it must be a valid address that nobody else uses.
                var newEmail = model.Email.Trim();
                if (!InputRules.IsValidEmail(newEmail))
                {
                    TempData["Error"] = "Enter a valid email address, for example name@example.com.";
                    return RedirectToAction("MedicalProfile");
                }
                if (!string.Equals(newEmail, user.Email, StringComparison.OrdinalIgnoreCase))
                {
                    if (_context.Users.Any(u => u.UserId != user.UserId && u.Email == newEmail))
                    {
                        TempData["Error"] = "That email address is already used by another account.";
                        return RedirectToAction("MedicalProfile");
                    }
                    var oldEmail = user.Email;
                    user.Email = newEmail;
                    emailChanged = true;
                    // Tell the OLD address, so a hijacked session cannot silently take over the account.
                    try
                    {
                        HttpContext.RequestServices.GetService<EmailService>()?.SendEmail(oldEmail, "Your HemaFlow login email was changed",
                            $"<p>Hi {System.Net.WebUtility.HtmlEncode(user.FirstName)},</p><p>The login email on your HemaFlow account was changed to <strong>{System.Net.WebUtility.HtmlEncode(newEmail)}</strong>. If this was not you, please contact the laboratory straight away.</p>");
                    }
                    catch (Exception ex) { HttpContext.RequestServices.GetService<ILogger<PatientController>>()?.LogWarning(ex, "Could not notify old email address"); }
                    LogPatientActivity("Email changed", $"Your login email was changed from {oldEmail} to {newEmail}.", "bi-envelope-at-fill");
                }
            }
            if (!string.IsNullOrWhiteSpace(model.Phone))
                user.Phone = model.Phone.Trim();

            // The SA ID number and date of birth are identity data fixed at registration: whatever the form posts for them is ignored.
            var profile = _context.PatientProfiles.FirstOrDefault(p => p.UserId == user.UserId);

            if (profile != null)
            {
                // ID number and date of birth are locked after registration (see above).
                if (!string.IsNullOrWhiteSpace(model.AddressLine1))
                    profile.AddressLine1 = model.AddressLine1.Trim();
                profile.ComplexName = string.IsNullOrWhiteSpace(model.ComplexName) ? null : model.ComplexName.Trim();
                // Address: either picked from the dropdowns (SuburbId) or typed in as "not listed" - both end up
                // as a Suburb row, so the address is always stored in the normalised tables.
                var anyAddress = model.SuburbId > 0 || !string.IsNullOrWhiteSpace(model.Suburb) || !string.IsNullOrWhiteSpace(model.City);
                if (anyAddress)
                {
                    var newSuburbId = LocationHelper.ResolveOrCreate(_context, model.SuburbId, model.ProvinceId, model.City, model.Suburb, model.PostalCode, out var addrError);
                    if (newSuburbId == null) { TempData["Error"] = addrError ?? "Please check your address."; return RedirectToAction("MedicalProfile"); }
                    profile.SuburbId = newSuburbId;
                }
            }

            _context.SaveChanges();

            // Keep the signed-in session in step with the new name / e-mail (claims are read from the cookie).
            AuthCookieHelper.RefreshNameAndEmailAsync(HttpContext, user).GetAwaiter().GetResult();

            TempData["Success"] = emailChanged
                ? "Personal details updated. Your login email is now " + user.Email + " - use it the next time you sign in."
                : "Personal details updated successfully.";
            LogPatientActivity("Profile updated", "Personal details were updated.", "bi-pencil-fill");
            return RedirectToAction("MedicalProfile");
        }

        // ═══════════════════════════════════════════════════════════════════
        // POST - MEDICAL HISTORY
        // ═══════════════════════════════════════════════════════════════════

        // Category the patient picked in the "filter by category" box when submitting an "Other" item
        // (only a suggestion - the admin still chooses the category when approving).
        private int? SuggestedCategoryId(string domain, string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return _context.Categories.Where(c => c.DomainType == domain && c.Name == name.Trim() && !c.IsArchived)
                .Select(c => (int?)c.CategoryId).FirstOrDefault();
        }

        // Shared by the Add Condition button, including the 'Other' request path.
        private IActionResult AddConditionCore(string conditionName, bool isOther = false, string? suggestedCategory = null, DateTime? diagnosedOn = null)
        {
            if (string.IsNullOrWhiteSpace(conditionName))
            {
                TempData["Error"] = "Condition name cannot be empty.";
                return RedirectToAction("MedicalProfile");
            }
            var name = conditionName.Trim();
            if (_conditions.Any(c => c.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                TempData["Error"] = $"'{name}' is already in your conditions list.";
                return RedirectToAction("MedicalProfile");
            }

            if (isOther)
            {
                if (CurrentUserId == null)
                {
                    TempData["Error"] = "You must be logged in to submit a new condition for review.";
                    return RedirectToAction("MedicalProfile");
                }
                if (_context.PendingApprovals.Any(p => p.SubmittedByUserId == CurrentUserId.Value && p.ApprovalStatusId == 1 && p.ApprovalTypeId == 1 && p.Name == name))
                {
                    TempData["Error"] = $"'{name}' is already waiting for admin review.";
                    return RedirectToAction("MedicalProfile");
                }
                _context.PendingApprovals.Add(new GenPendingApproval
                {
                    CategoryId = SuggestedCategoryId("Medical", suggestedCategory),
                    ApprovalTypeId = 1, // NewCondition
                    ApprovalStatusId = 1, // Pending
                    Name = name,
                    SubmittedByUserId = CurrentUserId.Value,
                    SubmittedOn = DateTime.UtcNow
                });
                _context.SaveChanges();
                TempData["Success"] = $"'{name}' has been submitted for admin review. Check back within 24 hours to see if it's been added to your profile.";
                LogPatientActivity("Condition submitted", $"Submitted '{name}' for admin review.", "bi-hourglass-split");
                return RedirectToAction("MedicalProfile");
            }

            // Selected from the real list - confirm it's still a valid, non-archived condition
            var match = AdminController.AllConditionsStatic(_context).FirstOrDefault(c => !c.IsArchived && c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                TempData["Error"] = "Please select a valid condition from the list, or choose 'Other' to submit a new one for review.";
                return RedirectToAction("MedicalProfile");
            }

            AddPatientCondition(match.Id, CleanPastDate(diagnosedOn));
            TempData["Success"] = $"Condition '{match.Name}' added.";
            LogPatientActivity("Condition added", $"Added '{match.Name}' to your medical conditions.", "bi-plus-circle-fill");
            return RedirectToAction("MedicalProfile");
        }

        // Wraps the add actions so a database failure shows the real reason on the
        // Medical Profile page (and is logged) instead of the generic error page.
        // TEMPORARY: remove the detail from the message before final submission.
        private IActionResult SafeMedicalAdd(string action, Func<IActionResult> body)
        {
            try
            {
                return body();
            }
            catch (Exception ex)
            {
                var root = ex;
                while (root.InnerException != null) root = root.InnerException;
                HttpContext.RequestServices.GetRequiredService<ILogger<PatientController>>()
                    .LogError(ex, "{Action} failed for user {UserId}", action, CurrentUserId);
                foreach (var entry in _context.ChangeTracker.Entries().Where(e => e.State == Microsoft.EntityFrameworkCore.EntityState.Added).ToList())
                    entry.State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                TempData["Error"] = "We could not save that change right now. Please try again, or contact support if it keeps happening.";
                return RedirectToAction("MedicalProfile");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddCondition(string conditionName, bool isOther = false, string? suggestedCategory = null, DateTime? diagnosedOn = null)
            => SafeMedicalAdd("AddCondition", () => AddConditionCore(conditionName, isOther, suggestedCategory, diagnosedOn));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddAllergy(string allergyName, string? allergyCategory, bool isOther = false, string? suggestedCategory = null, string? severity = null)
            => SafeMedicalAdd("AddAllergy", () => AddAllergyCore(allergyName, suggestedCategory ?? allergyCategory, isOther, severity));

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddMedication(string medName, string? strength, string? dose, string? form, string? frequency, bool isOther = false, string? suggestedCategory = null, DateTime? startedOn = null)
            => SafeMedicalAdd("AddMedication", () => AddMedicationCore(medName, strength, dose, form, frequency, isOther, suggestedCategory, startedOn));

        // Removes a condition after the confirm popup.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveCondition(string conditionName)
        {
            RemovePatientCondition(conditionName);
            LogPatientActivity("Condition removed", $"Removed '{conditionName}' from your medical conditions.", "bi-trash3-fill");
            TempData["Success"] = $"Condition '{conditionName}' removed.";
            return RedirectToAction("MedicalProfile");
        }

        // Shared by the Add Allergy button, including the 'Other' request path.
        private IActionResult AddAllergyCore(string allergyName, string? allergyCategory, bool isOther = false, string? severity = null)
        {
            if (string.IsNullOrWhiteSpace(allergyName))
            {
                TempData["Error"] = "Allergy name cannot be empty.";
                return RedirectToAction("MedicalProfile");
            }
            var name = allergyName.Trim();
            if (_allergies.Any(a => a.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                TempData["Error"] = $"'{name}' is already in your allergy list.";
                return RedirectToAction("MedicalProfile");
            }

            if (isOther)
            {
                if (CurrentUserId == null)
                {
                    TempData["Error"] = "You must be logged in to submit a new allergy for review.";
                    return RedirectToAction("MedicalProfile");
                }
                if (_context.PendingApprovals.Any(p => p.SubmittedByUserId == CurrentUserId.Value && p.ApprovalStatusId == 1 && p.ApprovalTypeId == 2 && p.Name == name))
                {
                    TempData["Error"] = $"'{name}' is already waiting for admin review.";
                    return RedirectToAction("MedicalProfile");
                }
                _context.PendingApprovals.Add(new GenPendingApproval
                {
                    CategoryId = SuggestedCategoryId("Allergy", allergyCategory),
                    ApprovalTypeId = 2, // NewAllergy
                    ApprovalStatusId = 1, // Pending
                    Name = name,
                    SubmittedByUserId = CurrentUserId.Value,
                    SubmittedOn = DateTime.UtcNow
                });
                _context.SaveChanges();
                TempData["Success"] = $"'{name}' has been submitted for admin review. Check back within 24 hours to see if it's been added to your profile.";
                LogPatientActivity("Allergy submitted", $"Submitted '{name}' for admin review.", "bi-hourglass-split");
                return RedirectToAction("MedicalProfile");
            }

            // Selected from the real list - re-derive the category server-side rather than
            // trusting the client, so it always matches the real master-list entry.
            var match = AdminController.AllAllergiesStatic(_context).FirstOrDefault(a => !a.IsArchived && a.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                TempData["Error"] = "Please select a valid allergy from the list, or choose 'Other' to submit a new one for review.";
                return RedirectToAction("MedicalProfile");
            }

            AddPatientAllergy(match.Id, severity);
            TempData["Success"] = $"Allergy '{match.Name}' added.";
            LogPatientActivity("Allergy added", $"Added '{match.Name}' to your allergies.", "bi-plus-circle-fill");
            return RedirectToAction("MedicalProfile");
        }

        // Removes an allergy after the confirm popup.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveAllergy(string allergyName)
        {
            RemovePatientAllergy(allergyName);
            LogPatientActivity("Allergy removed", $"Removed '{allergyName}' from your allergies.", "bi-trash3-fill");
            TempData["Success"] = $"Allergy '{allergyName}' removed.";
            return RedirectToAction("MedicalProfile");
        }

        // Shared by the Add Medication button, including the 'Other' request path.
        private IActionResult AddMedicationCore(string medName, string? strength, string? dose, string? form, string? frequency, bool isOther = false, string? suggestedCategory = null, DateTime? startedOn = null)
        {
            if (string.IsNullOrWhiteSpace(medName))
            {
                TempData["Error"] = "Medication name cannot be empty.";
                return RedirectToAction("MedicalProfile");
            }
            var name = medName.Trim();
            if (_medications.Any(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                TempData["Error"] = $"'{name}' is already in your medication list.";
                return RedirectToAction("MedicalProfile");
            }

            if (isOther)
            {
                if (CurrentUserId == null)
                {
                    TempData["Error"] = "You must be logged in to submit a new medication for review.";
                    return RedirectToAction("MedicalProfile");
                }
                var extra = string.Join(" / ", new[] { strength, dose, form, frequency }.Where(s => !string.IsNullOrWhiteSpace(s)));
                if (_context.PendingApprovals.Any(p => p.SubmittedByUserId == CurrentUserId.Value && p.ApprovalStatusId == 1 && p.ApprovalTypeId == 3 && p.Name == name))
                {
                    TempData["Error"] = $"'{name}' is already waiting for admin review.";
                    return RedirectToAction("MedicalProfile");
                }
                _context.PendingApprovals.Add(new GenPendingApproval
                {
                    CategoryId = SuggestedCategoryId("Medication", suggestedCategory),
                    ApprovalTypeId = 3, // NewMedication
                    ApprovalStatusId = 1, // Pending
                    Name = name,
                    ExtraInfo = string.IsNullOrWhiteSpace(extra) ? null : extra,
                    SubmittedByUserId = CurrentUserId.Value,
                    SubmittedOn = DateTime.UtcNow
                });
                _context.SaveChanges();
                TempData["Success"] = $"'{name}' has been submitted for admin review. Check back within 24 hours to see if it's been added to your profile.";
                LogPatientActivity("Medication submitted", $"Submitted '{name}' for admin review.", "bi-hourglass-split");
                return RedirectToAction("MedicalProfile");
            }

            var match = AdminController.AllMedicationsStatic(_context).FirstOrDefault(m => !m.IsArchived && m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (match == null)
            {
                TempData["Error"] = "Please select a valid medication from the list, or choose 'Other' to submit a new one for review.";
                return RedirectToAction("MedicalProfile");
            }

            AddPatientMedication(match.Id, strength, dose, form, frequency, CleanPastDate(startedOn));
            TempData["Success"] = $"Medication '{match.Name}' added.";
            LogPatientActivity("Medication added", $"Added '{match.Name}' to your medications.", "bi-plus-circle-fill");
            return RedirectToAction("MedicalProfile");
        }

        // Removes a medication after the confirm popup.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemoveMedication(string medName)
        {
            RemovePatientMedication(medName);
            LogPatientActivity("Medication removed", $"Removed '{medName}' from your medications.", "bi-trash3-fill");
            TempData["Success"] = $"Medication '{medName}' removed.";
            return RedirectToAction("MedicalProfile");
        }

        // ═══════════════════════════════════════════════════════════════════
        // PASSWORD
        // ═══════════════════════════════════════════════════════════════════

        // Change password form.
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View(new AccountChangePasswordViewModel());
        }

        // Checks the current password and the password rule, then saves the new one.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ChangePassword(AccountChangePasswordViewModel model)
        {
            if (model.NewPassword != model.ConfirmPassword)
            {
                TempData["Error"] = "New password and confirmation do not match.";
                return RedirectToAction("ChangePassword");
            }

            if (!Project.Services.PasswordPolicy.IsValid(model.NewPassword))
            {
                TempData["Error"] = Project.Services.PasswordPolicy.Message;
                return RedirectToAction("ChangePassword");
            }

            var user = _context.Users.FirstOrDefault(u => u.UserId == CurrentUserId);
            if (user == null) return RedirectToAction("Login", "Account");

            var hasher = new PasswordHasher<GenUser>();
            var verification = hasher.VerifyHashedPassword(user, user.PasswordHash, model.CurrentPassword);
            if (verification == PasswordVerificationResult.Failed)
            {
                TempData["Error"] = "Current password is incorrect.";
                return RedirectToAction("ChangePassword");
            }

            user.PasswordHash = hasher.HashPassword(user, model.NewPassword);
            _context.SaveChanges();

            TempData["Success"] = "Your password has been changed.";
            LogPatientActivity("Password changed", "Your account password was changed.", "bi-key-fill");
            return RedirectToAction("MedicalProfile");
        }

        // ═══════════════════════════════════════════════════════════════════
        // TWO-FACTOR AUTHENTICATION (real TOTP, works with Google/Microsoft
        // Authenticator).
        // ═══════════════════════════════════════════════════════════════════

        // GET: /Patient/Setup2FA - shows the QR code + manual key to scan.
        [HttpGet]
        public IActionResult Setup2FA()
        {
            if (CurrentUser == null) return RedirectToAction("Login", "Account");

            if (_twoFactor != null && _twoFactor.IsEnabled)
            {
                TempData["Success"] = "Two-factor authentication is already enabled.";
                return RedirectToAction("MedicalProfile");
            }

            // Reuse an in-progress (not yet confirmed) secret if one exists,
            // so refreshing this page doesn't invalidate a code already scanned.
            var row = _twoFactor;
            if (row == null)
            {
                row = new GenUserTwoFactor
                {
                    UserId = CurrentUser.UserId,
                    SecretKey = TotpService.GenerateSecretKey(),
                    IsEnabled = false
                };
                _context.UserTwoFactors.Add(row);
                _context.SaveChanges();
            }

            var model = new TwoFactorSetupViewModel
            {
                SecretKey = row.SecretKey,
                OtpAuthUri = TotpService.BuildOtpAuthUri("HemaFlow", _email, row.SecretKey),
                AccountEmail = _email
            };
            return View(model);
        }

        // POST: /Patient/Setup2FAConfirm - verifies the code the patient just
        // read off their authenticator app; only then does 2FA actually turn on.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Setup2FAConfirm(string code)
        {
            if (CurrentUser == null) return RedirectToAction("Login", "Account");

            var row = _twoFactor;
            if (row == null)
            {
                TempData["Error"] = "Start the setup again.";
                return RedirectToAction("Setup2FA");
            }

            if (!TotpService.ValidateCode(row.SecretKey, code))
            {
                TempData["Error"] = "That code didn't match. Make sure your authenticator app's clock is correct and try the current code.";
                return RedirectToAction("Setup2FA");
            }

            row.IsEnabled = true;
            row.EnabledOn = DateTime.UtcNow;
            row.LastVerifiedOn = DateTime.UtcNow;

            // Fresh backup codes every time 2FA is (re-)enabled - clear any old ones first.
            var oldCodes = _context.UserBackupCodes.Where(b => b.UserId == CurrentUser.UserId);
            _context.UserBackupCodes.RemoveRange(oldCodes);

            var plainCodes = TotpService.GenerateBackupCodes();
            foreach (var plain in plainCodes)
            {
                _context.UserBackupCodes.Add(new GenUserBackupCode
                {
                    UserId = CurrentUser.UserId,
                    CodeHash = TotpService.HashBackupCode(plain)
                });
            }
            _context.SaveChanges();

            LogPatientActivity("Two-factor authentication enabled", "An authenticator app was linked to your account.", "bi-shield-check");

            // Shown exactly once - MedicalProfile.cshtml pops a modal when this is present.
            TempData["NewBackupCodes"] = string.Join(",", plainCodes);
            TempData["Success"] = "Two-factor authentication is now enabled. Save your backup codes somewhere safe.";
            return RedirectToAction("MedicalProfile");
        }

        // POST: /Patient/DisableTwoFactor - password-gated, like disabling any
        // security feature should be.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DisableTwoFactor(string password)
        {
            var user = CurrentUser;
            if (user == null) return RedirectToAction("Login", "Account");

            var hasher = new PasswordHasher<GenUser>();
            if (hasher.VerifyHashedPassword(user, user.PasswordHash, password ?? "") == PasswordVerificationResult.Failed)
            {
                TempData["Error"] = "Incorrect password. Two-factor authentication was not disabled.";
                return RedirectToAction("MedicalProfile");
            }

            var row = _twoFactor;
            if (row != null) _context.UserTwoFactors.Remove(row);
            _context.UserBackupCodes.RemoveRange(_context.UserBackupCodes.Where(b => b.UserId == user.UserId));
            _context.SaveChanges();

            LogPatientActivity("Two-factor authentication disabled", "The authenticator app link was removed from your account.", "bi-shield-x");
            TempData["Success"] = "Two-factor authentication has been disabled.";
            return RedirectToAction("MedicalProfile");
        }

        // POST: /Patient/RegenerateBackupCodes
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RegenerateBackupCodes()
        {
            if (CurrentUser == null) return RedirectToAction("Login", "Account");
            if (_twoFactor == null || !_twoFactor.IsEnabled)
            {
                TempData["Error"] = "Enable two-factor authentication first.";
                return RedirectToAction("MedicalProfile");
            }

            _context.UserBackupCodes.RemoveRange(_context.UserBackupCodes.Where(b => b.UserId == CurrentUser.UserId));
            var plainCodes = TotpService.GenerateBackupCodes();
            foreach (var plain in plainCodes)
            {
                _context.UserBackupCodes.Add(new GenUserBackupCode
                {
                    UserId = CurrentUser.UserId,
                    CodeHash = TotpService.HashBackupCode(plain)
                });
            }
            _context.SaveChanges();

            LogPatientActivity("Backup codes regenerated", "New two-factor backup codes were generated; the old ones no longer work.", "bi-key-fill");
            TempData["NewBackupCodes"] = string.Join(",", plainCodes);
            TempData["Success"] = "New backup codes generated. Your old codes no longer work.";
            return RedirectToAction("MedicalProfile");
        }

        // ═══════════════════════════════════════════════════════════════════
        // POST - CONSENT
        // ═══════════════════════════════════════════════════════════════════

        // Adds access for a doctor, either to everything or to chosen requests.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddConsentAccess(string hpcsaNumber, List<int>? requestIds)
        {
            var doc = _consentDoctors.FirstOrDefault(d => d.HpcsaNumber == hpcsaNumber);
            if (doc == null) { TempData["Error"] = "Doctor not found."; return RedirectToAction("ConsentManagement"); }
            if (doc.IsFullAccess) { TempData["Error"] = $"{doc.DoctorName} already has full access to all your results."; return RedirectToAction("ConsentManagement"); }
            if (requestIds == null || !requestIds.Any()) { TempData["Error"] = "Please select at least one test to add."; return RedirectToAction("ConsentManagement"); }

            var consent = GetOrCreateConsent(hpcsaNumber, out _);
            if (consent == null) { TempData["Error"] = "Doctor not found."; return RedirectToAction("ConsentManagement"); }
            var items = OwnItemIds(requestIds);
            var have = _context.ConsentedTestRequests.Where(c => c.PatientDoctorConsentId == consent.PatientDoctorConsentId).Select(c => c.TestRequestItemId).ToHashSet();
            foreach (var itemId in items.Where(i => !have.Contains(i)))
                _context.ConsentedTestRequests.Add(new Project.Models.Entities.ConsentedTestRequest { PatientDoctorConsent = consent, TestRequestItemId = itemId, IsConsented = true });
            _context.SaveChanges();

            TempData["Success"] = $"Access to {requestIds.Count} additional test request(s) granted to {doc.DoctorName}.";
            LogPatientActivity("Access extended", $"Gave {doc.DoctorName} access to {requestIds.Count} more test request(s).", "bi-shield-plus");
            return RedirectToAction("ConsentManagement");
        }

        // Grants a doctor access and e-mails them.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GrantConsent(string hpcsaNumber, string accessType, List<int>? requestIds)
        {
            if (string.IsNullOrWhiteSpace(hpcsaNumber)) { TempData["Error"] = "HPCSA number is required."; return RedirectToAction("ConsentManagement"); }
            if (_consentDoctors.Any(d => string.Equals(d.HpcsaNumber, hpcsaNumber.Trim(), StringComparison.OrdinalIgnoreCase)))
            { TempData["Error"] = "This doctor already has access."; return RedirectToAction("ConsentManagement"); }

            var isFull = accessType != "specific";
            if (!isFull && (requestIds == null || !requestIds.Any()))
            { TempData["Error"] = "Please select at least one test to share, or choose Full Access."; return RedirectToAction("ConsentManagement"); }

            var consent = GetOrCreateConsent(hpcsaNumber, out var name);
            if (consent == null) { TempData["Error"] = $"Doctor with HPCSA number '{hpcsaNumber}' not found."; return RedirectToAction("ConsentManagement"); }
            if (!isFull)
            {
                var items = OwnItemIds(requestIds!);
                if (!items.Any()) { TempData["Error"] = "None of the selected requests could be shared."; return RedirectToAction("ConsentManagement"); }
                foreach (var itemId in items)
                    _context.ConsentedTestRequests.Add(new Project.Models.Entities.ConsentedTestRequest { PatientDoctorConsent = consent, TestRequestItemId = itemId, IsConsented = true });
            }
            _context.SaveChanges();

            NotifyDoctorOfConsent(hpcsaNumber, name, isFull);
            TempData["Success"] = $"Access granted to {name}.";
            LogPatientActivity("Access granted", $"Granted {name} {(isFull ? "full" : "specific")} access to your results.", "bi-shield-check");
            return RedirectToAction("ConsentManagement");
        }

        // Takes access away, for chosen requests or for everything.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RevokeConsent(string hpcsaNumber, List<int>? requestIds, bool revokeAll = false)
        {
            var doc = _consentDoctors.FirstOrDefault(d => d.HpcsaNumber == hpcsaNumber);
            if (doc == null) return RedirectToAction("ConsentManagement");

            if (revokeAll || doc.IsFullAccess)
            {
                RevokeConsentRow(hpcsaNumber);
                TempData["Success"] = $"All access revoked from {doc.DoctorName}.";
                LogPatientActivity("Access revoked", $"Revoked all access from {doc.DoctorName}.", "bi-shield-x");
                return RedirectToAction("ConsentManagement");
            }

            if (requestIds != null && requestIds.Any())
            {
                var uid = CurrentUserId!.Value;
                var items = OwnItemIds(requestIds);
                var rows = (from cr in _context.ConsentedTestRequests
                            join c in _context.PatientDoctorConsents on cr.PatientDoctorConsentId equals c.PatientDoctorConsentId
                            join d in _context.DoctorProfiles on c.DoctorUserId equals d.UserId
                            where c.PatientUserId == uid && d.HpcsaNumber == hpcsaNumber && items.Contains(cr.TestRequestItemId)
                            select cr).ToList();
                _context.ConsentedTestRequests.RemoveRange(rows);
                _context.SaveChanges();
            }

            // If nothing is left shared, the consent itself is revoked (otherwise it would fall back to "full access").
            var stillShared = (from cr in _context.ConsentedTestRequests
                               join c in _context.PatientDoctorConsents on cr.PatientDoctorConsentId equals c.PatientDoctorConsentId
                               join d in _context.DoctorProfiles on c.DoctorUserId equals d.UserId
                               where c.PatientUserId == CurrentUserId!.Value && d.HpcsaNumber == hpcsaNumber && c.RevokedOn == null
                               select cr.ConsentedTestRequestId).Any();
            if (!stillShared)
            {
                RevokeConsentRow(hpcsaNumber);
                TempData["Success"] = $"Access revoked from {doc.DoctorName}.";
                LogPatientActivity("Access revoked", $"Revoked all access from {doc.DoctorName}.", "bi-shield-x");
            }
            else
            {
                TempData["Success"] = $"Selected test access revoked from {doc.DoctorName}.";
                LogPatientActivity("Access revoked", $"Revoked access to {(requestIds?.Count ?? 0)} test request(s) from {doc.DoctorName}.", "bi-shield-x");
            }
            return RedirectToAction("ConsentManagement");
        }

        // ═══════════════════════════════════════════════════════════════════
        // POST - REPORTS / PDF
        // ═══════════════════════════════════════════════════════════════════

        // Category report as a PDF with totals and the date range.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DownloadReport(string category, DateTime fromDate, DateTime toDate)
        {
            var today = SastTime.ToSast(DateTime.UtcNow).Date;
            if (toDate.Date > today || fromDate.Date > today)
            {
                TempData["Error"] = "Dates cannot be in the future.";
                return RedirectToAction("Reports");
            }
            if (toDate < fromDate)
            {
                TempData["Error"] = "To date cannot be before the From date.";
                return RedirectToAction("Reports");
            }

            var filtered = (category == "All" || string.IsNullOrEmpty(category)
                    ? _testResults.ToList()
                    : _testResults.Where(r => r.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList())
                .Where(r => r.ResultDate.Date >= fromDate.Date && r.ResultDate.Date <= toDate.Date)
                .OrderBy(r => r.Category).ThenBy(r => r.ResultDate).ThenBy(r => r.TestName).ToList();

            var data = new PatientCategoryReportData
            {
                PatientName = _patientName,
                IdNumber = _idNumber,
                Category = category == "All" ? "All Categories" : category,
                FromDate = fromDate,
                ToDate = toDate,
                Results = filtered
            };

            var bytes = GetPdf().GeneratePatientCategoryReport(data);
            var fileName = $"HemaFlow_{(category ?? "All").Replace(" ", "_")}_{fromDate:ddMMMyyyy}_to_{toDate:ddMMMyyyy}.pdf";
            LogPatientActivity("Report generated", $"'{fileName}' ({data.Category})", "bi-file-earmark-pdf");
            return File(bytes, "application/pdf", fileName);
        }

        // Single result as a PDF.
        [HttpGet]
        public IActionResult DownloadResult(int id, string? test = null)
        {
            // Only released requests that belong to this patient can be downloaded.
            var request = _requests.FirstOrDefault(r => r.RequestId == id);
            if (request == null || request.Status != "Released by Doctor")
            {
                TempData["Error"] = "Results for that request are not available.";
                return RedirectToAction("MyResults");
            }
            var rows = _testResults.Where(r => r.RequestId == id && (string.IsNullOrEmpty(test) || r.TestName == test))
                                   .OrderBy(r => r.Category).ThenBy(r => r.TestName).ToList();
            if (!rows.Any())
            {
                TempData["Error"] = "No results were found for that request.";
                return RedirectToAction("MyResults");
            }

            var reportData = new PatientCategoryReportData
            {
                PatientName = _patientName,
                IdNumber = _idNumber,
                Category = string.IsNullOrEmpty(test) ? request.TestName : test,
                FromDate = request.RequestDate,
                ToDate = request.RequestDate,
                Results = rows
            };
            byte[] pdfBytes = GetPdf().GeneratePatientCategoryReport(reportData);
            string cleanFileName = $"HemaFlow_Result_{id}_{(string.IsNullOrEmpty(test) ? "Request" : test.Replace(" ", "_"))}.pdf";
            LogPatientActivity("Report generated", $"'{cleanFileName}' ({reportData.Category})", "bi-file-earmark-pdf");
            return File(pdfBytes, "application/pdf", cleanFileName);
        }
    }
}
