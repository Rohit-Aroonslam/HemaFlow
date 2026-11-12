using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class TechnicianTestType
{
    public int TechnicianTestTypeId { get; set; }

    public int TechnicianUserId { get; set; }

    public int TestTypeId { get; set; }

    public virtual TechnicianProfile TechnicianUser { get; set; } = null!;

    public virtual TestType TestType { get; set; } = null!;
}
