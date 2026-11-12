using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class OrderItem
{
    public int OrderItemId { get; set; }

    public int OrderId { get; set; }

    public int ConsumableId { get; set; }

    public int QuantityOrdered { get; set; }

    public int QuantityReceived { get; set; }

    public byte OrderItemStatusId { get; set; }

    public DateTime? ReceivedOn { get; set; }

    public string? CancelReason { get; set; }

    public DateTime? CancelledOn { get; set; }

    public virtual Consumable Consumable { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;

    public virtual OrderItemStatus OrderItemStatus { get; set; } = null!;
}
