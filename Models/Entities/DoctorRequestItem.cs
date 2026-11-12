using System;
using System.Collections.Generic;

namespace Project.Models.Entities
{
    // NOT a database table - one row per test REQUEST as shown on the doctor screens
    // (dashboard, Track Requests, Request Details, Patient Profile). Filled from TestRequest + its items.
    public class DoctorRequestItem
    {
        public int TestRequestId { get; set; }
        public int PatientUserId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string PatientEmail { get; set; } = string.Empty;
        public string PatientIdNumber { get; set; } = string.Empty;

        // Tests / categories on the request, joined for list screens ("FBC, Lipid Panel").
        public string TestName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public int ItemCount { get; set; }

        public DateTime OrderDate { get; set; }                 // South African time
        public string Urgency { get; set; } = "Routine";
        public string Status { get; set; } = "Submitted";       // label shown to the doctor
        public string StatusKey { get; set; } = "submitted";    // lower-case key used by the filter buttons

        public int ResultCount { get; set; }
        public int AbnormalCount { get; set; }
        public DateTime? ReleasedOn { get; set; }
        public string? ClinicalNotes { get; set; }
        public string? DoctorNote { get; set; }
        public string? CancelReason { get; set; }

        public bool HasResults => ResultCount > 0;
        public bool CanCancel { get; set; }                     // Submitted / Samples Received only
        public bool CanRelease { get; set; }                    // Completed, not released yet

        public List<DoctorRequestTestLine> Tests { get; set; } = new();
        public List<DoctorRequestSampleLine> Samples { get; set; } = new();
    }

    public class DoctorRequestTestLine
    {
        public string TestName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string SampleType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    public class DoctorRequestSampleLine
    {
        public string Barcode { get; set; } = string.Empty;
        public string SampleType { get; set; } = string.Empty;
        public string TestName { get; set; } = string.Empty;
        public DateTime? ReceivedOn { get; set; }               // null = not yet received by the lab
    }
}
