using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class Province
{
    public byte ProvinceId { get; set; }

    public string Name { get; set; } = null!;

    public virtual ICollection<Town> Towns { get; set; } = new List<Town>();
}
