using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class PasswordResetToken
{
    public int TokenId { get; set; }

    public int UserId { get; set; }

    public string Token { get; set; } = null!;

    public DateTime ExpiresOn { get; set; }

    public DateTime? UsedOn { get; set; }

    public DateTime CreatedOn { get; set; }

    public virtual User User { get; set; } = null!;
}
