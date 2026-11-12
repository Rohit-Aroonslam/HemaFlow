using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class TestType
{
    public int TestTypeId { get; set; }

    public string Name { get; set; } = null!;

    public int TestCategoryId { get; set; }

    public int SampleTypeId { get; set; }

    public string? Unit { get; set; }

    public decimal? NormalMin { get; set; }

    public decimal? NormalMax { get; set; }

    public int? TurnaroundMinutes { get; set; }

    public bool IsArchived { get; set; }

    public virtual SampleType SampleType { get; set; } = null!;

    public virtual ICollection<TechnicianTestType> TechnicianTestTypes { get; set; } = new List<TechnicianTestType>();

    public virtual TestCategory TestCategory { get; set; } = null!;

    public virtual ICollection<TestConsumable> TestConsumables { get; set; } = new List<TestConsumable>();

    public virtual ICollection<TestRequestItem> TestRequestItems { get; set; } = new List<TestRequestItem>();
}
