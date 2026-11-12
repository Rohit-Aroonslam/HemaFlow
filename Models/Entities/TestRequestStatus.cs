using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class TestRequestStatus
{
    public byte TestRequestStatusId { get; set; }

    public string Code { get; set; } = null!;

    public virtual ICollection<TestRequest> TestRequests { get; set; } = new List<TestRequest>();
}
