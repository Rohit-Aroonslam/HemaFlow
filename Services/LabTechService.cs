using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Project.Data;
using LM = Project.Models.LabManager;
using E = Project.Models.Entities;

namespace Project.Services
{
    // The Lab Technician workflow on the real database.
    //
    // Doctor creates a request  -> TestRequest (1 Submitted) + one TestRequestItem (1 Awaiting) + one RequestSample per test.
    // Technician receives       -> item 2 SampleCollected, sample gets ReceivedOn + receiving technician, request 5 SamplesReceived.
    // Technician selects        -> item 3 Testing (StartedOn, assigned), consumables deducted, request 2 InProgress.
    // Technician captures       -> TestResult upserted, item 6 Completed (CompletedOn). Once every
    //                              item on the request has been captured (none left in Awaiting/
    //                              SampleCollected/Testing), the request itself goes to 3 Completed.
    // Second technician reviews -> a Verification row per review: Outcome "Verified" (item 4) or "Returned" (item 7).
    //                              The row snapshots the result that was reviewed, so recaptures never lose history.
    // All items verified        -> the doctor is notified by e-mail with the results attached as a PDF.
    //
    // QueueItem is only a view model: LoadQueue() builds it from those tables (times converted to SAST).
    public class LabTechService
    {
        // TestRequestItemStatus ids (see database/001 and 019)
        public const byte ItAwaiting = 1, ItSampleCollected = 2, ItTesting = 3, ItVerified = 4, ItRejected = 5, ItCompleted = 6, ItToBeReviewed = 7;
        // TestRequestStatus ids
        public const byte RqSubmitted = 1, RqInProgress = 2, RqCompleted = 3, RqCancelled = 4, RqSamplesReceived = 5, RqReleased = 6;

        private static readonly Regex BarcodeRx = new(@"^[A-Za-z0-9\-]{1,40}$", RegexOptions.Compiled);

        private readonly ApplicationDbContext _db;

        public LabTechService(ApplicationDbContext db) { _db = db; }

        public static string ReqNo(int id) => "REQ-" + id.ToString("D3");

        private static DateTime? Sast(DateTime? utc) => utc.HasValue ? SastTime.ToSast(utc.Value) : null;

        // ───────────────────────── catalogue (shared by every request) ─────────────────────────
        private Dictionary<int, LM.TestType>? _types;
        private Dictionary<int, LM.Consumable>? _consumables;

        private void EnsureCatalogue()
        {
            if (_types != null) return;

            _consumables = _db.Consumables.AsNoTracking()
                .Select(c => new LM.Consumable { Id = c.ConsumableId, Name = c.Name, ReorderLevel = c.ReorderLevel, QuantityOnHand = c.QuantityOnHand, IsArchived = c.IsArchived })
                .ToDictionary(c => c.Id);

            var links = _db.TestConsumables.AsNoTracking()
                .Select(l => new { l.TestTypeId, l.ConsumableId, l.QuantityPerTest }).ToList()
                .GroupBy(l => l.TestTypeId)
                .ToDictionary(g => g.Key, g => g.Select(l => new LM.TestTypeConsumable { TestTypeId = l.TestTypeId, ConsumableId = l.ConsumableId, QuantityRequired = l.QuantityPerTest }).ToList());

            _types = _db.TestTypes.AsNoTracking()
                .Select(t => new
                {
                    t.TestTypeId, t.Name, t.TestCategoryId, CategoryName = t.TestCategory.Name,
                    t.SampleTypeId, SampleName = t.SampleType.Name, t.Unit, t.NormalMin, t.NormalMax, t.TurnaroundMinutes, t.IsArchived
                })
                .ToList()
                .ToDictionary(t => t.TestTypeId, t => new LM.TestType
                {
                    Id = t.TestTypeId, Name = t.Name, TestCategoryId = t.TestCategoryId,
                    TestCategory = new LM.TestCategory { Id = t.TestCategoryId, Name = t.CategoryName },
                    SampleTypeId = t.SampleTypeId,
                    SampleType = new LM.SampleType { Id = t.SampleTypeId, Name = t.SampleName },
                    Units = t.Unit ?? "", NormalMin = t.NormalMin ?? 0, NormalMax = t.NormalMax ?? 0,
                    TurnaroundTime = t.TurnaroundMinutes ?? 0, IsArchived = t.IsArchived,
                    TestTypeConsumables = links.TryGetValue(t.TestTypeId, out var l) ? l : new List<LM.TestTypeConsumable>()
                });
        }

