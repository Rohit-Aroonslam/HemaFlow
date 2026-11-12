using Project.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Project.Models.Entities;

namespace Project.Models.LabManager
{
    /*
     =========================================================
     LabManagerActivityLog - replaces TempDataStore.Log() for the
     Lab Manager subsystem. Writes to the team's shared
     dbo.StaffActivityLog table in GRP-04-13.

     How a Lab Manager entry is stored (readable in the Admin
     subsystem's log view too):
       ActorId    = the Lab Manager's UserId
       ActorRole  = RoleId of 'LabManager'
       Action     = "<Action> <EntityType>"   e.g. "Created TestCategory"
       Detail     = "<EntityName>" or "<EntityName> - <Notes>"
       LogTypeId  = info / success / warning / danger (team's LogType codes)
       Timestamp  = DB default sysutcdatetime()

     Usage in a controller (no Program.cs registration needed):
         private LabManagerActivityLog Activity => new(_context, User);
         await Activity.LogAsync("Created", "TestCategory", model.Name);

     Logging never breaks the action: errors are swallowed.

     🟢 TEMP: until Lab Manager login is enforced, entries made
              without a logged-in Lab Manager are recorded against LM-004.
     =========================================================
    */
    public class LabManagerActivityLog
    {
        public const string Separator = " - ";
        private const string FallbackManagerEmail = "m.vonbuchenroder@hemaflow.co.za";

        private readonly ApplicationDbContext _context;
        private readonly ClaimsPrincipal _user;

        public LabManagerActivityLog(ApplicationDbContext context, ClaimsPrincipal user)
        {
            _context = context;
            _user = user;
        }

        // Logged-in Lab Manager's UserId (fallback LM-004 until [Authorize] is on)
        public static async Task<int?> CurrentLabManagerIdAsync(ApplicationDbContext context, ClaimsPrincipal user)
        {
            if (int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) &&
                await context.LabManagerProfiles.AnyAsync(p => p.UserId == uid && !p.IsArchived))
                return uid;

            return await context.LabManagerProfiles
                .Where(p => p.User.Email == FallbackManagerEmail)
                .Select(p => (int?)p.UserId)
                .FirstOrDefaultAsync();
        }

        public async Task LogAsync(string action, string entityType, string entityName, string notes = "")
        {
            try
            {
                var actorId = await CurrentLabManagerIdAsync(_context, _user);
                if (actorId == null) return;

                var roleId = await _context.Roles
                    .Where(r => r.Code == "LabManager").Select(r => (byte?)r.RoleId).FirstOrDefaultAsync();
                var logTypeCode = LogTypeFor(action);
                var logTypeId = await _context.LogTypes
                    .Where(l => l.Code == logTypeCode).Select(l => (byte?)l.LogTypeId).FirstOrDefaultAsync()
                    ?? await _context.LogTypes.Where(l => l.Code == "info").Select(l => (byte?)l.LogTypeId).FirstOrDefaultAsync();
                if (roleId == null || logTypeId == null) return;

                var detail = string.IsNullOrWhiteSpace(notes) ? entityName : entityName + Separator + notes;

                _context.StaffActivityLogs.Add(new StaffActivityLog
                {
                    ActorId = actorId.Value,
                    ActorRole = roleId.Value,
                    Action = Truncate($"{action} {entityType}", 150),
                    Detail = Truncate(detail, 500),
                    LogTypeId = logTypeId.Value
                });
                await _context.SaveChangesAsync();
            }
            catch
            {
                // Never let a logging failure undo or block the user's action
            }
        }

        private static string LogTypeFor(string action) => action switch
        {
            "Created" or "Restored" or "OrderPlaced" or "ItemReceived" or "OrderCompleted" => "success",
            "Archived" or "OrderCancelled" or "ItemCancelled" => "danger",
            "StockAdjusted" => "warning",
            _ => "info"
        };

        private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

        // ── Reading entries back for the Activity Log / Profile pages ──
        // "Created TestCategory" -> ("Created", "TestCategory")
        public static (string Action, string EntityType) SplitAction(string stored)
        {
            var i = stored.IndexOf(' ');
            return i < 0 ? (stored, "") : (stored[..i], stored[(i + 1)..]);
        }

        // "Full Blood Count - note" -> ("Full Blood Count", "note")
        public static (string EntityName, string Notes) SplitDetail(string? stored)
        {
            if (string.IsNullOrEmpty(stored)) return ("", "");
            var i = stored.IndexOf(Separator, StringComparison.Ordinal);
            return i < 0 ? (stored, "") : (stored[..i], stored[(i + Separator.Length)..]);
        }
    }
}