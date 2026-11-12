using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class MedicalCondition
{
    public int ConditionId { get; set; }

    public string Name { get; set; } = null!;

    public int CategoryId { get; set; }

    public string? Icd10Code { get; set; }

    public string? Description { get; set; }

    public bool IsArchived { get; set; }

    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<PatientCondition> PatientConditions { get; set; } = new List<PatientCondition>();
}
