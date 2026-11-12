using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class UserBackupCode
{
    public int BackupCodeId { get; set; }

    public int UserId { get; set; }

    public string CodeHash { get; set; } = null!;

    public DateTime? UsedOn { get; set; }

    public DateTime CreatedOn { get; set; }

    public virtual User User { get; set; } = null!;
}
