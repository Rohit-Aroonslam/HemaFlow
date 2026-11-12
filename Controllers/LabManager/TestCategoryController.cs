using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project.Data;
using Microsoft.EntityFrameworkCore;
using Project.Models.Entities;
using Project.Models.LabManager;
using DbTestCategory = Project.Models.Entities.TestCategory;   // database table
using VmTestCategory = Project.Models.LabManager.TestCategory;  // model the views use

/*
 =========================================================
 TestCategoryController - Lab Manager, spec §1
 ✅ DB: reads and writes dbo.TestCategory in GRP-04-13
        via ApplicationDbContext.
 
 The views are unchanged: they still use
 Models/LabManager/TestCategory (Id, Name, Description),
 so this controller maps between that and the database
 model (TestCategoryId, Name, Description).
 
 Uniqueness: the DB enforces a unique category name
 (UQ_TestCategory_Name) across active AND archived rows,
 so the duplicate check here covers both, and tells the
 user to restore an archived category instead of
 creating a second one.
 
 ✅ Activity is logged to dbo.StaffActivityLog (LabManagerActivityLog).
 =========================================================
*/
[Authorize(Roles = "LabManager")]
public class TestCategoryController : Controller
{
    private readonly ApplicationDbContext _context;
    private LabManagerActivityLog Activity => new(_context, User);

    public TestCategoryController(ApplicationDbContext context)
    {
        _context = context;
    }

    // ── Mapping helpers ─────────────────────────────────
    private static VmTestCategory ToViewModel(DbTestCategory c) => new VmTestCategory
    {
        Id = c.TestCategoryId,
        Name = c.Name,
        Description = c.Description ?? string.Empty,
        IsArchived = c.IsArchived
    };

    // Returns an error message if the name is taken, otherwise null.
    private async Task<string?> NameTakenMessage(string name, int excludeId = 0)
    {
        var clash = await _context.TestCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Name == name && c.TestCategoryId != excludeId);

        if (clash == null) return null;
        return clash.IsArchived
            ? "An archived category with this name already exists. Restore it from View Archived instead."
            : "A category with this name already exists.";
    }

    // ── List ────────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var data = await _context.TestCategories
            .AsNoTracking()
            .Where(c => !c.IsArchived)
            .OrderBy(c => c.Name)
            .ToListAsync();

        return View(data.Select(ToViewModel).ToList());
    }

    public async Task<IActionResult> Archived()
    {
        var data = await _context.TestCategories
            .AsNoTracking()
            .Where(c => c.IsArchived)
            .OrderBy(c => c.Name)
            .ToListAsync();

        return View(data.Select(ToViewModel).ToList());
    }

    // ── Create ──────────────────────────────────────────
    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VmTestCategory model)
    {
        model.Name = model.Name?.Trim() ?? string.Empty;
        model.Description = model.Description?.Trim() ?? string.Empty;

        var taken = await NameTakenMessage(model.Name);
        if (taken != null) ModelState.AddModelError("Name", taken);

        if (!ModelState.IsValid) return View(model);

        _context.TestCategories.Add(new DbTestCategory
        {
            Name = model.Name,
            Description = model.Description,
            IsArchived = false
        });

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Unique index caught a duplicate saved between our check and the save
            ModelState.AddModelError("Name", "A category with this name already exists.");
            return View(model);
        }

        await Activity.LogAsync("Created", "TestCategory", model.Name);
        TempData["SuccessMessage"] = "Category successfully added.";
        return RedirectToAction("Index");
    }

    // ── Edit ────────────────────────────────────────────
    public async Task<IActionResult> Edit(int id)
    {
        var item = await _context.TestCategories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.TestCategoryId == id);
        if (item == null) return NotFound();

        return View(ToViewModel(item));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(VmTestCategory model)
    {
        model.Name = model.Name?.Trim() ?? string.Empty;
        model.Description = model.Description?.Trim() ?? string.Empty;

        var existing = await _context.TestCategories.FindAsync(model.Id);
        if (existing == null) return NotFound();

        var taken = await NameTakenMessage(model.Name, excludeId: model.Id);
        if (taken != null) ModelState.AddModelError("Name", taken);

        if (!ModelState.IsValid) return View(model);

        existing.Name = model.Name;
        existing.Description = model.Description;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("Name", "A category with this name already exists.");
            return View(model);
        }

        await Activity.LogAsync("Updated", "TestCategory", model.Name);
        TempData["SuccessMessage"] = "Category updated.";
        return RedirectToAction("Index");
    }

    // ── Archive / Restore (soft delete) ─────────────────
    public async Task<IActionResult> Archive(int id)
    {
        var item = await _context.TestCategories.FindAsync(id);
        if (item == null) return NotFound();

        item.IsArchived = true;
        await _context.SaveChangesAsync();

        await Activity.LogAsync("Archived", "TestCategory", item.Name);
        TempData["SuccessMessage"] = "Category archived.";
        return RedirectToAction("Index");
    }

    public async Task<IActionResult> Unarchive(int id)
    {
        var item = await _context.TestCategories.FindAsync(id);
        if (item == null) return NotFound();

        item.IsArchived = false;
        await _context.SaveChangesAsync();

        await Activity.LogAsync("Restored", "TestCategory", item.Name);
        TempData["SuccessMessage"] = "Category restored.";
        return RedirectToAction("Archived");
    }

}//end of class TestCategoryController
