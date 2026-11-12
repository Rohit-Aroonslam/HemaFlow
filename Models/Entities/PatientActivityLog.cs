using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class PatientActivityLog
{
    public int LogId { get; set; }

    public int PatientUserId { get; set; }

    public string Action { get; set; } = null!;

    public string? Detail { get; set; }

    public string? Icon { get; set; }

    public byte LogTypeId { get; set; }

    public DateTime Timestamp { get; set; }

    public virtual LogType LogType { get; set; } = null!;

    public virtual PatientProfile PatientUser { get; set; } = null!;
}
