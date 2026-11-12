using Project.Data;
using System.Linq.Expressions;
using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Models.Entities;
using Project.Models.LabManager;
using Project.Services;
using DbUser = Project.Models.Entities.User;
using DbTechnicianTestType = Project.Models.Entities.TechnicianTestType;
using VmTechnician = Project.Models.LabManager.Technician;
using VmTechnicianTestType = Project.Models.LabManager.TechnicianTestType;
using VmTestType = Project.Models.LabManager.TestType;
using VmTestCategory = Project.Models.LabManager.TestCategory;

/*
 =========================================================
 TechnicianController - Lab Manager, spec §7 (Manage Lab Technician Users)
 
 ✅ DB: ONE shared record per technician in GRP-04-13
     dbo.Users              FirstName, Surname, Email (username), Phone,
                            PasswordHash, MustChangePassword, IsArchived,
                            UserCode (LT-0xx), Identifier ("EMP: …"),
                            RoleId = Technician, Status
     dbo.TechnicianProfile  UserId, EmployeeCode (employee number, unique),
                            SaIdNumber (unique, 13 digits), IsArchived
     dbo.TechnicianTestType which test types each technician may perform
 
   The Lab Tech subsystem reads the SAME rows for the logged-in
   technician - including TechnicianTestType for permissions.
 
 Spec §7 rules implemented:
   • SA ID number unique + valid ([SouthAfricanId]: date, citizenship, Luhn)
   • Employee number unique; e-mail (username) unique across ALL users
   • Password auto-generated, HASHED (same as AccountController), e-mailed
   • MustChangePassword = true on creation
   • At least one test type; many technicians may share a test type
   • Archive = soft delete on Users + TechnicianProfile (blocks login)
 
 Existing technicians were created by the team without SA ID numbers,
 so Edit lets the manager capture the ID ONCE when it is still empty;
 after that it is locked.
 
 E-mail: ON by default (username, temporary password, login link) -
 same settings as LMDoctorController:
   "LabManager": { "SendAccountEmails": false, "AccountEmailRedirectTo": "…" }
 When not e-mailed, the temporary password is shown in a panel on the
 Technicians page until dismissed.
 
 ✅ Activity is logged to dbo.StaffActivityLog (LabManagerActivityLog).
 =========================================================
*/
[Authorize(Roles = "LabManager")]
public class TechnicianController : Controller
{
    private readonly ApplicationDbContext _context;
    private LabManagerActivityLog Activity => new(_context, User);   // → dbo.StaffActivityLog
    private readonly EmailService _email;
    private readonly IConfiguration _config;
    private readonly ILogger<TechnicianController> _logger;

    public TechnicianController(ApplicationDbContext context, EmailService email,
                                IConfiguration config, ILogger<TechnicianController> logger)
    {
        _context = context;
        _email = email;
        _config = config;
        _logger = logger;
    }

    // ── Queries / mapping ───────────────────────────────
    // Filter + sort on the DB entities first, then project to the view model
    private IQueryable<VmTechnician> TechnicianQuery(Expression<Func<TechnicianProfile, bool>> filter) =>
        _context.TechnicianProfiles.AsNoTracking()
            .Where(p => p.User.Role.Code == "Technician")
            .Where(filter)
            .OrderBy(p => p.User.Surname).ThenBy(p => p.User.FirstName)
            .Select(p => new VmTechnician
            {
                Id = p.UserId,
                Name = p.User.FirstName,
                Surname = p.User.Surname,
                IdNumber = p.SaIdNumber ?? "",
                EmployeeNumber = p.EmployeeCode,
                Email = p.User.Email,
                ContactNumber = p.User.Phone ?? "",
                Password = "",
                MustChangePassword = p.User.MustChangePassword,
                IsArchived = p.IsArchived || p.User.IsArchived,
                TechnicianTestTypes = p.TechnicianTestTypes
                    .Select(t => new VmTechnicianTestType { TechnicianId = t.TechnicianUserId, TestTypeId = t.TestTypeId })
                    .ToList()
            });

