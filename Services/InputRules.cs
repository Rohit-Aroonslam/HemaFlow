using System.Text.RegularExpressions;

namespace Project.Services
{
    // One place for the small input checks shared by Admin, Patient and Doctor screens.
    public static class InputRules
    {
        private static readonly Regex EmailRx = new(@"^[A-Za-z0-9._%+\-]+@[A-Za-z0-9\-]+(\.[A-Za-z0-9\-]+)*\.[A-Za-z]{2,}$", RegexOptions.Compiled);
        private static readonly Regex PhoneRx = new(@"^[0-9+ ()\-]{10,15}$", RegexOptions.Compiled);
        private static readonly Regex NameRx = new(@"^[\p{L}][\p{L}\s'\-\.]{0,98}$", RegexOptions.Compiled);

        public const string EmailMessage = "Enter a valid email address, for example name@example.com.";
        public const string PhoneMessage = "Enter a valid cellphone number (10 to 15 digits; spaces, + ( ) - allowed).";
        public const string NameMessage = "Names may only contain letters, spaces, apostrophes, hyphens and full stops.";

        public static bool IsValidEmail(string? s) => !string.IsNullOrWhiteSpace(s) && s.Trim().Length <= 254 && EmailRx.IsMatch(s.Trim());
        public static bool IsValidPhone(string? s) => !string.IsNullOrWhiteSpace(s) && PhoneRx.IsMatch(s.Trim());
        public static bool IsValidName(string? s) => !string.IsNullOrWhiteSpace(s) && NameRx.IsMatch(s.Trim());
    }
}
