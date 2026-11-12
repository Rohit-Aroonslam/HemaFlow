using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class DomainType
{
    public byte DomainTypeId { get; set; }

    public string Code { get; set; } = null!;

    public string? Description { get; set; }
}
