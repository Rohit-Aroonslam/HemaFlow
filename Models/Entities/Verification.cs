using System;
using System.Collections.Generic;

namespace Project.Models.Entities;

public partial class Verification
{
    public int VerificationId { get; set; }

    public int TestResultId { get; set; }

    public int VerifiedByUserId { get; set; }

    public DateTime VerifiedOn { get; set; }

    public string? Comment { get; set; }

    // "Verified" or "Returned". A result can be reviewed many times (returned, recaptured, verified again),
    // so there is one Verification row per review, not one per result.
    public string Outcome { get; set; } = "Verified";

    // Snapshot of the captured result this review was about (the live TestResult row is overwritten on recapture).
    public decimal? ResultValue { get; set; }

    public int? ResultRecordedByUserId { get; set; }

    public DateTime? ResultRecordedOn { get; set; }

    public virtual TestResult TestResult { get; set; } = null!;

    public virtual TechnicianProfile VerifiedByUser { get; set; } = null!;
}
