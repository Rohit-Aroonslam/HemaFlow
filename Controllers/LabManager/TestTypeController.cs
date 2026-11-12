using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project.Data;
using Microsoft.EntityFrameworkCore;
using Project.Models.Entities;
using Project.Models.LabManager;
using DbTestType = Project.Models.Entities.TestType;
using VmTestType = Project.Models.LabManager.TestType;
using VmTestCategory = Project.Models.LabManager.TestCategory;
using VmSampleType = Project.Models.LabManager.SampleType;
using VmConsumable = Project.Models.LabManager.Consumable;
using VmTechnician = Project.Models.LabManager.Technician;
using VmTestTypeConsumable = Project.Models.LabManager.TestTypeConsumable;

/*
 =========================================================
 TestTypeController - Lab Manager, spec §2
 ✅ DB: reads and writes GRP-04-13 via ApplicationDbContext
        dbo.TestType         test name, category, sample type,
                             unit, normal range, TurnaroundMinutes
        dbo.TestConsumable   consumables used (+ QuantityPerTest)
        dbo.TechnicianTestType / TechnicianProfile / Users
                             "which technicians are assigned" (spec §2)
 
 The views are unchanged: they still use the
 Models/LabManager classes, so this controller maps between
 those and the database models.
 
 Spec rules enforced here:
   • Test name unique (also enforced by UQ_TestType_Name,
     across active AND archived rows)
   • Each test belongs to one (active) category and sample type
   • Normal min must not be greater than normal max
   • Turnaround time is in minutes
 
 ✅ Activity is logged to dbo.StaffActivityLog (LabManagerActivityLog).
 =========================================================
*/
[Authorize(Roles = "LabManager")]
public class TestTypeController : Controller
{
    private readonly ApplicationDbContext _context;
    private LabManagerActivityLog Activity => new(_context, User);

    public TestTypeController(ApplicationDbContext context)
    {
        _context = context;
    }

    // ── Mapping ─────────────────────────────────────────
    private static VmTestType ToViewModel(DbTestType t) => new VmTestType
    {
        Id = t.TestTypeId,
        Name = t.Name,
        TestCategoryId = t.TestCategoryId,
        TestCategory = t.TestCategory == null ? null! : new VmTestCategory
        {
            Id = t.TestCategory.TestCategoryId,
            Name = t.TestCategory.Name,
            Description = t.TestCategory.Description ?? string.Empty
        },
        SampleTypeId = t.SampleTypeId,
        SampleType = t.SampleType == null ? null! : new VmSampleType
        {
            Id = t.SampleType.SampleTypeId,
            Name = t.SampleType.Name
        },
        Units = t.Unit ?? string.Empty,
        NormalMin = t.NormalMin ?? 0m,
        NormalMax = t.NormalMax ?? 0m,
        TurnaroundTime = t.TurnaroundMinutes ?? 0,
        IsArchived = t.IsArchived,
        TestTypeConsumables = t.TestConsumables.Select(tc => new VmTestTypeConsumable
        {
            TestTypeId = tc.TestTypeId,
            ConsumableId = tc.ConsumableId,
            QuantityRequired = tc.QuantityPerTest
        }).ToList()
    };

    // ── Dropdowns / checkbox list for Create and Edit ───
    private async Task LoadLookups()
    {
        ViewBag.Categories = (await _context.TestCategories.AsNoTracking()
                .Where(c => !c.IsArchived).OrderBy(c => c.Name).ToListAsync())
            .Select(c => new VmTestCategory { Id = c.TestCategoryId, Name = c.Name, Description = c.Description ?? "" })
            .ToList();

        ViewBag.SampleTypes = (await _context.SampleTypes.AsNoTracking()
                .Where(s => !s.IsArchived).OrderBy(s => s.Name).ToListAsync())
            .Select(s => new VmSampleType { Id = s.SampleTypeId, Name = s.Name })
            .ToList();

        // Edit/Create views cast this to List<Models.LabManager.Consumable>
        ViewBag.Consumables = (await _context.Consumables.AsNoTracking()
                .Where(c => !c.IsArchived).OrderBy(c => c.Name).ToListAsync())
            .Select(c => new VmConsumable
            {
                Id = c.ConsumableId,
                Name = c.Name,
                ReorderLevel = c.ReorderLevel,
                QuantityOnHand = c.QuantityOnHand,
                SupplierId = c.SupplierId
            })
            .ToList();
    }

