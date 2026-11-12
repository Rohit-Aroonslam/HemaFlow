using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class PatientAllergy
{
    public int PatientAllergyId { get; set; }

    public int PatientUserId { get; set; }

    public int AllergyId { get; set; }

    public string? Notes { get; set; }

    public string? Severity { get; set; }

    public virtual Allergy Allergy { get; set; } = null!;

    public virtual PatientProfile PatientUser { get; set; } = null!;
}
