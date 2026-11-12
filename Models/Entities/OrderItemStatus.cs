using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class OrderItemStatus
{
    public byte OrderItemStatusId { get; set; }

    public string Code { get; set; } = null!;

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