        public Dictionary<int, LM.Consumable> ConsumablesById() { EnsureCatalogue(); return _consumables!; }

        public List<LM.TestCategory> Categories() =>
            _db.TestCategories.AsNoTracking().Where(c => !c.IsArchived).OrderBy(c => c.Name)
                .Select(c => new LM.TestCategory { Id = c.TestCategoryId, Name = c.Name, Description = c.Description ?? "", IsArchived = c.IsArchived })
                .ToList();

        // ───────────────────────── technician ─────────────────────────
        public LM.Technician? GetTechnician(int userId)
        {
            var u = _db.Users.AsNoTracking().Where(x => x.UserId == userId && x.TechnicianProfile != null)
                .Select(x => new { x.FirstName, x.Surname, x.Email, x.Phone, Emp = x.TechnicianProfile!.EmployeeCode, Sa = x.TechnicianProfile.SaIdNumber })
                .FirstOrDefault();
            if (u == null) return null;
            var tech = new LM.Technician
            {
                Id = userId, Name = u.FirstName, Surname = u.Surname, Email = u.Email, ContactNumber = u.Phone ?? "",
                EmployeeNumber = u.Emp, IdNumber = u.Sa ?? ""
            };
            foreach (var tid in _db.TechnicianTestTypes.AsNoTracking().Where(t => t.TechnicianUserId == userId).Select(t => t.TestTypeId).ToList())
                tech.TechnicianTestTypes.Add(new LM.TechnicianTestType { TechnicianId = userId, TestTypeId = tid });
            return tech;
        }

        // ───────────────────────── stock ─────────────────────────
        // "OutOfStock" if any consumable the test needs has less on hand than one run uses,
        // "LowStock" if one run takes a consumable to or under its reorder level, else "OK".
        public string StockStatus(int testTypeId)
        {
            EnsureCatalogue();
            if (!_types!.TryGetValue(testTypeId, out var tt) || tt.TestTypeConsumables == null || !tt.TestTypeConsumables.Any()) return "OK";
            var status = "OK";
            foreach (var link in tt.TestTypeConsumables)
            {
                if (!_consumables!.TryGetValue(link.ConsumableId, out var c)) continue;
                if (c.QuantityOnHand < link.QuantityRequired) return "OutOfStock";
                if (c.QuantityOnHand - link.QuantityRequired <= c.ReorderLevel) status = "LowStock";
            }
            return status;
        }

        public Dictionary<int, string> StockStatusByTestType()
        {
            EnsureCatalogue();
            return _types!.Keys.ToDictionary(k => k, StockStatus);
        }

