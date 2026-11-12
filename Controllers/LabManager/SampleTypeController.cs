using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project.Data;
using Microsoft.EntityFrameworkCore;
using Project.Models.Entities;
using Project.Models.LabManager;
using DbSampleType = Project.Models.Entities.SampleType;
using VmSampleType = Project.Models.LabManager.SampleType;

/*
 =========================================================
 SampleTypeController - Lab Manager, spec §2
 ("A list of sample types should be maintained")
 ✅ DB: reads and writes dbo.SampleType in GRP-04-13
        via ApplicationDbContext.
 
 Views unchanged: they still use Models/LabManager/SampleType
 (Id, Name), so this controller maps to/from the DB model
 (SampleTypeId, Name).
 
 Rules:
   • Name must be unique - checked against active AND
     archived sample types.
   • Cannot archive a sample type that an active test type
     still uses.
 
 ✅ Activity is logged to dbo.StaffActivityLog (LabManagerActivityLog).
 =========================================================
*/
[Authorize(Roles = "LabManager")]
public class SampleTypeController : Controller
{
    private readonly ApplicationDbContext _context;
    private LabManagerActivityLog Activity => new(_context, User);

    public SampleTypeController(ApplicationDbContext context)
    {
        _context = context;
    }

    private static VmSampleType ToViewModel(DbSampleType s) => new VmSampleType
    {
        Id = s.SampleTypeId,
        Name = s.Name,
        IsArchived = s.IsArchived
    };

    // Returns an error message if the name is taken, otherwise null.
    private async Task<string?> NameTakenMessage(string name, int excludeId = 0)
    {
        var clash = await _context.SampleTypes.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Name == name && s.SampleTypeId != excludeId);

        if (clash == null) return null;
        return clash.IsArchived
            ? "An archived sample type with this name already exists. Restore it from View Archived instead."
            : "A sample type with this name already exists.";
    }

    // ── List ────────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var data = await _context.SampleTypes.AsNoTracking()
            .Where(s => !s.IsArchived)
            .OrderBy(s => s.Name)
            .ToListAsync();

        return View(data.Select(ToViewModel).ToList());
    }

    public async Task<IActionResult> Archived()
    {
        var data = await _context.SampleTypes.AsNoTracking()
            .Where(s => s.IsArchived)
            .OrderBy(s => s.Name)
            .ToListAsync();

        return View(data.Select(ToViewModel).ToList());
    }

    // ── Create ──────────────────────────────────────────
    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VmSampleType model)
    {
        model.Name = model.Name?.Trim() ?? string.Empty;

        var taken = await NameTakenMessage(model.Name);
        if (taken != null) ModelState.AddModelError("Name", taken);

        if (!ModelState.IsValid) return View(model);

        _context.SampleTypes.Add(new DbSampleType { Name = model.Name, IsArchived = false });
        await _context.SaveChangesAsync();

        await Activity.LogAsync("Created", "SampleType", model.Name);
        TempData["SuccessMessage"] = "Sample type successfully added.";
        return RedirectToAction("Index");
    }

    // ── Edit ────────────────────────────────────────────
    public async Task<IActionResult> Edit(int id)
    {
        var item = await _context.SampleTypes.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SampleTypeId == id);
        if (item == null) return NotFound();

        return View(ToViewModel(item));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(VmSampleType model)
    {
        model.Name = model.Name?.Trim() ?? string.Empty;

        var existing = await _context.SampleTypes.FindAsync(model.Id);
        if (existing == null) return NotFound();

        var taken = await NameTakenMessage(model.Name, excludeId: model.Id);
        if (taken != null) ModelState.AddModelError("Name", taken);

        if (!ModelState.IsValid) return View(model);

        existing.Name = model.Name;
        await _context.SaveChangesAsync();

        await Activity.LogAsync("Updated", "SampleType", model.Name);
        TempData["SuccessMessage"] = "Sample type updated.";
        return RedirectToAction("Index");
    }

    // ── Archive / Restore (soft delete) ─────────────────
    public async Task<IActionResult> Archive(int id)
    {
        var item = await _context.SampleTypes.FindAsync(id);
        if (item == null) return NotFound();

        // Guard: don't silently orphan test types that rely on this sample type
        bool inUse = await _context.TestTypes.AnyAsync(t => t.SampleTypeId == id && !t.IsArchived);
        if (inUse)
        {
            TempData["ErrorMessage"] = "Cannot archive - one or more active test types still use this sample type.";
            return RedirectToAction("Index");
        }

        item.IsArchived = true;
        await _context.SaveChangesAsync();

        await Activity.LogAsync("Archived", "SampleType", item.Name);
        TempData["SuccessMessage"] = "Sample type archived.";
        return RedirectToAction("Index");
    }

    public async Task<IActionResult> Unarchive(int id)
    {
        var item = await _context.SampleTypes.FindAsync(id);
        if (item == null) return NotFound();

        item.IsArchived = false;
        await _context.SaveChangesAsync();

        await Activity.LogAsync("Restored", "SampleType", item.Name);
        TempData["SuccessMessage"] = "Sample type restored.";
        return RedirectToAction("Archived");
    }

}//end of class SampleTypeController
