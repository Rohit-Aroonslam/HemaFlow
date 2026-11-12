using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class Town
{
    public int TownId { get; set; }

    public string Name { get; set; } = null!;

    public byte ProvinceId { get; set; }

    public virtual Province Province { get; set; } = null!;

    public virtual ICollection<Suburb> Suburbs { get; set; } = new List<Suburb>();
}
