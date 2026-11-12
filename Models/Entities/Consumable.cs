using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class Consumable
{
    public int ConsumableId { get; set; }

    public string Name { get; set; } = null!;

    public int SupplierId { get; set; }

    public string? UnitOfMeasure { get; set; }

    public int ReorderLevel { get; set; }

    public int QuantityOnHand { get; set; }

    public bool IsArchived { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual Supplier Supplier { get; set; } = null!;

    public virtual ICollection<TestConsumable> TestConsumables { get; set; } = new List<TestConsumable>();
}
