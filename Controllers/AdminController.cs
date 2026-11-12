using Microsoft.AspNetCore.Authorization;
using Project.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Models;
using Project.Services;
using GenCategory = Project.Models.Entities.Category;
using GenMedicalCondition = Project.Models.Entities.MedicalCondition;
using GenAllergy = Project.Models.Entities.Allergy;
using GenMedication = Project.Models.Entities.Medication;
using GenPatientCondition = Project.Models.Entities.PatientCondition;
using GenPendingApproval = Project.Models.Entities.PendingApproval;
using GenPatientAllergy = Project.Models.Entities.PatientAllergy;
using GenPatientMedication = Project.Models.Entities.PatientMedication;
using GenArchivedRecord = Project.Models.Entities.ArchivedRecord;
using GenStaffActivityLog = Project.Models.Entities.StaffActivityLog;
using GenUser = Project.Models.Entities.User;
using GenAdminProfile = Project.Models.Entities.AdminProfile;
using System.Security.Claims;

namespace Project.Controllers
{
    // Now that real login exists, this whole controller requires being signed
    // in as Admin or SuperAdmin. CreateAdminUser below is further restricted
    // to SuperAdmin only (both [Authorize] attributes must pass).
    [Authorize(Roles = "Admin,SuperAdmin")]
    public class AdminController : Controller
    {
        // ═══════════════════════════════════════════════════════════════════
        // DATABASE
        // ═══════════════════════════════════════════════════════════════════

        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ═══════════════════════════════════════════════════════════════════
        // The acting admin comes from the login cookie. Everything here
        // reads and writes the real database.
        // ═══════════════════════════════════════════════════════════════════

        // Fallback actor for the log helpers if the real user cannot be found.
        private const string _adminEmail = "s.naidoo@hemaflow.co.za";

        // The email/UserId of whoever is actually signed in right now, read
        // from the auth cookie's claims (see AccountController.Login).
        private string CurrentUserEmail => User.FindFirstValue(ClaimTypes.Email) ?? _adminEmail;
        // Id of the signed-in admin, read from the login cookie.
        private int? CurrentUserId =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (int?)null;
        // Role of the signed-in admin (Admin or SuperAdmin).
        private string CurrentUserRoleCode => User.FindFirstValue(ClaimTypes.Role) ?? "Admin";

        // Read-only access for other controllers (e.g. Patient) to the master lists
        // below - each takes its own injected context. All four master-data types
        // (Categories, Conditions, Allergies, Medications) are real DB reads now.
        internal static IReadOnlyList<CategoryItem> AllCategoriesStatic(ApplicationDbContext context) =>
            context.Categories.Where(c => !c.IsArchived)
                .Select(c => new CategoryItem { Id = c.CategoryId, Name = c.Name, SysCode = c.SysCode, DomainType = c.DomainType, IsArchived = c.IsArchived })
                .ToList();

        // Shared lists used by other screens that need the master data.
        internal static IReadOnlyList<ConditionRow> AllConditionsStatic(ApplicationDbContext context) =>
            context.MedicalConditions.Include(c => c.Category).Where(c => !c.IsArchived)
                .Select(c => new ConditionRow { Id = c.ConditionId, Name = c.Name, Category = c.Category.Name, Icd10Code = c.Icd10Code ?? "", Description = c.Description ?? "", IsArchived = c.IsArchived })
                .ToList();

        internal static IReadOnlyList<AllergyRow> AllAllergiesStatic(ApplicationDbContext context) =>
            context.Allergies.Include(a => a.Category).Where(a => !a.IsArchived)
                .Select(a => new AllergyRow { Id = a.AllergyId, Name = a.Name, Category = a.Category.Name, TypicalReaction = a.TypicalReaction ?? "", Notes = a.Notes ?? "", IsArchived = a.IsArchived })
                .ToList();

        internal static IReadOnlyList<MedicationRow> AllMedicationsStatic(ApplicationDbContext context) =>
            context.Medications.Include(m => m.Category).Where(m => !m.IsArchived)
                .Select(m => new MedicationRow { Id = m.MedicationId, Name = m.Name, Category = m.Category.Name, Notes = m.Notes ?? "", IsArchived = m.IsArchived })
                .ToList();

        // ═══════════════════════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════════════════════

        // Categories now come from the real database. Same return shape
        // (List<CategoryItem>) as before, so every view keeps working untouched.
        private List<CategoryItem> GetCats(string domain) =>
            _context.Categories
                .Where(c => c.DomainType == domain && !c.IsArchived)
                .Select(c => new CategoryItem { Id = c.CategoryId, Name = c.Name, SysCode = c.SysCode, DomainType = c.DomainType, IsArchived = c.IsArchived })
                .ToList();

        // Same as GetCats but without the domain filter - used wherever the old
        // code looked a category up by Id/Name across ALL domains.
        private List<CategoryItem> GetAllCats() =>
            _context.Categories
                .Select(c => new CategoryItem { Id = c.CategoryId, Name = c.Name, SysCode = c.SysCode, DomainType = c.DomainType, IsArchived = c.IsArchived })
                .ToList();

        // Conditions now come from the real database. Category name is pulled
        // via the FK join so the view keeps getting a plain string, unchanged.
        private List<ConditionRow> GetConditions(bool includeArchived = false) =>
            _context.MedicalConditions.Include(c => c.Category)
                .Where(c => includeArchived || !c.IsArchived)
                .OrderBy(c => c.Category.Name).ThenBy(c => c.Name)
                .Select(c => new ConditionRow { Id = c.ConditionId, Name = c.Name, Category = c.Category.Name, Icd10Code = c.Icd10Code ?? "", Description = c.Description ?? "", IsArchived = c.IsArchived })
                .ToList();

        // Allergies and Medications now come from the real database too, same
        // join-for-category-name pattern as Conditions.
        private List<AllergyRow> GetAllergies(bool includeArchived = false) =>
            _context.Allergies.Include(a => a.Category)
                .Where(a => includeArchived || !a.IsArchived)
                .OrderBy(a => a.Category.Name).ThenBy(a => a.Name)
                .Select(a => new AllergyRow { Id = a.AllergyId, Name = a.Name, Category = a.Category.Name, TypicalReaction = a.TypicalReaction ?? "", Notes = a.Notes ?? "", IsArchived = a.IsArchived })
                .ToList();

        // All medications, with or without the archived ones.
        private List<MedicationRow> GetMedications(bool includeArchived = false) =>
            _context.Medications.Include(m => m.Category)
                .Where(m => includeArchived || !m.IsArchived)
                .OrderBy(m => m.Category.Name).ThenBy(m => m.Name)
                .Select(m => new MedicationRow { Id = m.MedicationId, Name = m.Name, Category = m.Category.Name, Notes = m.Notes ?? "", IsArchived = m.IsArchived })
                .ToList();

        // Every user as a row for the staff screens (archived ones show as "Archived").
        private List<UserRow> GetUsers() =>
            _context.Users.Include(u => u.Role)
                .Select(u => new UserRow
                {
                    UserId = u.UserCode,
                    FullName = u.FirstName + " " + u.Surname,
                    Identifier = u.Identifier ?? "",
                    Email = u.Email,
                    Role = u.Role.DisplayName ?? u.Role.Code,
                    Status = u.IsArchived ? "Archived" : u.Status,
                    RoleCode = u.Role.Code,
                    IsArchived = u.IsArchived
                })
                .ToList();

        // Staff list shown in User Management / dashboard / export. Patients and doctors are
        // not listed, and Super Admin accounts are hidden from everyone: an Admin never sees
        // them, and a Super Admin cannot edit them through User Management.
        private List<UserRow> GetManagedUsers()
        {
            var superCodes = _context.Users.Where(u => u.Role.Code == "SuperAdmin").Select(u => u.UserCode).ToList();
            var rows = GetUsers().Where(u => u.Role != "Patient" && u.Role != "Doctor" && !superCodes.Contains(u.UserId)).ToList();
            foreach (var r in rows)
                r.CanManage = !r.IsArchived && CanManageRole(r.RoleCode);
            return rows;
        }

        // Only a Super Admin may manage Admin accounts. Admins manage Lab Technicians and Lab Managers.
        private bool CanManageRole(string roleCode) =>
            User.IsInRole("SuperAdmin")
                ? roleCode != "SuperAdmin"
                : roleCode == "Technician" || roleCode == "LabManager";

        // Returns an error message when the signed-in admin may not change this account, otherwise null.
        private string? ManageDeniedMessage(GenUser target)
        {
            if (target.IsArchived) return "Archived accounts cannot be changed here.";
            if (!CanManageRole(target.Role.Code))
                return target.Role.Code == "Admin"
                    ? "Only a Super Admin can manage Admin accounts."
                    : "You cannot manage this account.";
            return null;
        }

        // True when the user code belongs to a Super Admin.
        private bool IsSuperAdminAccount(string? userCode) =>
            _context.Users.Any(u => u.UserCode == userCode && u.Role.Code == "SuperAdmin");

        // Builds the PDF helper used by the export buttons.
        private PdfService GetPdf() =>
            new PdfService(HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>());

        // Archive and activity log rows. Explicit joins are used because the
        // actor columns have no navigation property.
        private List<ArchivedRecord> GetArchive() =>
            (from a in _context.ArchivedRecords
             join u in _context.Users on a.ArchivedByUserId equals u.UserId
             orderby a.ArchivedOn descending
             select new ArchivedRecord
             {
                 EntityType = a.EntityType,
                 EntityId = a.EntityId,
                 DisplayName = a.DisplayName,
                 Reference = a.Reference,
                 Reason = a.Reason ?? "",
                 ArchivedBy = u.Email,
                 ArchivedOn = a.ArchivedOn
             }).ToList()
            .Select(a => { a.ArchivedOn = SastTime.ToSast(a.ArchivedOn); return a; }).ToList();

