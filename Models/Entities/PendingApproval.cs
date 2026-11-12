using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class PendingApproval
{
    public int PendingApprovalId { get; set; }

    public byte ApprovalTypeId { get; set; }

    public byte ApprovalStatusId { get; set; }

    public string Name { get; set; } = null!;

    public int? CategoryId { get; set; }

    public string? ExtraInfo { get; set; }

    public int SubmittedByUserId { get; set; }

    public DateTime SubmittedOn { get; set; }

    public int? ReviewedByUserId { get; set; }

    public DateTime? ReviewedOn { get; set; }

    public string? DenyReason { get; set; }

    public virtual ApprovalStatus ApprovalStatus { get; set; } = null!;

    public virtual ApprovalType ApprovalType { get; set; } = null!;

    public virtual Category? Category { get; set; }

    public virtual User? ReviewedByUser { get; set; }

    public virtual User SubmittedByUser { get; set; } = null!;
}