        // ───────────────────────── queue (read) ─────────────────────────
        // Builds the queue rows. requestId = only that request's tests; null = everything.
        public List<LM.QueueItem> LoadQueue(int? requestId = null)
        {
            EnsureCatalogue();

            var q = _db.TestRequestItems.AsNoTracking().AsQueryable();
            if (requestId.HasValue) q = q.Where(i => i.TestRequestId == requestId.Value);

            var rows = q.Select(i => new
            {
                i.TestRequestItemId, i.TestRequestId, i.TestTypeId, i.TestRequestItemStatusId, i.AssignedTechnicianUserId,
                i.StartedOn, i.CompletedOn,
                ReqStatus = i.TestRequest.TestRequestStatusId,
                Urgency = i.TestRequest.Urgency.Code,
                i.TestRequest.RequestedOn,
                ClinicalNotes = i.TestRequest.Notes,
                i.TestRequest.CancelReason,
                i.TestRequest.CancelledOn,
                i.TestRequest.PatientUserId,
                PatientFirst = i.TestRequest.PatientUser.User.FirstName,
                PatientSurname = i.TestRequest.PatientUser.User.Surname,
                PatientIdNumber = i.TestRequest.PatientUser.IdNumber,
                PatientDob = i.TestRequest.PatientUser.DateOfBirth,
                DoctorFirst = i.TestRequest.DoctorUser.User.FirstName,
                DoctorSurname = i.TestRequest.DoctorUser.User.Surname,
                DoctorEmail = i.TestRequest.DoctorUser.User.Email,
                Sample = i.RequestSamples.OrderBy(s => s.RequestSampleId)
                    .Select(s => new { s.BarcodeLabel, s.ReceivedOn, s.CollectedByUserId }).FirstOrDefault(),
                ResultId = (int?)i.TestResult!.TestResultId,
                ResultValue = (decimal?)i.TestResult.ResultValue,
                ResultUnit = i.TestResult.Unit,
                NormalMin = i.TestResult.NormalMinAtTest,
                NormalMax = i.TestResult.NormalMaxAtTest,
                RecordedBy = (int?)i.TestResult.RecordedByUserId,
                RecordedOn = (DateTime?)i.TestResult.RecordedOn,
                ResultNotes = i.TestResult.Notes
            }).ToList();

            if (rows.Count == 0) return new List<LM.QueueItem>();

            var itemIds = rows.Select(r => r.TestRequestItemId).ToList();
            var verifs = _db.Verifications.AsNoTracking()
                .Where(v => itemIds.Contains(v.TestResult.TestRequestItemId))
                .Select(v => new
                {
                    ItemId = v.TestResult.TestRequestItemId, v.VerifiedByUserId, v.VerifiedOn, v.Comment, v.Outcome,
                    v.ResultRecordedByUserId, v.ResultRecordedOn
                })
                .ToList()
                .GroupBy(v => v.ItemId)
                .ToDictionary(g => g.Key, g => g.OrderBy(v => v.VerifiedOn).ToList());

            // Technician names (assigned / received / captured / verified by) in one go.
            var names = _db.Users.AsNoTracking().Where(u => u.TechnicianProfile != null)
                .Select(u => new { u.UserId, u.FirstName, u.Surname }).ToList()
                .ToDictionary(u => u.UserId, u => $"{u.FirstName} {u.Surname}");
            string Name(int? id) => id.HasValue && names.TryGetValue(id.Value, out var n) ? n : "-";

            // Patient context (conditions / allergies / medication) once per distinct patient.
            var patientIds = rows.Select(r => r.PatientUserId).Distinct().ToList();
            var conds = _db.PatientConditions.AsNoTracking().Where(c => patientIds.Contains(c.PatientUserId))
                .Select(c => new { c.PatientUserId, c.Condition.Name, c.DiagnosedOn, c.Notes }).ToList()
                .ToLookup(c => c.PatientUserId);
            var alls = _db.PatientAllergies.AsNoTracking().Where(a => patientIds.Contains(a.PatientUserId))
                .Select(a => new { a.PatientUserId, a.Allergy.Name, a.Severity, a.Notes }).ToList()
                .ToLookup(a => a.PatientUserId);
            var meds = _db.PatientMedications.AsNoTracking().Where(m => patientIds.Contains(m.PatientUserId))
                .Select(m => new { m.PatientUserId, m.Medication.Name, m.Strength, m.Dose, m.Frequency }).ToList()
                .ToLookup(m => m.PatientUserId);

            var list = new List<LM.QueueItem>(rows.Count);
            foreach (var r in rows)
            {
                _types!.TryGetValue(r.TestTypeId, out var tt);
                var qi = new LM.QueueItem
                {
                    Id = r.TestRequestItemId,
                    RequestId = r.TestRequestId,
                    PatientUserId = r.PatientUserId,
                    RequestNumber = ReqNo(r.TestRequestId),
                    RequestDate = SastTime.ToSast(r.RequestedOn),
                    Urgency = r.Urgency.Equals("Stat", StringComparison.OrdinalIgnoreCase) ? "STAT" : r.Urgency,
                    ClinicalNotes = r.ClinicalNotes,
                    DoctorName = $"Dr. {r.DoctorFirst} {r.DoctorSurname}",
                    DoctorEmail = r.DoctorEmail,
                    Barcode = r.Sample?.BarcodeLabel ?? "",
                    TestTypeId = r.TestTypeId,
                    TestType = tt,
                    SampleTypeName = tt?.SampleType?.Name ?? "-",
                    CategoryName = tt?.TestCategory?.Name ?? "Uncategorised",
                    AssignedTechnicianId = r.AssignedTechnicianUserId,
                    ReceivedAt = Sast(r.Sample?.ReceivedOn),
                    ReceivedBy = r.Sample?.ReceivedOn != null ? Name(r.Sample.CollectedByUserId) : null,
                    StartedAt = Sast(r.StartedOn),
                    PatientName = $"{r.PatientFirst} {r.PatientSurname}",
                    PatientIdNumber = r.PatientIdNumber,
                    PatientDob = r.PatientDob.HasValue ? r.PatientDob.Value.ToString("yyyy-MM-dd") : "",
                    CancelReason = r.CancelReason
                };

                // Status in the technician's own words (see QueueItem.Status).
                qi.Status = r.ReqStatus == RqCancelled ? "Cancelled" : r.TestRequestItemStatusId switch
                {
                    ItAwaiting => "Submitted",
                    ItSampleCollected => "Available",
                    ItTesting => "Active",
                    ItCompleted => "Verify",
                    ItToBeReviewed => "Review",
                    ItVerified => "Verified",
                    ItRejected => "Cancelled",
                    _ => "Submitted"
                };

                foreach (var c in conds[r.PatientUserId])
                    qi.PatientConditions.Add(new LM.ConditionNote { Condition = c.Name, DiagnosedOn = c.DiagnosedOn?.ToString("dd MMM yyyy"), Note = c.Notes });
                foreach (var a in alls[r.PatientUserId])
                    qi.PatientAllergies.Add(new LM.AllergyInfo { Name = a.Name, Severity = a.Severity ?? "", Note = a.Notes });
                foreach (var m in meds[r.PatientUserId])
                    qi.PatientMedications.Add(new LM.MedicationInfo { Name = m.Name, Strength = m.Strength, Dose = m.Dose, Frequency = m.Frequency });

                // Result (the live TestResult row) + verification state.
                verifs.TryGetValue(r.TestRequestItemId, out var vlist);
                vlist ??= new();
                if (r.ResultId.HasValue)
                {
                    qi.ResultValue = r.ResultValue;
                    qi.ResultUnits = r.ResultUnit;
                    qi.TechNotes = r.ResultNotes;
                    qi.CapturedAt = Sast(r.RecordedOn);
                    qi.CapturedByUserId = r.RecordedBy;
                    qi.CapturedBy = Name(r.RecordedBy);
                    qi.Flag = r.ResultValue.HasValue && ((r.NormalMin.HasValue && r.ResultValue < r.NormalMin) || (r.NormalMax.HasValue && r.ResultValue > r.NormalMax))
                        ? "Abnormal" : "Normal";
                }
                var lastVerified = vlist.LastOrDefault(v => v.Outcome == "Verified");
                var lastReturned = vlist.LastOrDefault(v => v.Outcome == "Returned");
                if (qi.Status == "Verified" && lastVerified != null)
                {
                    qi.VerifiedBy = Name(lastVerified.VerifiedByUserId);
                    qi.VerifiedAt = Sast(lastVerified.VerifiedOn);
                }
                if (qi.Status == "Review") qi.VerifierNote = lastReturned?.Comment;

                // Audit trail: rebuilt from the real rows, oldest first.
                var ev = qi.History;
                if (r.Sample?.ReceivedOn != null)
                    ev.Add(new LM.QueueItemEvent { Timestamp = SastTime.ToSast(r.Sample.ReceivedOn.Value), EventType = "Received", By = Name(r.Sample.CollectedByUserId), Note = $"Barcode {r.Sample.BarcodeLabel}" });
                if (r.StartedOn.HasValue)
                    ev.Add(new LM.QueueItemEvent { Timestamp = SastTime.ToSast(r.StartedOn.Value), EventType = "Started", By = Name(r.AssignedTechnicianUserId) });
                var captures = 0;
                foreach (var v in vlist)
                {
                    if (v.ResultRecordedOn.HasValue)
                    {
                        ev.Add(new LM.QueueItemEvent { Timestamp = SastTime.ToSast(v.ResultRecordedOn.Value), EventType = captures == 0 ? "Captured" : "Recaptured", By = Name(v.ResultRecordedByUserId) });
                        captures++;
                    }
                    ev.Add(new LM.QueueItemEvent { Timestamp = SastTime.ToSast(v.VerifiedOn), EventType = v.Outcome, By = Name(v.VerifiedByUserId), Note = v.Comment });
                }
                // The live result has no review after it yet: show its capture too.
                if (r.RecordedOn.HasValue && !vlist.Any(v => v.ResultRecordedOn == r.RecordedOn))
                    ev.Add(new LM.QueueItemEvent { Timestamp = SastTime.ToSast(r.RecordedOn.Value), EventType = captures == 0 ? "Captured" : "Recaptured", By = Name(r.RecordedBy), Note = r.ResultNotes });
                if (r.ReqStatus == RqCancelled && r.CancelledOn.HasValue)
                    ev.Add(new LM.QueueItemEvent { Timestamp = SastTime.ToSast(r.CancelledOn.Value), EventType = "Cancelled", By = "-", Note = r.CancelReason });
                qi.History = ev.OrderBy(e => e.Timestamp).ToList();

                list.Add(qi);
            }
            return list;
        }

