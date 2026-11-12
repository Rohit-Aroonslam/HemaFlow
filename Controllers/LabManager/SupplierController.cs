using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project.Data;
using Microsoft.EntityFrameworkCore;
using Project.Models.Entities;
using Project.Models.LabManager;
using DbSupplier = Project.Models.Entities.Supplier;
using VmSupplier = Project.Models.LabManager.Supplier;

/*
 =========================================================
 SupplierController - Lab Manager, spec §4
 ✅ DB: reads and writes dbo.Supplier in GRP-04-13
        via ApplicationDbContext.
 
 Spec §4 field  ->  DB column
   Supplier name (unique)   Supplier.Name   (UQ_Supplier_Name)
   Contact person           Supplier.ContactPerson
   E-mail address           Supplier.ContactEmail
 
 Views unchanged: they still use Models/LabManager/Supplier
 (Id, Name, ContactPerson, Email).
 
 Rules:
   • Name unique - checked against active AND archived
     suppliers (the DB index covers both).
   • Name cannot be changed after creation (the Edit view
     shows it disabled, so it is NOT posted back - the
     controller keeps the stored name).
   • Cannot archive a supplier that still supplies active
     consumables.
 
 ✅ Activity is logged to dbo.StaffActivityLog (LabManagerActivityLog).
 =========================================================
*/
[Authorize(Roles = "LabManager")]
public class SupplierController : Controller
{
    private readonly ApplicationDbContext _context;
    private LabManagerActivityLog Activity => new(_context, User);

    public SupplierController(ApplicationDbContext context)
    {
        _context = context;
    }

    private static VmSupplier ToViewModel(DbSupplier s) => new VmSupplier
    {
        Id = s.SupplierId,
        Name = s.Name,
        ContactPerson = s.ContactPerson ?? string.Empty,
        Email = s.ContactEmail ?? string.Empty,
        IsArchived = s.IsArchived
    };

    // ── List ────────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var data = await _context.Suppliers.AsNoTracking()
            .Where(s => !s.IsArchived)
            .OrderBy(s => s.Name)
            .ToListAsync();

        return View(data.Select(ToViewModel).ToList());
    }

    public async Task<IActionResult> Archived()
    {
        var data = await _context.Suppliers.AsNoTracking()
            .Where(s => s.IsArchived)
            .OrderBy(s => s.Name)
            .ToListAsync();

        return View(data.Select(ToViewModel).ToList());
    }

    // ── Create ──────────────────────────────────────────
    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VmSupplier model)
    {
        model.Name = model.Name?.Trim() ?? string.Empty;
        model.ContactPerson = model.ContactPerson?.Trim() ?? string.Empty;
        model.Email = model.Email?.Trim() ?? string.Empty;

        var clash = await _context.Suppliers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Name == model.Name);
        if (clash != null)
        {
            ModelState.AddModelError("Name", clash.IsArchived
                ? "An archived supplier with this name already exists. Restore it from View Archived instead."
                : "A supplier with this name already exists.");
        }

        if (!ModelState.IsValid) return View(model);

        _context.Suppliers.Add(new DbSupplier
        {
            Name = model.Name,
            ContactPerson = model.ContactPerson,
            ContactEmail = model.Email,
            IsArchived = false
        });

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("Name", "A supplier with this name already exists.");
            return View(model);
        }

        await Activity.LogAsync("Created", "Supplier", model.Name);
        TempData["SuccessMessage"] = "Supplier successfully added.";
        return RedirectToAction("Index");
    }

    // ── Edit (contact person + e-mail only) ─────────────
    public async Task<IActionResult> Edit(int id)
    {
        var item = await _context.Suppliers.AsNoTracking()
            .FirstOrDefaultAsync(s => s.SupplierId == id);
        if (item == null) return NotFound();

        return View(ToViewModel(item));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(VmSupplier model)
    {
        var existing = await _context.Suppliers.FindAsync(model.Id);
        if (existing == null) return NotFound();

        // Name is a disabled field, so the browser never posts it.
        // Use the stored name and drop its "required" error.
        model.Name = existing.Name;
        ModelState.Remove(nameof(VmSupplier.Name));

        model.ContactPerson = model.ContactPerson?.Trim() ?? string.Empty;
        model.Email = model.Email?.Trim() ?? string.Empty;

        if (!ModelState.IsValid) return View(model);

        existing.ContactPerson = model.ContactPerson;
        existing.ContactEmail = model.Email;
        await _context.SaveChangesAsync();

        await Activity.LogAsync("Updated", "Supplier", existing.Name);
        TempData["SuccessMessage"] = "Supplier updated.";
        return RedirectToAction("Index");
    }

    // ── Archive / Restore (soft delete) ─────────────────
    public async Task<IActionResult> Archive(int id)
    {
        var item = await _context.Suppliers.FindAsync(id);
        if (item == null) return NotFound();

        // Guard: each consumable is supplied by one supplier (spec §3)
        bool inUse = await _context.Consumables.AnyAsync(c => c.SupplierId == id && !c.IsArchived);
        if (inUse)
        {
            TempData["ErrorMessage"] = "Cannot archive - this supplier still supplies active consumables.";
            return RedirectToAction("Index");
        }

        item.IsArchived = true;
        await _context.SaveChangesAsync();

        await Activity.LogAsync("Archived", "Supplier", item.Name);
        TempData["SuccessMessage"] = "Supplier archived.";
        return RedirectToAction("Index");
    }

    public async Task<IActionResult> Unarchive(int id)
    {
        var item = await _context.Suppliers.FindAsync(id);
        if (item == null) return NotFound();

        item.IsArchived = false;
        await _context.SaveChangesAsync();

        await Activity.LogAsync("Restored", "Supplier", item.Name);
        TempData["SuccessMessage"] = "Supplier restored.";
        return RedirectToAction("Archived");
    }

}//end of class SupplierController
