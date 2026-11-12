using System;
using System.Collections.Generic;

namespace Project.Models.Doctor
{
    public class CreateTestRequestViewModel
    {
        public int PatientId { get; set; }
        public DateTime RequestDate { get; set; } = DateTime.Now;
        public string Urgency { get; set; } = "Routine";
        public string? ClinicalNotes { get; set; }

        public List<int> TestTypeIds { get; set; } = new();
        public List<SampleBarcodeViewModel> Barcodes { get; set; } = new();

        public List<PatientItemViewModel> Patients { get; set; } = new();
        public List<TestCategoryViewModel> Categories { get; set; } = new();
    }

    public class PatientItemViewModel
    {
        public int PatientId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Surname { get; set; } = string.Empty;
        public string SaIdNumber { get; set; } = string.Empty;
        public string DateOfBirth { get; set; } = string.Empty;
        public string ContactNumber { get; set; } = string.Empty;
        public List<string> Allergies { get; set; } = new();
        public List<string> Conditions { get; set; } = new();
        public List<string> Medications { get; set; } = new();

        public bool HasGivenConsentForExternalResults { get; set; }
        public List<PatientTestHistoryViewModel> TestHistory { get; set; } = new();
    }

    public class PatientTestHistoryViewModel
    {
        public int TestRequestId { get; set; }
        public DateTime OrderDate { get; set; }
        public string TestName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string ResultValue { get; set; } = string.Empty;
        public string ReferenceRange { get; set; } = string.Empty;
        public string Status { get; set; } = "Completed";
        public bool IsAbnormal { get; set; }
        public string OrderedByDoctor { get; set; } = string.Empty;
        public bool IsOrderedByCurrentDoctor { get; set; }
    }

    public class TestCategoryViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public List<TestTypeOptionViewModel> TestTypes { get; set; } = new();
    }

    public class TestTypeOptionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SampleType { get; set; } = string.Empty;
        public string SampleColor { get; set; } = string.Empty;
    }

    public class SampleBarcodeViewModel
    {
        public string SampleType { get; set; } = string.Empty;
        public string BarcodeNumber { get; set; } = string.Empty;
    }
}