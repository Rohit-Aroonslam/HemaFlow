using Project.Services;

namespace Project.Models
{
    // ── Shared building blocks ────────────────────────────────────────────────

    public class DoctorSummary
    {
        public string Name { get; set; } = "";
        public string HpcsaNumber { get; set; } = "";
        public string Practice { get; set; } = "";
        public string Specialty { get; set; } = "";
    }

    public class TestRequestSummary
    {
        public int RequestId { get; set; }
        public string TestName { get; set; } = "";
        public DateTime RequestDate { get; set; }
        public string Status { get; set; } = "";
        public string ResultFlag { get; set; } = ""; // Normal | Abnormal | "" (not yet released)
    }

    public class MedicationItem
    {
        public string Name { get; set; } = "";
        public string Dosage { get; set; } = "";      // legacy / combined "strength, dose" text
        public string Strength { get; set; } = "";
        public string Dose { get; set; } = "";
        public string Form { get; set; } = "";
        public string Frequency { get; set; } = "";
        public DateOnly? StartedOn { get; set; }
    }

    public class ConditionItem
    {
        public string Name { get; set; } = "";
        public DateOnly? DiagnosedOn { get; set; }
    }

    public class AllergyItem
    {
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";   // e.g. Drug, Food, Environmental
        public string Severity { get; set; } = "";   // Mild / Moderate / Severe / Life-threatening
    }

    public class TrendDataPoint
    {
        public string Label { get; set; } = "";
        public DateTime Date { get; set; }
        public decimal Value { get; set; }
    }

    public class GeneratedReport
    {
        public string FileName { get; set; } = "";
        public DateTime GeneratedDate { get; set; }
        public string Category { get; set; } = "";
        public double FileSizeMb { get; set; }
    }

    public class AuthorizedDoctorAccess
    {
        public string DoctorName { get; set; } = "";
        public string HpcsaNumber { get; set; } = "";
        public string Practice { get; set; } = "";
        public string Specialty { get; set; } = "";
        public DateTime GrantedDate { get; set; }
        public bool IsFullAccess { get; set; }
        public List<int> GrantedRequestIds { get; set; } = new();
    }

    // ── Page-level ViewModels ─────────────────────────────────────────────────

    public class PatientDashboardViewModel
    {
        public string PatientName { get; set; } = "";
        public string PatientNumber { get; set; } = "";
        public int ActiveRequestCount { get; set; }
        public List<TestRequestSummary> RecentRequests { get; set; } = new();
        public List<string> Conditions { get; set; } = new();
        public List<string> Allergies { get; set; } = new();
        public List<MedicationItem> Medications { get; set; } = new();

        // Dashboard trend chart (item #7 - patient can choose which test to track)
        public List<string> AvailableTrackedTests { get; set; } = new();
        public string TrackedTest { get; set; } = "";
        public string TrackedTestUnit { get; set; } = "";
        public decimal TrackedTestNormalMax { get; set; }
        public List<TrendDataPoint> TrackedTestDataPoints { get; set; } = new();
    }

    public class MedicalProfileViewModel
    {
        public string PatientName { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string Surname { get; set; } = "";
        public string PatientId { get; set; } = "";
        public string IdNumber { get; set; } = "";
        public DateTime DateOfBirth { get; set; }
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string AddressLine1 { get; set; } = "";
        public string? ComplexName { get; set; }
        public int? SuburbId { get; set; }
        public string Suburb { get; set; } = "";
        public string City { get; set; } = "";
        public string PostalCode { get; set; } = "";
        public DoctorSummary? PrimaryDoctor { get; set; }
        // A patient can have more than one primary doctor.
        public List<DoctorSummary> PrimaryDoctors { get; set; } = new();
        public List<string> Conditions { get; set; } = new();
        public List<ConditionItem> ConditionDetails { get; set; } = new();
        public List<AllergyItem> Allergies { get; set; } = new();
        public List<MedicationItem> Medications { get; set; } = new();

        // Real, admin-managed master lists for the Add pickers (name + category)
        public List<ConditionRow> AvailableConditions { get; set; } = new();
        // Things this patient submitted as "Other" that an admin has not approved yet.
        public List<PendingSubmissionItem> PendingSubmissions { get; set; } = new();
        public List<AllergyRow> AvailableAllergies { get; set; } = new();
        public List<MedicationRow> AvailableMedications { get; set; } = new();
        public List<CategoryItem> ConditionCategories { get; set; } = new();
        public List<CategoryItem> AllergyCategories { get; set; } = new();
        public List<CategoryItem> MedicationCategories { get; set; } = new();

        // Real 2FA state - genuinely blank for a brand-new account,
        // populated from UserTwoFactor once the patient sets them up.
        public bool TwoFactorEnabled { get; set; }
        public DateTime? TwoFactorEnabledOn { get; set; }
        public DateTime? TwoFactorLastVerifiedOn { get; set; }
        public int BackupCodesRemaining { get; set; }
    }

