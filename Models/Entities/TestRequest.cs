using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class TestRequest
{
    public int TestRequestId { get; set; }

    public int PatientUserId { get; set; }

    public int DoctorUserId { get; set; }

    public byte TestRequestStatusId { get; set; }

    public byte UrgencyId { get; set; }

    public DateTime RequestedOn { get; set; }

    public string? Notes { get; set; }

    public DateTime? ReleasedOn { get; set; }

    public string? DoctorNote { get; set; }

    public string? CancelReason { get; set; }

    public DateTime? CancelledOn { get; set; }

    public virtual DoctorProfile DoctorUser { get; set; } = null!;

    public virtual PatientProfile PatientUser { get; set; } = null!;

    public virtual ICollection<TestRequestItem> TestRequestItems { get; set; } = new List<TestRequestItem>();

    public virtual TestRequestStatus TestRequestStatus { get; set; } = null!;

    public virtual Urgency Urgency { get; set; } = null!;
}
