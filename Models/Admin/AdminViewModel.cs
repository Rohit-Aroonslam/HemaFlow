using System.ComponentModel.DataAnnotations;

namespace Project.Models
{
    // ── Shared ────────────────────────────────────────────────────────────────

    public class CategoryItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string SysCode { get; set; } = "";
        public string DomainType { get; set; } = "";
        public bool IsArchived { get; set; }
    }

    public class RecentUserRow
    {
        public string UserId { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = "";
    }

    public class UserRow
    {
        public string UserId { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Identifier { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = "";
        public string Status { get; set; } = "";
        public string RoleCode { get; set; } = "";
        public bool IsArchived { get; set; }
        // False when the signed-in admin may not edit, reset or deactivate this account
        public bool CanManage { get; set; }
    }

    public class ConditionRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string Icd10Code { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsArchived { get; set; }
    }

    public class AllergyRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string TypicalReaction { get; set; } = "";
        public string Notes { get; set; } = "";
        public bool IsArchived { get; set; }
    }

    public class MedicationRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string Notes { get; set; } = "";
        public bool IsArchived { get; set; }
    }

    public class ArchivedRecord
    {
        public string EntityType { get; set; } = "";
        public int EntityId { get; set; }
        public string DisplayName { get; set; } = "";
        public string Reference { get; set; } = "";
        public string Reason { get; set; } = "";
        public string ArchivedBy { get; set; } = "";
        public DateTime ArchivedOn { get; set; }
    }

    // ── Activity Log ──────────────────────────────────────────────────────────
    public class ActivityLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Actor { get; set; } = "";
        public string Role { get; set; } = "";
        public string Action { get; set; } = "";
        public string Detail { get; set; } = "";
        public string LogType { get; set; } = "info"; // info | success | warning | danger
    }

    public class ActivityLogPageViewModel
    {
        public List<ActivityLogEntry> Entries { get; set; } = new();
        public string Query { get; set; } = "";
        public string Type { get; set; } = "";
        public string Role { get; set; } = "";
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public int Page { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int TotalCount { get; set; }
        public List<string> Roles { get; set; } = new();
    }

    // ── Page ViewModels ───────────────────────────────────────────────────────

    public class AdminDashboardViewModel
    {
        public string FirstName { get; set; } = "";
        public string Surname { get; set; } = "";
        public int TotalUsers { get; set; }
        public int TotalConditions { get; set; }
        public int TotalAllergies { get; set; }
        public int TotalMedications { get; set; }
        public int TotalCategories { get; set; }
        public int StaffCount { get; set; }
        public int PendingApprovalsCount { get; set; }
        public List<RecentUserRow> RecentUsers { get; set; } = new();
        public List<ActivityLogEntry> ActivityLog { get; set; } = new();
        public Dictionary<string, int> UsersByRole { get; set; } = new();
    }

    public class AdminProfileViewModel
    {
        public string FullName { get; set; } = "";
        public string FirstName { get; set; } = "";
        public string Surname { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string AdminId { get; set; } = "";
        public DateTime DateOfBirth { get; set; }
        public string AddressLine1 { get; set; } = "";
        public string? ComplexName { get; set; }
        public int? SuburbId { get; set; }
        public int? ProvinceId { get; set; }
        public string Suburb { get; set; } = "";
        public string City { get; set; } = "";
        public string PostalCode { get; set; } = "";

        // Security section (real data from UserTwoFactor / UserBackupCode)
        public bool TwoFactorEnabled { get; set; }
        public DateTime? TwoFactorLastVerified { get; set; }
        public int BackupCodesLeft { get; set; }
        public List<string> NewBackupCodes { get; set; } = new();
    }

    public class GlobalCategoriesViewModel
    {
        public List<CategoryItem> MedicalCategories { get; set; } = new();
        public List<CategoryItem> AllergyCategories { get; set; } = new();
        public List<CategoryItem> MedicationCategories { get; set; } = new();
    }

    public class PendingApprovalsViewModel
    {
        public List<PendingApprovalItem> Items { get; set; } = new();
        public List<PendingApprovalItem> Reviewed { get; set; } = new();
        public List<CategoryItem> ConditionCategories { get; set; } = new();
        public List<CategoryItem> AllergyCategories { get; set; } = new();
        public List<CategoryItem> MedicationCategories { get; set; } = new();
    }

    public class MedicalConditionsViewModel
    {
        public List<ConditionRow> Conditions { get; set; } = new();
        public List<CategoryItem> Categories { get; set; } = new();
    }

    public class AllergiesViewModel
    {
        public List<AllergyRow> Allergies { get; set; } = new();
        public List<CategoryItem> Categories { get; set; } = new();
    }

    public class MedicationsViewModel
    {
        public List<MedicationRow> Medications { get; set; } = new();
        public List<CategoryItem> Categories { get; set; } = new();
    }

    public class UserManagementViewModel
    {
        public List<UserRow> Users { get; set; } = new();
        public UserRow? EditingUser { get; set; }
        public bool ShowArchived { get; set; }
        public int ArchivedCount { get; set; }
    }

    public class SystemArchiveViewModel
    {
        public List<ArchivedRecord> Records { get; set; } = new();
    }

    // ── Create/Edit ViewModels ────────────────────────────────────────────────

    public class CreateConditionViewModel
    {
        public List<CategoryItem> Categories { get; set; } = new();
        public int EditId { get; set; }

        [Required(ErrorMessage = "Condition name is required.")]
        [StringLength(200, MinimumLength = 2)]
        public string Name { get; set; } = "";

        [RegularExpression(@"^[A-Z][0-9]{2}(\.[0-9]{1,4})?$",
            ErrorMessage = "Format: A00 or A00.0")]
        public string? Icd10Code { get; set; }

        [Required(ErrorMessage = "Please select a category.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a category.")]
        public int CategoryId { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }
    }

    public class CreateAllergyViewModel
    {
        public List<CategoryItem> Categories { get; set; } = new();
        public int EditId { get; set; }

        [Required(ErrorMessage = "Allergy name is required.")]
        [StringLength(200, MinimumLength = 2)]
        public string Name { get; set; } = "";

        [Required(ErrorMessage = "Please select a category.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a category.")]
        public int CategoryId { get; set; }

        [StringLength(300)]
        public string? TypicalReaction { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }
    }

    public class CreateMedicationViewModel
    {
        public List<CategoryItem> Categories { get; set; } = new();
        public int EditId { get; set; }

        [Required(ErrorMessage = "Medication name is required.")]
        [StringLength(200, MinimumLength = 2)]
        public string Name { get; set; } = "";

        [Required(ErrorMessage = "Please select a category.")]
        [Range(1, int.MaxValue, ErrorMessage = "Please select a category.")]
        public int CategoryId { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }
    }

    public class EditUserViewModel
    {
        public string UserId { get; set; } = "";
        [Required] public string FullName { get; set; } = "";
        [Required][EmailAddress] public string Email { get; set; } = "";
        public string Identifier { get; set; } = "";
        public string Role { get; set; } = "";
        public string Status { get; set; } = "";
    }
}
