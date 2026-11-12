using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Project.Data;
using Microsoft.EntityFrameworkCore;
using Project.Models.Entities;
using Project.Models.LabManager;
using DbConsumable = Project.Models.Entities.Consumable;
using VmConsumable = Project.Models.LabManager.Consumable;
using VmSupplier = Project.Models.LabManager.Supplier;

/*
 =========================================================
 ConsumableController - Lab Manager, spec §3
 ✅ DB: reads and writes dbo.Consumable in GRP-04-13
        via ApplicationDbContext.
 
 Spec §3:
   • Consumable name (unique)    Consumable.Name (UQ_Consumable_Name)
   • Reorder level               Consumable.ReorderLevel
   • Quantity on hand            Consumable.QuantityOnHand
   • One supplier per consumable Consumable.SupplierId
   • Adjust stock: increase by n / decrease by n / set to n
 
 Views unchanged: they still use Models/LabManager/Consumable.
 
 Rules:
   • Name unique - checked against active AND archived rows.
   • Supplier must exist and not be archived.
   • Stock never goes below 0; adjustment value must be >= 0.
   • Edit changes name / supplier / reorder level only -
     stock is changed through Adjust Stock (the Edit form
     has no stock field).
   • Cannot archive a consumable that an active test type uses.
 
 ✅ Activity is logged to dbo.StaffActivityLog (LabManagerActivityLog).
 =========================================================
*/
[Authorize(Roles = "LabManager")]
public class ConsumableController : Controller
{
    private readonly ApplicationDbContext _context;
    private LabManagerActivityLog Activity => new(_context, User);

    public ConsumableController(ApplicationDbContext context)
    {
        _context = context;
    }

    // ── Mapping ─────────────────────────────────────────
    private static VmConsumable ToViewModel(DbConsumable c) => new VmConsumable
    {
        Id = c.ConsumableId,
        Name = c.Name,
        ReorderLevel = c.ReorderLevel,
        QuantityOnHand = c.QuantityOnHand,
        SupplierId = c.SupplierId,
        Supplier = c.Supplier == null ? null! : new VmSupplier
        {
            Id = c.Supplier.SupplierId,
            Name = c.Supplier.Name,
            ContactPerson = c.Supplier.ContactPerson ?? string.Empty,
            Email = c.Supplier.ContactEmail ?? string.Empty
        },
        IsArchived = c.IsArchived
    };

    // Active suppliers for the dropdown (+ the current one on Edit,
    // so an archived current supplier still shows as selected)
    private async Task LoadSuppliers(int? includeSupplierId = null)
    {
        ViewBag.Suppliers = (await _context.Suppliers.AsNoTracking()
                .Where(s => !s.IsArchived || s.SupplierId == includeSupplierId)
                .OrderBy(s => s.Name)
                .ToListAsync())
            .Select(s => new VmSupplier { Id = s.SupplierId, Name = s.Name })
            .ToList();
    }

    // The view model has navigation properties (Supplier, TestTypeConsumables,
    // OrderItems) that the forms never post. With nullable reference types on,
    // ASP.NET treats the non-nullable Supplier as [Required] and fails silently,
    // so drop any validation errors for those properties.
    private void IgnoreNavigationProperties()
    {
        foreach (var key in ModelState.Keys.Where(k =>
                     k == nameof(VmConsumable.Supplier) || k.StartsWith(nameof(VmConsumable.Supplier) + ".") ||
                     k.StartsWith(nameof(VmConsumable.TestTypeConsumables)) ||
                     k.StartsWith(nameof(VmConsumable.OrderItems))).ToList())
        {
            ModelState.Remove(key);
        }
    }

    private async Task ValidateConsumable(VmConsumable model, int excludeId = 0)
    {
        IgnoreNavigationProperties();

        var clash = await _context.Consumables.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Name == model.Name && c.ConsumableId != excludeId);
        if (clash != null)
        {
            ModelState.AddModelError("Name", clash.IsArchived
                ? "An archived consumable with this name already exists. Restore it from View Archived instead."
                : "A consumable with this name already exists.");
        }

