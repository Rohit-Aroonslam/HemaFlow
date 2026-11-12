using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class User
{
    public int UserId { get; set; }

    public byte RoleId { get; set; }

    public string FirstName { get; set; } = null!;

    public string Surname { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? Phone { get; set; }

    public string PasswordHash { get; set; } = null!;

    public bool MustChangePassword { get; set; }

    public bool TwoFactorEnabled { get; set; }

    public bool IsArchived { get; set; }

    public DateTime CreatedOn { get; set; }

    public string UserCode { get; set; } = null!;

    public string? Identifier { get; set; }

    public string Status { get; set; } = null!;

    public virtual AdminProfile? AdminProfile { get; set; }

    public virtual ICollection<ArchivedRecord> ArchivedRecords { get; set; } = new List<ArchivedRecord>();

    public virtual DoctorProfile? DoctorProfile { get; set; }

    public virtual LabManagerProfile? LabManagerProfile { get; set; }

    public virtual ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();

    public virtual PatientProfile? PatientProfile { get; set; }

    public virtual ICollection<PendingApproval> PendingApprovalReviewedByUsers { get; set; } = new List<PendingApproval>();

    public virtual ICollection<PendingApproval> PendingApprovalSubmittedByUsers { get; set; } = new List<PendingApproval>();

    public virtual Role Role { get; set; } = null!;

    public virtual ICollection<StaffActivityLog> StaffActivityLogs { get; set; } = new List<StaffActivityLog>();

    public virtual TechnicianProfile? TechnicianProfile { get; set; }

    public virtual ICollection<UserBackupCode> UserBackupCodes { get; set; } = new List<UserBackupCode>();

    public virtual ICollection<UserPasskey> UserPasskeys { get; set; } = new List<UserPasskey>();

    public virtual UserTwoFactor? UserTwoFactor { get; set; }
}