    private async Task LoadTestTypeLookups()
    {
        ViewBag.Categories = (await _context.TestCategories.AsNoTracking()
                .Where(c => !c.IsArchived).OrderBy(c => c.Name).ToListAsync())
            .Select(c => new VmTestCategory { Id = c.TestCategoryId, Name = c.Name, Description = c.Description ?? "" })
            .ToList();

        ViewBag.TestTypes = (await _context.TestTypes.AsNoTracking()
                .Where(t => !t.IsArchived).OrderBy(t => t.Name).ToListAsync())
            .Select(t => new VmTestType
            {
                Id = t.TestTypeId,
                Name = t.Name,
                TestCategoryId = t.TestCategoryId,
                SampleTypeId = t.SampleTypeId,
                Units = t.Unit ?? "",
                TurnaroundTime = t.TurnaroundMinutes ?? 0
            })
            .ToList();
    }

    private static void Normalise(VmTechnician m)
    {
        m.Name = m.Name?.Trim() ?? "";
        m.Surname = m.Surname?.Trim() ?? "";
        m.Email = m.Email?.Trim().ToLowerInvariant() ?? "";
        m.ContactNumber = m.ContactNumber?.Trim() ?? "";
        m.IdNumber = m.IdNumber?.Trim() ?? "";
        m.EmployeeNumber = m.EmployeeNumber?.Trim().ToUpperInvariant() ?? "";
    }

    // Only real, active test types; error if none (spec: at least one)
    private async Task<List<int>> ValidTestTypes(List<int>? selected)
    {
        var ids = (selected ?? new List<int>()).Distinct().ToList();
        var valid = await _context.TestTypes
            .Where(t => ids.Contains(t.TestTypeId) && !t.IsArchived)
            .Select(t => t.TestTypeId)
            .ToListAsync();

        if (!valid.Any())
            ModelState.AddModelError(nameof(VmTechnician.TechnicianTestTypes),
                "At least one test type must be assigned to the technician.");
        return valid;
    }

    // Next LT-0xx code, following the team's LT-001 … LT-008 pattern
    private async Task<string> NextTechnicianCode()
    {
        var codes = await _context.Users
            .Where(u => u.UserCode.StartsWith("LT-"))
            .Select(u => u.UserCode)
            .ToListAsync();

        int max = 0;
        foreach (var c in codes)
            if (int.TryParse(c.Substring(3), out var n) && n > max) max = n;

        return $"LT-{max + 1:000}";
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

        for (int i = chars.Count - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars.ToArray());
    }