        // ───────────────────────── actions (write) ─────────────────────────
        // Each returns null on success, or a message that is safe to show the technician.

        public string? ReceiveSamples(int requestId, IEnumerable<LM.ReceivedSampleInput> samples, int techUserId)
        {
            var req = _db.TestRequests.Include(r => r.TestRequestItems).ThenInclude(i => i.RequestSamples)
                .FirstOrDefault(r => r.TestRequestId == requestId);
            if (req == null) return "That request was not found.";
            if (req.TestRequestStatusId != RqSubmitted && req.TestRequestStatusId != RqSamplesReceived)
                return "That request has no samples waiting to be received.";
            var waiting = req.TestRequestItems.Where(i => i.TestRequestItemStatusId == ItAwaiting).ToList();
            if (!waiting.Any()) return "That request has no samples waiting to be received.";

            var now = DateTime.UtcNow;
            var list = samples.ToList();
            foreach (var s in list)
            {
                var original = (s.OriginalBarcode ?? "").Trim();
                var sample = waiting.SelectMany(i => i.RequestSamples)
                    .FirstOrDefault(x => string.Equals(x.BarcodeLabel, original, StringComparison.OrdinalIgnoreCase));
                if (sample == null) return $"Barcode '{original}' does not belong to this request.";

                if (string.Equals(s.Action, "Reject", StringComparison.OrdinalIgnoreCase))
                {
                    var reason = (s.RejectReason ?? "").Trim();
                    if (reason.Length == 0) return $"A reason is required to reject the sample '{original}'.";
                    sample.RejectReason = reason.Length > 500 ? reason[..500] : reason;
                    sample.RejectedOn = now;
                    sample.RejectedByUserId = techUserId;
                    sample.TestRequestItem.TestRequestItemStatusId = ItRejected;
                    continue;
                }

                var barcode = (s.Barcode ?? "").Trim();
                if (!BarcodeRx.IsMatch(barcode)) return $"Barcode '{barcode}' is not valid (letters, numbers and hyphens, up to 40 characters).";
                if (!string.Equals(sample.BarcodeLabel, barcode, StringComparison.Ordinal))
                {
                    if (_db.RequestSamples.Any(x => x.RequestSampleId != sample.RequestSampleId && x.BarcodeLabel == barcode))
                        return $"Barcode '{barcode}' is already used by another sample.";
                    sample.BarcodeLabel = barcode;
                }
                sample.CollectedByUserId = techUserId;
                sample.ReceivedOn = now;
                sample.TestRequestItem.TestRequestItemStatusId = ItSampleCollected;
            }
            if (req.TestRequestStatusId == RqSubmitted) req.TestRequestStatusId = RqSamplesReceived;
            _db.SaveChanges();
            return null;
        }

