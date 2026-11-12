using System.Net;
using Project.Data;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Models.Entities;
using Project.Models.LabManager;
using Project.Services;
using DbUser = Project.Models.Entities.User;
using VmDoctor = Project.Models.LabManager.Doctor;

/*
 =========================================================
 LMDoctorController - Lab Manager, spec §6 (Manage Doctor Users)
 Separate from the Doctor subsystem's DoctorController.
 
 ✅ DB: ONE shared record per doctor in GRP-04-13
     dbo.Users          FirstName, Surname, Email (username), Phone,
                        PasswordHash, MustChangePassword, IsArchived,
                        UserCode (DR-0xx), Identifier ("HPCSA: MP…"),
                        RoleId = Doctor, Status
     dbo.DoctorProfile  UserId (same id), DoctorCode (= UserCode),
                        HpcsaNumber (unique), IsArchived
 
   The Doctor subsystem reads the SAME rows for the logged-in
   doctor (UserId from the login cookie) - no duplicate data.
 
 Spec §6 rules implemented:
   • HPCSA number unique (DoctorProfile UQ) - cannot change after creation
   • E-mail is the username - unique across ALL users (UQ_Users_Email)
   • Password auto-generated (8+ chars, upper, digit, special),
     HASHED with ASP.NET PasswordHasher (same as AccountController),
     e-mailed to the doctor
   • MustChangePassword = true on creation ("Pending First Login")
   • Archive = soft delete on Users + DoctorProfile; archived users
     cannot log in (AccountController already checks IsArchived)
 
 E-mail: ON by default - username, temporary password and a link
 to the login page are e-mailed to the new doctor. To switch off/redirect:
   "LabManager": {
     "SendAccountEmails": false,
     "AccountEmailRedirectTo": "you@example.com"   // optional
   }
   When e-mail is off or fails, the temporary password is shown in a
   panel on the Doctors page until dismissed, so the manager can pass it on.
 
 ✅ Activity is logged to dbo.StaffActivityLog (LabManagerActivityLog).
 =========================================================
*/
[Authorize(Roles = "LabManager")]
public class LMDoctorController : Controller
{
    private readonly ApplicationDbContext _context;
    private LabManagerActivityLog Activity => new(_context, User);
    private readonly EmailService _email;
    private readonly IConfiguration _config;
    private readonly ILogger<LMDoctorController> _logger;

    public LMDoctorController(ApplicationDbContext context, EmailService email,
                              IConfiguration config, ILogger<LMDoctorController> logger)
    {
        _context = context;
        _email = email;
        _config = config;
        _logger = logger;
    }

    // ── Helpers ─────────────────────────────────────────
    private async Task<byte> DoctorRoleId() =>
        await _context.Roles.Where(r => r.Code == "Doctor").Select(r => r.RoleId).SingleAsync();

    // Doctors = Doctor-role users that have a DoctorProfile
    private IQueryable<VmDoctor> DoctorQuery(bool archived) =>
        _context.DoctorProfiles.AsNoTracking()
            .Where(p => p.User.Role.Code == "Doctor"
                        && (archived ? (p.IsArchived || p.User.IsArchived)
                                     : (!p.IsArchived && !p.User.IsArchived)))
            .OrderBy(p => p.User.Surname).ThenBy(p => p.User.FirstName)
            .Select(p => new VmDoctor
            {
                Id = p.UserId,
                Name = p.User.FirstName,
                Surname = p.User.Surname,
                HPCSANumber = p.HpcsaNumber,
                Email = p.User.Email,
                ContactNumber = p.User.Phone ?? "",
                Password = "",
                MustChangePassword = p.User.MustChangePassword,
                IsArchived = p.IsArchived || p.User.IsArchived
            });

    // Forms never post Password (and Edit never posts the disabled HPCSA field)
    private void IgnoreUnpostedFields(bool editing)
    {
        ModelState.Remove(nameof(VmDoctor.Password));
        if (editing) ModelState.Remove(nameof(VmDoctor.HPCSANumber));
    }

    private static void Normalise(VmDoctor m)
    {
        m.Name = m.Name?.Trim() ?? "";
        m.Surname = m.Surname?.Trim() ?? "";
        m.Email = m.Email?.Trim().ToLowerInvariant() ?? "";
        m.ContactNumber = m.ContactNumber?.Trim() ?? "";
        m.HPCSANumber = m.HPCSANumber?.Trim().ToUpperInvariant() ?? "";
    }

