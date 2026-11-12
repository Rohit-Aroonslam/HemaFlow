using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class Medication
{
    public int MedicationId { get; set; }

    public string Name { get; set; } = null!;

    public int CategoryId { get; set; }

    public string? Notes { get; set; }

    public bool IsArchived { get; set; }

    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<PatientMedication> PatientMedications { get; set; } = new List<PatientMedication>();
}
