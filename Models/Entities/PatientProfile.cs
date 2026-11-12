using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class PatientProfile
{
    public int UserId { get; set; }

    public string PatientCode { get; set; } = null!;

    public string IdNumber { get; set; } = null!;

    public DateOnly? DateOfBirth { get; set; }

    public string? AddressLine1 { get; set; }

    // Optional complex / building / unit name, shown above the street address.
    public string? ComplexName { get; set; }

    // Normalised address: Province -> Town -> Suburb (which carries the postal code).
    public int? SuburbId { get; set; }

    public virtual Suburb? Suburb { get; set; }

    public bool IsArchived { get; set; }

    public virtual ICollection<PatientActivityLog> PatientActivityLogs { get; set; } = new List<PatientActivityLog>();

    public virtual ICollection<PatientAllergy> PatientAllergies { get; set; } = new List<PatientAllergy>();

    public virtual ICollection<PatientCondition> PatientConditions { get; set; } = new List<PatientCondition>();

    public virtual ICollection<PatientDoctorConsent> PatientDoctorConsents { get; set; } = new List<PatientDoctorConsent>();

    public virtual ICollection<PatientMedication> PatientMedications { get; set; } = new List<PatientMedication>();

    public virtual ICollection<TestRequest> TestRequests { get; set; } = new List<TestRequest>();

    public virtual User User { get; set; } = null!;
}