        public string? CancelRequest(int requestId, string reason)
        {
            var req = _db.TestRequests.FirstOrDefault(r => r.TestRequestId == requestId);
            if (req == null) return "That request was not found.";
            if (req.TestRequestStatusId != RqSubmitted && req.TestRequestStatusId != RqSamplesReceived)
                return $"{ReqNo(requestId)} can no longer be cancelled because the lab has already started on it.";
            req.TestRequestStatusId = RqCancelled;
            req.CancelReason = reason.Length > 500 ? reason[..500] : reason;
            req.CancelledOn = DateTime.UtcNow;
            _db.SaveChanges();
            return null;
        }

        public string? SelectTest(int itemId, int techUserId)
        {
            EnsureCatalogue();
            var item = _db.TestRequestItems.Include(i => i.TestRequest).FirstOrDefault(i => i.TestRequestItemId == itemId);
            if (item == null || item.TestRequestItemStatusId != ItSampleCollected || item.TestRequest.TestRequestStatusId == RqCancelled)
                return "That test is no longer waiting to be selected.";
            if (!_db.TechnicianTestTypes.Any(t => t.TechnicianUserId == techUserId && t.TestTypeId == item.TestTypeId))
                return "That test type hasn't been assigned to you by the laboratory manager.";

            // Consumables the test needs: refuse if short, otherwise deduct (same save, so both or neither).
            var needs = _db.TestConsumables.Where(l => l.TestTypeId == item.TestTypeId).ToList();
            var cons = _db.Consumables.Where(c => needs.Select(n => n.ConsumableId).Contains(c.ConsumableId)).ToList();
            foreach (var n in needs)
            {
                var c = cons.FirstOrDefault(x => x.ConsumableId == n.ConsumableId);
                if (c != null && c.QuantityOnHand < n.QuantityPerTest)
                    return $"Not enough {c.Name} in stock to perform this test.";
            }
            foreach (var n in needs)
            {
                var c = cons.FirstOrDefault(x => x.ConsumableId == n.ConsumableId);
                if (c != null) c.QuantityOnHand -= n.QuantityPerTest;
            }

            item.TestRequestItemStatusId = ItTesting;
            item.AssignedTechnicianUserId = techUserId;
            item.StartedOn = DateTime.UtcNow;
            if (item.TestRequest.TestRequestStatusId == RqSubmitted || item.TestRequest.TestRequestStatusId == RqSamplesReceived)
                item.TestRequest.TestRequestStatusId = RqInProgress;
            _db.SaveChanges();
            return null;
        }

