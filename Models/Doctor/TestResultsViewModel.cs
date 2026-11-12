using System;
using System.Collections.Generic;
using System.Linq;

namespace Project.Models.Doctor
{
    public class TestResultsViewModel
    {
        public int TestRequestId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string PatientSurname { get; set; } = string.Empty;
        public string PatientEmail { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? DoctorNote { get; set; }
        public DateTime? ReleasedOn { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? VerifiedBy { get; set; }
        public DateTime? VerifiedAt { get; set; }
        public bool CanRelease { get; set; }       // lab finished, doctor has not released yet
        public bool IsReleased { get; set; }

        public List<TestResultRow> Results { get; set; } = new List<TestResultRow>();
        public int AbnormalCount => Results.Count(r => r.IsAbnormal);
    }

    public class TestResultRow
    {
        public string TestTypeName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string ResultValue { get; set; } = string.Empty;
        public string Units { get; set; } = string.Empty;
        public string NormalRangeMin { get; set; } = string.Empty;
        public string NormalRangeMax { get; set; } = string.Empty;
        public bool IsAbnormal { get; set; }
        public string Direction { get; set; } = string.Empty;   // "High" / "Low" when abnormal
        public string? Notes { get; set; }
    }
}
