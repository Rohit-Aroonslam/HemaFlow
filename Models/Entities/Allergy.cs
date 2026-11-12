using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class Allergy
{
    public int AllergyId { get; set; }

    public string Name { get; set; } = null!;

    public int CategoryId { get; set; }

    public string? TypicalReaction { get; set; }

    public string? Notes { get; set; }

    public bool IsArchived { get; set; }

    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<PatientAllergy> PatientAllergies { get; set; } = new List<PatientAllergy>();
}
