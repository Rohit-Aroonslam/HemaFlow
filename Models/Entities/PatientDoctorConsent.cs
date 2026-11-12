using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class PatientDoctorConsent
{
    public int PatientDoctorConsentId { get; set; }

    public int PatientUserId { get; set; }

    public int DoctorUserId { get; set; }

    public DateTime GrantedOn { get; set; }

    public DateTime? RevokedOn { get; set; }

    public virtual ICollection<ConsentedTestRequest> ConsentedTestRequests { get; set; } = new List<ConsentedTestRequest>();

    public virtual DoctorProfile DoctorUser { get; set; } = null!;

    public virtual PatientProfile PatientUser { get; set; } = null!;
}
