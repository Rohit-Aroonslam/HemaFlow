using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class Category
{
    public int CategoryId { get; set; }

    public string Name { get; set; } = null!;

    public string SysCode { get; set; } = null!;

    public string DomainType { get; set; } = null!;

    public bool IsArchived { get; set; }

    public virtual ICollection<Allergy> Allergies { get; set; } = new List<Allergy>();

    public virtual ICollection<MedicalCondition> MedicalConditions { get; set; } = new List<MedicalCondition>();

    public virtual ICollection<Medication> Medications { get; set; } = new List<Medication>();

    public virtual ICollection<PendingApproval> PendingApprovals { get; set; } = new List<PendingApproval>();
}
