using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class TestResult
{
    public int TestResultId { get; set; }

    public int TestRequestItemId { get; set; }

    public decimal ResultValue { get; set; }

    public string? Unit { get; set; }

    public decimal? NormalMinAtTest { get; set; }

    public decimal? NormalMaxAtTest { get; set; }

    public int RecordedByUserId { get; set; }

    public DateTime RecordedOn { get; set; }

    public string? Notes { get; set; }

    public virtual TechnicianProfile RecordedByUser { get; set; } = null!;

    public virtual TestRequestItem TestRequestItem { get; set; } = null!;

    public virtual ICollection<Verification> Verifications { get; set; } = new List<Verification>();
}
