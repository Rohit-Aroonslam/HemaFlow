namespace Project.Models.LabManager
{
    /*
     =========================================================
     🟢 TEMP: Per-QueueItem audit trail entry - demo-only, lets
            CaptureResult/VerifyResult/VerificationHistory show
            "where this test has been" (received → captured →
            sent for verification → verified/returned → ...).
     🔴 DB:  Replace with a real TestRequestItemHistory table.
     =========================================================
    */
    public class QueueItemEvent
    {
        public DateTime Timestamp { get; set; }

        // "Received" | "Started" | "Captured" | "Recaptured" |
        // "SentForVerification" | "Verified" | "Returned" | "Cancelled"
        public string EventType { get; set; } = "";

        public string By { get; set; } = "";

        public string? Note { get; set; }
    }

    // Small nested value types used on QueueItem's patient fields -
    // plain classes (not tuples) so Razor views can bind named properties.

    public class AllergyInfo
    {
        public string Name { get; set; } = "";
        public string Severity { get; set; } = ""; // "Severe" | "Moderate" | "Mild"
        public string? Note { get; set; }
    }

    // Mirrors PatientCondition (DiagnosedOn, Notes)
    public class ConditionNote
    {
        public string Condition { get; set; } = "";
        public string? DiagnosedOn { get; set; }
        public string? Note { get; set; }
    }

    // Mirrors PatientMedication (Strength, Dose, Frequency, Notes)
    public class MedicationInfo
    {
        public string Name { get; set; } = "";
        public string? Strength { get; set; }
        public string? Dose { get; set; }
        public string? Frequency { get; set; }
    }
}
