using System.Text.RegularExpressions;

namespace Project.Services
{
    // One password rule for the whole system (specification: 8+ characters, an uppercase
    // letter, a number and a special character).
    public static class PasswordPolicy
    {
        public const string Message =
            "The password must be at least 8 characters and include an uppercase letter, a number and a special character.";

        public static bool IsValid(string? pw) =>
            !string.IsNullOrEmpty(pw) && pw.Length >= 8
            && Regex.IsMatch(pw, "[A-Z]") && Regex.IsMatch(pw, "[0-9]") && Regex.IsMatch(pw, "[^A-Za-z0-9]");

        // Random temporary password that always satisfies IsValid (used when staff create accounts for patients).
        public static string GenerateTemporary()
        {
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ", lower = "abcdefghijkmnpqrstuvwxyz",
                         digits = "23456789", special = "!@#$%&*?";
            var chars = new List<char>
            {
                Pick(upper), Pick(upper), Pick(lower), Pick(lower), Pick(lower), Pick(lower),
                Pick(digits), Pick(digits), Pick(special), Pick(special)
            };
            // shuffle
            for (int i = chars.Count - 1; i > 0; i--)
            {
                int j = System.Security.Cryptography.RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }
            return new string(chars.ToArray());
        }

        private static char Pick(string set) => set[System.Security.Cryptography.RandomNumberGenerator.GetInt32(set.Length)];
    }
}
