using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class TestRequestItemStatus
{
    public byte TestRequestItemStatusId { get; set; }

    public string Code { get; set; } = null!;

    public virtual ICollection<TestRequestItem> TestRequestItems { get; set; } = new List<TestRequestItem>();
}
