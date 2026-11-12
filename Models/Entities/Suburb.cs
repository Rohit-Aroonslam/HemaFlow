using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class Suburb
{
    public int SuburbId { get; set; }

    public string Name { get; set; } = null!;

    public int TownId { get; set; }

    public string PostalCode { get; set; } = null!;

    public virtual Town Town { get; set; } = null!;
}
