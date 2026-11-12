using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class UserTwoFactor
{
    public int UserId { get; set; }

    public string SecretKey { get; set; } = null!;

    public bool IsEnabled { get; set; }

    public DateTime? EnabledOn { get; set; }

    public DateTime? LastVerifiedOn { get; set; }

    public virtual User User { get; set; } = null!;
}
