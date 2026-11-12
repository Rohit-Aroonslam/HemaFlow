namespace Project.Models.LabManager
{
    // Dashboard filters (spec: urgency, category, due time, request number).
    public class LabTechFilter
    {
        public string? Urgency { get; set; }   // "STAT" | "Urgent" | "Routine"
        public int? Category { get; set; }     // TestCategory.Id
        public string? Due { get; set; }       // "overdue" | "nearing" | "ontime"
        public string? Req { get; set; }       // request number (partial match)

        public bool Any => !string.IsNullOrEmpty(Urgency) || Category.HasValue
                           || !string.IsNullOrEmpty(Due) || !string.IsNullOrWhiteSpace(Req);

        public IEnumerable<QueueItem> Apply(IEnumerable<QueueItem> items)
        {
            if (!string.IsNullOrEmpty(Urgency)) items = items.Where(i => i.Urgency == Urgency);
            if (Category.HasValue) items = items.Where(i => i.CategoryId == Category.Value);
            if (Due == "overdue") items = items.Where(i => i.IsOverdue);
            else if (Due == "nearing") items = items.Where(i => i.IsNearingTat);
            else if (Due == "ontime") items = items.Where(i => !i.IsOverdue && !i.IsNearingTat);
            if (!string.IsNullOrWhiteSpace(Req))
            {
                var r = Req.Trim().TrimStart('#');
                items = items.Where(i => i.RequestNumber.TrimStart('#').Contains(r, StringComparison.OrdinalIgnoreCase));
            }
            return items;
        }
    }

    // One dashboard list (bento card + its modal).
    public class LabTechQueueList
    {
        public string Key { get; set; } = "";        // modal id suffix
        public string Title { get; set; } = "";
        public string Subtitle { get; set; } = "";
        public string Icon { get; set; } = "";
        public string Empty { get; set; } = "";
        public List<QueueItem> Items { get; set; } = new();

        // Per-category colour identity, reusing the same soft-pastel-bg +
        // saturated-text palette the badge-* classes already use in site.css
        // (badge-stat/urgent/routine/inprogress/samples) so new colour here
        // is never arbitrary - each tone either IS an existing badge colour
        // or sits in the same family. Used for the dashboard's stat tiles
        // and bento card headers (icon chip + top accent).
        public (string Bg, string Fg) Tone => Key switch
        {
            "stat"      => ("#fee2e2", "#991b1b"),   // red   - badge-stat
            "overdue"   => ("#fef2f2", "#dc2626"),   // red   - danger accent
            "nearing"   => ("#fffbeb", "#b45309"),   // amber - warning accent
            "mine"      => ("#dbeafe", "#1e40af"),   // blue  - badge-samples
            "available" => ("#e2e8f0", "#334155"),   // slate - badge-routine
            "verify"    => ("#ede9fe", "#5b21b6"),   // violet - awaiting someone else
            "review"    => ("#fff7ed", "#ea580c"),   // orange - badge-urgent
            _           => ("#f1f5f9", "#475569")    // slate fallback
        };
    }

    // Everything the dashboard modal partial needs.
    public class LabTechQueueModal
    {
        public LabTechQueueList List { get; set; } = new();
        public LabTechFilter Filter { get; set; } = new();
        public List<TestCategory> Categories { get; set; } = new();
        public Dictionary<int, string> StockStatus { get; set; } = new();
        public HashSet<int> VerifiableIds { get; set; } = new();
        public string CurrentTechnician { get; set; } = "";
    }

    // Posted from ReceiveSample - one entry per physical sample.
    // Action "Receive" accepts the sample (Barcode is used/validated); "Reject" rejects it
    // with RejectReason instead - the two other samples on the same request can still be
    // accepted independently.
    public class ReceivedSampleInput
    {
        public string OriginalBarcode { get; set; } = "";
        public string Barcode { get; set; } = "";
        public string Action { get; set; } = "Receive";
        public string? RejectReason { get; set; }
    }

    // Real DB-backed profile (Users/TechnicianProfile/UserTwoFactor/UserBackupCode),
    // same shape as the admin profile model.
    public class LabTechProfileViewModel
    {
        public string FirstName { get; set; } = "";
        public string Surname { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string EmployeeCode { get; set; } = "";
        public string SaIdNumber { get; set; } = "";
        public List<string> AssignedTestTypes { get; set; } = new();

        public bool TwoFactorEnabled { get; set; }
        public DateTime? TwoFactorLastVerified { get; set; }
        public int BackupCodesLeft { get; set; }
        public List<string> NewBackupCodes { get; set; } = new();
    }

    // Report's "grouped by category" breakdown (spec: Technician Reports).
    // A named type, not an anonymous one - anonymous types are internal to
    // their declaring assembly, so dynamic ViewBag access to them throws a
    // RuntimeBinderException from a Razor Runtime Compilation view (a
    // separate assembly) even though it compiles fine with var.
    public class CategoryGroupRow
    {
        public string Category { get; set; } = "";
        public int Count { get; set; }
        public int Abnormal { get; set; }
    }
}
