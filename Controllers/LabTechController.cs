using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Project.Data;
using Project.Models.LabManager;
using Project.Services;
using GenUser = Project.Models.Entities.User;
using GenUserTwoFactor = Project.Models.Entities.UserTwoFactor;

namespace Project.Controllers
{
    // The whole technician workflow now runs on the real database through LabTechService:
    // requests come from the doctor, results go back to the doctor and patient.
    [Authorize(Roles = "Technician")]
    public class LabTechController : Controller
    {
        // Shared branded QuestPDF service (Technician Report + the results PDF
        // e-mailed to the doctor) and the app's SMTP wrapper - both used as-is.
        private readonly PdfService _pdfService;
        private readonly EmailService _emailService;
        private readonly ApplicationDbContext _context;
        private readonly LabTechService _svc;

        public LabTechController(PdfService pdfService, EmailService emailService, ApplicationDbContext context, LabTechService svc)
        {
            _pdfService = pdfService;
            _emailService = emailService;
            _context = context;
            _svc = svc;
        }

        // The signed-in technician's UserId, from the auth cookie.
        private int? CurrentUserId =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (int?)null;

        // The sidebar shows who is signed in (name + employee number) on every page.
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            base.OnActionExecuting(context);
            var tech = CurrentTechnicianOrNull;
            ViewBag.TechName = tech != null ? $"{tech.Name} {tech.Surname}" : User.Identity?.Name;
            ViewBag.TechCode = tech?.EmployeeNumber;
        }

        // Writes a real StaffActivityLog row for the signed-in technician - same
        // shared table/pattern AdminController uses for its own actions. Never
        // throws: a logging failure must not break the action that triggered it.
        private void LogAction(string action, string detail, string logType = "info")
        {
            try
            {
                var uid = CurrentUserId;
                if (uid == null) return;
                var roleRow = _context.Roles.FirstOrDefault(r => r.Code == "Technician");
                var logTypeRow = _context.LogTypes.FirstOrDefault(lt => lt.Code == logType)
                                  ?? _context.LogTypes.FirstOrDefault(lt => lt.Code == "info");
                if (roleRow == null || logTypeRow == null) return;

                _context.StaffActivityLogs.Add(new Project.Models.Entities.StaffActivityLog
                {
                    ActorId = uid.Value,
                    ActorRole = roleRow.RoleId,
                    Action = action,
                    Detail = detail,
                    LogTypeId = logTypeRow.LogTypeId
                });
                _context.SaveChanges();
            }
            catch { /* best-effort - never block the real action over a log write */ }
        }

        // ── My Activity (own StaffActivityLog rows only) ──────────

        public IActionResult ActivityLog()
        {
            var uid = CurrentUserId;
            var entries = _context.StaffActivityLogs.AsNoTracking()
                .Include(l => l.LogType)
                .Where(l => l.ActorId == uid)
                .OrderByDescending(l => l.Timestamp)
                .Take(100)
                .ToList();
            return View(entries);
        }

        // ── Shared lookups (loaded once per request, from the database) ──

        private Technician? _tech;
        private bool _techLoaded;
        private Technician? CurrentTechnicianOrNull
        {
            get
            {
                if (!_techLoaded)
                {
                    _techLoaded = true;
                    _tech = CurrentUserId.HasValue ? _svc.GetTechnician(CurrentUserId.Value) : null;
                }
                return _tech;
            }
        }

        private int CurrentTechnicianId => CurrentUserId ?? 0;

        private Technician CurrentTechnician => CurrentTechnicianOrNull
            ?? throw new InvalidOperationException("The signed-in account has no technician profile.");

        private string TechnicianFullName => $"{CurrentTechnician.Name} {CurrentTechnician.Surname}";

        // Test types the lab manager assigned to this technician.
        private HashSet<int> PermittedTestTypeIds =>
            CurrentTechnician.TechnicianTestTypes.Select(tt => tt.TestTypeId).ToHashSet();

        private List<QueueItem>? _queue;
        private List<QueueItem> Queue => _queue ??= _svc.LoadQueue();
        private void ReloadQueue() => _queue = null;

        private List<QueueItem> RequestItems(string requestNumber) =>
            Queue.Where(q => string.Equals(q.RequestNumber, NormaliseRequest(requestNumber), StringComparison.OrdinalIgnoreCase)).ToList();

        // Request numbers look like "REQ-007" everywhere; tolerate a leading "#" from old links.
        private static string NormaliseRequest(string id) => (id ?? "").Trim().TrimStart('#').ToUpperInvariant();

        private static DateTime SastNow => QueueItem.SastNow;

        private string StockStatus(int testTypeId) => _svc.StockStatus(testTypeId);
        private Dictionary<int, string> StockStatusByTestType() => _svc.StockStatusByTestType();

        // Spec: a technician cannot verify their own results, and can only verify
        // test types assigned to them by the laboratory manager.
        private bool CanVerify(QueueItem q) =>
            q.Status == "Verify"
            && q.CapturedByUserId != CurrentTechnicianId
            && PermittedTestTypeIds.Contains(q.TestTypeId);

        private bool IsMyReview(QueueItem q) => q.Status == "Review" && q.CapturedByUserId == CurrentTechnicianId;

