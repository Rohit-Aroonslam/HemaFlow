using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class ApprovalStatus
{
    public byte ApprovalStatusId { get; set; }

    public string Code { get; set; } = null!;

    public virtual ICollection<PendingApproval> PendingApprovals { get; set; } = new List<PendingApproval>();
}
