using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class TechnicianProfile
{
    public int UserId { get; set; }

    public string EmployeeCode { get; set; } = null!;

    public string? SaIdNumber { get; set; }

    public string? Department { get; set; }

    public bool IsArchived { get; set; }

    public virtual ICollection<RequestSample> RequestSamples { get; set; } = new List<RequestSample>();

    public virtual ICollection<TechnicianTestType> TechnicianTestTypes { get; set; } = new List<TechnicianTestType>();

    public virtual ICollection<TestRequestItem> TestRequestItems { get; set; } = new List<TestRequestItem>();

    public virtual ICollection<TestResult> TestResults { get; set; } = new List<TestResult>();

    public virtual User User { get; set; } = null!;

    public virtual ICollection<Verification> Verifications { get; set; } = new List<Verification>();
}