        // Latest staff activity, newest first.
        private List<ActivityLogEntry> GetActivityLog(int take = 100) =>
            (from log in _context.StaffActivityLogs
             join u in _context.Users on log.ActorId equals u.UserId
             join r in _context.Roles on log.ActorRole equals r.RoleId
             join lt in _context.LogTypes on log.LogTypeId equals lt.LogTypeId
             orderby log.Timestamp descending
             select new ActivityLogEntry
             {
                 Timestamp = log.Timestamp,
                 Actor = u.Email,
                 Role = r.DisplayName ?? r.Code,
                 Action = log.Action,
                 Detail = log.Detail ?? "",
                 LogType = lt.Code
             }).Take(take).ToList()
            .Select(e => { e.Timestamp = SastTime.ToSast(e.Timestamp); return e; }).ToList();

        // Resolves an actor email to a real Users row (falling back to the
        // "currently logged in admin" mock account, which is always seeded)
        // and writes a real StaffActivityLog row.
        private void LogAction(string actor, string role, string action, string detail, string logType = "info")
        {
            var actorUser = _context.Users.FirstOrDefault(u => u.Email == actor)
                             ?? _context.Users.FirstOrDefault(u => u.Email == _adminEmail);
            var roleRow = _context.Roles.FirstOrDefault(r => r.Code == role);
            var logTypeRow = _context.LogTypes.FirstOrDefault(lt => lt.Code == logType)
                              ?? _context.LogTypes.FirstOrDefault(lt => lt.Code == "info");

            if (actorUser == null || roleRow == null || logTypeRow == null) return;

            _context.StaffActivityLogs.Add(new GenStaffActivityLog
            {
                ActorId = actorUser.UserId,
                ActorRole = roleRow.RoleId,
                Action = action,
                Detail = detail,
                LogTypeId = logTypeRow.LogTypeId
            });
            _context.SaveChanges();
        }

        // The cookie check caches "is this account still active" for 30 s; clear it so a change applies at once.
        private void ForgetActiveCache(int userId) =>
            HttpContext.RequestServices.GetService<Microsoft.Extensions.Caching.Memory.IMemoryCache>()?.Remove("acct-active-" + userId);

        // A master-data item may only point at an existing, un-archived category of the right type.
        private bool CategoryExists(int categoryId, string domainType) =>
            _context.Categories.Any(c => c.CategoryId == categoryId && c.DomainType == domainType && !c.IsArchived);

        // Resolves an "archived by" email the same way LogAction resolves
        // actor, and writes a real ArchivedRecord row.
        private void AddArchive(string entityType, int entityId, string displayName, string reference, string? reason, string archivedByEmail)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == archivedByEmail)
                       ?? _context.Users.FirstOrDefault(u => u.Email == _adminEmail);
            if (user == null) return;

