using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class Supplier
{
    public int SupplierId { get; set; }

    public string Name { get; set; } = null!;

    public string? ContactPerson { get; set; }

    public string? ContactEmail { get; set; }

    public string? ContactPhone { get; set; }

    public bool IsArchived { get; set; }

    public virtual ICollection<Consumable> Consumables { get; set; } = new List<Consumable>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
