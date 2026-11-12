using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class OrderStatus
{
    public byte OrderStatusId { get; set; }

    public string Code { get; set; } = null!;

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