    private bool SendAccountEmail(string toEmail, string fullName, string password)
    {
        // ON unless switched off in appsettings ("LabManager:SendAccountEmails": false)
        if (!_config.GetValue("LabManager:SendAccountEmails", true))
        {
            _logger.LogInformation("[EMAIL OFF] Technician account e-mail for {Email} not sent", toEmail);
            return false;
        }

        var to = _config["LabManager:AccountEmailRedirectTo"];
        if (string.IsNullOrWhiteSpace(to)) to = toEmail;

        // Absolute link to the login page of whichever site is running (localhost or the published URL)
        var loginUrl = Url.Action("Login", "Account", null, Request.Scheme) ?? "";

        try
        {
            _email.SendEmail(to, "HemaFlow: your lab technician account",
                $"<p>Hello {WebUtility.HtmlEncode(fullName)},</p>" +
                "<p>A lab technician account has been created for you on the Nelson Mandela Bay Haematology Laboratory system.</p>" +
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
            _logger.LogWarning(ex, "Technician account e-mail failed for {Email}", toEmail);
            return false;
        }
    }

    // ── List ────────────────────────────────────────────
    public async Task<IActionResult> Index() =>
        View(await TechnicianQuery(p => !p.IsArchived && !p.User.IsArchived).ToListAsync());

    public async Task<IActionResult> Archived() =>
        View(await TechnicianQuery(p => p.IsArchived || p.User.IsArchived).ToListAsync());

    // ── Create ──────────────────────────────────────────
    // testTypeId: opened from a test type's Technicians page ("New Technician") -
    // that test type is pre-ticked and Save / Cancel return to that page.
    public async Task<IActionResult> Create(int? testTypeId)
    {
        await LoadTestTypeLookups();
        await SetReturnTestType(testTypeId, testTypeId.HasValue ? new List<int> { testTypeId.Value } : null);
        return View();
    }

    // Only an active test type can be pre-ticked / returned to
    private async Task SetReturnTestType(int? testTypeId, List<int>? ticked)
    {
        int? valid = testTypeId.HasValue && await _context.TestTypes.AnyAsync(t => t.TestTypeId == testTypeId && !t.IsArchived)
            ? testTypeId : null;
        ViewBag.ReturnTestTypeId = valid;
        ViewBag.SelectedTestTypeIds = (ticked ?? new List<int>())
            .Where(id => id != testTypeId || valid.HasValue).Distinct().ToList();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VmTechnician model, List<int> selectedTestTypes, int? returnTestTypeId)
    {
        Normalise(model);
        ModelState.Remove(nameof(VmTechnician.Password));          // never posted

        if (await _context.TechnicianProfiles.AnyAsync(p => p.SaIdNumber == model.IdNumber))
            ModelState.AddModelError("IdNumber", "A technician with this SA ID number already exists.");

        if (await _context.TechnicianProfiles.AnyAsync(p => p.EmployeeCode == model.EmployeeNumber))
            ModelState.AddModelError("EmployeeNumber", "This employee number is already in use.");

        if (await _context.Users.AnyAsync(u => u.Email == model.Email))
            ModelState.AddModelError("Email", "This e-mail address is already used by another account.");

        var testTypeIds = await ValidTestTypes(selectedTestTypes);

        if (!ModelState.IsValid)
        {
            await LoadTestTypeLookups();
            await SetReturnTestType(returnTestTypeId, selectedTestTypes);   // keep the user's ticks
            return View(model);
        }

        var password = GeneratePassword();
        var code = await NextTechnicianCode();

        var user = new DbUser
        {
            RoleId = await _context.Roles.Where(r => r.Code == "Technician").Select(r => r.RoleId).SingleAsync(),
            FirstName = model.Name,
            Surname = model.Surname,
            Email = model.Email,
            Phone = model.ContactNumber,
            MustChangePassword = true,
            TwoFactorEnabled = false,
            IsArchived = false,
            UserCode = code,
            Identifier = $"EMP: {model.EmployeeNumber}",   // same format the team uses
            Status = "Active",
            CreatedOn = DateTime.UtcNow
        };
        user.PasswordHash = new PasswordHasher<DbUser>().HashPassword(user, password);

        user.TechnicianProfile = new TechnicianProfile
        {
            EmployeeCode = model.EmployeeNumber,
            SaIdNumber = model.IdNumber,
            Department = "Haematology",
            IsArchived = false
        };
        foreach (var ttId in testTypeIds)
            user.TechnicianProfile.TechnicianTestTypes.Add(new DbTechnicianTestType { TestTypeId = ttId });

        _context.Users.Add(user);

        try
        {
            await _context.SaveChangesAsync();   // Users + profile + test types in one transaction
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("Email", "Could not save - this e-mail, SA ID or employee number was just taken. Please check and try again.");
            await LoadTestTypeLookups();
            await SetReturnTestType(returnTestTypeId, selectedTestTypes);
            return View(model);
        }

        var fullName = $"{model.Name} {model.Surname}";
        bool emailed = SendAccountEmail(model.Email, fullName, password);
        await Activity.LogAsync("Created", "Technician", fullName,
            $"{testTypeIds.Count} test type(s); " + (emailed ? "credentials e-mailed" : "credentials shown to manager"));

        TempData["SuccessMessage"] = $"{fullName} added ({code})." + (emailed ? " Login details have been e-mailed." : "");
        if (!emailed)
        {
            TempData["NewAccountName"] = fullName;
            TempData["NewAccountEmail"] = model.Email;
            TempData["NewAccountPassword"] = password;
        }

        // Opened from a test type's Technicians page: go back there
        if (returnTestTypeId.HasValue && await _context.TestTypes.AnyAsync(t => t.TestTypeId == returnTestTypeId && !t.IsArchived))
            return RedirectToAction("Technicians", "TestType", new { id = returnTestTypeId.Value });

        return RedirectToAction("Index");
    }

    // ── Edit ────────────────────────────────────────────
    public async Task<IActionResult> Edit(int id)
    {
        var tech = await TechnicianQuery(p => p.UserId == id).FirstOrDefaultAsync();
        if (tech == null) return NotFound();

        await LoadTestTypeLookups();
        ViewBag.AssignedTestTypeIds = tech.TechnicianTestTypes.Select(t => t.TestTypeId).ToList();
        ViewBag.IdLocked = !string.IsNullOrEmpty(tech.IdNumber);   // based on what is STORED
        return View(tech);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(VmTechnician model, List<int> selectedTestTypes)
    {
        var profile = await _context.TechnicianProfiles
            .Include(p => p.User)
            .Include(p => p.TechnicianTestTypes)
            .FirstOrDefaultAsync(p => p.UserId == model.Id);
        if (profile == null) return NotFound();

        Normalise(model);

        // Not on the Edit form / disabled → never posted
        ModelState.Remove(nameof(VmTechnician.Password));
        ModelState.Remove(nameof(VmTechnician.EmployeeNumber));
        model.EmployeeNumber = profile.EmployeeCode;

        bool capturingId = string.IsNullOrEmpty(profile.SaIdNumber);
        if (!capturingId)
        {
            // ID already on record - locked
            ModelState.Remove(nameof(VmTechnician.IdNumber));
            model.IdNumber = profile.SaIdNumber!;
        }
        else if (await _context.TechnicianProfiles.AnyAsync(p => p.SaIdNumber == model.IdNumber && p.UserId != model.Id))
        {
            ModelState.AddModelError("IdNumber", "A technician with this SA ID number already exists.");
        }

        if (await _context.Users.AnyAsync(u => u.Email == model.Email && u.UserId != model.Id))
            ModelState.AddModelError("Email", "This e-mail address is already used by another account.");

        var testTypeIds = await ValidTestTypes(selectedTestTypes);

        if (!ModelState.IsValid)
        {
            await LoadTestTypeLookups();
            ViewBag.AssignedTestTypeIds = (selectedTestTypes ?? new List<int>()).ToList();  // keep the user's ticks
            ViewBag.IdLocked = !capturingId;   // based on what is STORED, not what was typed
            return View(model);
        }

        profile.User.FirstName = model.Name;
        profile.User.Surname = model.Surname;
        profile.User.Email = model.Email;
        profile.User.Phone = model.ContactNumber;
        if (capturingId) profile.SaIdNumber = model.IdNumber;

        // Sync test types: remove unticked, add newly ticked
        foreach (var link in profile.TechnicianTestTypes.Where(l => !testTypeIds.Contains(l.TestTypeId)).ToList())
            _context.TechnicianTestTypes.Remove(link);
        var already = profile.TechnicianTestTypes.Select(l => l.TestTypeId).ToHashSet();
        foreach (var ttId in testTypeIds.Where(id => !already.Contains(id)))
            profile.TechnicianTestTypes.Add(new DbTechnicianTestType { TestTypeId = ttId });

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("Email", "Could not save - this e-mail or SA ID number is already in use.");
            await LoadTestTypeLookups();
            ViewBag.AssignedTestTypeIds = testTypeIds;
            ViewBag.IdLocked = !capturingId;
            return View(model);
        }

        await Activity.LogAsync("Updated", "Technician", $"{model.Name} {model.Surname}", $"{testTypeIds.Count} test type(s)");
        TempData["SuccessMessage"] = "Technician successfully updated.";
        return RedirectToAction("Index");
    }

    // ── Archive / Restore (soft delete - blocks / allows login) ──
    public async Task<IActionResult> Archive(int id)
    {
        var profile = await _context.TechnicianProfiles.Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == id);
        if (profile == null) return NotFound();

        profile.IsArchived = true;
        profile.User.IsArchived = true;
        await _context.SaveChangesAsync();

        await Activity.LogAsync("Archived", "Technician", $"{profile.User.FirstName} {profile.User.Surname}");
        TempData["SuccessMessage"] = "Technician archived. They can no longer log in.";
        return RedirectToAction("Index");
    }

    public async Task<IActionResult> Unarchive(int id)
    {
        var profile = await _context.TechnicianProfiles.Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == id);
        if (profile == null) return NotFound();

        profile.IsArchived = false;
        profile.User.IsArchived = false;
        await _context.SaveChangesAsync();

        await Activity.LogAsync("Restored", "Technician", $"{profile.User.FirstName} {profile.User.Surname}");
        TempData["SuccessMessage"] = "Technician restored.";
        return RedirectToAction("Archived");
    }

}//end of class TechnicianController
