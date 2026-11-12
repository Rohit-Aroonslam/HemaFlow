using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class Role
{
    public byte RoleId { get; set; }

    public string Code { get; set; } = null!;

    public string? DisplayName { get; set; }

    public virtual ICollection<StaffActivityLog> StaffActivityLogs { get; set; } = new List<StaffActivityLog>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
