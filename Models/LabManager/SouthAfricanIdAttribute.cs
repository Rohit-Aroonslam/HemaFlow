using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Project.Models.LabManager
{
    /*
     SA ID number: YYMMDD SSSS C A Z  (13 digits)
       YYMMDD  date of birth - must be a REAL calendar date
               (the century is not stored, so it is valid if the
                date exists in either 19YY or 20YY - e.g. 29 Feb)
       SSSS    gender sequence
       C       citizenship: 0 = SA citizen, 1 = permanent resident
       A       (usually 8)
       Z       Luhn check digit
    */
    public class SouthAfricanIdAttribute : ValidationAttribute
    {
        public SouthAfricanIdAttribute()
        {
            ErrorMessage = "Not a valid SA ID number.";
        }

        protected override ValidationResult IsValid(object? value, ValidationContext context)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                return new ValidationResult("SA ID number is required.");

            string id = value.ToString()!.Trim();

            if (!Regex.IsMatch(id, @"^\d{13}$"))
                return new ValidationResult("Not a valid SA ID number.");

            if (!HasValidBirthDate(id))
                return new ValidationResult("Not a valid SA ID number.");

            char citizenDigit = id[10];
            if (citizenDigit != '0' && citizenDigit != '1')
                return new ValidationResult("Not a valid SA ID number.");

            if (!PassesLuhn(id))
                return new ValidationResult("Not a valid SA ID number.");

            return ValidationResult.Success;
        }

        // YYMMDD must be a real date in 19YY or 20YY (rejects e.g. 31 Feb, 31 Apr)
        private static bool HasValidBirthDate(string id)
        {
            string yymmdd = id.Substring(0, 6);
            return DateTime.TryParseExact("19" + yymmdd, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                || DateTime.TryParseExact("20" + yymmdd, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        }

        private static bool PassesLuhn(string id)
        {
            int total = 0;
            bool doubleDigit = false;

            for (int i = id.Length - 1; i >= 0; i--)
            {
                int digit = id[i] - '0';

                if (doubleDigit)
                {
                    digit *= 2;
                    if (digit > 9)
                        digit -= 9;
                }

                total += digit;
                doubleDigit = !doubleDigit;
            }

            return total % 10 == 0;
        }

    }//end of class SouthAfricanIdAttribute
}//end of namespace
