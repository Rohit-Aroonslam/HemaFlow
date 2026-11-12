using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class PatientMedication
{
    public int PatientMedicationId { get; set; }

    public int PatientUserId { get; set; }

    public int MedicationId { get; set; }

    public DateOnly? StartedOn { get; set; }

    public string? Notes { get; set; }

    public string? Strength { get; set; }

    public string? Dose { get; set; }

    public string? Form { get; set; }

    public string? Frequency { get; set; }

    public virtual Medication Medication { get; set; } = null!;

    public virtual PatientProfile PatientUser { get; set; } = null!;
}
