using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class DoctorProfile
{
    public int UserId { get; set; }

    public string DoctorCode { get; set; } = null!;

    public string HpcsaNumber { get; set; } = null!;

    public string? Specialization { get; set; }

    public bool IsArchived { get; set; }

    public virtual ICollection<PatientDoctorConsent> PatientDoctorConsents { get; set; } = new List<PatientDoctorConsent>();

    public virtual ICollection<TestRequest> TestRequests { get; set; } = new List<TestRequest>();

    public virtual User User { get; set; } = null!;
}
