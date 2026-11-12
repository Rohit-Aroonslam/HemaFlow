using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class Urgency
{
    public byte UrgencyId { get; set; }

    public string Code { get; set; } = null!;

    public virtual ICollection<TestRequest> TestRequests { get; set; } = new List<TestRequest>();
}
