using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class TestConsumable
{
    public int TestConsumableId { get; set; }

    public int TestTypeId { get; set; }

    public int ConsumableId { get; set; }

    public int QuantityPerTest { get; set; }

    public virtual Consumable Consumable { get; set; } = null!;

    public virtual TestType TestType { get; set; } = null!;
}
