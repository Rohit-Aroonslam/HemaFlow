namespace Project.Models
{
    // A patient-submitted "Other" allergy/condition/medication awaiting admin review.
    public class PendingApprovalItem
    {
        public int Id { get; set; }
        public string EntityType { get; set; } = ""; // Condition | Allergy | Medication
        public string Name { get; set; } = "";
        public string? ExtraInfo { get; set; }        // e.g. dosage/frequency for medications
        public string PatientName { get; set; } = "";
        public string? SuggestedCategory { get; set; }   // category the patient picked when submitting

        // The real logged-in patient's Users.UserId, set at submission time
        // (PatientController.AddCondition/AddAllergy/AddMedication). Needed
        // so AdminController.ApprovePending knows which patient's own
        // PatientCondition/PatientAllergy/PatientMedication row to create
        // once the item is approved - PatientName alone isn't a safe lookup
        // key (two patients can share a name).
        public int PatientUserId { get; set; }

        public DateTime SubmittedOn { get; set; }
        public string Status { get; set; } = "Pending"; // Pending | Approved | Denied

        // Patient context shown beside the request, plus the outcome once reviewed.
        public string PatientCode { get; set; } = "";
        public string PatientEmail { get; set; } = "";
        public int PatientOtherPending { get; set; }
        public DateTime? ReviewedOn { get; set; }
        public string? DenyReason { get; set; }
    }

    // Shared in-memory store so Patient submissions and Admin review both see the
    // same list. Will be replaced by a real DB table once the database is wired up.
    public static class PendingApprovalStore
    {
        private static int _nextId = 1;
        public static List<PendingApprovalItem> Items { get; } = new();

        public static int TakeNextId() => _nextId++;
    }
}