            _context.ArchivedRecords.Add(new GenArchivedRecord
            {
                EntityType = entityType,
                EntityId = entityId,
                DisplayName = displayName,
                Reference = reference,
                Reason = string.IsNullOrWhiteSpace(reason) ? "No reason provided." : reason.Trim(),
                ArchivedByUserId = user.UserId,
                ArchivedOn = DateTime.UtcNow
            });
            _context.SaveChanges();
        }

        // ═══════════════════════════════════════════════════════════════════
        // DUPLICATE-NAME GUARD
        // True when the name is already used. If the match is archived, the admin
        // gets a Restore button instead of a second copy.
        // ═══════════════════════════════════════════════════════════════════
        private bool NameConflict(string entityType, string? name, string? domain, int excludeId, string returnAction)
        {
            var n = (name ?? "").Trim();
            if (n.Length == 0) return false;
            int hitId = 0; bool hitArchived = false;
            switch (entityType)
            {
                case "Category":
                {
                    var x = _context.Categories.Where(r => r.CategoryId != excludeId && r.Name == n && r.DomainType == domain)
                        .OrderBy(r => r.IsArchived).Select(r => new { Id = r.CategoryId, r.IsArchived }).FirstOrDefault();
                    if (x != null) { hitId = x.Id; hitArchived = x.IsArchived; }
                    break;
                }
                case "Condition":
                {
                    var x = _context.MedicalConditions.Where(r => r.ConditionId != excludeId && r.Name == n)
                        .OrderBy(r => r.IsArchived).Select(r => new { Id = r.ConditionId, r.IsArchived }).FirstOrDefault();
                    if (x != null) { hitId = x.Id; hitArchived = x.IsArchived; }
                    break;
                }
                case "Allergy":
                {
                    var x = _context.Allergies.Where(r => r.AllergyId != excludeId && r.Name == n)
                        .OrderBy(r => r.IsArchived).Select(r => new { Id = r.AllergyId, r.IsArchived }).FirstOrDefault();
                    if (x != null) { hitId = x.Id; hitArchived = x.IsArchived; }
                    break;
                }
                case "Medication":
                {
                    var x = _context.Medications.Where(r => r.MedicationId != excludeId && r.Name == n)
                        .OrderBy(r => r.IsArchived).Select(r => new { Id = r.MedicationId, r.IsArchived }).FirstOrDefault();
                    if (x != null) { hitId = x.Id; hitArchived = x.IsArchived; }
                    break;
                }
            }
            if (hitId == 0) return false;
            if (hitArchived) TempData["RestorePrompt"] = $"{entityType}|{hitId}|{n}|{returnAction}";
            else TempData["DupError"] = $"A {entityType.ToLower()} named '{n}' already exists. Please use a different name, or edit the existing one.";
            return true;
        }

        // Short unique SysCode for a new category when the admin leaves it blank
        // (the database has a unique index on SysCode + DomainType).
        private string MakeSysCode(string name, string domain)
        {
            var letters = new string((name ?? "").Where(char.IsLetter).ToArray()).ToUpperInvariant();
            var baseCode = letters.Length >= 2 ? letters.Substring(0, Math.Min(4, letters.Length)) : "CAT";
            var code = baseCode; int i = 2;
            while (_context.Categories.Any(c => c.DomainType == domain && c.SysCode == code))
                code = (baseCode.Length > 8 ? baseCode.Substring(0, 8) : baseCode) + i++;
            return code;
        }

        // ═══════════════════════════════════════════════════════════════════
        // GET ACTIONS
        // ═══════════════════════════════════════════════════════════════════

        // Dashboard: summary numbers plus the latest users and activity.
        public IActionResult Index()
        {
            var me = _context.Users.FirstOrDefault(u => u.UserId == CurrentUserId);
            var allUsers = GetManagedUsers();   // patients and doctors are not listed for the admin (patients manage themselves; doctors are managed by the lab)
            var recent = allUsers.Where(u => !u.IsArchived).Take(5)
                .Select(u => new RecentUserRow { UserId = u.UserId, FullName = u.FullName, Email = u.Email, Role = u.Role })
                .ToList();
            return View(new AdminDashboardViewModel
            {
                FirstName = me?.FirstName ?? "",
                Surname = me?.Surname ?? "",
                TotalUsers = allUsers.Count,
                TotalConditions = _context.MedicalConditions.Count(c => !c.IsArchived),
                TotalAllergies = _context.Allergies.Count(a => !a.IsArchived),
                TotalMedications = _context.Medications.Count(m => !m.IsArchived),
                TotalCategories = _context.Categories.Count(c => !c.IsArchived),
                StaffCount = allUsers.Count,
                PendingApprovalsCount = _context.PendingApprovals.Count(p => p.ApprovalStatusId == 1),
                RecentUsers = recent,
                ActivityLog = GetActivityLog(5),
                UsersByRole = allUsers.GroupBy(u => u.Role).ToDictionary(g => g.Key, g => g.Count())
            });
        }

        // Page that lists the categories used by conditions, allergies and medications.
        public IActionResult GlobalCategories() => View(new GlobalCategoriesViewModel
        {
            MedicalCategories = GetCats("Medical"),
            AllergyCategories = GetCats("Allergy"),
            MedicationCategories = GetCats("Medication")
        });

        // Medical conditions list with search and category filter.
        public IActionResult MedicalConditions() => View(new MedicalConditionsViewModel
        {
            Conditions = GetConditions(),
            Categories = GetCats("Medical")
        });

        // Add or edit form for a medical condition.
        public IActionResult CreateCondition(int? id = null)
        {
            var vm = new CreateConditionViewModel { Categories = GetCats("Medical") };
            if (id.HasValue)
            {
                var e = GetConditions(includeArchived: true).FirstOrDefault(c => c.Id == id.Value);
                if (e != null)
                {
                    vm.EditId = e.Id; vm.Name = e.Name; vm.Icd10Code = e.Icd10Code;
                    vm.Description = e.Description;
                    vm.CategoryId = GetAllCats().FirstOrDefault(c => c.Name == e.Category)?.Id ?? 0;
                }
            }
            return View(vm);
        }

        // Allergies list with search and category filter.
        public IActionResult Allergies() => View(new AllergiesViewModel
        {
            Allergies = GetAllergies(),
            Categories = GetCats("Allergy")
        });

        // Add or edit form for an allergy.
        public IActionResult CreateAllergy(int? id = null)
        {
            var vm = new CreateAllergyViewModel { Categories = GetCats("Allergy") };
            if (id.HasValue)
            {
                var e = GetAllergies(includeArchived: true).FirstOrDefault(a => a.Id == id.Value);
                if (e != null)
                {
                    vm.EditId = e.Id; vm.Name = e.Name;
                    vm.TypicalReaction = e.TypicalReaction; vm.Notes = e.Notes;
                    vm.CategoryId = GetAllCats().FirstOrDefault(c => c.Name == e.Category)?.Id ?? 0;
                }
            }
            return View(vm);
        }

        // Medications list with search and category filter.
        public IActionResult Medications() => View(new MedicationsViewModel
        {
            Medications = GetMedications(),
            Categories = GetCats("Medication")
        });

        // Add or edit form for a medication.
        public IActionResult CreateMedication(int? id = null)
        {
            var vm = new CreateMedicationViewModel { Categories = GetCats("Medication") };
            if (id.HasValue)
            {
                var e = GetMedications(includeArchived: true).FirstOrDefault(m => m.Id == id.Value);
                if (e != null)
                {
                    vm.EditId = e.Id; vm.Name = e.Name; vm.Notes = e.Notes;
                    vm.CategoryId = GetAllCats().FirstOrDefault(c => c.Name == e.Category)?.Id ?? 0;
                }
            }
            return View(vm);
        }

        // Staff list. Archived users are kept on their own page.
        public IActionResult UserManagement(string? editId = null, bool archived = false)
        {
            var all = GetManagedUsers();   // patients manage themselves
            // Archived accounts stay off the main list; they have their own view
            var users = all.Where(u => u.IsArchived == archived).ToList();
            var vm = new UserManagementViewModel { Users = users, ShowArchived = archived, ArchivedCount = all.Count(u => u.IsArchived) };
            if (editId != null) vm.EditingUser = users.FirstOrDefault(u => u.UserId == editId);
            return View(vm);
        }

        // Full System Activity Log (the dashboard only shows the latest few).
        public IActionResult ActivityLog(string? q, string? type, string? role, string? from, string? to, int page = 1)
        {
            const int pageSize = 50;
            var query = from log in _context.StaffActivityLogs
                        join u in _context.Users on log.ActorId equals u.UserId
                        join r in _context.Roles on log.ActorRole equals r.RoleId
                        join lt in _context.LogTypes on log.LogTypeId equals lt.LogTypeId
                        select new { log.Timestamp, Actor = u.Email, Role = r.DisplayName ?? r.Code, log.Action, Detail = log.Detail ?? "", LogType = lt.Code };

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                query = query.Where(x => x.Action.Contains(term) || x.Detail.Contains(term) || x.Actor.Contains(term));
            }
            if (!string.IsNullOrWhiteSpace(type)) query = query.Where(x => x.LogType == type);
            if (!string.IsNullOrWhiteSpace(role)) query = query.Where(x => x.Role == role);
            var todaySast = SastTime.ToSast(DateTime.UtcNow).Date;
            if (DateTime.TryParse(from, out var f0) && f0.Date > todaySast) from = todaySast.ToString("yyyy-MM-dd");
            if (DateTime.TryParse(to, out var t0) && t0.Date > todaySast) to = todaySast.ToString("yyyy-MM-dd");
            if (DateTime.TryParse(from, out var fromDate)) { var fromUtc = SastTime.SastDateToUtc(fromDate); query = query.Where(x => x.Timestamp >= fromUtc); }
            if (DateTime.TryParse(to, out var toDate)) { var toUtc = SastTime.SastDateToUtc(toDate).AddDays(1); query = query.Where(x => x.Timestamp < toUtc); }

            var total = query.Count();
            var pages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            page = Math.Min(Math.Max(page, 1), pages);

            var rows = query.OrderByDescending(x => x.Timestamp).Skip((page - 1) * pageSize).Take(pageSize).ToList()
                .Select(x => new ActivityLogEntry { Timestamp = SastTime.ToSast(x.Timestamp), Actor = x.Actor, Role = x.Role, Action = x.Action, Detail = x.Detail, LogType = x.LogType })
                .ToList();

            return View(new ActivityLogPageViewModel
            {
                Entries = rows, Query = q ?? "", Type = type ?? "", Role = role ?? "", From = from ?? "", To = to ?? "",
                Page = page, TotalPages = pages, TotalCount = total,
                Roles = _context.Roles.Where(r => r.Code != "Patient").Select(r => r.DisplayName ?? r.Code).OrderBy(n => n).ToList()
            });
        }

        // Everything that has been archived, so it can be restored.
        public IActionResult SystemArchive() =>
            View(new SystemArchiveViewModel { Records = GetArchive() });

        // ═══════════════════════════════════════════════════════════════════
        // POST - CATEGORIES (real database now)
        // ═══════════════════════════════════════════════════════════════════

        // Adds a category after checking the name and code are not taken.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveCategory(string domainType, string name, string? sysCode)
        {
            if (string.IsNullOrWhiteSpace(name)) { TempData["Error"] = "Category name is required."; return RedirectToAction("GlobalCategories"); }
            if (NameConflict("Category", name, domainType, 0, "GlobalCategories")) return RedirectToAction("GlobalCategories");
            var code = string.IsNullOrWhiteSpace(sysCode) ? MakeSysCode(name, domainType) : sysCode.Trim().ToUpper();
            if (_context.Categories.Any(c => c.DomainType == domainType && c.SysCode == code))
            {
                TempData["DupError"] = $"The code '{code}' is already used by another {domainType.ToLower()} category.";
                return RedirectToAction("GlobalCategories");
            }
            _context.Categories.Add(new GenCategory { Name = name.Trim(), SysCode = code, DomainType = domainType, IsArchived = false });
            _context.SaveChanges();
            LogAction(CurrentUserEmail, CurrentUserRoleCode, "Category added", $"New {domainType} category '{name}' created", "success");
            TempData["Success"] = $"Category '{name}' added.";
            return RedirectToAction("GlobalCategories");
        }

        // Renames a category.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateCategory(int id, string name, string domainType)
        {
            if (string.IsNullOrWhiteSpace(name)) { TempData["Error"] = "The category name cannot be empty."; return RedirectToAction("GlobalCategories"); }
            var cat = _context.Categories.FirstOrDefault(c => c.CategoryId == id);
            if (cat != null)
            {
                var newDomain = string.IsNullOrWhiteSpace(domainType) ? cat.DomainType : domainType;
                if (NameConflict("Category", name, newDomain, id, "GlobalCategories")) return RedirectToAction("GlobalCategories");
                cat.Name = name.Trim();
                if (!string.IsNullOrWhiteSpace(domainType)) cat.DomainType = domainType;
                _context.SaveChanges();
                LogAction(CurrentUserEmail, CurrentUserRoleCode, "Category updated", $"Category '{name}' updated (type: {cat.DomainType})", "info");
                TempData["Success"] = $"Category updated to '{name}'.";
            }
            return RedirectToAction("GlobalCategories");
        }

        // Archives a category (a reason is required).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteCategory(int id, string reason)
        {
            var cat = _context.Categories.FirstOrDefault(c => c.CategoryId == id);
            if (cat != null)
            {
                cat.IsArchived = true;
                _context.SaveChanges();
                AddArchive("Category", id, cat.Name, $"Domain: {cat.DomainType}", reason, CurrentUserEmail);
                LogAction(CurrentUserEmail, CurrentUserRoleCode, "Category archived", $"Category '{cat.Name}' soft-deleted", "warning");
                TempData["Success"] = $"Category '{cat.Name}' archived.";
            }
            return RedirectToAction("GlobalCategories");
        }

        // ═══════════════════════════════════════════════════════════════════
        // POST - CONDITIONS
        // ═══════════════════════════════════════════════════════════════════

        // Saves a new or edited medical condition.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateCondition(CreateConditionViewModel model)
        {
            model.Categories = GetCats("Medical");
            if (!ModelState.IsValid) return View(model);
            if (!CategoryExists(model.CategoryId, "Medical")) { ModelState.AddModelError("CategoryId", "Please choose a valid category."); return View(model); }
            if (NameConflict("Condition", model.Name, null, model.EditId, "MedicalConditions")) return View(model);
            if (model.EditId > 0)
            {
                var e = _context.MedicalConditions.FirstOrDefault(c => c.ConditionId == model.EditId);
                if (e != null) { e.Name = model.Name; e.Icd10Code = model.Icd10Code ?? ""; e.CategoryId = model.CategoryId; e.Description = model.Description ?? ""; _context.SaveChanges(); }
                LogAction(CurrentUserEmail, CurrentUserRoleCode, "Condition updated", $"Condition '{model.Name}' updated", "info");
                TempData["Success"] = $"Condition '{model.Name}' updated successfully.";
            }
            else
            {
                _context.MedicalConditions.Add(new GenMedicalCondition { Name = model.Name, Icd10Code = model.Icd10Code ?? "", CategoryId = model.CategoryId, Description = model.Description ?? "", IsArchived = false });
                _context.SaveChanges();
                LogAction(CurrentUserEmail, CurrentUserRoleCode, "Condition added", $"New condition '{model.Name}' added to catalogue", "success");
                TempData["Success"] = $"Condition '{model.Name}' added successfully.";
            }
            return RedirectToAction("MedicalConditions");
        }


        // Edits a condition from the list popup.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateCondition(int id, string name, string? icd10Code, int categoryId)
        {
            var cond = _context.MedicalConditions.FirstOrDefault(c => c.ConditionId == id);
            if (cond == null) { TempData["Error"] = "Condition not found."; return RedirectToAction("MedicalConditions"); }
            if (string.IsNullOrWhiteSpace(name)) { TempData["Error"] = "The name cannot be empty."; return RedirectToAction("MedicalConditions"); }
            if (!CategoryExists(categoryId, "Medical")) { TempData["Error"] = "Please choose a valid category."; return RedirectToAction("MedicalConditions"); }
            if (NameConflict("Condition", name, null, id, "MedicalConditions")) return RedirectToAction("MedicalConditions");
            cond.Name = name.Trim();
            cond.Icd10Code = icd10Code?.Trim() ?? cond.Icd10Code;
            cond.CategoryId = categoryId;
            _context.SaveChanges();
            LogAction(CurrentUserEmail, CurrentUserRoleCode, "Condition updated", $"Condition '{cond.Name}' updated via edit modal", "info");
            TempData["Success"] = $"Condition '{cond.Name}' updated successfully.";
            return RedirectToAction("MedicalConditions");
        }

        // Edits an allergy from the list popup.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateAllergy(int id, string name, int categoryId, string? typicalReaction)
        {
            var allergy = _context.Allergies.FirstOrDefault(a => a.AllergyId == id);
            if (allergy == null) { TempData["Error"] = "Allergy not found."; return RedirectToAction("Allergies"); }
            if (string.IsNullOrWhiteSpace(name)) { TempData["Error"] = "The name cannot be empty."; return RedirectToAction("Allergies"); }
            if (!CategoryExists(categoryId, "Allergy")) { TempData["Error"] = "Please choose a valid category."; return RedirectToAction("Allergies"); }
            if (NameConflict("Allergy", name, null, id, "Allergies")) return RedirectToAction("Allergies");
            allergy.Name = name.Trim();
            allergy.CategoryId = categoryId;
            allergy.TypicalReaction = typicalReaction?.Trim() ?? allergy.TypicalReaction;
            _context.SaveChanges();
            LogAction(CurrentUserEmail, CurrentUserRoleCode, "Allergy updated", $"Allergy '{allergy.Name}' updated via edit modal", "info");
            TempData["Success"] = $"Allergy '{allergy.Name}' updated successfully.";
            return RedirectToAction("Allergies");
        }

        // Edits a medication from the list popup.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateMedication(int id, string name, int categoryId, string? notes)
        {
            var med = _context.Medications.FirstOrDefault(m => m.MedicationId == id);
            if (med == null) { TempData["Error"] = "Medication not found."; return RedirectToAction("Medications"); }
            if (string.IsNullOrWhiteSpace(name)) { TempData["Error"] = "The name cannot be empty."; return RedirectToAction("Medications"); }
            if (!CategoryExists(categoryId, "Medication")) { TempData["Error"] = "Please choose a valid category."; return RedirectToAction("Medications"); }
            if (NameConflict("Medication", name, null, id, "Medications")) return RedirectToAction("Medications");
            med.Name = name.Trim();
            med.CategoryId = categoryId;
            med.Notes = notes?.Trim() ?? med.Notes;
            _context.SaveChanges();
            LogAction(CurrentUserEmail, CurrentUserRoleCode, "Medication updated", $"Medication '{med.Name}' updated via edit modal", "info");
            TempData["Success"] = $"Medication '{med.Name}' updated successfully.";
            return RedirectToAction("Medications");
        }

        // Archives a condition (a reason is required).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteCondition(int id, string reason)
        {
            var c = _context.MedicalConditions.Include(x => x.Category).FirstOrDefault(x => x.ConditionId == id);
            if (c != null) { c.IsArchived = true; _context.SaveChanges(); AddArchive("Condition", id, c.Name, $"Cat: {c.Category.Name}", reason, CurrentUserEmail); LogAction(CurrentUserEmail, CurrentUserRoleCode, "Condition archived", $"Condition '{c.Name}' soft-deleted", "warning"); TempData["Success"] = $"Condition '{c.Name}' archived."; }
            return RedirectToAction("MedicalConditions");
        }

        // ═══════════════════════════════════════════════════════════════════
        // POST - ALLERGIES
        // ═══════════════════════════════════════════════════════════════════

        // Saves a new or edited allergy.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateAllergen(CreateAllergyViewModel model)
        {
            model.Categories = GetCats("Allergy");
            if (!ModelState.IsValid) return View("CreateAllergy", model);
            if (NameConflict("Allergy", model.Name, null, model.EditId, "Allergies")) return View("CreateAllergy", model);
            if (model.EditId > 0)
            {
                var e = _context.Allergies.FirstOrDefault(a => a.AllergyId == model.EditId);
                if (e != null) { e.Name = model.Name; e.CategoryId = model.CategoryId; e.TypicalReaction = model.TypicalReaction ?? ""; e.Notes = model.Notes ?? ""; _context.SaveChanges(); }
                LogAction(CurrentUserEmail, CurrentUserRoleCode, "Allergy updated", $"Allergy '{model.Name}' updated", "info");
                TempData["Success"] = $"Allergy '{model.Name}' updated successfully.";
            }
            else
            {
                _context.Allergies.Add(new GenAllergy { Name = model.Name, CategoryId = model.CategoryId, TypicalReaction = model.TypicalReaction ?? "", Notes = model.Notes ?? "", IsArchived = false });
                _context.SaveChanges();
                LogAction(CurrentUserEmail, CurrentUserRoleCode, "Allergy added", $"New allergy '{model.Name}' added to catalogue", "success");
                TempData["Success"] = $"Allergy '{model.Name}' added successfully.";
            }
            return RedirectToAction("Allergies");
        }

        // Archives an allergy (a reason is required).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteAllergy(int id, string reason)
        {
            var a = _context.Allergies.Include(x => x.Category).FirstOrDefault(x => x.AllergyId == id);
            if (a != null) { a.IsArchived = true; _context.SaveChanges(); AddArchive("Allergy", id, a.Name, $"Cat: {a.Category.Name}", reason, CurrentUserEmail); LogAction(CurrentUserEmail, CurrentUserRoleCode, "Allergy archived", $"Allergy '{a.Name}' soft-deleted", "warning"); TempData["Success"] = $"Allergy '{a.Name}' archived."; }
            return RedirectToAction("Allergies");
        }

        // ═══════════════════════════════════════════════════════════════════
        // POST - MEDICATIONS
        // ═══════════════════════════════════════════════════════════════════

        // Saves a new or edited medication.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateMedication(CreateMedicationViewModel model)
        {
            model.Categories = GetCats("Medication");
            if (!ModelState.IsValid) return View(model);
            if (!CategoryExists(model.CategoryId, "Medication")) { ModelState.AddModelError("CategoryId", "Please choose a valid category."); return View(model); }
            if (NameConflict("Medication", model.Name, null, model.EditId, "Medications")) return View(model);
            if (model.EditId > 0)
            {
                var e = _context.Medications.FirstOrDefault(m => m.MedicationId == model.EditId);
                if (e != null) { e.Name = model.Name; e.CategoryId = model.CategoryId; e.Notes = model.Notes ?? ""; _context.SaveChanges(); }
                LogAction(CurrentUserEmail, CurrentUserRoleCode, "Medication updated", $"Medication '{model.Name}' updated", "info");
                TempData["Success"] = $"Medication '{model.Name}' updated successfully.";
            }
            else
            {
                _context.Medications.Add(new GenMedication { Name = model.Name, CategoryId = model.CategoryId, Notes = model.Notes ?? "", IsArchived = false });
                _context.SaveChanges();
                LogAction(CurrentUserEmail, CurrentUserRoleCode, "Medication added", $"New medication '{model.Name}' added to catalogue", "success");
                TempData["Success"] = $"Medication '{model.Name}' added successfully.";
            }
            return RedirectToAction("Medications");
        }

        // Archives a medication (a reason is required).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteMedication(int id, string reason)
        {
            var m = _context.Medications.Include(x => x.Category).FirstOrDefault(x => x.MedicationId == id);
            if (m != null) { m.IsArchived = true; _context.SaveChanges(); AddArchive("Medication", id, m.Name, $"Cat: {m.Category.Name}", reason, CurrentUserEmail); LogAction(CurrentUserEmail, CurrentUserRoleCode, "Medication archived", $"Medication '{m.Name}' soft-deleted", "warning"); TempData["Success"] = $"Medication '{m.Name}' archived."; }
            return RedirectToAction("Medications");
        }

        // ═══════════════════════════════════════════════════════════════════
        // PROFILE (own account, not User Management)
        // ═══════════════════════════════════════════════════════════════════

        // Shows the admin's own profile and security settings.
        [HttpGet]
        public IActionResult Profile()
        {
            var user = _context.Users.FirstOrDefault(u => u.UserId == CurrentUserId);
            if (user == null) return RedirectToAction("Login", "Account");

            var profile = _context.AdminProfiles.FirstOrDefault(p => p.UserId == user.UserId);

            var model = new AdminProfileViewModel
            {
                FullName = $"{user.FirstName} {user.Surname}",
                FirstName = user.FirstName,
                Surname = user.Surname,
                Email = user.Email,
                Phone = user.Phone ?? "",
                AdminId = user.UserCode,
                DateOfBirth = profile?.DateOfBirth is DateOnly dob ? dob.ToDateTime(TimeOnly.MinValue) : default,
                AddressLine1 = profile?.AddressLine1 ?? "",
                ComplexName = profile?.ComplexName,
                SuburbId = profile?.SuburbId,
                Suburb = LocationHelper.Describe(_context, profile?.SuburbId).Suburb,
                City = LocationHelper.Describe(_context, profile?.SuburbId).Town,
                PostalCode = LocationHelper.Describe(_context, profile?.SuburbId).PostalCode
            };

            var tf = _context.UserTwoFactors.FirstOrDefault(t => t.UserId == user.UserId && t.IsEnabled);
            model.TwoFactorEnabled = tf != null;
            model.TwoFactorLastVerified = tf?.LastVerifiedOn == null ? null : SastTime.ToSast(tf.LastVerifiedOn.Value);
            model.BackupCodesLeft = _context.UserBackupCodes.Count(b => b.UserId == user.UserId && b.UsedOn == null);
            if (TempData["NewBackupCodes"] is string codes && codes.Length > 0)
                model.NewBackupCodes = codes.Split(',').ToList();
            return View(model);
        }

        // ── 2FA (authenticator app) for the signed-in admin ──────
        [HttpGet]
        public IActionResult Setup2FA()
        {
            var user = _context.Users.FirstOrDefault(u => u.UserId == CurrentUserId);
            if (user == null) return RedirectToAction("Login", "Account");
            var row = _context.UserTwoFactors.FirstOrDefault(t => t.UserId == user.UserId);
            if (row != null && row.IsEnabled)
            {
                TempData["Success"] = "Two-factor authentication is already enabled.";
                return RedirectToAction("Profile");
            }
            if (row == null)
            {
                row = new Project.Models.Entities.UserTwoFactor { UserId = user.UserId, SecretKey = TotpService.GenerateSecretKey(), IsEnabled = false };
                _context.UserTwoFactors.Add(row);
                _context.SaveChanges();
            }
            return View(new TwoFactorSetupViewModel
            {
                SecretKey = row.SecretKey,
                OtpAuthUri = TotpService.BuildOtpAuthUri("HemaFlow", user.Email, row.SecretKey),
                AccountEmail = user.Email
            });
        }

        // Checks the 6 digit code and switches 2FA on.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Setup2FAConfirm(string code)
        {
            var user = _context.Users.FirstOrDefault(u => u.UserId == CurrentUserId);
            if (user == null) return RedirectToAction("Login", "Account");
            var row = _context.UserTwoFactors.FirstOrDefault(t => t.UserId == user.UserId);
            if (row == null) { TempData["Error"] = "Start the setup again."; return RedirectToAction("Setup2FA"); }
            if (!TotpService.ValidateCode(row.SecretKey, code))
            {
                TempData["Error"] = "That code didn't match. Check your authenticator app's clock and try the current code.";
                return RedirectToAction("Setup2FA");
            }
            row.IsEnabled = true;
            row.EnabledOn = DateTime.UtcNow;
            row.LastVerifiedOn = DateTime.UtcNow;
            var plain = ReplaceBackupCodes(user.UserId);
            _context.SaveChanges();
            LogAction(CurrentUserEmail, CurrentUserRoleCode, "2FA enabled", "Admin enabled two-factor authentication", "info");
            TempData["NewBackupCodes"] = string.Join(",", plain);
            TempData["Success"] = "Two-factor authentication is now enabled. Save your backup codes somewhere safe.";
            return RedirectToAction("Profile");
        }

        // Turns 2FA off after the password is confirmed.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DisableTwoFactor(string password)
        {
            var user = _context.Users.FirstOrDefault(u => u.UserId == CurrentUserId);
            if (user == null) return RedirectToAction("Login", "Account");
            var hasher = new PasswordHasher<GenUser>();
            if (string.IsNullOrEmpty(password) ||
                hasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed)
            {
                TempData["Error"] = "Incorrect password. Two-factor authentication was not disabled.";
                return RedirectToAction("Profile");
            }
            var row = _context.UserTwoFactors.FirstOrDefault(t => t.UserId == user.UserId);
            if (row != null) _context.UserTwoFactors.Remove(row);
            _context.UserBackupCodes.RemoveRange(_context.UserBackupCodes.Where(b => b.UserId == user.UserId));
            _context.SaveChanges();
            LogAction(CurrentUserEmail, CurrentUserRoleCode, "2FA disabled", "Admin disabled two-factor authentication", "warning");
            TempData["Success"] = "Two-factor authentication has been disabled.";
            return RedirectToAction("Profile");
        }

        // Replaces the backup codes with a fresh set.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RegenerateBackupCodes()
        {
            var uid = CurrentUserId;
            var tf = _context.UserTwoFactors.FirstOrDefault(t => t.UserId == uid && t.IsEnabled);
            if (tf == null) { TempData["Error"] = "Enable two-factor authentication first."; return RedirectToAction("Profile"); }
            var plain = ReplaceBackupCodes(uid ?? 0);
            _context.SaveChanges();
            LogAction(CurrentUserEmail, CurrentUserRoleCode, "Backup codes regenerated", "Admin generated new 2FA backup codes", "info");
            TempData["NewBackupCodes"] = string.Join(",", plain);
            TempData["Success"] = "New backup codes generated. Your old codes no longer work.";
            return RedirectToAction("Profile");
        }

        // Deletes the old backup codes and stores new hashed ones.
        private List<string> ReplaceBackupCodes(int userId)
        {
            _context.UserBackupCodes.RemoveRange(_context.UserBackupCodes.Where(b => b.UserId == userId));
            var plain = TotpService.GenerateBackupCodes();
            foreach (var c in plain)
                _context.UserBackupCodes.Add(new Project.Models.Entities.UserBackupCode { UserId = userId, CodeHash = TotpService.HashBackupCode(c) });
            return plain;
        }

        // Saves the admin's own profile details after validating them.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Profile(AdminProfileViewModel model)
        {
            var user = _context.Users.FirstOrDefault(u => u.UserId == CurrentUserId);
            if (user == null) return RedirectToAction("Login", "Account");

            if (User.IsInRole("SuperAdmin"))
            {
                // Super Admin has no personal details to edit (email + department only, both read-only).
                TempData["Success"] = "Nothing to update for the Super Admin account.";
                return RedirectToAction("Profile");
            }
            if (!string.IsNullOrWhiteSpace(model.FirstName) && !InputRules.IsValidName(model.FirstName)) { TempData["Error"] = InputRules.NameMessage; return RedirectToAction("Profile"); }
            if (!string.IsNullOrWhiteSpace(model.Surname) && !InputRules.IsValidName(model.Surname)) { TempData["Error"] = InputRules.NameMessage; return RedirectToAction("Profile"); }
            if (!string.IsNullOrWhiteSpace(model.Phone) && !InputRules.IsValidPhone(model.Phone)) { TempData["Error"] = InputRules.PhoneMessage; return RedirectToAction("Profile"); }
            if (!string.IsNullOrWhiteSpace(model.Email))
            {
                var newEmail = model.Email.Trim();
                if (!InputRules.IsValidEmail(newEmail)) { TempData["Error"] = InputRules.EmailMessage; return RedirectToAction("Profile"); }
                if (!string.Equals(newEmail, user.Email, StringComparison.OrdinalIgnoreCase)
                    && _context.Users.Any(u => u.UserId != user.UserId && u.Email == newEmail))
                { TempData["Error"] = "That email address is already used by another account."; return RedirectToAction("Profile"); }
                user.Email = newEmail;
            }
            if (!string.IsNullOrWhiteSpace(model.FirstName)) user.FirstName = model.FirstName.Trim();
            if (!string.IsNullOrWhiteSpace(model.Surname)) user.Surname = model.Surname.Trim();
            if (!string.IsNullOrWhiteSpace(model.Phone)) user.Phone = model.Phone.Trim();

            var profile = _context.AdminProfiles.FirstOrDefault(p => p.UserId == user.UserId);
            if (profile == null)
            {
                profile = new GenAdminProfile { UserId = user.UserId, AdminCode = user.UserCode };
                _context.AdminProfiles.Add(profile);
            }
            // Date of birth is only ever set once; after that it is read-only.
            if (profile.DateOfBirth == null && model.DateOfBirth != default) profile.DateOfBirth = DateOnly.FromDateTime(model.DateOfBirth);
            if (!string.IsNullOrWhiteSpace(model.AddressLine1)) profile.AddressLine1 = model.AddressLine1.Trim();
            profile.ComplexName = string.IsNullOrWhiteSpace(model.ComplexName) ? null : model.ComplexName.Trim();
            if (!User.IsInRole("SuperAdmin") && (model.SuburbId > 0 || !string.IsNullOrWhiteSpace(model.Suburb) || !string.IsNullOrWhiteSpace(model.City)))
            {
                var newSuburbId = LocationHelper.ResolveOrCreate(_context, model.SuburbId, model.ProvinceId, model.City, model.Suburb, model.PostalCode, out var addrError);
                if (newSuburbId == null) { TempData["Error"] = addrError ?? "Please check your address."; return RedirectToAction("Profile"); }
                profile.SuburbId = newSuburbId;
            }

            _context.SaveChanges();

            // Keep the signed-in session in step with the new name / e-mail (claims are read from the cookie).
            AuthCookieHelper.RefreshNameAndEmailAsync(HttpContext, user).GetAwaiter().GetResult();
            LogAction(CurrentUserEmail, CurrentUserRoleCode, "Profile updated", "Admin updated their own account details", "info");
            TempData["Success"] = "Your profile has been updated.";
            return RedirectToAction("Profile");
        }

        // Change password form.
        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View(new AccountChangePasswordViewModel());
        }

        // Checks the current password and the password rule, then saves the new one.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ChangePassword(AccountChangePasswordViewModel model)
        {
            if (model.NewPassword != model.ConfirmPassword)
            {
                TempData["Error"] = "New password and confirmation do not match.";
                return RedirectToAction("ChangePassword");
            }

            if (!Project.Services.PasswordPolicy.IsValid(model.NewPassword))
            {
                TempData["Error"] = Project.Services.PasswordPolicy.Message;
                return RedirectToAction("ChangePassword");
            }

            var user = _context.Users.FirstOrDefault(u => u.UserId == CurrentUserId);
            if (user == null) return RedirectToAction("Login", "Account");

            var hasher = new PasswordHasher<GenUser>();
            var verification = hasher.VerifyHashedPassword(user, user.PasswordHash, model.CurrentPassword);
            if (verification == PasswordVerificationResult.Failed)
            {
                TempData["Error"] = "Current password is incorrect.";
                return RedirectToAction("ChangePassword");
            }

            user.PasswordHash = hasher.HashPassword(user, model.NewPassword);
            _context.SaveChanges();

            LogAction(CurrentUserEmail, CurrentUserRoleCode, "Password changed", "Admin changed their own password", "info");
            TempData["Success"] = "Your password has been changed.";
            return RedirectToAction("Profile");
        }

        // ═══════════════════════════════════════════════════════════════════
        // PENDING APPROVALS (patient "Other" allergies, conditions and medications)
        //   ApprovalType:   1 condition, 2 allergy, 3 medication, 4 category, 5 other
        //   ApprovalStatus: 1 pending, 2 approved, 3 denied
        // ═══════════════════════════════════════════════════════════════════

        // Maps the approval type number to the list it belongs to.
        private static string ApprovalTypeToEntityType(byte typeId) => typeId switch
        {
            1 => "Condition",
            2 => "Allergy",
            3 => "Medication",
            4 => "Category",
            _ => "Other"
        };

        // Patient requests for items that are not on the lists yet.
        public IActionResult PendingApprovals()
        {
            // Pending items and the last 15 reviewed ones, with the patient's code and email.
            var rows = (from p in _context.PendingApprovals
                        join u in _context.Users on p.SubmittedByUserId equals u.UserId
                        where p.ApprovalStatusId == 1 || (p.ApprovalStatusId != 1 && p.ReviewedOn != null)
                        select new
                        {
                            p.PendingApprovalId, p.ApprovalTypeId, p.ApprovalStatusId, p.Name, p.ExtraInfo,
                            SuggestedCategory = p.Category != null ? p.Category.Name : null,
                            p.SubmittedByUserId, p.SubmittedOn, p.ReviewedOn, p.DenyReason,
                            u.FirstName, u.Surname, u.Email, u.UserCode
                        }).ToList();

            PendingApprovalItem Map(dynamic x, int otherPending) => new PendingApprovalItem
            {
                Id = x.PendingApprovalId,
                EntityType = ApprovalTypeToEntityType((byte)x.ApprovalTypeId),
                Name = x.Name,
                ExtraInfo = x.ExtraInfo,
                SuggestedCategory = x.SuggestedCategory,
                PatientName = $"{x.FirstName} {x.Surname}",
                PatientCode = x.UserCode ?? "",
                PatientEmail = x.Email ?? "",
                PatientUserId = x.SubmittedByUserId,
                PatientOtherPending = otherPending,
                SubmittedOn = SastTime.ToSast((DateTime)x.SubmittedOn),
                ReviewedOn = x.ReviewedOn == null ? (DateTime?)null : SastTime.ToSast((DateTime)x.ReviewedOn),
                DenyReason = x.DenyReason,
                Status = x.ApprovalStatusId == 1 ? "Pending" : x.ApprovalStatusId == 2 ? "Approved" : "Denied"
            };

            var pendingRows = rows.Where(r => r.ApprovalStatusId == 1).OrderBy(r => r.SubmittedOn).ToList();
            var items = pendingRows
                .Select(r => Map(r, pendingRows.Count(o => o.SubmittedByUserId == r.SubmittedByUserId && o.PendingApprovalId != r.PendingApprovalId)))
                .ToList();
            var reviewed = rows.Where(r => r.ApprovalStatusId != 1).OrderByDescending(r => r.ReviewedOn).Take(15)
                .Select(r => Map(r, 0)).ToList();

            return View(new PendingApprovalsViewModel
            {
                Items = items,
                Reviewed = reviewed,
                ConditionCategories = GetCats("Medical"),
                AllergyCategories = GetCats("Allergy"),
                MedicationCategories = GetCats("Medication")
            });
        }

        // Adds the requested item to the master list under the chosen category.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ApprovePending(int id, string category, string? entityType = null, string? name = null)
        {
            var item = _context.PendingApprovals.FirstOrDefault(p => p.PendingApprovalId == id && p.ApprovalStatusId == 1);
            if (item == null) { TempData["Error"] = "Item not found or already processed."; return RedirectToAction("PendingApprovals"); }
            if (string.IsNullOrWhiteSpace(category)) { TempData["Error"] = "Please assign a category before approving."; return RedirectToAction("PendingApprovals"); }

            // The admin may correct the type (e.g. a patient filed a medication under "allergy") and the name.
            var newTypeId = entityType switch { "Condition" => (byte)1, "Allergy" => (byte)2, "Medication" => (byte)3, _ => item.ApprovalTypeId };
            item.ApprovalTypeId = newTypeId;
            var cleanName = (name ?? "").Trim();
            if (cleanName.Length > 200) { TempData["Error"] = "Name is too long (200 characters maximum)."; return RedirectToAction("PendingApprovals"); }
            if (cleanName.Length > 0) item.Name = cleanName;

            entityType = ApprovalTypeToEntityType(item.ApprovalTypeId);
            // Only categories of the matching kind are valid (a category name can exist under several kinds).
            var domain = entityType == "Condition" ? "Medical" : entityType;
            var catsOfDomain = GetAllCats().Where(c => c.DomainType == domain && !c.IsArchived).ToList();
            if (!catsOfDomain.Any(c => c.Name == category)) { TempData["Error"] = $"'{category}' is not a {entityType.ToLower()} category."; return RedirectToAction("PendingApprovals"); }
            switch (entityType)
            {
                case "Condition":
                    var condCatId = catsOfDomain.FirstOrDefault(c => c.Name == category)?.Id ?? 0;
                    var newCondition = _context.MedicalConditions.FirstOrDefault(x => x.Name == item.Name);
                    if (newCondition != null) newCondition.IsArchived = false; // already in the catalogue: reuse it
                    else
                    {
                        if (condCatId == 0) { TempData["Error"] = $"Category '{category}' was not found."; return RedirectToAction("PendingApprovals"); }
                        newCondition = new GenMedicalCondition { Name = item.Name, CategoryId = condCatId, Icd10Code = "", Description = "Added via patient-submitted request.", IsArchived = false };
                        _context.MedicalConditions.Add(newCondition);
                    }
                    _context.SaveChanges(); // need the generated ConditionId before attaching it to the patient

                    // Attach straight to the submitting patient's own profile,
                    // not just the master list - item.SubmittedByUserId is the
                    // real Users.UserId captured at submission time.
                    if (!_context.PatientConditions.Any(pc => pc.PatientUserId == item.SubmittedByUserId && pc.ConditionId == newCondition.ConditionId))
                    {
                        _context.PatientConditions.Add(new GenPatientCondition
                        {
                            PatientUserId = item.SubmittedByUserId,
                            ConditionId = newCondition.ConditionId,
                            DiagnosedOn = DateOnly.FromDateTime(DateTime.Today)
                        });
                        _context.SaveChanges();
                    }
                    break;
                case "Allergy":
                    var allCatId = catsOfDomain.FirstOrDefault(c => c.Name == category)?.Id ?? 0;
                    var newAllergy = _context.Allergies.FirstOrDefault(x => x.Name == item.Name);
                    if (newAllergy != null) newAllergy.IsArchived = false;
                    else
                    {
                        if (allCatId == 0) { TempData["Error"] = $"Category '{category}' was not found."; return RedirectToAction("PendingApprovals"); }
                        newAllergy = new GenAllergy { Name = item.Name, CategoryId = allCatId, TypicalReaction = "", Notes = "Added via patient-submitted request.", IsArchived = false };
                        _context.Allergies.Add(newAllergy);
                    }
                    _context.SaveChanges();

                    if (!_context.PatientAllergies.Any(pa => pa.PatientUserId == item.SubmittedByUserId && pa.AllergyId == newAllergy.AllergyId))
                    {
                        _context.PatientAllergies.Add(new GenPatientAllergy
                        {
                            PatientUserId = item.SubmittedByUserId,
                            AllergyId = newAllergy.AllergyId
                        });
                        _context.SaveChanges();
                    }
                    break;
                case "Medication":
                    var medCatId = catsOfDomain.FirstOrDefault(c => c.Name == category)?.Id ?? 0;
                    var newMedication = _context.Medications.FirstOrDefault(x => x.Name == item.Name);
                    if (newMedication != null) newMedication.IsArchived = false;
                    else
                    {
                        if (medCatId == 0) { TempData["Error"] = $"Category '{category}' was not found."; return RedirectToAction("PendingApprovals"); }
                        newMedication = new GenMedication { Name = item.Name, CategoryId = medCatId, Notes = "Added via patient-submitted request.", IsArchived = false };
                        _context.Medications.Add(newMedication);
                    }
                    _context.SaveChanges();

                    if (!_context.PatientMedications.Any(pm => pm.PatientUserId == item.SubmittedByUserId && pm.MedicationId == newMedication.MedicationId))
                    {
                        // The patient typed dosage and frequency as one text, so it is saved as it is.
                        _context.PatientMedications.Add(new GenPatientMedication
                        {
                            PatientUserId = item.SubmittedByUserId,
                            MedicationId = newMedication.MedicationId,
                            StartedOn = DateOnly.FromDateTime(DateTime.Today),
                            Notes = item.ExtraInfo ?? ""
                        });
                        _context.SaveChanges();
                    }
                    break;
            }

            item.ApprovalStatusId = 2; // Approved
            item.ReviewedByUserId = CurrentUserId;
            item.ReviewedOn = DateTime.UtcNow;
            _context.SaveChanges();

            LogAction(CurrentUserEmail, CurrentUserRoleCode, $"{entityType} approved", $"Patient-submitted {entityType.ToLower()} '{item.Name}' approved and added (category: {category})", "success");
            TempData["Success"] = $"'{item.Name}' approved and added to the master list.";
            return RedirectToAction("PendingApprovals");
        }

        // Lets the admin correct a pending submission (typo in the name, details text) before approving it.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditPending(int id, string name, string? extraInfo)
        {
            var item = _context.PendingApprovals.FirstOrDefault(p => p.PendingApprovalId == id && p.ApprovalStatusId == 1);
            if (item == null) { TempData["Error"] = "Item not found or already processed."; return RedirectToAction("PendingApprovals"); }
            name = (name ?? "").Trim();
            if (name.Length == 0) { TempData["Error"] = "Name cannot be empty."; return RedirectToAction("PendingApprovals"); }
            if (name.Length > 200) { TempData["Error"] = "Name is too long (200 characters maximum)."; return RedirectToAction("PendingApprovals"); }
            var oldName = item.Name;
            item.Name = name;
            if (item.ApprovalTypeId == 3) item.ExtraInfo = string.IsNullOrWhiteSpace(extraInfo) ? null : extraInfo.Trim();
            _context.SaveChanges();
            LogAction(CurrentUserEmail, CurrentUserRoleCode, "Pending item edited", $"Pending {ApprovalTypeToEntityType(item.ApprovalTypeId).ToLower()} '{oldName}' edited to '{name}' before review", "info");
            TempData["Success"] = $"'{name}' updated.";
            return RedirectToAction("PendingApprovals");
        }

        // Rejects a request and keeps the reason.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DenyPending(int id, string reason)
        {
            var item = _context.PendingApprovals.FirstOrDefault(p => p.PendingApprovalId == id && p.ApprovalStatusId == 1);
            if (item != null)
            {
                var entityType = ApprovalTypeToEntityType(item.ApprovalTypeId);
                item.ApprovalStatusId = 3; // Denied
                item.DenyReason = string.IsNullOrWhiteSpace(reason) ? null : reason;
                item.ReviewedByUserId = CurrentUserId;
                item.ReviewedOn = DateTime.UtcNow;
                _context.SaveChanges();

                LogAction(CurrentUserEmail, CurrentUserRoleCode, $"{entityType} denied", $"Patient-submitted {entityType.ToLower()} '{item.Name}' denied. Reason: {(string.IsNullOrWhiteSpace(reason) ? "No reason provided." : reason)}", "warning");
                TempData["Success"] = $"'{item.Name}' was denied.";
            }
            return RedirectToAction("PendingApprovals");
        }

        // ═══════════════════════════════════════════════════════════════════
        // POST - USERS
        // ═══════════════════════════════════════════════════════════════════

        // Edits a user's name, e-mail, identifier and status.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateUser(EditUserViewModel model)
        {
            if (!ModelState.IsValid) { TempData["Error"] = "Please correct the errors."; return RedirectToAction("UserManagement", new { editId = model.UserId }); }
            var user = _context.Users.Include(u => u.Role).FirstOrDefault(u => u.UserCode == model.UserId);
            if (user == null) { TempData["Error"] = "That user could not be found."; return RedirectToAction("UserManagement"); }

            // Super Admin accounts can never be edited from User Management.
            if (user.Role.Code == "SuperAdmin")
            {
                TempData["Error"] = "Super Admin accounts cannot be edited from User Management.";
                return RedirectToAction("UserManagement");
            }
            var denied = ManageDeniedMessage(user);
            if (denied != null) { TempData["Error"] = denied; return RedirectToAction("UserManagement"); }

            var email = (model.Email ?? "").Trim();
            if (!InputRules.IsValidEmail(email)) { TempData["Error"] = InputRules.EmailMessage; return RedirectToAction("UserManagement"); }
            if (string.IsNullOrWhiteSpace(model.FullName)) { TempData["Error"] = "The full name cannot be empty."; return RedirectToAction("UserManagement"); }
            if (_context.Users.Any(u => u.UserId != user.UserId && u.Email == email))
            {
                TempData["Error"] = $"Another account already uses the email '{email}'.";
                return RedirectToAction("UserManagement");
            }
            var allowedStatus = new[] { "Active", "Pending", "Inactive" };
            if (!allowedStatus.Contains(model.Status)) { TempData["Error"] = "Invalid status."; return RedirectToAction("UserManagement"); }
            if (user.UserId == CurrentUserId && model.Status != "Active") { TempData["Error"] = "You cannot deactivate your own account."; return RedirectToAction("UserManagement"); }

            var parts = (model.FullName ?? "").Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0) user.FirstName = parts[0];
            if (parts.Length > 1) user.Surname = parts[1];
            user.Email = email;
            user.Identifier = string.IsNullOrWhiteSpace(model.Identifier) ? null : model.Identifier.Trim();
            user.Status = model.Status;
            ForgetActiveCache(user.UserId);
            // NOTE: Role is deliberately NOT changed here. A role change needs a different
            // profile table (Doctor/Technician/Patient...) so roles are fixed at creation.
            try
            {
                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                HttpContext.RequestServices.GetService<ILogger<AdminController>>()?.LogError(ex, "UpdateUser failed");
                TempData["Error"] = "We could not save the changes right now. Please try again.";
                return RedirectToAction("UserManagement");
            }
            LogAction(CurrentUserEmail, CurrentUserRoleCode, "User updated", $"User '{model.FullName}' details updated", "info");
            TempData["Success"] = $"User '{model.FullName}' updated.";
            return RedirectToAction("UserManagement");
        }

        // E-mails the user a one time link to choose a new password.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ResetPassword(string userId)
        {
            if (IsSuperAdminAccount(userId)) { TempData["Error"] = "Super Admin accounts cannot be managed here."; return RedirectToAction("UserManagement"); }
            var user = _context.Users.Include(u => u.Role).FirstOrDefault(u => u.UserCode == userId);
            if (user == null) { TempData["Error"] = "That user could not be found."; return RedirectToAction("UserManagement"); }
            var denied = ManageDeniedMessage(user);
            if (denied != null) { TempData["Error"] = denied; return RedirectToAction("UserManagement"); }

            // Same one-time, 30-minute link as "Forgot password": the admin never sees or sets the password.
            var rawToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');
            _context.PasswordResetTokens.Add(new Project.Models.Entities.PasswordResetToken
            {
                UserId = user.UserId,
                Token = rawToken,
                ExpiresOn = DateTime.UtcNow.AddMinutes(30)
            });
            _context.SaveChanges();

            var link = Url.Action("ResetPassword", "Account", new { token = rawToken }, Request.Scheme);
            var body = $@"<p>Hi {System.Net.WebUtility.HtmlEncode(user.FirstName)},</p>
                <p>An administrator has started a password reset for your HemaFlow account. Click the link below to choose a new password - it expires in 30 minutes and can only be used once.</p>
                <p><a href=""{link}"">Reset your password</a></p>
                <p>If you did not expect this, please contact the laboratory.</p>";
            try
            {
                var mail = HttpContext.RequestServices.GetService<EmailService>();
                if (mail == null) throw new InvalidOperationException("E-mail service is not configured.");
                mail.SendEmail(user.Email, "Reset your HemaFlow password", body);
            }
            catch (Exception ex)
            {
                HttpContext.RequestServices.GetService<ILogger<AdminController>>()?.LogError(ex, "Admin password reset e-mail failed for user {UserId}", user.UserId);
                TempData["Error"] = $"The reset e-mail to {user.Email} could not be sent. Check the e-mail settings and try again.";
                return RedirectToAction("UserManagement");
            }
            LogAction(CurrentUserEmail, CurrentUserRoleCode, "Password reset", $"Password reset e-mail sent to {user.Email}", "info");
            TempData["Success"] = $"Password reset e-mail sent to {user.Email}.";
            return RedirectToAction("UserManagement");
        }

        // Blocks the user from signing in. An admin cannot deactivate themselves.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeactivateUser(string userId)
        {
            if (IsSuperAdminAccount(userId)) { TempData["Error"] = "Super Admin accounts cannot be managed here."; return RedirectToAction("UserManagement"); }
            var user = _context.Users.Include(u => u.Role).FirstOrDefault(u => u.UserCode == userId);
            if (user != null && user.UserId == CurrentUserId) { TempData["Error"] = "You cannot deactivate your own account."; return RedirectToAction("UserManagement"); }
            var deniedDeactivate = user == null ? null : ManageDeniedMessage(user);
            if (deniedDeactivate != null) { TempData["Error"] = deniedDeactivate; return RedirectToAction("UserManagement"); }
            if (user != null)
            {
                user.Status = "Inactive";
                _context.SaveChanges();
                ForgetActiveCache(user.UserId);
                var fullName = $"{user.FirstName} {user.Surname}";
                AddArchive("User", user.UserId, fullName, $"Role: {user.Role.DisplayName ?? user.Role.Code}", "Deactivated by administrator.", CurrentUserEmail);
                LogAction(CurrentUserEmail, CurrentUserRoleCode, "User deactivated", $"Account for '{fullName}' deactivated", "warning");
                TempData["Success"] = $"User '{fullName}' deactivated.";
            }
            return RedirectToAction("UserManagement");
        }

        // Lets a deactivated user sign in again.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ActivateUser(string userId)
        {
            if (IsSuperAdminAccount(userId)) { TempData["Error"] = "Super Admin accounts cannot be managed here."; return RedirectToAction("UserManagement"); }
            var user = _context.Users.Include(u => u.Role).FirstOrDefault(u => u.UserCode == userId);
            var deniedActivate = user == null ? null : ManageDeniedMessage(user);
            if (deniedActivate != null) { TempData["Error"] = deniedActivate; return RedirectToAction("UserManagement"); }
            if (user != null)
            {
                user.Status = "Active";
                _context.SaveChanges();
                var fullName = $"{user.FirstName} {user.Surname}";
                LogAction(CurrentUserEmail, CurrentUserRoleCode, "User activated", $"Account for '{fullName}' reactivated", "success");
                TempData["Success"] = $"User '{fullName}' activated.";
            }
            return RedirectToAction("UserManagement");
        }

        // ═══════════════════════════════════════════════════════════════════
        // SUPER ADMIN ONLY - create new Admin accounts
        // ═══════════════════════════════════════════════════════════════════

        // Next free ADM-### code. The 4 digit ADM-0001 account is skipped when counting.
        private string GenerateNextAdminUserCode()
        {
            var existing = _context.Users
                .Where(u => u.UserCode.StartsWith("ADM-") && u.UserCode != "ADM-0001")
                .Select(u => u.UserCode)
                .ToList();

            int max = 0;
            foreach (var code in existing)
            {
                if (int.TryParse(code.Replace("ADM-", ""), out int n) && n > max) max = n;
            }
            return $"ADM-{(max + 1):000}";
        }

        // Create Admin form (Super Admin only).
        [Authorize(Roles = "SuperAdmin")]
        [HttpGet]
        public IActionResult CreateAdminUser() => View(new CreateAdminUserViewModel());

        // Creates an Admin account with a profile row.
        [Authorize(Roles = "SuperAdmin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateAdminUser(CreateAdminUserViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            if (model.Password != model.ConfirmPassword)
            {
                ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
                return View(model);
            }

            if (!PasswordPolicy.IsValid(model.Password))
            {
                ModelState.AddModelError("Password", PasswordPolicy.Message);
                return View(model);
            }
            if (!InputRules.IsValidEmail(model.Email))
            {
                ModelState.AddModelError("Email", InputRules.EmailMessage);
                return View(model);
            }

            if (_context.Users.Any(u => u.Email == model.Email.Trim()))
            {
                ModelState.AddModelError("Email", "An account with this email already exists.");
                return View(model);
            }

            var adminRole = _context.Roles.FirstOrDefault(r => r.Code == "Admin");
            if (adminRole == null)
            {
                TempData["Error"] = "Admin role not found - check the Role lookup table.";
                return View(model);
            }

            var newCode = GenerateNextAdminUserCode();
            var newUser = new GenUser
            {
                RoleId = adminRole.RoleId,
                FirstName = model.FirstName.Trim(),
                Surname = model.Surname.Trim(),
                Email = model.Email.Trim(),
                UserCode = newCode,
                Identifier = "ADM-SYS-" + newCode.Replace("ADM-", ""),
                Status = "Active",
                PasswordHash = "" // set below, needs the entity instance for the hasher
            };
            var hasher = new PasswordHasher<GenUser>();
            newUser.PasswordHash = hasher.HashPassword(newUser, model.Password);

            _context.Users.Add(newUser);
            _context.SaveChanges();
            // Every admin gets a profile row so the Profile page has somewhere to save details
            _context.AdminProfiles.Add(new GenAdminProfile { UserId = newUser.UserId, AdminCode = newCode });
            _context.SaveChanges();

            LogAction(CurrentUserEmail, CurrentUserRoleCode, "Admin account created", $"New Admin account created: {newUser.FirstName} {newUser.Surname} ({newUser.Email})", "success");
            TempData["Success"] = $"Admin account for '{newUser.FirstName} {newUser.Surname}' created. Code: {newUser.UserCode}.";
            return RedirectToAction("UserManagement");
        }

        // Next free LM-### code for a Lab Manager.
        private string GenerateNextLabManagerCode()
        {
            var existing = _context.Users.Where(u => u.UserCode.StartsWith("LM-")).Select(u => u.UserCode).ToList();
            int max = 0;
            foreach (var code in existing)
                if (int.TryParse(code.Substring(3), out int n) && n > max) max = n;
            return $"LM-{(max + 1):000}";
        }

        // Create Lab Manager form (Admin and Super Admin).
        [HttpGet]
        public IActionResult CreateLabManager() => View(new CreateAdminUserViewModel());

        // Creates a Lab Manager account with a profile row. They must change the password at first login.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateLabManager(CreateAdminUserViewModel model)
        {
            if (!ModelState.IsValid) return View(model);
            if (model.Password != model.ConfirmPassword)
            {
                ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");
                return View(model);
            }
            if (!PasswordPolicy.IsValid(model.Password))
            {
                ModelState.AddModelError("Password", PasswordPolicy.Message);
                return View(model);
            }
            if (!InputRules.IsValidEmail(model.Email))
            {
                ModelState.AddModelError("Email", InputRules.EmailMessage);
                return View(model);
            }
            if (_context.Users.Any(u => u.Email == model.Email.Trim()))
            {
                ModelState.AddModelError("Email", "An account with this email already exists.");
                return View(model);
            }
            var role = _context.Roles.FirstOrDefault(r => r.Code == "LabManager");
            if (role == null)
            {
                TempData["Error"] = "Lab Manager role not found - check the Role lookup table.";
                return View(model);
            }

            var code = GenerateNextLabManagerCode();
            var user = new GenUser
            {
                RoleId = role.RoleId,
                FirstName = model.FirstName.Trim(),
                Surname = model.Surname.Trim(),
                Email = model.Email.Trim(),
                UserCode = code,
                Identifier = "EMP: " + code,
                Status = "Active",
                MustChangePassword = true,
                CreatedOn = DateTime.UtcNow,
                PasswordHash = ""
            };
            user.PasswordHash = new PasswordHasher<GenUser>().HashPassword(user, model.Password);
            _context.Users.Add(user);
            _context.SaveChanges();
            _context.LabManagerProfiles.Add(new Project.Models.Entities.LabManagerProfile { UserId = user.UserId, ManagerCode = code });
            _context.SaveChanges();

            LogAction(CurrentUserEmail, CurrentUserRoleCode, "Lab Manager created", $"New Lab Manager account: {user.FirstName} {user.Surname} ({user.Email})", "success");
            TempData["Success"] = $"Lab Manager '{user.FirstName} {user.Surname}' created. Code: {code}.";
            return RedirectToAction("UserManagement");
        }

        // ═══════════════════════════════════════════════════════════════════
        // POST - ARCHIVE / RESTORE
        // ═══════════════════════════════════════════════════════════════════

        // Puts an archived record back on its list.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RestoreEntity(string entityType, int entityId, string? returnTo = null)
        {
            string name = entityType;
            switch (entityType)
            {
                case "Condition":
                    var cond = _context.MedicalConditions.FirstOrDefault(c => c.ConditionId == entityId);
                    if (cond != null) { cond.IsArchived = false; name = cond.Name; _context.SaveChanges(); }
                    break;
                case "Allergy":
                    var all = _context.Allergies.FirstOrDefault(a => a.AllergyId == entityId);
                    if (all != null) { all.IsArchived = false; name = all.Name; _context.SaveChanges(); }
                    break;
                case "Medication":
                    var med = _context.Medications.FirstOrDefault(m => m.MedicationId == entityId);
                    if (med != null) { med.IsArchived = false; name = med.Name; _context.SaveChanges(); }
                    break;
                case "Category":
                    var cat = _context.Categories.FirstOrDefault(c => c.CategoryId == entityId);
                    if (cat != null) { cat.IsArchived = false; name = cat.Name; _context.SaveChanges(); }
                    break;
                case "User":
                    var user = _context.Users.FirstOrDefault(u => u.UserId == entityId);
                    if (user != null) { user.Status = "Active"; name = $"{user.FirstName} {user.Surname}"; _context.SaveChanges(); }
                    break;
            }
            var archiveRows = _context.ArchivedRecords.Where(r => r.EntityType == entityType && r.EntityId == entityId).ToList();
            if (archiveRows.Count > 0) { _context.ArchivedRecords.RemoveRange(archiveRows); _context.SaveChanges(); }
            LogAction(CurrentUserEmail, CurrentUserRoleCode, "Record restored", $"'{name}' ({entityType}) restored from archive", "success");
            TempData["Success"] = $"'{name}' restored successfully.";
            var backTo = new[] { "GlobalCategories", "MedicalConditions", "Allergies", "Medications" };
            if (returnTo != null && backTo.Contains(returnTo)) return RedirectToAction(returnTo);
            return RedirectToAction("SystemArchive");
        }

        // ═══════════════════════════════════════════════════════════════════
        // PDF EXPORTS
        // ═══════════════════════════════════════════════════════════════════

        // PDF of the staff list.
        public IActionResult ExportUsers()
        {
            var bytes = GetPdf().GenerateUserListReport(GetManagedUsers().Where(u => !u.IsArchived).ToList(), "All");
            return File(bytes, "application/pdf", $"HemaFlow_Users_{DateTime.Now:yyyyMMdd}.pdf");
        }

        // PDF of the conditions list.
        public IActionResult ExportConditions()
        {
            var bytes = GetPdf().GenerateConditionsReport(GetConditions());
            return File(bytes, "application/pdf", $"HemaFlow_Conditions_{DateTime.Now:yyyyMMdd}.pdf");
        }

        // PDF of the allergies list.
        public IActionResult ExportAllergies()
        {
            var bytes = GetPdf().GenerateAllergiesReport(GetAllergies());
            return File(bytes, "application/pdf", $"HemaFlow_Allergies_{DateTime.Now:yyyyMMdd}.pdf");
        }

        // PDF of the medications list.
        public IActionResult ExportMedications()
        {
            var bytes = GetPdf().GenerateMedicationsReport(GetMedications());
            return File(bytes, "application/pdf", $"HemaFlow_Medications_{DateTime.Now:yyyyMMdd}.pdf");
        }
    }
}