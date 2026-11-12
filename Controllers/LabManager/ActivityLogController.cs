using Project.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Models.Entities;
using Project.Models.LabManager;
using VmActivityLog = Project.Models.LabManager.ActivityLog;

/*
 =========================================================
 ActivityLogController - Lab Manager
 ✅ DB: reads dbo.StaffActivityLog in GRP-04-13, Lab Manager
        entries only (ActorRole = 'LabManager').
        Written by Models/LabManager/LabManagerActivityLog.
 
 View unchanged: still uses Models/LabManager/ActivityLog.
 Stored as UTC; shown in local time.
 =========================================================
*/
[Authorize(Roles = "LabManager")]
public class ActivityLogController : Controller
{
    private readonly ApplicationDbContext _context;

    public ActivityLogController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string actionFilter = "All", string entityFilter = "All",
                                           string dateFrom = "", string dateTo = "")
    {
        var lmLogs = _context.StaffActivityLogs.AsNoTracking()
            .Where(l => l.ActorRoleNavigation.Code == "LabManager");

        var query = lmLogs;

        // Action is stored as "<Action> <EntityType>"
        if (!string.IsNullOrEmpty(actionFilter) && actionFilter != "All")
            query = query.Where(l => l.Action.StartsWith(actionFilter + " "));

        if (!string.IsNullOrEmpty(entityFilter) && entityFilter != "All")
            query = query.Where(l => l.Action.EndsWith(" " + entityFilter));

        // Date pickers are local dates; the table stores UTC
        if (DateTime.TryParse(dateFrom, out var from))
        {
            var fromUtc = DateTime.SpecifyKind(from.Date, DateTimeKind.Local).ToUniversalTime();
            query = query.Where(l => l.Timestamp >= fromUtc);
        }
        if (DateTime.TryParse(dateTo, out var to))
        {
            var toUtc = DateTime.SpecifyKind(to.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
            query = query.Where(l => l.Timestamp < toUtc);
        }

        var rows = await query
            .OrderByDescending(l => l.Timestamp)
            .Take(500)
            .Select(l => new
            {
                l.LogId,
                l.Timestamp,
                l.Action,
                l.Detail,
                ActorName = l.Actor.FirstName + " " + l.Actor.Surname
            })
            .ToListAsync();

        var data = rows.Select(r =>
        {
            var (action, entityType) = LabManagerActivityLog.SplitAction(r.Action);
            var (entityName, notes) = LabManagerActivityLog.SplitDetail(r.Detail);
            return new VmActivityLog
            {
                Id = r.LogId,
                Timestamp = DateTime.SpecifyKind(r.Timestamp, DateTimeKind.Utc).ToLocalTime(),
                Action = action,
                EntityType = entityType,
                EntityName = entityName,
                PerformedBy = r.ActorName,
                Notes = notes
            };
        }).ToList();

        ViewBag.ActionFilter = actionFilter;
        ViewBag.EntityFilter = entityFilter;
        ViewBag.DateFrom = dateFrom;
        ViewBag.DateTo = dateTo;
        ViewBag.TotalEntries = await lmLogs.CountAsync();

        return View(data);
    }

}//end of class ActivityLogController
