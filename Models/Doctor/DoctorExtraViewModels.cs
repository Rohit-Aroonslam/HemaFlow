using System;
using System.Collections.Generic;

namespace Project.Models.Doctor
{
    // One abnormal result shown as a card on the Alerts page.
    public class DoctorAlertItem
    {
        public int TestRequestId { get; set; }
        public string Test { get; set; } = string.Empty;
        public string Patient { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Req { get; set; } = string.Empty;          // "#REQ-005"
        public string Reported { get; set; } = string.Empty;     // "2026-05-14 08:20" (SAST)
        public string Result { get; set; } = string.Empty;       // "5.2 g/dL"
        public string Range { get; set; } = string.Empty;        // "12.0 - 16.0 g/dL"
        public string Urgency { get; set; } = "Routine";
        public string UrgClass { get; set; } = "bg-secondary";
    }

    public class DoctorAddPatientModel
    {
        public string FirstName { get; set; } = "";
        public string Surname { get; set; } = "";
        public string SaIdNumber { get; set; } = "";
        public DateTime? DateOfBirth { get; set; }
        public string Phone { get; set; } = "";
        public string Email { get; set; } = "";
        public string Complex { get; set; } = "";   // optional: unit / complex / building
        public string Street { get; set; } = "";
        public int? SuburbId { get; set; }
        public int? ProvinceId { get; set; }
        public string Suburb { get; set; } = "";
        public string City { get; set; } = "";
        public string PostalCode { get; set; } = "";
    }
}
