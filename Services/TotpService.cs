using System.Security.Cryptography;
using System.Text;

namespace Project.Services
{
    // Hand-rolled RFC 6238 TOTP (Time-based One-Time Password) implementation -
    // the same algorithm Google Authenticator / Microsoft Authenticator use.
    // Deliberately built on nothing but built-in .NET crypto (HMACSHA1) so it
    // doesn't add a new NuGet dependency that could break the build.
    //
    // Usage:
    //   var secret = TotpService.GenerateSecretKey();                 // store this (Base32) per user
    //   var uri     = TotpService.BuildOtpAuthUri("HemaFlow", email, secret); // feed to a QR code
    //   bool ok    = TotpService.ValidateCode(secret, userEnteredCode);       // check a submitted code
    public static class TotpService
    {
        private const int SecretByteLength = 20; // 160-bit secret, standard for TOTP
        private const int Digits = 6;
        private const int PeriodSeconds = 30;

        // ── Secret generation ──────────────────────────────────────────────
        public static string GenerateSecretKey()
        {
            var bytes = RandomNumberGenerator.GetBytes(SecretByteLength);
            return Base32Encode(bytes);
        }

        // ── otpauth:// URI for the QR code (rendered client-side by a JS QR library) ──
        public static string BuildOtpAuthUri(string issuer, string accountEmail, string base32Secret)
        {
            var label = Uri.EscapeDataString($"{issuer}:{accountEmail}");
            var issuerEsc = Uri.EscapeDataString(issuer);
            return $"otpauth://totp/{label}?secret={base32Secret}&issuer={issuerEsc}&digits={Digits}&period={PeriodSeconds}";
        }

        // ── Code generation / validation ────────────────────────────────────
        public static string GetCurrentCode(string base32Secret, DateTime? asOfUtc = null)
        {
            var counter = GetCounter(asOfUtc ?? DateTime.UtcNow);
            return ComputeTotp(Base32Decode(base32Secret), counter);
        }

        // Allows the code from one step before/after the current one, since the
        // user's clock and the server's clock are never perfectly in sync.
        public static bool ValidateCode(string base32Secret, string? code, int allowedDriftSteps = 1)
        {
            if (string.IsNullOrWhiteSpace(code)) return false;
            code = code.Trim();
            if (code.Length != Digits || !code.All(char.IsDigit)) return false;

            var keyBytes = Base32Decode(base32Secret);
            var currentCounter = GetCounter(DateTime.UtcNow);

            for (var drift = -allowedDriftSteps; drift <= allowedDriftSteps; drift++)
            {
                var candidate = ComputeTotp(keyBytes, currentCounter + drift);
                if (candidate == code) return true;
            }
            return false;
        }

        private static long GetCounter(DateTime utcTime)
        {
            var unixSeconds = new DateTimeOffset(utcTime).ToUnixTimeSeconds();
            return unixSeconds / PeriodSeconds;
        }

        private static string ComputeTotp(byte[] key, long counter)
        {
            var counterBytes = BitConverter.GetBytes(counter);
            if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes); // RFC requires big-endian

            using var hmac = new HMACSHA1(key);
            var hash = hmac.ComputeHash(counterBytes);

            // Dynamic truncation (RFC 4226 section 5.3)
            var offset = hash[^1] & 0x0F;
            var binaryCode = ((hash[offset] & 0x7F) << 24)
                            | ((hash[offset + 1] & 0xFF) << 16)
                            | ((hash[offset + 2] & 0xFF) << 8)
                            | (hash[offset + 3] & 0xFF);

            var otp = binaryCode % (int)Math.Pow(10, Digits);
            return otp.ToString(new string('0', Digits));
        }

        // ── Backup codes ─────────────────────────────────────────────────────
        // Plaintext codes are shown to the user exactly once; only the hash is stored.
        public static List<string> GenerateBackupCodes(int count = 6)
        {
            var codes = new List<string>();
            for (var i = 0; i < count; i++)
            {
                var part1 = RandomNumberGenerator.GetInt32(0, 36 * 36 * 36 * 36);
                var part2 = RandomNumberGenerator.GetInt32(0, 36 * 36 * 36 * 36);
                codes.Add($"{ToBase36(part1, 4)}-{ToBase36(part2, 4)}");
            }
            return codes;
        }

        public static string HashBackupCode(string code)
        {
            var bytes = Encoding.UTF8.GetBytes(code.Trim().ToUpperInvariant());
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
        }

        private static string ToBase36(int value, int width)
        {
            const string chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            var sb = new StringBuilder();
            for (var i = 0; i < width; i++)
            {
                sb.Insert(0, chars[value % 36]);
                value /= 36;
            }
            return sb.ToString();
        }

        // ── Base32 (RFC 4648, no padding) - needed because TOTP secrets/URIs use it ──
        private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

        private static string Base32Encode(byte[] data)
        {
            var sb = new StringBuilder();
            int bitBuffer = 0, bitsInBuffer = 0;
            foreach (var b in data)
            {
                bitBuffer = (bitBuffer << 8) | b;
                bitsInBuffer += 8;
                while (bitsInBuffer >= 5)
                {
                    bitsInBuffer -= 5;
                    sb.Append(Base32Alphabet[(bitBuffer >> bitsInBuffer) & 0x1F]);
                }
            }
            if (bitsInBuffer > 0)
                sb.Append(Base32Alphabet[(bitBuffer << (5 - bitsInBuffer)) & 0x1F]);
            return sb.ToString();
        }

        private static byte[] Base32Decode(string base32)
        {
            base32 = base32.Trim().TrimEnd('=').ToUpperInvariant();
            var bytes = new List<byte>();
            int bitBuffer = 0, bitsInBuffer = 0;
            foreach (var c in base32)
            {
                var index = Base32Alphabet.IndexOf(c);
                if (index < 0) continue; // skip any stray formatting characters
                bitBuffer = (bitBuffer << 5) | index;
                bitsInBuffer += 5;
                if (bitsInBuffer >= 8)
                {
                    bitsInBuffer -= 8;
                    bytes.Add((byte)((bitBuffer >> bitsInBuffer) & 0xFF));
                }
            }
            return bytes.ToArray();
        }
    }
}