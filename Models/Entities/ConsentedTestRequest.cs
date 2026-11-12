using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class ConsentedTestRequest
{
    public int ConsentedTestRequestId { get; set; }

    public int PatientDoctorConsentId { get; set; }

    public int TestRequestItemId { get; set; }

    public bool IsConsented { get; set; }

    public virtual PatientDoctorConsent PatientDoctorConsent { get; set; } = null!;

    public virtual TestRequestItem TestRequestItem { get; set; } = null!;
}
