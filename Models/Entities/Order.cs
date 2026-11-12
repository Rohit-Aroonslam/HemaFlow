using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class Order
{
    public int OrderId { get; set; }

    public int SupplierId { get; set; }

    public byte OrderStatusId { get; set; }

    public int PlacedByUserId { get; set; }

    public DateTime PlacedOn { get; set; }

    public DateTime? ReceivedOn { get; set; }

    public string? OrderNumber { get; set; }

    public string? CancelReason { get; set; }

    public DateTime? CancelledOn { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual OrderStatus OrderStatus { get; set; } = null!;

    public virtual LabManagerProfile PlacedByUser { get; set; } = null!;

    public virtual Supplier Supplier { get; set; } = null!;
}
