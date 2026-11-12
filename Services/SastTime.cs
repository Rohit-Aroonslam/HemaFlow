namespace Project.Services
{
    // Everything is stored in the database as UTC; people in South Africa see it as SAST (UTC+2, no daylight saving).
    public static class SastTime
    {
        public static readonly TimeSpan Offset = TimeSpan.FromHours(2);

        public static DateTime ToSast(DateTime stored) => DateTime.SpecifyKind(stored, DateTimeKind.Unspecified) + Offset;

        // A date the person typed (in SAST) -> the UTC moment it starts at.
        public static DateTime SastDateToUtc(DateTime sastDate) => sastDate.Date - Offset;
    }
}