        public string? CaptureResult(int itemId, decimal value, string? notes, int techUserId, out bool abnormal)
        {
            abnormal = false;
            var item = _db.TestRequestItems.Include(i => i.TestRequest).ThenInclude(r => r.TestRequestItems)
                .Include(i => i.TestType).Include(i => i.TestResult)
                .FirstOrDefault(i => i.TestRequestItemId == itemId);
            if (item == null) return "That test was not found.";
            if (item.TestRequest.TestRequestStatusId == RqCancelled) return "That request was cancelled.";

            var mineActive = item.TestRequestItemStatusId == ItTesting && item.AssignedTechnicianUserId == techUserId;
            var mineReview = item.TestRequestItemStatusId == ItToBeReviewed && item.TestResult?.RecordedByUserId == techUserId;
            if (!mineActive && !mineReview) return "You can only capture results for tests you selected.";
            if (Math.Abs(value) >= 10_000_000m) return "That result value is too large.";
            value = Math.Round(value, 3);

            var tt = item.TestType;
            var now = DateTime.UtcNow;
            var res = item.TestResult;
            if (res == null)
            {
                res = new E.TestResult { TestRequestItemId = item.TestRequestItemId };
                _db.TestResults.Add(res);
            }
            res.ResultValue = value;
            res.Unit = tt.Unit;
            res.NormalMinAtTest = tt.NormalMin;
            res.NormalMaxAtTest = tt.NormalMax;
            res.RecordedByUserId = techUserId;
            res.RecordedOn = now;
            res.Notes = string.IsNullOrWhiteSpace(notes) ? null : (notes.Trim().Length > 500 ? notes.Trim()[..500] : notes.Trim());

            item.TestRequestItemStatusId = ItCompleted;
            item.CompletedOn = now;

            // Spec: once every test on the request has had its result captured (none left
            // Awaiting/SampleCollected/Testing - a rejected sample doesn't block this), the
            // request itself becomes Completed, independent of verification.
            var outstanding = new[] { ItAwaiting, ItSampleCollected, ItTesting };
            var allCaptured = item.TestRequest.TestRequestItems.All(i => !outstanding.Contains(i.TestRequestItemStatusId));
            if (allCaptured && item.TestRequest.TestRequestStatusId == RqInProgress)
                item.TestRequest.TestRequestStatusId = RqCompleted;

            _db.SaveChanges();

            abnormal = (tt.NormalMin.HasValue && value < tt.NormalMin) || (tt.NormalMax.HasValue && value > tt.NormalMax);
            return null;
        }

