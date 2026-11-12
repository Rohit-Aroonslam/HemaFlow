using System;
using System.Collections.Generic;

namespace Project.Models.Doctor
{
    // Real two-factor / registered-device state for the doctor's Profile page (read from the database).
    public class DoctorSecurityViewModel
    {
        public bool TwoFactorEnabled { get; set; }
        public DateTime? TwoFactorLastVerified { get; set; }
        public int BackupCodesLeft { get; set; }
        public List<string> NewBackupCodes { get; set; } = new();
    }

}
