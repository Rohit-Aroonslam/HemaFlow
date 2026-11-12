using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class TestCategory
{
    public int TestCategoryId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsArchived { get; set; }

    public virtual ICollection<TestType> TestTypes { get; set; } = new List<TestType>();
}
