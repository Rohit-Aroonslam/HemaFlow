using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class RequestSample
{
    public int RequestSampleId { get; set; }

    public int TestRequestItemId { get; set; }

    public int SampleTypeId { get; set; }

    public string BarcodeLabel { get; set; } = null!;

    // null while the sample has only been registered by the doctor; filled in when a technician receives it
    public int? CollectedByUserId { get; set; }

    public DateTime CollectedOn { get; set; }

    // When the laboratory received the physical sample (CollectedByUserId is then the receiving technician). UTC.
    public DateTime? ReceivedOn { get; set; }

    // Set instead of ReceivedOn when the technician rejects the physical sample at receiving
    // (e.g. clotted, insufficient volume, wrong tube) rather than accepting it.
    public string? RejectReason { get; set; }

    public DateTime? RejectedOn { get; set; }

    public int? RejectedByUserId { get; set; }

    public virtual TechnicianProfile? CollectedByUser { get; set; }

    public virtual TechnicianProfile? RejectedByUser { get; set; }

    public virtual SampleType SampleType { get; set; } = null!;

    public virtual TestRequestItem TestRequestItem { get; set; } = null!;
}
