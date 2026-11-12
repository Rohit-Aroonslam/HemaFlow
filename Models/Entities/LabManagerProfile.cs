using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class LabManagerProfile
{
    public int UserId { get; set; }

    public string ManagerCode { get; set; } = null!;

    public bool IsArchived { get; set; }

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();

    public virtual User User { get; set; } = null!;
}
