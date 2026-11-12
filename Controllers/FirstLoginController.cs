using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Data;
using DbUser = Project.Models.Entities.User;

/*
 =========================================================
 FirstLoginController - forced password change on first login.
 Users created with Users.MustChangePassword = true (e.g. doctors
 and technicians created by the Lab Manager) are sent here by the
 check in Program.cs until they choose their own password.
 Rule (same as the rest of the system): 8+ characters, one
 uppercase letter, one number, one special character.
 =========================================================
*/
[Authorize]
public class FirstLoginController : Controller
{
    private readonly ApplicationDbContext _context;

    public FirstLoginController(ApplicationDbContext context)
    {
        _context = context;
    }

    private async Task<DbUser?> CurrentUserAsync()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return null;
        return await _context.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == id);
    }

    private IActionResult HomeFor(string? role) => role switch
    {
        "SuperAdmin" => RedirectToAction("Index", "Admin"),
        "Admin" => RedirectToAction("Index", "Admin"),
        "Doctor" => RedirectToAction("Index", "Doctor"),
        "LabManager" => RedirectToAction("Index", "Dashboard"),
        "Technician" => RedirectToAction("Index", "LabTech"),
        "Patient" => RedirectToAction("Index", "Patient"),
        _ => RedirectToAction("Login", "Account")
    };

    private static bool MeetsRules(string pw) =>
        pw.Length >= 8 &&
        pw.Any(char.IsUpper) &&
        pw.Any(char.IsDigit) &&
        pw.Any(c => !char.IsLetterOrDigit(c));

    // GET: /FirstLogin/ChangePassword
    [HttpGet]
    public async Task<IActionResult> ChangePassword()
    {
        var user = await CurrentUserAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        // Nothing to do if they have already changed it
        if (!user.MustChangePassword) return HomeFor(user.Role.Code);

        return View();
    }

    // POST: /FirstLogin/ChangePassword
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
    {
        var user = await CurrentUserAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        currentPassword ??= "";
        newPassword ??= "";
        confirmPassword ??= "";

        var hasher = new PasswordHasher<DbUser>();
        if (hasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
        {
            TempData["Error"] = "Your current (temporary) password is incorrect.";
            return RedirectToAction("ChangePassword");
        }

        if (newPassword != confirmPassword)
        {
            TempData["Error"] = "The new password and confirmation do not match.";
            return RedirectToAction("ChangePassword");
        }

        if (!MeetsRules(newPassword))
        {
            TempData["Error"] = "The new password must be at least 8 characters and include an uppercase letter, a number and a special character.";
            return RedirectToAction("ChangePassword");
        }

        if (newPassword == currentPassword)
        {
            TempData["Error"] = "Please choose a password that is different from your temporary one.";
            return RedirectToAction("ChangePassword");
        }

        user.PasswordHash = hasher.HashPassword(user, newPassword);
        user.MustChangePassword = false;
        await _context.SaveChangesAsync();
        HttpContext.RequestServices.GetService<Microsoft.Extensions.Caching.Memory.IMemoryCache>()?.Remove("mustchange:" + user.UserId);

        TempData["Success"] = "Your password has been changed. Welcome to HemaFlow.";
        return HomeFor(user.Role.Code);
    }
}
