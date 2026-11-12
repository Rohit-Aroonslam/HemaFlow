using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class PatientCondition
{
    public int PatientConditionId { get; set; }

    public int PatientUserId { get; set; }

    public int ConditionId { get; set; }

    public DateOnly? DiagnosedOn { get; set; }

    public string? Notes { get; set; }

    public virtual MedicalCondition Condition { get; set; } = null!;

    public virtual PatientProfile PatientUser { get; set; } = null!;
}
