using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class SampleType
{
    public int SampleTypeId { get; set; }

    public string Name { get; set; } = null!;

    public bool IsArchived { get; set; }

    public virtual ICollection<RequestSample> RequestSamples { get; set; } = new List<RequestSample>();

    public virtual ICollection<TestType> TestTypes { get; set; } = new List<TestType>();
}