        // action "Verify" or anything else = return for review (note required, checked by the caller).
        // allVerified = every test on the request is now verified - the trigger to e-mail the doctor
        // the results as a PDF (the request itself reached Completed earlier, at capture time).
        public string? Review(int itemId, bool verify, string? note, int techUserId, out bool allVerified)
        {
            allVerified = false;
            var item = _db.TestRequestItems.Include(i => i.TestRequest).ThenInclude(r => r.TestRequestItems).Include(i => i.TestResult)
                .FirstOrDefault(i => i.TestRequestItemId == itemId);
            if (item == null || item.TestResult == null) return "That test was not found.";
            if (item.TestRequestItemStatusId != ItCompleted) return "That result is not waiting for verification.";
            if (item.TestResult.RecordedByUserId == techUserId) return "You can't verify your own result.";
            if (!_db.TechnicianTestTypes.Any(t => t.TechnicianUserId == techUserId && t.TestTypeId == item.TestTypeId))
                return "That test type hasn't been assigned to you by the laboratory manager.";

            var res = item.TestResult;
            _db.Verifications.Add(new E.Verification
            {
                TestResultId = res.TestResultId,
                VerifiedByUserId = techUserId,
                VerifiedOn = DateTime.UtcNow,
                Comment = string.IsNullOrWhiteSpace(note) ? null : (note.Trim().Length > 500 ? note.Trim()[..500] : note.Trim()),
                Outcome = verify ? "Verified" : "Returned",
                ResultValue = res.ResultValue,
                ResultRecordedByUserId = res.RecordedByUserId,
                ResultRecordedOn = res.RecordedOn
            });
            item.TestRequestItemStatusId = verify ? ItVerified : ItToBeReviewed;

            if (verify)
            {
                var all = item.TestRequest.TestRequestItems;
                allVerified = all.All(i => i.TestRequestItemStatusId == ItVerified);   // this item was just set above
            }
            _db.SaveChanges();
            return null;
        }
    }
}