        // ── Dashboard ────────────────────────────────────────────

        // AccountController sends a signed-in Technician to /LabTech/Index.
        public IActionResult Index() => RedirectToAction("Dashboard");

        public IActionResult Dashboard(string? urgency, int? category, string? due, string? req, string? open)
        {
            var filter = new LabTechFilter { Urgency = urgency, Category = category, Due = due, Req = req };
            var permitted = PermittedTestTypeIds;

            // Everything this technician can see / act on, then the filters on top.
            var visible = filter.Apply(Queue.Where(q => q.Status != "Cancelled")).ToList();

            var mine      = visible.Where(q => q.Status == "Active" && q.AssignedTechnicianId == CurrentTechnicianId).ToList();
            var available = visible.Where(q => q.Status == "Available" && permitted.Contains(q.TestTypeId)).ToList();
            var toVerify  = visible.Where(CanVerify).ToList();
            var toReview  = visible.Where(IsMyReview).ToList();
            var workload  = mine.Concat(available).Concat(toReview).ToList();

            var lists = new List<LabTechQueueList>
            {
                new() { Key = "stat", Title = "Urgent (STAT)", Icon = "bi-lightning-fill text-danger",
                        Subtitle = "STAT tests you're working on or can select.", Empty = "No STAT tests right now.",
                        Items = workload.Where(q => q.Urgency == "STAT").ToList() },
                new() { Key = "overdue", Title = "Overdue", Icon = "bi-exclamation-triangle-fill text-danger",
                        Subtitle = "Past their expected completion time (received + turnaround time).", Empty = "Nothing overdue.",
                        Items = workload.Where(q => q.IsOverdue).ToList() },
                new() { Key = "nearing", Title = "Nearing Turnaround Limit", Icon = "bi-hourglass-split text-warning",
                        Subtitle = $"Due within the next {QueueItem.NearingTatMinutes} minutes.", Empty = "Nothing close to its turnaround limit.",
                        Items = workload.Where(q => q.IsNearingTat).ToList() },
                new() { Key = "mine", Title = "My Tests", Icon = "bi-activity",
                        Subtitle = "Tests you've selected and are performing.", Empty = "Nothing in progress.",
                        Items = mine },
                new() { Key = "available", Title = "Waiting to be Selected", Icon = "bi-inbox-fill",
                        Subtitle = "Samples received, for test types assigned to you.", Empty = "No tests waiting for you.",
                        Items = available },
                new() { Key = "verify", Title = "Waiting to be Verified", Icon = "bi-shield-check",
                        Subtitle = "Results captured by other technicians, for test types assigned to you.", Empty = "Nothing awaiting verification.",
                        Items = toVerify },
                new() { Key = "review", Title = "Waiting to be Reviewed", Icon = "bi-arrow-return-left",
                        Subtitle = "Your results a verifier returned with a note.", Empty = "Nothing returned for review.",
                        Items = toReview },
            };
            foreach (var l in lists)
                l.Items = l.Items.OrderBy(q => q.DueAt ?? DateTime.MaxValue).ToList();

            ViewBag.CurrentTechnician = CurrentTechnician;
            ViewBag.Lists = lists;
            ViewBag.Filter = filter;
            ViewBag.Categories = _svc.Categories();
            ViewBag.StockStatus = StockStatusByTestType();
            ViewBag.VerifiableIds = toVerify.Select(q => q.Id).ToHashSet();
            ViewBag.Open = open;
            return View();
        }

        // ── My Tests (selected / in-progress work for this technician) ──

        public IActionResult MyTests(string filter = "all")
        {
            var items = Queue
                .Where(q => q.Status == "Active" && q.AssignedTechnicianId == CurrentTechnicianId)
                .OrderBy(q => q.DueAt)
                .ToList();

            ViewBag.CurrentTechnician = CurrentTechnician;
            ViewBag.Items = items;
            ViewBag.InitialFilter = string.IsNullOrWhiteSpace(filter) ? "all" : filter.ToLower();
            return View();
        }

        // ── 1. Receive patient test requests and samples ─────────

        public IActionResult SampleReceipt()
        {
            var pending = Queue
                .Where(q => q.Status == "Submitted")
                .GroupBy(q => q.RequestNumber)
                .Select(g => g.ToList())
                .OrderBy(g => g[0].Urgency == "STAT" ? 0 : g[0].Urgency == "Urgent" ? 1 : 2)
                .ThenBy(g => g[0].RequestDate)
                .ToList();

            ViewBag.Pending = pending;
            return View();
        }

