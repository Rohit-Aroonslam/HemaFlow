using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class StaffActivityLog
{
    public int LogId { get; set; }

    public int ActorId { get; set; }

    public byte ActorRole { get; set; }

    public string Action { get; set; } = null!;

    public string? Detail { get; set; }

    public byte LogTypeId { get; set; }

    public DateTime Timestamp { get; set; }

    public virtual User Actor { get; set; } = null!;

    public virtual Role ActorRoleNavigation { get; set; } = null!;

    public virtual LogType LogType { get; set; } = null!;
}
