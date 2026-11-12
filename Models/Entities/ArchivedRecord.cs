using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class ArchivedRecord
{
    public int ArchivedRecordId { get; set; }

    public string EntityType { get; set; } = null!;

    public int EntityId { get; set; }

    public string DisplayName { get; set; } = null!;

    public string Reference { get; set; } = null!;

    public string? Reason { get; set; }

    public int ArchivedByUserId { get; set; }

    public DateTime ArchivedOn { get; set; }

    public virtual User ArchivedByUser { get; set; } = null!;
}
