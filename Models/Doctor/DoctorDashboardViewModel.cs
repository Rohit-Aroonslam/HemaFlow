using System.Collections.Generic;
using Project.Models.Entities;

namespace Project.Models.Doctor
{
    public class DoctorDashboardViewModel
    {
        // Summary Cards
        public string DoctorSurname { get; set; } = string.Empty;
        public int SubmittedCount { get; set; }
        public int InProgressCount { get; set; }
        public int AwaitingReleaseCount { get; set; }
        public int AlertCount { get; set; }
        public List<DoctorRequestItem> RecentRequests { get; set; } = new();

        public int TotalPatientsCount { get; set; }
        public int PendingTestRequestsCount { get; set; }
        public int AbnormalResultsCount { get; set; }
        public int CompletedResultsCount { get; set; }

        // Data Lists for Dashboard Widgets
        public List<RecentPatientViewModel> RecentPatients { get; set; } = new();
        public List<DashboardAlertViewModel> CriticalAlerts { get; set; } = new();
    }

    public class RecentPatientViewModel
    {
        public int PatientId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string SaIdNumber { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public bool HasConsent { get; set; }
    }

    public class DashboardAlertViewModel
    {
        public int TestRequestId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string TestName { get; set; } = string.Empty;
        public string ResultValue { get; set; } = string.Empty;
        public string Severity { get; set; } = "High"; // High / Critical
        public System.DateTime FlaggedDate { get; set; }
    }
}