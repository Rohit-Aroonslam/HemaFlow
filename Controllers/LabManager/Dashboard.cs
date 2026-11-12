using Project.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Models.Entities;
using Project.Models.LabManager;
using VmConsumable = Project.Models.LabManager.Consumable;
using VmOrder = Project.Models.LabManager.Order;
using VmSupplier = Project.Models.LabManager.Supplier;

/*
 =========================================================
 DashboardController - Lab Manager home
 ✅ DB: every stat card, the low-stock list and recent orders
        come from GRP-04-13. View unchanged.
 
 Low stock = within 10% of reorder level (spec §5):
   qty <= reorder * 1.1   →   qty * 10 <= reorder * 11  (integer-safe in SQL)
 =========================================================
*/
[Authorize(Roles = "LabManager")]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;

    public DashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    private static string OrderStatusToUi(string code) => code switch
    {
        "Placed" or "Draft" => "Ordered",
        "PartiallyReceived" => "Partial",
        "Received" => "Complete",
        _ => code
    };

    public async Task<IActionResult> Index()
    {
        var lowStockItems = (await _context.Consumables.AsNoTracking()
                .Where(c => !c.IsArchived && c.QuantityOnHand * 10 <= c.ReorderLevel * 11)
                .OrderBy(c => c.QuantityOnHand - c.ReorderLevel)      // most urgent first
                .ToListAsync())
            .Select(c => new VmConsumable
            {
                Id = c.ConsumableId,
                Name = c.Name,
                QuantityOnHand = c.QuantityOnHand,
                ReorderLevel = c.ReorderLevel,
                SupplierId = c.SupplierId
            })
            .ToList();

        var recentOrders = (await _context.Orders.AsNoTracking()
                .Include(o => o.Supplier)
                .Include(o => o.OrderStatus)
                .OrderByDescending(o => o.PlacedOn)
                .Take(5)
                .ToListAsync())
            .Select(o => new VmOrder
            {
                Id = o.OrderId,
                OrderNumber = o.OrderNumber ?? $"#{o.OrderId}",
                SupplierId = o.SupplierId,
                Supplier = new VmSupplier { Id = o.Supplier.SupplierId, Name = o.Supplier.Name },
                OrderDate = DateTime.SpecifyKind(o.PlacedOn, DateTimeKind.Utc).ToLocalTime(),
                Status = OrderStatusToUi(o.OrderStatus.Code)
            })
            .ToList();

        ViewBag.TotalCategories = await _context.TestCategories.CountAsync(c => !c.IsArchived);
        ViewBag.TotalTestTypes = await _context.TestTypes.CountAsync(t => !t.IsArchived);
        ViewBag.TotalConsumables = await _context.Consumables.CountAsync(c => !c.IsArchived);
        ViewBag.TotalLowStock = lowStockItems.Count;
        ViewBag.TotalOrders = await _context.Orders.CountAsync();
        ViewBag.TotalTechnicians = await _context.TechnicianProfiles.CountAsync(p => !p.IsArchived && !p.User.IsArchived);
        ViewBag.TotalDoctors = await _context.DoctorProfiles.CountAsync(p => !p.IsArchived && !p.User.IsArchived);
        ViewBag.TotalSuppliers = await _context.Suppliers.CountAsync(s => !s.IsArchived);
        ViewBag.LowStockItems = lowStockItems;
        ViewBag.RecentOrders = recentOrders;

        return View();
    }

}//end of class DashboardController
