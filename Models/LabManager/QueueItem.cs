namespace Project.Models.LabManager
{
    /*
     =========================================================
     Work-queue row for the Lab Technician portal.
     One QueueItem = one test type on a test request (TestRequestItem).
     It is NOT a table: Services/LabTechService builds it from
     TestRequest / TestRequestItem / RequestSample / TestResult /
     Verification, and every change goes back through that service.
     All DateTime values here are SAST (the database stores UTC).
     =========================================================
    */
    public class QueueItem
    {
        // How close to its turnaround limit a test must be to count as "nearing".
        public const int NearingTatMinutes = 30;

        // "Now" in South Africa, so due times and "overdue" compare like with like.
        public static DateTime SastNow => Project.Services.SastTime.ToSast(DateTime.UtcNow);

        public int Id { get; set; }                 // TestRequestItemId
        public int RequestId { get; set; }          // TestRequestId
        public int PatientUserId { get; set; }

        // ── Test request (captured by the Doctor subsystem) ──
        public string RequestNumber { get; set; } = "";   // "REQ-007", same as the doctor and patient screens
        public DateTime RequestDate { get; set; }
        public string Urgency { get; set; } = "Routine";   // "STAT" | "Urgent" | "Routine"
        public string? ClinicalNotes { get; set; }          // TestRequest.Notes
        public string DoctorName { get; set; } = "";
        public string DoctorEmail { get; set; } = "";
        public string Barcode { get; set; } = "";           // RequestSample.BarcodeLabel

        // FK into TempDataStore.TestTypes (Lab Manager catalogue) - name,
        // sample type, category, reference range and turnaround all come from there.
        public int TestTypeId { get; set; }

        // Submitted → Available (samples received) → Active (in progress) →
        // Verify (completed, awaiting verification) → Verified | Review (to be reviewed).
        // "Cancelled" when the request was cancelled.
        public string Status { get; set; } = "Submitted";

        public int? AssignedTechnicianId { get; set; }

        // ── Sample receipt ──
        public DateTime? ReceivedAt { get; set; }
        public string? ReceivedBy { get; set; }

        // ── Processing ──
        public DateTime? StartedAt { get; set; }

        // ── Patient (only what the Patient subsystem captures) ──
        public string PatientName { get; set; } = "";
        public string PatientIdNumber { get; set; } = "";
        public string PatientDob { get; set; } = "";
        public List<ConditionNote> PatientConditions { get; set; } = new List<ConditionNote>();
        public List<AllergyInfo> PatientAllergies { get; set; } = new List<AllergyInfo>();
        public List<MedicationInfo> PatientMedications { get; set; } = new List<MedicationInfo>();

        // ── Result (populated once captured) ──
        public decimal? ResultValue { get; set; }
        public string? ResultUnits { get; set; }
        public string? Flag { get; set; }          // "Normal" | "Abnormal"
        public DateTime? CapturedAt { get; set; }  // completion date/time
        public string? CapturedBy { get; set; }
        public int? CapturedByUserId { get; set; }
        public string? TechNotes { get; set; }

        // ── Verification ──
        public string? VerifiedBy { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public string? VerifierNote { get; set; }  // note on return for review

        public string? CancelReason { get; set; }

        // Full chronological history of the test (receipt, start, captures,
        // returns with notes, verification).
        public List<QueueItemEvent> History { get; set; } = new List<QueueItemEvent>();

        // ── From the Lab Manager catalogue (filled in by LabTechService) ──
        public TestType? TestType { get; set; }
        public string TestTypeName => TestType?.Name ?? "-";
        public string SampleTypeName { get; set; } = "-";
        public int CategoryId => TestType?.TestCategoryId ?? 0;
        public string CategoryName { get; set; } = "Uncategorised";

        // Tube colour for the sample type (standard haematology tube caps).
        public string TubeColour => SampleTypeName switch
        {
            "Whole Blood" => "Purple top",
            "Plasma"      => "Blue top",
            "Serum"       => "Gold top",
            _             => "Sterile container"
        };

        // ── Turnaround: expected completion = received + turnaround time ──
        public DateTime? DueAt => ReceivedAt?.AddMinutes(TestType?.TurnaroundTime ?? 0);

        // Turnaround only runs while the result is still outstanding.
        private bool TatRunning => Status is "Available" or "Active" or "Review";

        public bool IsOverdue => TatRunning && DueAt.HasValue && DueAt.Value < SastNow;

        public bool IsNearingTat => TatRunning && DueAt.HasValue && !IsOverdue
                                    && (DueAt.Value - SastNow).TotalMinutes <= NearingTatMinutes;

        public string DueText
        {
            get
            {
                if (!DueAt.HasValue) return "-";
                var diff = DueAt.Value - SastNow;
                if (!TatRunning) return DueAt.Value.ToString("dd MMM HH:mm");
                return diff.TotalMinutes < 0 ? $"Overdue by {Span(-diff)}" : $"{Span(diff)} left";
            }
        }

        private static string Span(TimeSpan t) =>
            t.TotalHours >= 1 ? $"{(int)t.TotalHours}h {t.Minutes:00}m" : $"{Math.Max(1, (int)t.TotalMinutes)} min";

        // Test progress status, worded as in the specification.
        public string StatusLabel => Status switch
        {
            "Submitted" => "Submitted",
            "Available" => "Sample Received",
            "Active"    => "In Progress",
            "Verify"    => "Completed",
            "Review"    => "To Be Reviewed",
            "Verified"  => "Verified",
            "Cancelled" => "Cancelled",
            _           => Status
        };

        // Request-level status, derived from all tests on the request
        // (spec: Submitted / Sample(s) received / In progress / Completed / Cancelled).
        public static string RequestStatus(IEnumerable<QueueItem> requestItems)
        {
            var items = requestItems.ToList();
            if (items.Count == 0) return "-";
            if (items.Any(i => i.Status == "Cancelled")) return "Cancelled";
            if (items.All(i => i.Status == "Submitted")) return "Submitted";
            if (items.All(i => i.Status is "Verify" or "Verified")) return "Completed";
            if (items.Any(i => i.Status is "Active" or "Verify" or "Review" or "Verified")) return "In Progress";
            return "Samples Received";
        }

    }//end of class QueueItem
}//end of namespace