    public class ConsentViewModel
    {
        public DoctorSummary? PrimaryDoctor { get; set; }
        public List<DoctorSummary> PrimaryDoctors { get; set; } = new();
        public List<AuthorizedDoctorAccess> AuthorizedDoctors { get; set; } = new();
        public List<TestRequestSummary> AllTestRequests { get; set; } = new();
    }

    public class TrackResultsViewModel
    {
        public List<string> AvailableTests { get; set; } = new();
        public string SelectedTest { get; set; } = "";
        public string Unit { get; set; } = "";
        public decimal NormalRangeMax { get; set; }
        public decimal NormalRangeMin { get; set; }   // 0 = no lower limit known
        public decimal MedianValue { get; set; }      // median of the patient's own readings
        public List<TrendDataPoint> DataPoints { get; set; } = new();
        public decimal LatestValue { get; set; }
        public decimal ChangePercent { get; set; }
    }

    public class MyResultsViewModel
    {
        public List<TestResultRow> Results { get; set; } = new();
        // Request header info (date, name, status) keyed by RequestId, so results can be grouped per request.
        public Dictionary<int, TestRequestSummary> Requests { get; set; } = new();
    }

    // One test request with every test result that belongs to it.
    public class RequestResultsViewModel
    {
        public TestRequestSummary Request { get; set; } = new();
        public List<TestResultRow> Results { get; set; } = new();
        public int AbnormalCount => Results.Count(r => r.IsAbnormal);
    }

    public class PatientActivityLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Action { get; set; } = "";
        public string Detail { get; set; } = "";
        public string Icon { get; set; } = "bi-info-circle-fill";
    }

    public class PatientActivityLogViewModel
    {
        public List<PatientActivityLogEntry> Entries { get; set; } = new();
    }

    public class ReportsViewModel
    {
        public List<GeneratedReport> GeneratedReports { get; set; } = new();
    }
    public class EditProfileViewModel
    {
        // Field names must match the form field names exactly (name="FirstName" etc.)
        public string FirstName { get; set; } = "";
        public string Surname { get; set; } = "";
        public string IdNumber { get; set; } = "";
        public DateTime DateOfBirth { get; set; }
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string AddressLine1 { get; set; } = "";
        public string? ComplexName { get; set; }
        public int? SuburbId { get; set; }
        public int? ProvinceId { get; set; }
        public string Suburb { get; set; } = "";
        public string City { get; set; } = "";
        public string PostalCode { get; set; } = "";
    }

    // ── 2FA setup / login-time verification ─────────────────────────────────

    // Shown on the "scan this QR code" page (PatientController.Setup2FA).
    public class TwoFactorSetupViewModel
    {
        public string SecretKey { get; set; } = "";     // Base32 - shown for manual entry
        public string OtpAuthUri { get; set; } = "";     // fed into a client-side QR renderer
        public string AccountEmail { get; set; } = "";
    }

    // Shown at login time once the password is correct and 2FA is enabled
    // (AccountController.VerifyTwoFactor).
    public class VerifyTwoFactorViewModel
    {
        // Accepts either a 6-digit authenticator code or a XXXX-XXXX backup code.
        public string Code { get; set; } = "";
    }

    public class PendingSubmissionItem
    {
        public string Type { get; set; } = "";   // Condition | Allergy | Medication
        public string Name { get; set; } = "";
        public DateTime SubmittedOn { get; set; }
    }
}