    // Reads quantities[<id>]=<value> pairs directly from the raw form,
    // avoiding the Dictionary<int,int> binder bug when the form has no matching keys.
    private Dictionary<int, int> ReadQuantities()
    {
        var result = new Dictionary<int, int>();
        foreach (var key in Request.Form.Keys)
        {
            if (key.StartsWith("quantities[") && key.EndsWith("]"))
            {
                var idPart = key.Substring("quantities[".Length, key.Length - "quantities[".Length - 1);
                if (int.TryParse(idPart, out int consumableId) &&
                    int.TryParse(Request.Form[key], out int qty))
                {
                    result[consumableId] = qty;
                }
            }
        }
        return result;
    }

    // Shared validation for Create and Edit
    private async Task ValidateTestType(VmTestType model, int excludeId = 0)
    {
        var clash = await _context.TestTypes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Name == model.Name && t.TestTypeId != excludeId);
        if (clash != null)
        {
            ModelState.AddModelError("Name", clash.IsArchived
                ? "An archived test type with this name already exists. Restore it from View Archived instead."
                : "A test type with this name already exists.");
        }

        if (!await _context.TestCategories.AnyAsync(c => c.TestCategoryId == model.TestCategoryId && !c.IsArchived))
            ModelState.AddModelError("TestCategoryId", "Please select a valid category.");

        if (!await _context.SampleTypes.AnyAsync(s => s.SampleTypeId == model.SampleTypeId && !s.IsArchived))
            ModelState.AddModelError("SampleTypeId", "Please select a valid sample type.");

