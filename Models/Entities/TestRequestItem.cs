using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class TestRequestItem
{
    public int TestRequestItemId { get; set; }

    public int TestRequestId { get; set; }

    public int TestTypeId { get; set; }

    public byte TestRequestItemStatusId { get; set; }

    public int? AssignedTechnicianUserId { get; set; }

    // When the technician selected the test, and when the (latest) result was captured. UTC.
    public DateTime? StartedOn { get; set; }

    public DateTime? CompletedOn { get; set; }

    public virtual TechnicianProfile? AssignedTechnicianUser { get; set; }

    public virtual ICollection<ConsentedTestRequest> ConsentedTestRequests { get; set; } = new List<ConsentedTestRequest>();

    public virtual ICollection<RequestSample> RequestSamples { get; set; } = new List<RequestSample>();

    public virtual TestRequest TestRequest { get; set; } = null!;

    public virtual TestRequestItemStatus TestRequestItemStatus { get; set; } = null!;

    public virtual TestResult? TestResult { get; set; }

    public virtual TestType TestType { get; set; } = null!;
}