        public IActionResult ReceiveSample(string id)
        {
            var items = RequestItems(id).Where(q => q.Status == "Submitted").ToList();
            if (!items.Any())
            {
                TempData["Error"] = "That request has no samples waiting to be received.";
                return RedirectToAction("SampleReceipt");
            }

            ViewBag.Items = items;
            ViewBag.ReceivedBy = TechnicianFullName;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("ReceiveSample")]
        public IActionResult ReceiveSamplePost(string requestNumber, List<ReceivedSampleInput> samples)
        {
            var reqNo = NormaliseRequest(requestNumber);
            var items = RequestItems(reqNo).Where(q => q.Status == "Submitted").ToList();
            if (!items.Any()) return RedirectToAction("SampleReceipt");

            var list = samples ?? new List<ReceivedSampleInput>();
            if (list.Count == 0)
            {
                TempData["Error"] = "Accept or reject every sample before completing the receipt.";
                return RedirectToAction("ReceiveSample", new { id = reqNo });
            }
            var toReceive = list.Where(s => !string.Equals(s.Action, "Reject", StringComparison.OrdinalIgnoreCase)).ToList();
            var toReject = list.Where(s => string.Equals(s.Action, "Reject", StringComparison.OrdinalIgnoreCase)).ToList();

            if (toReceive.Any(s => string.IsNullOrWhiteSpace(s.Barcode)))
            {
                TempData["Error"] = "Every accepted sample needs a barcode.";
                return RedirectToAction("ReceiveSample", new { id = reqNo });
            }
            var acceptedBarcodes = toReceive.Select(s => s.Barcode.Trim()).ToList();
            if (acceptedBarcodes.Distinct(StringComparer.OrdinalIgnoreCase).Count() != acceptedBarcodes.Count)
            {
                TempData["Error"] = "Two samples can't share the same barcode.";
                return RedirectToAction("ReceiveSample", new { id = reqNo });
            }
            if (toReject.Any(s => string.IsNullOrWhiteSpace(s.RejectReason)))
            {
                TempData["Error"] = "A reason is required to reject a sample.";
                return RedirectToAction("ReceiveSample", new { id = reqNo });
            }

            var error = _svc.ReceiveSamples(items[0].RequestId, list, CurrentTechnicianId);
            if (error != null)
            {
                TempData["Error"] = error;
                return RedirectToAction("ReceiveSample", new { id = reqNo });
            }
            ReloadQueue();

            var corrected = toReceive.Count(s => !string.Equals(s.Barcode.Trim(), (s.OriginalBarcode ?? "").Trim(), StringComparison.Ordinal));
            LogAction("Samples received", $"Request {reqNo} - {toReceive.Count} accepted" + (corrected > 0 ? $", {corrected} barcode(s) corrected" : "") + (toReject.Count > 0 ? $", {toReject.Count} rejected" : ""));

            var sent = true;
            if (toReject.Any())
            {
                var now = SastNow;
                var first = items[0];
                string H(string s) => System.Net.WebUtility.HtmlEncode(s);
                var rows = toReject.Select(r =>
                {
                    var original = (r.OriginalBarcode ?? "").Trim();
                    var tests = items.Where(i => string.Equals(i.Barcode, original, StringComparison.OrdinalIgnoreCase))
                        .Select(i => i.TestTypeName).Distinct().ToList();
                    return $"<li><strong>{H(original)}</strong> ({H(string.Join(", ", tests))}): {H(r.RejectReason!.Trim())}</li>";
                });
                sent = TrySendEmail(first.DoctorEmail,
                    $"Sample(s) rejected on test request {reqNo}",
                    $@"<p>Dear {H(first.DoctorName)},</p>
                       <p>{toReject.Count} sample(s) on test request <strong>{reqNo}</strong> for patient <strong>{H(first.PatientName)}</strong>
                       could not be accepted by lab technician {H(TechnicianFullName)} on {now:dd MMM yyyy HH:mm}:</p>
                       <ul>{string.Join("", rows)}</ul>
                       <p>The remaining test(s) on this request are proceeding as normal.</p>");
            }

            TempData["Success"] = $"Request {reqNo}: {toReceive.Count} sample(s) received" +
                (toReject.Count > 0 ? $", {toReject.Count} rejected" + (sent ? " - the doctor has been notified." : ".") : $" - request is now {QueueItem.RequestStatus(RequestItems(reqNo))}.");
            if (toReject.Count > 0 && !sent)
                TempData["Error"] = $"{toReject.Count} sample(s) rejected, but the doctor could not be e-mailed - please notify them directly.";
            return RedirectToAction("SampleReceipt");
        }

        // Spec (doctor subsystem): a request can be cancelled with a reason while
        // it is Submitted or Samples Received; if a lab technician cancels it the
        // doctor is notified.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CancelRequest(string requestNumber, string reason)
        {
            var reqNo = NormaliseRequest(requestNumber);
            var items = RequestItems(reqNo);
            if (!items.Any()) return RedirectToAction("SampleReceipt");
            var status = QueueItem.RequestStatus(items);

            if (status != "Submitted" && status != "Samples Received")
            {
                TempData["Error"] = $"Request {reqNo} is {status} and can no longer be cancelled.";
                return RedirectToAction("SampleReceipt");
            }
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "A reason is required to cancel a test request.";
                return RedirectToAction("SampleReceipt");
            }

            var error = _svc.CancelRequest(items[0].RequestId, reason.Trim());
            if (error != null)
            {
                TempData["Error"] = error;
                return RedirectToAction("SampleReceipt");
            }
            ReloadQueue();

            var now = SastNow;
            var first = items[0];
            string H(string s) => System.Net.WebUtility.HtmlEncode(s);
            var tests = string.Join("", items.Select(i => $"<li>{H(i.TestTypeName)}</li>"));
            var sent = TrySendEmail(first.DoctorEmail,
                $"Test request {reqNo} cancelled by the laboratory",
                $@"<p>Dear {H(first.DoctorName)},</p>
                   <p>Test request <strong>{reqNo}</strong> for patient <strong>{H(first.PatientName)}</strong>
                   was cancelled by lab technician {H(TechnicianFullName)} on {now:dd MMM yyyy HH:mm}.</p>
                   <p><strong>Reason:</strong> {H(reason.Trim())}</p>
                   <p>Tests on this request:</p><ul>{tests}</ul>");

            LogAction("Request cancelled", $"Request {reqNo}: {reason.Trim()}", "warning");
            TempData["Success"] = $"Request {reqNo} cancelled." + (sent ? $" {first.DoctorName} has been notified." : "");
            if (!sent) TempData["Error"] = $"The doctor could not be e-mailed - please notify {first.DoctorName} directly.";
            return RedirectToAction("SampleReceipt");
        }

        // ── 2. Select and process test type ──────────────────────

        public IActionResult SelectTest()
        {
            var permitted = PermittedTestTypeIds;
            var items = Queue
                .Where(q => q.Status == "Available" && permitted.Contains(q.TestTypeId))
                .OrderBy(q => q.DueAt)
                .ToList();

            ViewBag.CurrentTechnician = CurrentTechnician;
            ViewBag.Items = items;
            ViewBag.StockStatus = StockStatusByTestType();
            ViewBag.ConsumableById = _svc.ConsumablesById();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("SelectTest")]
        public IActionResult SelectTestPost(int testRequestItemId, string? returnTo)
        {
            var item = Queue.FirstOrDefault(q => q.Id == testRequestItemId);
            var back = returnTo == "Dashboard" ? "Dashboard" : "SelectTest";

            if (item == null || item.Status != "Available")
            {
                TempData["Error"] = "That test is no longer waiting to be selected.";
                return RedirectToAction(back);
            }
            if (!PermittedTestTypeIds.Contains(item.TestTypeId))
            {
                TempData["Error"] = "That test type hasn't been assigned to you by the laboratory manager.";
                return RedirectToAction(back);
            }
            if (StockStatus(item.TestTypeId) == "OutOfStock")
            {
                TempData["Error"] = $"Not enough consumables in stock to perform {item.TestTypeName}.";
                return RedirectToAction(back);
            }

            bool firstOnRequest = !RequestItems(item.RequestNumber)
                .Any(q => q.Status is "Active" or "Verify" or "Review" or "Verified");

            // Consumables assigned to the test are deducted automatically (inside the service).
            var error = _svc.SelectTest(item.Id, CurrentTechnicianId);
            if (error != null)
            {
                TempData["Error"] = error;
                return RedirectToAction(back);
            }
            ReloadQueue();

            LogAction("Test selected", $"{item.TestTypeName} - request {item.RequestNumber}");
            TempData["Success"] = $"{item.TestTypeName} selected - consumables deducted from stock."
                + (firstOnRequest ? $" Request {item.RequestNumber} is now In Progress." : "");
            return RedirectToAction("CaptureResult", new { id = item.Id });
        }

        // ── 3. Capture test results ──────────────────────────────

        // returnTo: which list page to send the technician back to once they're
        // done (Dashboard by default; VerificationHistory when "Resume" was
        // clicked there, so returned-and-resubmitted tests show their update
        // without having to navigate back and re-find the row).
        private static string SafeReturnTo(string? returnTo) =>
            returnTo == "VerificationHistory" ? "VerificationHistory" : "Dashboard";

        public IActionResult CaptureResult(int id, string? returnTo)
        {
            var item = Queue.FirstOrDefault(q => q.Id == id);
            if (item == null) return NotFound();

            bool mineActive = item.Status == "Active" && item.AssignedTechnicianId == CurrentTechnicianId;
            bool mineReview = IsMyReview(item);
            if (!mineActive && !mineReview)
            {
                TempData["Error"] = "You can only capture results for tests you selected.";
                return RedirectToAction("Dashboard");
            }

            ViewBag.Item = item;
            ViewBag.TestType = item.TestType;
            ViewBag.IsReview = mineReview;
            ViewBag.ReturnTo = SafeReturnTo(returnTo);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("CaptureResult")]
        public IActionResult CaptureResultPost(int id, decimal? ResultValue, string? Notes, string? returnTo)
        {
            var item = Queue.FirstOrDefault(q => q.Id == id);
            if (item == null) return NotFound();
            var back = SafeReturnTo(returnTo);

            bool wasReview = IsMyReview(item);
            bool mineActive = item.Status == "Active" && item.AssignedTechnicianId == CurrentTechnicianId;
            if (!wasReview && !mineActive)
            {
                TempData["Error"] = "You can only capture results for tests you selected.";
                return RedirectToAction("Dashboard");
            }
            if (!ResultValue.HasValue)
            {
                TempData["Error"] = "Enter a result value.";
                return RedirectToAction("CaptureResult", new { id, returnTo = back });
            }

            var error = _svc.CaptureResult(item.Id, ResultValue.Value, Notes, CurrentTechnicianId, out var abnormal);
            if (error != null)
            {
                TempData["Error"] = error;
                return RedirectToAction("CaptureResult", new { id, returnTo = back });
            }
            ReloadQueue();

            var reqStatus = QueueItem.RequestStatus(RequestItems(item.RequestNumber));
            LogAction(wasReview ? "Result recaptured" : "Result captured", $"{item.TestTypeName} - request {item.RequestNumber}" + (abnormal ? " (abnormal)" : ""), abnormal ? "warning" : "info");
            TempData["Success"] = "Result captured - test is Completed and waiting for verification."
                + (reqStatus == "Completed" ? $" All tests on request {item.RequestNumber} are now Completed." : "");
            return RedirectToAction(back);
        }

        // ── 4. Test result verification (two-step) ───────────────

        public IActionResult VerifyResult(int id, string? returnTo)
        {
            var item = Queue.FirstOrDefault(q => q.Id == id);
            if (item == null) return NotFound();
            if (!CanVerify(item))
            {
                TempData["Error"] = "You can't verify this result - it is your own, or not a test type assigned to you.";
                return RedirectToAction("Dashboard");
            }

            ViewBag.Item = item;
            ViewBag.TestType = item.TestType;
            ViewBag.ReturnTo = SafeReturnTo(returnTo);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("VerifyResult")]
        public IActionResult VerifyResultPost(int id, string action, string? VerificationNote, string? returnTo)
        {
            var item = Queue.FirstOrDefault(q => q.Id == id);
            if (item == null) return NotFound();
            var back = SafeReturnTo(returnTo);
            if (!CanVerify(item))
            {
                TempData["Error"] = "You can't verify this result - it is your own, or not a test type assigned to you.";
                return RedirectToAction("Dashboard");
            }
            if (action != "Verify" && string.IsNullOrWhiteSpace(VerificationNote))
            {
                TempData["Error"] = "A note is required when returning a result for review.";
                return RedirectToAction("VerifyResult", new { id, returnTo = back });
            }

            var verify = action == "Verify";
            var error = _svc.Review(item.Id, verify, VerificationNote, CurrentTechnicianId, out var allVerified);
            if (error != null)
            {
                TempData["Error"] = error;
                return RedirectToAction(back);
            }
            ReloadQueue();

            if (verify)
            {
                var msg = "Result verified.";
                if (allVerified)
                    msg += NotifyDoctorAllVerified(RequestItems(item.RequestNumber));
                LogAction("Result verified", $"{item.TestTypeName} - request {item.RequestNumber}, captured by {item.CapturedBy}");
                TempData["Success"] = msg;
            }
            else
            {
                LogAction("Result returned for review", $"{item.TestTypeName} - request {item.RequestNumber}, to {item.CapturedBy}: {VerificationNote!.Trim()}", "warning");
                TempData["Success"] = $"Result returned to {item.CapturedBy} for review.";
            }
            return RedirectToAction(back);
        }

        // Spec: once all tests on a request are verified, the requesting doctor
        // is e-mailed with the results attached as a PDF.
        private string NotifyDoctorAllVerified(List<QueueItem> items)
        {
            var first = items[0];
            var dob = DateTime.TryParse(first.PatientDob, out var d) ? d : DateTime.MinValue;
            var pdf = _pdfService.GeneratePatientResultsReport(new PatientResultsReportData
            {
                PatientName = first.PatientName,
                IdNumber    = first.PatientIdNumber,
                DateOfBirth = dob,
                DoctorName  = first.DoctorName,
                Results = items.Select(q => new TestResultRow
                {
                    RequestId      = q.RequestId,
                    TestName       = q.TestTypeName,
                    Category       = q.CategoryName,
                    ResultValue    = q.ResultValue?.ToString("0.##") ?? "-",
                    Unit           = q.ResultUnits ?? "",
                    NormalRangeMin = q.TestType?.NormalMin,
                    NormalRangeMax = q.TestType?.NormalMax,
                    Notes          = q.TechNotes ?? "",
                    IsAbnormal     = q.Flag == "Abnormal",
                    ResultDate     = q.CapturedAt ?? SastNow
                }).ToList()
            });

            string H(string s) => System.Net.WebUtility.HtmlEncode(s);
            var abnormal = items.Count(q => q.Flag == "Abnormal");
            var sent = TrySendEmail(first.DoctorEmail,
                $"Results verified - test request {first.RequestNumber}",
                $@"<p>Dear {H(first.DoctorName)},</p>
                   <p>All tests on request <strong>{first.RequestNumber}</strong> for patient
                   <strong>{H(first.PatientName)}</strong> have been verified.
                   {(abnormal > 0 ? $"<strong>{abnormal} abnormal result(s)</strong> are highlighted in the report." : "")}</p>
                   <p>The results are attached as a PDF. You can release them to the patient from your Doctor Portal.</p>",
                pdf, $"Results_{first.RequestNumber}.pdf");

            return sent
                ? $" All tests on {first.RequestNumber} are verified - {first.DoctorName} has been e-mailed the results."
                : $" All tests on {first.RequestNumber} are verified, but the e-mail to {first.DoctorName} could not be sent.";
        }

        private bool TrySendEmail(string to, string subject, string html, byte[]? attachment = null, string? name = null)
        {
            try
            {
                _emailService.SendEmail(to, subject, html, attachment, name);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public IActionResult VerificationHistory()
        {
            var items = Queue
                .Where(q => q.Status is "Verified" or "Review" or "Verify")
                .OrderByDescending(q => q.CapturedAt)
                .ToList();

            ViewBag.Items = items;
            // So each row can offer the right action: resume a Returned result
            // you captured yourself, or verify a Pending one you're allowed to.
            ViewBag.Me = TechnicianFullName;
            ViewBag.VerifiableIds = items.Where(CanVerify).Select(q => q.Id).ToHashSet();
            return View();
        }

        // Lazy-loaded audit trail for one row's "View Trail" modal - fetched by
        // the client only when that modal is first opened (see _LabTechLayout's
        // show.bs.modal handler), instead of shipping every row's full history
        // in VerificationHistory's initial HTML.
        [HttpGet]
        public IActionResult TrailPartial(int id)
        {
            var item = Queue.FirstOrDefault(q => q.Id == id);
            if (item == null) return NotFound();
            return PartialView("_HistoryTrail", item.History);
        }

        // ── Technician report (PDF) ──────────────────────────────

        // Completed = result captured by this technician (awaiting verification or verified).
        private IEnumerable<QueueItem> CompletedByMe() =>
            Queue.Where(q => (q.Status == "Verify" || q.Status == "Verified")
                             && q.CapturedByUserId == CurrentTechnicianId && q.CapturedAt.HasValue);

        // Shared by the on-page range filter, the PDF data builder, and the
        // e-mailed copy, so "what the page shows" and "what the PDF contains"
        // can never drift apart.
        private static (DateTime From, DateTime ToInclusive, DateTime ToDisplay) ResolveRange(DateTime? from, DateTime? to)
        {
            var fromDate = (from ?? SastNow.AddDays(-7)).Date;
            var toDisplay = to ?? SastNow;
            var toInclusive = toDisplay.Date.AddDays(1).AddTicks(-1);
            return (fromDate, toInclusive, toDisplay);
        }

        private List<QueueItem> CompletedInRange(DateTime? from, DateTime? to)
        {
            var (fromDate, toInclusive, _) = ResolveRange(from, to);
            return CompletedByMe()
                .Where(q => q.CapturedAt!.Value >= fromDate && q.CapturedAt.Value <= toInclusive)
                .OrderByDescending(q => q.CapturedAt)
                .ToList();
        }

        // Spec: "Tests completed by technician grouped by category" - built once,
        // shown on the page itself (not just buried inside the PDF) and reused
        // for the PDF/e-mail builder below.
        public IActionResult Report(DateTime? from, DateTime? to)
        {
            var (fromDate, _, toDisplay) = ResolveRange(from, to);
            var items = CompletedInRange(from, to);

            ViewBag.From = fromDate;
            ViewBag.To = toDisplay;
            ViewBag.Items = items;
            ViewBag.CategoryGroups = items
                .GroupBy(q => q.CategoryName)
                .Select(g => new CategoryGroupRow { Category = g.Key, Count = g.Count(), Abnormal = g.Count(q => q.Flag == "Abnormal") })
                .OrderByDescending(g => g.Count)
                .ToList();
            ViewBag.TotalCount = items.Count;
            ViewBag.AbnormalCount = items.Count(q => q.Flag == "Abnormal");
            return View();
        }

        // Renders the report inline so the browser shows it in the preview <iframe>.
        [HttpGet]
        public IActionResult PreviewReport(DateTime? from, DateTime? to)
        {
            var bytes = _pdfService.GenerateTechnicianReport(BuildTechnicianReportData(from, to));
            return File(bytes, "application/pdf");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Report")]
        public IActionResult ReportPost(DateTime? from, DateTime? to)
        {
            var data = BuildTechnicianReportData(from, to);
            var bytes = _pdfService.GenerateTechnicianReport(data);
            var fileName = $"HemaFlow_TechReport_{CurrentTechnician.Surname}_{data.FromDate:ddMMMyyyy}_to_{data.ToDate:ddMMMyyyy}.pdf";
            return File(bytes, "application/pdf", fileName);
        }

        // Action: e-mail yourself a copy of the same PDF instead of downloading it -
        // reuses the same PdfService builder and the EmailService already wired up
        // for doctor notifications, so it's a genuinely working action, not a stub.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EmailReport(DateTime? from, DateTime? to)
        {
            var data = BuildTechnicianReportData(from, to);
            var bytes = _pdfService.GenerateTechnicianReport(data);
            var fileName = $"HemaFlow_TechReport_{CurrentTechnician.Surname}_{data.FromDate:ddMMMyyyy}_to_{data.ToDate:ddMMMyyyy}.pdf";

            var sent = TrySendEmail(CurrentTechnician.Email,
                $"Your HemaFlow technician report ({data.FromDate:dd MMM} - {data.ToDate:dd MMM yyyy})",
                $@"<p>Hi {System.Net.WebUtility.HtmlEncode(CurrentTechnician.Name)},</p>
                   <p>Attached is your completed-tests report for {data.FromDate:dd MMM yyyy} to {data.ToDate:dd MMM yyyy}
                   ({data.Results.Count} test{(data.Results.Count == 1 ? "" : "s")}).</p>",
                bytes, fileName);

            TempData[sent ? "Success" : "Error"] = sent
                ? $"Report e-mailed to {CurrentTechnician.Email}."
                : "Could not send the e-mail right now - please try downloading the PDF instead.";
            return RedirectToAction("Report", new { from = data.FromDate.ToString("yyyy-MM-dd"), to = data.ToDate.ToString("yyyy-MM-dd") });
        }

        private TechnicianReportData BuildTechnicianReportData(DateTime? from, DateTime? to)
        {
            var (fromDate, toInclusive, toDisplay) = ResolveRange(from, to);

            var rows = CompletedByMe()
                .Where(q => q.CapturedAt!.Value >= fromDate && q.CapturedAt.Value <= toInclusive)
                .OrderByDescending(q => q.CapturedAt)
                .Select(q => new TechnicianReportRow
                {
                    RequestNumber = q.RequestNumber,
                    TestName      = q.TestTypeName,
                    Category      = q.CategoryName,
                    ResultValue   = q.ResultValue?.ToString("0.##") ?? "-",
                    Unit          = q.ResultUnits ?? q.TestType?.Units ?? "",
                    NormalRange   = q.TestType != null ? $"{q.TestType.NormalMin} - {q.TestType.NormalMax}" : "-",
                    IsAbnormal    = q.Flag == "Abnormal",
                    CompletedAt   = q.CapturedAt!.Value
                }).ToList();

            return new TechnicianReportData
            {
                TechnicianName = TechnicianFullName,
                EmployeeNumber = CurrentTechnician.EmployeeNumber,
                FromDate       = fromDate,
                ToDate         = toDisplay,
                Results        = rows
            };
        }

        // ── Profile (real DB - Users/TechnicianProfile/UserTwoFactor/
        //    UserBackupCode, same pattern as AdminController) ──

        private LabTechProfileViewModel BuildProfileViewModel(GenUser user)
        {
            var model = new LabTechProfileViewModel
            {
                FirstName     = user.FirstName,
                Surname       = user.Surname,
                Email         = user.Email,
                Phone         = user.Phone ?? "",
                EmployeeCode  = user.TechnicianProfile?.EmployeeCode ?? "",
                SaIdNumber    = user.TechnicianProfile?.SaIdNumber ?? "",
                AssignedTestTypes = user.TechnicianProfile?.TechnicianTestTypes
                    .Select(tt => tt.TestType.Name)
                    .OrderBy(n => n)
                    .ToList() ?? new List<string>()
            };

            var tf = _context.UserTwoFactors.FirstOrDefault(t => t.UserId == user.UserId && t.IsEnabled);
            model.TwoFactorEnabled = tf != null;
            model.TwoFactorLastVerified = tf?.LastVerifiedOn;
            model.BackupCodesLeft = _context.UserBackupCodes.Count(b => b.UserId == user.UserId && b.UsedOn == null);
            if (TempData["NewBackupCodes"] is string codes && codes.Length > 0)
                model.NewBackupCodes = codes.Split(',').ToList();

            return model;
        }

        [HttpGet]
        public IActionResult Profile()
        {
            var user = _context.Users.Include(u => u.TechnicianProfile).ThenInclude(p => p!.TechnicianTestTypes).ThenInclude(t => t.TestType)
                .FirstOrDefault(u => u.UserId == CurrentUserId);
            if (user == null) return RedirectToAction("Login", "Account");
            return View(BuildProfileViewModel(user));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("Profile")]
        public IActionResult ProfilePost(LabTechProfileViewModel model)
        {
            var user = _context.Users.FirstOrDefault(u => u.UserId == CurrentUserId);
            if (user == null) return RedirectToAction("Login", "Account");

            if (!string.IsNullOrWhiteSpace(model.FirstName)) user.FirstName = model.FirstName.Trim();
            if (!string.IsNullOrWhiteSpace(model.Surname)) user.Surname = model.Surname.Trim();
            if (!string.IsNullOrWhiteSpace(model.Phone)) user.Phone = model.Phone.Trim();
            if (!string.IsNullOrWhiteSpace(model.Email) && !string.Equals(model.Email.Trim(), user.Email, StringComparison.OrdinalIgnoreCase))
            {
                var taken = _context.Users.Any(u => u.Email == model.Email.Trim() && u.UserId != user.UserId);
                if (taken) { TempData["Error"] = "This e-mail address is already used by another account."; return RedirectToAction("Profile"); }
                user.Email = model.Email.Trim();
            }
            _context.SaveChanges();

            LogAction("Profile updated", "Updated personal details");
            TempData["Success"] = "Your profile has been updated.";
            return RedirectToAction("Profile");
        }

        // ── 2FA (authenticator app) ────────────────────

        [HttpGet]
        public IActionResult Setup2FA()
        {
            var user = _context.Users.FirstOrDefault(u => u.UserId == CurrentUserId);
            if (user == null) return RedirectToAction("Login", "Account");
            var row = _context.UserTwoFactors.FirstOrDefault(t => t.UserId == user.UserId);
            if (row != null && row.IsEnabled)
            {
                TempData["Success"] = "Two-factor authentication is already enabled.";
                return RedirectToAction("Profile");
            }
            if (row == null)
            {
                row = new GenUserTwoFactor { UserId = user.UserId, SecretKey = TotpService.GenerateSecretKey(), IsEnabled = false };
                _context.UserTwoFactors.Add(row);
                _context.SaveChanges();
            }
            return View(new Project.Models.TwoFactorSetupViewModel
            {
                SecretKey = row.SecretKey,
                OtpAuthUri = TotpService.BuildOtpAuthUri("HemaFlow", user.Email, row.SecretKey),
                AccountEmail = user.Email
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Setup2FAConfirm(string code)
        {
            var user = _context.Users.FirstOrDefault(u => u.UserId == CurrentUserId);
            if (user == null) return RedirectToAction("Login", "Account");
            var row = _context.UserTwoFactors.FirstOrDefault(t => t.UserId == user.UserId);
            if (row == null) { TempData["Error"] = "Start the setup again."; return RedirectToAction("Setup2FA"); }
            if (!TotpService.ValidateCode(row.SecretKey, code))
            {
                TempData["Error"] = "That code didn't match. Check your authenticator app's clock and try the current code.";
                return RedirectToAction("Setup2FA");
            }
            row.IsEnabled = true;
            row.EnabledOn = DateTime.UtcNow;
            row.LastVerifiedOn = DateTime.UtcNow;
            var plain = ReplaceBackupCodes(user.UserId);
            _context.SaveChanges();
            TempData["NewBackupCodes"] = string.Join(",", plain);
            LogAction("2FA enabled", "Enabled two-factor authentication");
            TempData["Success"] = "Two-factor authentication is now enabled. Save your backup codes somewhere safe.";
            return RedirectToAction("Profile");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DisableTwoFactor(string password)
        {
            var user = _context.Users.FirstOrDefault(u => u.UserId == CurrentUserId);
            if (user == null) return RedirectToAction("Login", "Account");
            var hasher = new PasswordHasher<GenUser>();
            if (string.IsNullOrEmpty(password) ||
                hasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed)
            {
                TempData["Error"] = "Incorrect password. Two-factor authentication was not disabled.";
                return RedirectToAction("Profile");
            }
            var row = _context.UserTwoFactors.FirstOrDefault(t => t.UserId == user.UserId);
            if (row != null) _context.UserTwoFactors.Remove(row);
            _context.UserBackupCodes.RemoveRange(_context.UserBackupCodes.Where(b => b.UserId == user.UserId));
            _context.SaveChanges();
            LogAction("2FA disabled", "Disabled two-factor authentication", "warning");
            TempData["Success"] = "Two-factor authentication has been disabled.";
            return RedirectToAction("Profile");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RegenerateBackupCodes()
        {
            var uid = CurrentUserId;
            var tf = _context.UserTwoFactors.FirstOrDefault(t => t.UserId == uid && t.IsEnabled);
            if (tf == null) { TempData["Error"] = "Enable two-factor authentication first."; return RedirectToAction("Profile"); }
            var plain = ReplaceBackupCodes(uid!.Value);
            _context.SaveChanges();
            TempData["NewBackupCodes"] = string.Join(",", plain);
            LogAction("Backup codes regenerated", "Generated new 2FA backup codes");
            TempData["Success"] = "New backup codes generated. Your old codes no longer work.";
            return RedirectToAction("Profile");
        }

        private List<string> ReplaceBackupCodes(int userId)
        {
            _context.UserBackupCodes.RemoveRange(_context.UserBackupCodes.Where(b => b.UserId == userId));
            var plain = TotpService.GenerateBackupCodes();
            foreach (var c in plain)
                _context.UserBackupCodes.Add(new Project.Models.Entities.UserBackupCode { UserId = userId, CodeHash = TotpService.HashBackupCode(c) });
            return plain;
        }

        // ── Change password (dedicated page, same as Admin/Patient) ──

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View(new Project.Models.AccountChangePasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ChangePassword(Project.Models.AccountChangePasswordViewModel model)
        {
            if (model.NewPassword != model.ConfirmPassword)
            {
                TempData["Error"] = "New password and confirmation do not match.";
                return RedirectToAction("ChangePassword");
            }
            if (!PasswordPolicy.IsValid(model.NewPassword))
            {
                TempData["Error"] = PasswordPolicy.Message;
                return RedirectToAction("ChangePassword");
            }

            var user = _context.Users.FirstOrDefault(u => u.UserId == CurrentUserId);
            if (user == null) return RedirectToAction("Login", "Account");

            var hasher = new PasswordHasher<GenUser>();
            if (hasher.VerifyHashedPassword(user, user.PasswordHash, model.CurrentPassword) == PasswordVerificationResult.Failed)
            {
                TempData["Error"] = "Current password is incorrect.";
                return RedirectToAction("ChangePassword");
            }

            user.PasswordHash = hasher.HashPassword(user, model.NewPassword);
            user.MustChangePassword = false;
            _context.SaveChanges();

            LogAction("Password changed", "Changed account password");
            TempData["Success"] = "Your password has been changed.";
            return RedirectToAction("Profile");
        }
    }
}