        if (model.NormalMin > model.NormalMax)
            ModelState.AddModelError("NormalMax", "Normal max must be greater than or equal to normal min.");
    }

    // Keeps only real, active consumables the user ticked, with qty >= 1
    private async Task<Dictionary<int, int>> SelectedConsumableQuantities(List<int>? selectedConsumables)
    {
        var quantities = ReadQuantities();
        var ids = (selectedConsumables ?? new List<int>()).Distinct().ToList();

        var validIds = await _context.Consumables
            .Where(c => ids.Contains(c.ConsumableId) && !c.IsArchived)
            .Select(c => c.ConsumableId)
            .ToListAsync();

        return validIds.ToDictionary(
            id => id,
            id => quantities.TryGetValue(id, out var q) && q > 0 ? q : 1);
    }

    // ── List ────────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var data = await _context.TestTypes.AsNoTracking()
            .Include(t => t.TestCategory)
            .Include(t => t.SampleType)
            .Include(t => t.TestConsumables)
            .Where(t => !t.IsArchived)
            .OrderBy(t => t.Name)
            .ToListAsync();

        return View(data.Select(ToViewModel).ToList());
    }

    public async Task<IActionResult> Archived()
    {
        var data = await _context.TestTypes.AsNoTracking()
            .Include(t => t.TestCategory)
            .Include(t => t.SampleType)
            .Include(t => t.TestConsumables)
            .Where(t => t.IsArchived)
            .OrderBy(t => t.Name)
            .ToListAsync();

        return View(data.Select(ToViewModel).ToList());
    }

    // ── Create ──────────────────────────────────────────
    public async Task<IActionResult> Create()
    {
        await LoadLookups();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VmTestType model, List<int> selectedConsumables)
    {
        model.Name = model.Name?.Trim() ?? string.Empty;
        model.Units = model.Units?.Trim() ?? string.Empty;

        await ValidateTestType(model);
        var wanted = await SelectedConsumableQuantities(selectedConsumables);
        if (!ModelState.IsValid)
        {
            await LoadLookups();
            KeepSelection(wanted);
            return View(model);
        }

        var entity = new DbTestType
        {
            Name = model.Name,
            TestCategoryId = model.TestCategoryId,
            SampleTypeId = model.SampleTypeId,
            Unit = model.Units,
            NormalMin = model.NormalMin,
            NormalMax = model.NormalMax,
            TurnaroundMinutes = model.TurnaroundTime,
            IsArchived = false
        };

        foreach (var (consumableId, qty) in wanted)
            entity.TestConsumables.Add(new TestConsumable { ConsumableId = consumableId, QuantityPerTest = qty });

        _context.TestTypes.Add(entity);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("Name", "A test type with this name already exists.");
            await LoadLookups();
            KeepSelection(wanted);
            return View(model);
        }

        await Activity.LogAsync("Created", "TestType", model.Name);
        TempData["SuccessMessage"] = "Test type successfully added.";
        return RedirectToAction("Index");
    }

    // Keeps the user's ticks and quantities when the Create form is redisplayed with errors
    private void KeepSelection(Dictionary<int, int> wanted)
    {
        ViewBag.SelectedConsumableIds = wanted.Keys.ToList();
        ViewBag.ConsumableQuantities  = wanted;
    }

    // ── Edit ────────────────────────────────────────────
    public async Task<IActionResult> Edit(int id)
    {
        var item = await _context.TestTypes.AsNoTracking()
            .Include(t => t.TestConsumables)
            .FirstOrDefaultAsync(t => t.TestTypeId == id);
        if (item == null) return NotFound();

        await LoadLookups();
        ViewBag.SelectedConsumableIds = item.TestConsumables.Select(tc => tc.ConsumableId).ToList();
        ViewBag.ConsumableQuantities = item.TestConsumables.ToDictionary(tc => tc.ConsumableId, tc => tc.QuantityPerTest);
        return View(ToViewModel(item));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(VmTestType model, List<int> selectedConsumables)
    {
        model.Name = model.Name?.Trim() ?? string.Empty;
        model.Units = model.Units?.Trim() ?? string.Empty;

        var existing = await _context.TestTypes
            .Include(t => t.TestConsumables)
            .FirstOrDefaultAsync(t => t.TestTypeId == model.Id);
        if (existing == null) return NotFound();

        var wanted = await SelectedConsumableQuantities(selectedConsumables);

        await ValidateTestType(model, excludeId: model.Id);
        if (!ModelState.IsValid)
        {
            await LoadLookups();
            // Keep the user's ticks and quantities on the redisplayed form
            ViewBag.SelectedConsumableIds = wanted.Keys.ToList();
            ViewBag.ConsumableQuantities = wanted;
            return View(model);
        }

        existing.Name = model.Name;
        existing.TestCategoryId = model.TestCategoryId;
        existing.SampleTypeId = model.SampleTypeId;
        existing.Unit = model.Units;
        existing.NormalMin = model.NormalMin;
        existing.NormalMax = model.NormalMax;
        existing.TurnaroundMinutes = model.TurnaroundTime;

        // Sync consumables: remove unticked, update quantities, add new ones
        foreach (var tc in existing.TestConsumables.ToList())
        {
            if (!wanted.ContainsKey(tc.ConsumableId))
                _context.TestConsumables.Remove(tc);
            else
                tc.QuantityPerTest = wanted[tc.ConsumableId];
        }
        var already = existing.TestConsumables.Select(tc => tc.ConsumableId).ToHashSet();
        foreach (var (consumableId, qty) in wanted.Where(w => !already.Contains(w.Key)))
            existing.TestConsumables.Add(new TestConsumable { ConsumableId = consumableId, QuantityPerTest = qty });

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("Name", "A test type with this name already exists.");
            await LoadLookups();
            ViewBag.SelectedConsumableIds = wanted.Keys.ToList();
            ViewBag.ConsumableQuantities = wanted;
            return View(model);
        }

        await Activity.LogAsync("Updated", "TestType", model.Name);
        TempData["SuccessMessage"] = "Test type updated.";
        return RedirectToAction("Index");
    }

    // ── Archive / Restore (soft delete) ─────────────────
    public async Task<IActionResult> Archive(int id)
    {
        var item = await _context.TestTypes.FindAsync(id);
        if (item == null) return NotFound();

        item.IsArchived = true;
        await _context.SaveChangesAsync();

        await Activity.LogAsync("Archived", "TestType", item.Name);
        TempData["SuccessMessage"] = "Test type archived.";
        return RedirectToAction("Index");
    }

    public async Task<IActionResult> Unarchive(int id)
    {
        var item = await _context.TestTypes.FindAsync(id);
        if (item == null) return NotFound();

        item.IsArchived = false;
        await _context.SaveChangesAsync();

        await Activity.LogAsync("Restored", "TestType", item.Name);
        TempData["SuccessMessage"] = "Test type restored.";
        return RedirectToAction("Archived");
    }

    // ── Technicians assigned to this test type (spec §2) ─
    public async Task<IActionResult> Technicians(int id)
    {
        var testType = await _context.TestTypes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TestTypeId == id);
        if (testType == null) return NotFound();

        var technicians = await _context.TechnicianTestTypes.AsNoTracking()
            .Where(tt => tt.TestTypeId == id
                      && !tt.TechnicianUser.IsArchived
                      && !tt.TechnicianUser.User.IsArchived)
            .OrderBy(tt => tt.TechnicianUser.User.Surname)
            .Select(tt => new VmTechnician
            {
                Id = tt.TechnicianUserId,
                Name = tt.TechnicianUser.User.FirstName,
                Surname = tt.TechnicianUser.User.Surname,
                EmployeeNumber = tt.TechnicianUser.EmployeeCode,
                Email = tt.TechnicianUser.User.Email,
                ContactNumber = tt.TechnicianUser.User.Phone ?? ""
            })
            .ToListAsync();

        // Active technicians NOT yet assigned - offered in the "Assign Technicians" panel
        var assignedIds = technicians.Select(t => t.Id).ToList();
        ViewBag.AvailableTechnicians = await _context.TechnicianProfiles.AsNoTracking()
            .Where(p => !p.IsArchived && !p.User.IsArchived && !assignedIds.Contains(p.UserId))
            .OrderBy(p => p.User.Surname).ThenBy(p => p.User.FirstName)
            .Select(p => new VmTechnician
            {
                Id = p.UserId,
                Name = p.User.FirstName,
                Surname = p.User.Surname,
                EmployeeNumber = p.EmployeeCode,
                Email = p.User.Email
            })
            .ToListAsync();

        ViewBag.TestTypeName = testType.Name;
        ViewBag.TestTypeId = testType.TestTypeId;
        ViewBag.TestTypeArchived = testType.IsArchived;
        return View(technicians);
    }

    // ── Assign existing technicians to this test type (straight from the test type) ─
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignTechnicians(int id, List<int>? technicianIds)
    {
        var testType = await _context.TestTypes.FirstOrDefaultAsync(t => t.TestTypeId == id);
        if (testType == null) return NotFound();

        if (testType.IsArchived)
        {
            TempData["ErrorMessage"] = "Technicians can't be assigned to an archived test type.";
            return RedirectToAction("Technicians", new { id });
        }

        var wanted = (technicianIds ?? new List<int>()).Distinct().ToList();
        if (!wanted.Any())
        {
            TempData["WarningMessage"] = "Tick at least one technician to assign.";
            return RedirectToAction("Technicians", new { id });
        }

        // Only real, active technicians that are not already linked to this test type
        var toAdd = await _context.TechnicianProfiles
            .Where(p => wanted.Contains(p.UserId) && !p.IsArchived && !p.User.IsArchived
                     && !p.TechnicianTestTypes.Any(x => x.TestTypeId == id))
            .Select(p => new { p.UserId, Name = p.User.FirstName + " " + p.User.Surname })
            .ToListAsync();

        if (!toAdd.Any())
        {
            TempData["WarningMessage"] = "Those technicians are already assigned to this test type.";
            return RedirectToAction("Technicians", new { id });
        }

        foreach (var t in toAdd)
            _context.TechnicianTestTypes.Add(new Project.Models.Entities.TechnicianTestType { TechnicianUserId = t.UserId, TestTypeId = id });

        await _context.SaveChangesAsync();

        foreach (var t in toAdd)
            await Activity.LogAsync("Assigned", "TestType", testType.Name, $"Technician {t.Name} can now perform this test");

        TempData["SuccessMessage"] = toAdd.Count == 1
            ? $"{toAdd[0].Name} assigned to {testType.Name}."
            : $"{toAdd.Count} technicians assigned to {testType.Name}.";
        return RedirectToAction("Technicians", new { id });
    }

    // ── Remove a technician from this test type ─────────
    // Spec: every technician must keep at least one (active) test type.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveTechnician(int id, int technicianId)
    {
        var testType = await _context.TestTypes.AsNoTracking().FirstOrDefaultAsync(t => t.TestTypeId == id);
        if (testType == null) return NotFound();

        var link = await _context.TechnicianTestTypes
            .Include(x => x.TechnicianUser).ThenInclude(p => p.User)
            .FirstOrDefaultAsync(x => x.TestTypeId == id && x.TechnicianUserId == technicianId);
        if (link == null) return NotFound();

        var name = $"{link.TechnicianUser.User.FirstName} {link.TechnicianUser.User.Surname}";

        int otherActiveTypes = await _context.TechnicianTestTypes
            .CountAsync(x => x.TechnicianUserId == technicianId && x.TestTypeId != id && !x.TestType.IsArchived);
        if (otherActiveTypes == 0)
        {
            TempData["ErrorMessage"] = $"{name} can't be removed - this is their only test type. " +
                                       "Assign them another test type first.";
            return RedirectToAction("Technicians", new { id });
        }

        _context.TechnicianTestTypes.Remove(link);
        await _context.SaveChangesAsync();

        await Activity.LogAsync("Unassigned", "TestType", testType.Name, $"Technician {name} removed from this test");

        TempData["SuccessMessage"] = $"{name} removed from {testType.Name}.";
        return RedirectToAction("Technicians", new { id });
    }

}//end of class TestTypeController
