using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class LogType
{
    public byte LogTypeId { get; set; }

    public string Code { get; set; } = null!;

    public virtual ICollection<PatientActivityLog> PatientActivityLogs { get; set; } = new List<PatientActivityLog>();

    public virtual ICollection<StaffActivityLog> StaffActivityLogs { get; set; } = new List<StaffActivityLog>();
}