        if (!await _context.Suppliers.AnyAsync(s => s.SupplierId == model.SupplierId && !s.IsArchived))
            ModelState.AddModelError("SupplierId", "Please select a valid supplier.");
    }

    // ── List ────────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var data = await _context.Consumables.AsNoTracking()
            .Include(c => c.Supplier)
            .Where(c => !c.IsArchived)
            .OrderBy(c => c.Name)
            .ToListAsync();

        return View(data.Select(ToViewModel).ToList());
    }

    public async Task<IActionResult> Archived()
    {
        var data = await _context.Consumables.AsNoTracking()
            .Include(c => c.Supplier)
            .Where(c => c.IsArchived)
            .OrderBy(c => c.Name)
            .ToListAsync();

        return View(data.Select(ToViewModel).ToList());
    }

    // ── Create ──────────────────────────────────────────
    public async Task<IActionResult> Create()
    {
        await LoadSuppliers();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VmConsumable model)
    {
        model.Name = model.Name?.Trim() ?? string.Empty;

        await ValidateConsumable(model);
        if (!ModelState.IsValid)
        {
            await LoadSuppliers();
            return View(model);
        }

        _context.Consumables.Add(new DbConsumable
        {
            Name = model.Name,
            SupplierId = model.SupplierId,
            ReorderLevel = model.ReorderLevel,
            QuantityOnHand = model.QuantityOnHand,
            IsArchived = false
        });

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("Name", "A consumable with this name already exists.");
            await LoadSuppliers();
            return View(model);
        }

        await Activity.LogAsync("Created", "Consumable", model.Name, $"Initial stock: {model.QuantityOnHand}");
        TempData["SuccessMessage"] = "Consumable successfully added.";
        return RedirectToAction("Index");
    }

    // ── Edit (name / supplier / reorder level) ──────────
    public async Task<IActionResult> Edit(int id)
    {
        var item = await _context.Consumables.AsNoTracking()
            .FirstOrDefaultAsync(c => c.ConsumableId == id);
        if (item == null) return NotFound();

        await LoadSuppliers(item.SupplierId);
        return View(ToViewModel(item));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(VmConsumable model)
    {
        model.Name = model.Name?.Trim() ?? string.Empty;

        var existing = await _context.Consumables.FindAsync(model.Id);
        if (existing == null) return NotFound();

        // The Edit form has no stock field - stock only changes via Adjust Stock
        ModelState.Remove(nameof(VmConsumable.QuantityOnHand));
        model.QuantityOnHand = existing.QuantityOnHand;

        await ValidateConsumable(model, excludeId: model.Id);

        // Keeping an already-archived supplier unchanged is allowed
        if (model.SupplierId == existing.SupplierId)
            ModelState.Remove(nameof(VmConsumable.SupplierId));

        if (!ModelState.IsValid)
        {
            await LoadSuppliers(existing.SupplierId);
            return View(model);
        }

        existing.Name = model.Name;
        existing.SupplierId = model.SupplierId;
        existing.ReorderLevel = model.ReorderLevel;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("Name", "A consumable with this name already exists.");
            await LoadSuppliers(existing.SupplierId);
            return View(model);
        }

        await Activity.LogAsync("Updated", "Consumable", model.Name);
        TempData["SuccessMessage"] = "Consumable updated.";
        return RedirectToAction("Index");
    }

    // ── Adjust stock (spec §3) ──────────────────────────
    // +10 / -10 quick buttons (links)
    public Task<IActionResult> AdjustStock(int id, string mode, int value)
        => DoAdjustStock(id, mode, value);

    // Manual Increase / Decrease / Set form
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AdjustStockPost(int id, string mode, int value)
        => DoAdjustStock(id, mode, value);

    private async Task<IActionResult> DoAdjustStock(int id, string mode, int value)
    {
        var item = await _context.Consumables.FindAsync(id);
        if (item == null) return NotFound();

        if (value < 0 || (mode != "increase" && mode != "decrease" && mode != "set"))
        {
            TempData["ErrorMessage"] = "Enter a stock value of 0 or more.";
            return RedirectToAction("Index");
        }

        int before = item.QuantityOnHand;
        switch (mode)
        {
            case "increase": item.QuantityOnHand += value; break;
            case "decrease": item.QuantityOnHand = Math.Max(0, item.QuantityOnHand - value); break;
            case "set": item.QuantityOnHand = value; break;
        }

        await _context.SaveChangesAsync();

        await Activity.LogAsync("StockAdjusted", "Consumable", item.Name,
            $"{mode} by {value} - was {before}, now {item.QuantityOnHand}");
        TempData["SuccessMessage"] = $"{item.Name}: stock {before} → {item.QuantityOnHand}.";
        return RedirectToAction("Index");
    }

    // ── Archive / Restore (soft delete) ─────────────────
    public async Task<IActionResult> Archive(int id)
    {
        var item = await _context.Consumables.FindAsync(id);
        if (item == null) return NotFound();

        // Guard: don't silently remove a consumable an active test needs
        bool inUse = await _context.TestConsumables
            .AnyAsync(tc => tc.ConsumableId == id && !tc.TestType.IsArchived);
        if (inUse)
        {
            TempData["ErrorMessage"] = "Cannot archive - one or more active test types use this consumable.";
            return RedirectToAction("Index");
        }

        item.IsArchived = true;
        await _context.SaveChangesAsync();

        await Activity.LogAsync("Archived", "Consumable", item.Name);
        TempData["SuccessMessage"] = "Consumable archived.";
        return RedirectToAction("Index");
    }

    public async Task<IActionResult> Unarchive(int id)
    {
        var item = await _context.Consumables.FindAsync(id);
        if (item == null) return NotFound();

        item.IsArchived = false;
        await _context.SaveChangesAsync();

        await Activity.LogAsync("Restored", "Consumable", item.Name);
        TempData["SuccessMessage"] = "Consumable restored.";
        return RedirectToAction("Archived");
    }

}//end of class ConsumableController
