using Project.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Models.Entities;
using Project.Models.LabManager;
using DbUser = Project.Models.Entities.User;
using VmActivityLog = Project.Models.LabManager.ActivityLog;

/*
 =========================================================
 ProfileController - Lab Manager "My Profile"
 ✅ DB: the logged-in Lab Manager's own dbo.Users row in GRP-04-13
        (until login is enforced: falls back to LM-004).
 
   View / edit:      FirstName, Surname, Email (username, unique), Phone
   Change password:  verified + re-hashed with ASP.NET PasswordHasher -
                     exactly how AccountController.Login checks it, so
                     the new password works at the next login.
                     Spec rule: 8+ chars, upper, number, special
                     (ChangePasswordViewModel regex). Clears
                     MustChangePassword.
   Recent activity:  this manager's own StaffActivityLog entries.
 =========================================================
*/
[Authorize(Roles = "LabManager")]
public class ProfileController : Controller
{
    private readonly ApplicationDbContext _context;
    private LabManagerActivityLog Activity => new(_context, User);   // → dbo.StaffActivityLog

    public ProfileController(ApplicationDbContext context)
    {
        _context = context;
    }

    private async Task<DbUser?> CurrentManager()
    {
        var id = await LabManagerActivityLog.CurrentLabManagerIdAsync(_context, User);
        return id == null ? null : await _context.Users.FirstOrDefaultAsync(u => u.UserId == id);
    }

    private static ManagerProfile ToViewModel(DbUser u) => new ManagerProfile
    {
        Name = u.FirstName,
        Surname = u.Surname,
        Email = u.Email,
        ContactNumber = u.Phone ?? "",
        Password = ""
    };

    // ── View ────────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var user = await CurrentManager();
        if (user == null) return NotFound();

        var rows = await _context.StaffActivityLogs.AsNoTracking()
            .Where(l => l.ActorId == user.UserId)
            .OrderByDescending(l => l.Timestamp)
            .Take(8)
            .Select(l => new { l.LogId, l.Timestamp, l.Action, l.Detail })
            .ToListAsync();

        ViewBag.RecentActivity = rows.Select(r =>
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
                PerformedBy = $"{user.FirstName} {user.Surname}",
                Notes = notes
            };
        }).ToList();

        return View(ToViewModel(user));
    }

    // ── Edit profile ────────────────────────────────────
    public async Task<IActionResult> Edit()
    {
        var user = await CurrentManager();
        if (user == null) return NotFound();
        return View(ToViewModel(user));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ManagerProfile model)
    {
        var user = await CurrentManager();
        if (user == null) return NotFound();

        ModelState.Remove(nameof(ManagerProfile.Password));   // not on this form

        model.Name = model.Name?.Trim() ?? "";
        model.Surname = model.Surname?.Trim() ?? "";
        model.Email = model.Email?.Trim().ToLowerInvariant() ?? "";
        model.ContactNumber = model.ContactNumber?.Trim() ?? "";

        // E-mail is the username - must be unique across ALL users
        if (await _context.Users.AnyAsync(u => u.Email == model.Email && u.UserId != user.UserId))
            ModelState.AddModelError("Email", "This e-mail address is already used by another account.");

        if (!ModelState.IsValid) return View(model);

        bool emailChanged = !string.Equals(user.Email, model.Email, StringComparison.OrdinalIgnoreCase);

        user.FirstName = model.Name;
        user.Surname = model.Surname;
        user.Email = model.Email;
        user.Phone = model.ContactNumber;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("Email", "This e-mail address is already used by another account.");
            return View(model);
        }

        await Activity.LogAsync("Updated", "Profile", $"{model.Name} {model.Surname}",
            emailChanged ? "Profile details updated (username changed)" : "Profile details updated");

        TempData["SuccessMessage"] = emailChanged
            ? "Profile updated. Use your new e-mail address to log in next time."
            : "Profile updated successfully.";
        return RedirectToAction("Index");
    }

    // ── Change password ─────────────────────────────────
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await CurrentManager();
        if (user == null) return NotFound();

        var hasher = new PasswordHasher<DbUser>();
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, model.CurrentPassword) == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
            return View(model);
        }

        if (model.NewPassword == model.CurrentPassword)
        {
            ModelState.AddModelError("NewPassword", "New password must be different from your current password.");
            return View(model);
        }

        user.PasswordHash = hasher.HashPassword(user, model.NewPassword);
        user.MustChangePassword = false;
        await _context.SaveChangesAsync();

        await Activity.LogAsync("Updated", "Profile", $"{user.FirstName} {user.Surname}", "Password changed");

        TempData["SuccessMessage"] = "Password changed successfully.";
        return RedirectToAction("Index");
    }

}//end of class ProfileController