    // Next DR-0xx code, following the team's existing DR-001 … DR-008 pattern
    private async Task<string> NextDoctorCode()
    {
        var codes = await _context.Users
            .Where(u => u.UserCode.StartsWith("DR-"))
            .Select(u => u.UserCode)
            .ToListAsync();
        var profileCodes = await _context.DoctorProfiles
            .Where(p => p.DoctorCode.StartsWith("DR-"))
            .Select(p => p.DoctorCode)
            .ToListAsync();

        int max = 0;
        foreach (var c in codes.Concat(profileCodes))
            if (int.TryParse(c.Substring(3), out var n) && n > max) max = n;

        return $"DR-{max + 1:000}";
    }

    // Spec password rule: 8+ chars, 1 uppercase, 1 number, 1 special (@#$!%*?)
    private static string GeneratePassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ", lower = "abcdefghjkmnpqrstuvwxyz",
                     digits = "23456789", special = "@#$!%*?";
        const string all = upper + lower + digits + special;

        var chars = new List<char>
        {
            upper[RandomNumberGenerator.GetInt32(upper.Length)],
            lower[RandomNumberGenerator.GetInt32(lower.Length)],
            digits[RandomNumberGenerator.GetInt32(digits.Length)],
            special[RandomNumberGenerator.GetInt32(special.Length)]
        };
        while (chars.Count < 10) chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);

        // Fisher–Yates shuffle with a cryptographic RNG
        for (int i = chars.Count - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars.ToArray());
    }

    // Returns true if the e-mail was actually sent
    private bool SendAccountEmail(string toEmail, string fullName, string password)
    {
        // ON unless switched off in appsettings ("LabManager:SendAccountEmails": false)
        if (!_config.GetValue("LabManager:SendAccountEmails", true))
        {
            _logger.LogInformation("[EMAIL OFF] Doctor account e-mail for {Email} not sent", toEmail);
            return false;
        }

        var to = _config["LabManager:AccountEmailRedirectTo"];
        if (string.IsNullOrWhiteSpace(to)) to = toEmail;

        // Absolute link to the login page of whichever site is running (localhost or the published URL)
        var loginUrl = Url.Action("Login", "Account", null, Request.Scheme) ?? "";

        try
        {
            _email.SendEmail(to, "HemaFlow: your doctor account",
                $"<p>Hello Dr {WebUtility.HtmlEncode(fullName)},</p>" +
                "<p>An account has been created for you on the Nelson Mandela Bay Haematology Laboratory system.</p>" +
                $"<p><strong>Username:</strong> {WebUtility.HtmlEncode(toEmail)}<br/>" +
                $"<strong>Temporary password:</strong> {WebUtility.HtmlEncode(password)}</p>" +
                "<p>You will be asked to change this password when you first log in.</p>" +
                $"<p><a href=\"{WebUtility.HtmlEncode(loginUrl)}\" style=\"display:inline-block;padding:10px 18px;" +
                "background:#0d6efd;color:#ffffff;text-decoration:none;border-radius:6px;\">Sign in to HemaFlow</a></p>" +
                $"<p style=\"font-size:12px;color:#6c757d;\">Or copy this link into your browser: {WebUtility.HtmlEncode(loginUrl)}</p>" +
                "<p>Kind regards,<br/>Laboratory Manager, NMBHDL</p>");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Doctor account e-mail failed for {Email}", toEmail);
            return false;
        }
    }

    // ── List ────────────────────────────────────────────
    public async Task<IActionResult> Index() => View(await DoctorQuery(archived: false).ToListAsync());
    public async Task<IActionResult> Archived() => View(await DoctorQuery(archived: true).ToListAsync());

    // ── Create ──────────────────────────────────────────
    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VmDoctor model)
    {
        Normalise(model);
        IgnoreUnpostedFields(editing: false);

        if (await _context.DoctorProfiles.AnyAsync(p => p.HpcsaNumber == model.HPCSANumber))
            ModelState.AddModelError("HPCSANumber", "A doctor with this HPCSA number already exists.");

        if (await _context.Users.AnyAsync(u => u.Email == model.Email))
            ModelState.AddModelError("Email", "This e-mail address is already used by another account.");

        if (!ModelState.IsValid) return View(model);

        var password = GeneratePassword();
        var code = await NextDoctorCode();

        var user = new DbUser
        {
            RoleId = await DoctorRoleId(),
            FirstName = model.Name,
            Surname = model.Surname,
            Email = model.Email,
            Phone = model.ContactNumber,
            MustChangePassword = true,                 // spec: change at first login
            TwoFactorEnabled = false,
            IsArchived = false,
            UserCode = code,
            Identifier = $"HPCSA: {model.HPCSANumber}",   // same format the team uses
            Status = "Active",             // AccountController only lets Active users log in
            CreatedOn = DateTime.UtcNow
        };
        user.PasswordHash = new PasswordHasher<DbUser>().HashPassword(user, password);

        user.DoctorProfile = new DoctorProfile
        {
            DoctorCode = code,
            HpcsaNumber = model.HPCSANumber,
            IsArchived = false
        };

        _context.Users.Add(user);

        try
        {
            await _context.SaveChangesAsync();   // Users + DoctorProfile in one transaction
        }
        catch (DbUpdateException)
        {
            // The Create view has no summary, so put the message on a visible field
            ModelState.AddModelError("Email", "Could not save - this e-mail or HPCSA number was just taken. Please check and try again.");
            return View(model);
        }

        var fullName = $"{model.Name} {model.Surname}";
        bool emailed = SendAccountEmail(model.Email, fullName, password);
        await Activity.LogAsync("Created", "Doctor", $"Dr {fullName}", emailed ? "Credentials e-mailed" : "Credentials shown to manager");

        TempData["SuccessMessage"] = $"Dr {fullName} added ({code})." + (emailed ? " Login details have been e-mailed." : "");
        if (!emailed)
        {
            // Shown in a panel on the Doctors page until dismissed (toasts vanish too quickly)
            TempData["NewAccountName"] = $"Dr {fullName}";
            TempData["NewAccountEmail"] = model.Email;
            TempData["NewAccountPassword"] = password;
        }

        return RedirectToAction("Index");
    }

    // ── Edit (name, surname, e-mail, contact) ───────────
    public async Task<IActionResult> Edit(int id)
    {
        var doctor = await _context.DoctorProfiles.AsNoTracking()
            .Where(p => p.UserId == id)
            .Select(p => new VmDoctor
            {
                Id = p.UserId,
                Name = p.User.FirstName,
                Surname = p.User.Surname,
                HPCSANumber = p.HpcsaNumber,
                Email = p.User.Email,
                ContactNumber = p.User.Phone ?? "",
                Password = "",
                MustChangePassword = p.User.MustChangePassword,
                IsArchived = p.IsArchived || p.User.IsArchived
            })
            .FirstOrDefaultAsync();

        if (doctor == null) return NotFound();
        return View(doctor);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(VmDoctor model)
    {
        var profile = await _context.DoctorProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == model.Id);
        if (profile == null) return NotFound();

        Normalise(model);
        IgnoreUnpostedFields(editing: true);
        model.HPCSANumber = profile.HpcsaNumber;   // disabled field - keep stored value

        if (await _context.Users.AnyAsync(u => u.Email == model.Email && u.UserId != model.Id))
            ModelState.AddModelError("Email", "This e-mail address is already used by another account.");

        if (!ModelState.IsValid) return View(model);

        profile.User.FirstName = model.Name;
        profile.User.Surname = model.Surname;
        profile.User.Email = model.Email;
        profile.User.Phone = model.ContactNumber;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("Email", "This e-mail address is already used by another account.");
            return View(model);
        }

        await Activity.LogAsync("Updated", "Doctor", $"Dr {model.Name} {model.Surname}");
        TempData["SuccessMessage"] = "Doctor successfully updated.";
        return RedirectToAction("Index");
    }

    // ── Archive / Restore (soft delete - blocks / allows login) ──
    public async Task<IActionResult> Archive(int id)
    {
        var profile = await _context.DoctorProfiles.Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == id);
        if (profile == null) return NotFound();

        profile.IsArchived = true;
        profile.User.IsArchived = true;
        await _context.SaveChangesAsync();

        await Activity.LogAsync("Archived", "Doctor", $"Dr {profile.User.FirstName} {profile.User.Surname}");
        TempData["SuccessMessage"] = "Doctor archived. They can no longer log in.";
        return RedirectToAction("Index");
    }

    public async Task<IActionResult> Unarchive(int id)
    {
        var profile = await _context.DoctorProfiles.Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == id);
        if (profile == null) return NotFound();

        profile.IsArchived = false;
        profile.User.IsArchived = false;
        await _context.SaveChangesAsync();

        await Activity.LogAsync("Restored", "Doctor", $"Dr {profile.User.FirstName} {profile.User.Surname}");
        TempData["SuccessMessage"] = "Doctor restored.";
        return RedirectToAction("Archived");
    }

}//end of class LMDoctorController
