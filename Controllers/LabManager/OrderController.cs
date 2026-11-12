using System.Net;
using Project.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Project.Models.Entities;
using Project.Models.LabManager;
using Project.Services;
using DbOrder = Project.Models.Entities.Order;
using DbOrderItem = Project.Models.Entities.OrderItem;
using VmOrder = Project.Models.LabManager.Order;
using VmOrderItem = Project.Models.LabManager.OrderItem;
using VmSupplier = Project.Models.LabManager.Supplier;
using VmConsumable = Project.Models.LabManager.Consumable;

/*
 =========================================================
 OrderController - Lab Manager, spec §5 (Consumable Orders)
 ✅ DB: dbo.[Order], dbo.OrderItem, dbo.Consumable in GRP-04-13
 
 Status names: the views show the SPEC names; the DB uses the
 team's lookup codes. Mapping:
     Order       Ordered   <-> Placed
                 Partial   <-> PartiallyReceived
                 Complete  <-> Received
                 Cancelled <-> Cancelled
     OrderItem   Ordered   <-> Pending (Backordered also shows as Ordered)
                 Received  <-> Received
                 Cancelled <-> Cancelled
 
 Spec §5 rules implemented:
   • Low-stock = within 10% of reorder level (qty <= reorder * 1.1)
   • Orders grouped by supplier - every item must be a consumable
     supplied by the order's supplier
   • Unique order number (ORD####, UQ_Order_OrderNumber)
   • Receive item: full qty received, stock updated, date captured
   • Cancel item / order: reason REQUIRED, date captured,
     supplier notified
   • Whole order can NOT be cancelled once any item has been
     received (Partial) - only its outstanding items can be cancelled
   • Order status recalculated after every item change;
     date complete captured when Complete
 
 E-mail (spec: "E-mail supplier automatically"):
   Uses the shared Services/EmailService. OFF by default so
   testing never e-mails the demo supplier addresses.
   Turn on in User Secrets / appsettings:
       "LabManager": {
         "SendSupplierEmails": true,
         "SupplierEmailRedirectTo": "you@example.com"   // optional:
       }                                                // send all supplier
                                                        // mail here instead
 ✅ Activity is logged to dbo.StaffActivityLog (LabManagerActivityLog).
 🟢 TEMP: until Lab Manager login is enforced, orders placed
          without a logged-in Lab Manager are recorded against LM-004.
 =========================================================
*/
[Authorize(Roles = "LabManager")]
public class OrderController : Controller
{
    private readonly ApplicationDbContext _context;
    private LabManagerActivityLog Activity => new(_context, User);
    private readonly EmailService _email;
    private readonly IConfiguration _config;
    private readonly ILogger<OrderController> _logger;

    public OrderController(ApplicationDbContext context, EmailService email,
                           IConfiguration config, ILogger<OrderController> logger)
    {
        _context = context;
        _email = email;
        _config = config;
        _logger = logger;
    }

    // ── Status mapping (UI spec names <-> DB codes) ─────
    private static string OrderStatusToUi(string code) => code switch
    {
        "Placed" => "Ordered",
        "Draft" => "Ordered",
        "PartiallyReceived" => "Partial",
        "Received" => "Complete",
        "Cancelled" => "Cancelled",
        _ => code
    };

    private static string? OrderStatusFromUi(string ui) => ui switch
    {
        "Ordered" => "Placed",
        "Partial" => "PartiallyReceived",
        "Complete" => "Received",
        "Cancelled" => "Cancelled",
        _ => null
    };

    private static string ItemStatusToUi(string code) => code switch
    {
        "Pending" => "Ordered",
        "Backordered" => "Ordered",
        "Received" => "Received",
        "Cancelled" => "Cancelled",
        _ => code
    };

    private static bool ItemIsOpen(DbOrderItem i) =>
        i.OrderItemStatus.Code == "Pending" || i.OrderItemStatus.Code == "Backordered";

    private async Task<byte> OrderStatusId(string code) =>
        await _context.OrderStatuses.Where(s => s.Code == code).Select(s => s.OrderStatusId).SingleAsync();

    private async Task<byte> ItemStatusId(string code) =>
        await _context.OrderItemStatuses.Where(s => s.Code == code).Select(s => s.OrderItemStatusId).SingleAsync();

    // ── Mapping DB -> view model ────────────────────────
    private static VmOrder ToViewModel(DbOrder o) => new VmOrder
    {
        Id = o.OrderId,
        OrderNumber = o.OrderNumber ?? $"#{o.OrderId}",
        SupplierId = o.SupplierId,
        Supplier = new VmSupplier
        {
            Id = o.Supplier.SupplierId,
            Name = o.Supplier.Name,
            ContactPerson = o.Supplier.ContactPerson ?? "",
            Email = o.Supplier.ContactEmail ?? ""
        },
        OrderDate = o.PlacedOn.ToLocalTime(),
        Status = OrderStatusToUi(o.OrderStatus.Code),
        DateCompleted = o.ReceivedOn?.ToLocalTime(),
        DateCancelled = o.CancelledOn?.ToLocalTime(),
        CancellationReason = o.CancelReason!,
        OrderItems = o.OrderItems.Select(i => new VmOrderItem
        {
            Id = i.OrderItemId,
            OrderId = i.OrderId,
            ConsumableId = i.ConsumableId,
            Consumable = i.Consumable == null ? null! : new VmConsumable { Id = i.ConsumableId, Name = i.Consumable.Name },
            QuantityOrdered = i.QuantityOrdered,
            Status = ItemStatusToUi(i.OrderItemStatus.Code),
            DateReceived = i.ReceivedOn?.ToLocalTime(),
            DateCancelled = i.CancelledOn?.ToLocalTime(),
            CancellationReason = i.CancelReason!
        }).ToList()
    };

    // Stored as UTC (column default is sysutcdatetime()); shown as local time.
    private static DateTime NowUtc => DateTime.UtcNow;

    // ── Who is placing the order ────────────────────────
    private async Task<int?> CurrentLabManagerId()
    {
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid) &&
            await _context.LabManagerProfiles.AnyAsync(p => p.UserId == uid && !p.IsArchived))
            return uid;

        // 🟢 TEMP fallback until [Authorize(Roles = "LabManager")] is on
        return await _context.LabManagerProfiles
            .Where(p => p.User.Email == "m.vonbuchenroder@hemaflow.co.za")
            .Select(p => (int?)p.UserId)
            .FirstOrDefaultAsync();
    }

    private async Task<string> NextOrderNumber()
    {
        var numbers = await _context.Orders
            .Where(o => o.OrderNumber != null && o.OrderNumber.StartsWith("ORD"))
            .Select(o => o.OrderNumber!)
            .ToListAsync();

        int max = 1000;
        foreach (var n in numbers)
            if (int.TryParse(n.Substring(3), out var v) && v > max) max = v;

        return $"ORD{max + 1}";
    }

    // ── Index (filter + search) ─────────────────────────
    public async Task<IActionResult> Index(string statusFilter = "All", string search = "")
    {
        var query = _context.Orders.AsNoTracking()
            .Include(o => o.Supplier)
            .Include(o => o.OrderStatus)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(o => o.OrderNumber != null && o.OrderNumber.Contains(search.Trim()));

        var code = OrderStatusFromUi(statusFilter);
        if (code != null)
            query = code == "Placed"
                ? query.Where(o => o.OrderStatus.Code == "Placed" || o.OrderStatus.Code == "Draft")
                : query.Where(o => o.OrderStatus.Code == code);

        var data = await query.OrderByDescending(o => o.PlacedOn).ToListAsync();

        // Low stock = within 10% of reorder level (integer-safe: qty*10 <= reorder*11)
        ViewBag.LowStockCount = await _context.Consumables
            .CountAsync(c => !c.IsArchived && c.QuantityOnHand * 10 <= c.ReorderLevel * 11);
        ViewBag.CurrentFilter = statusFilter;
        ViewBag.Search = search;

        return View(data.Select(ToViewModel).ToList());
    }

    // ── Details ─────────────────────────────────────────
    public async Task<IActionResult> Details(int id)
    {
        var order = await _context.Orders.AsNoTracking()
            .Include(o => o.Supplier)
            .Include(o => o.OrderStatus)
            .Include(o => o.OrderItems).ThenInclude(i => i.Consumable)
            .Include(o => o.OrderItems).ThenInclude(i => i.OrderItemStatus)
            .FirstOrDefaultAsync(o => o.OrderId == id);
        if (order == null) return NotFound();

        return View(ToViewModel(order));
    }

    // ── Create ──────────────────────────────────────────
    private async Task LoadCreateLookups()
    {
        ViewBag.Suppliers = (await _context.Suppliers.AsNoTracking()
                .Where(s => !s.IsArchived).OrderBy(s => s.Name).ToListAsync())
            .Select(s => new VmSupplier { Id = s.SupplierId, Name = s.Name })
            .ToList();

        var consumables = await _context.Consumables.AsNoTracking()
            .Where(c => !c.IsArchived).OrderBy(c => c.Name).ToListAsync();

        ViewBag.Consumables = consumables.Select(c => new VmConsumable
        {
            Id = c.ConsumableId,
            Name = c.Name,
            QuantityOnHand = c.QuantityOnHand,
            ReorderLevel = c.ReorderLevel,
            SupplierId = c.SupplierId
        }).ToList();

        ViewBag.LowStockIds = consumables
            .Where(c => c.QuantityOnHand * 10 <= c.ReorderLevel * 11)
            .Select(c => c.ConsumableId)
            .ToList();
    }

    public async Task<IActionResult> Create()
    {
        await LoadCreateLookups();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int supplierId, List<int> consumableIds, List<int> quantities)
    {
        consumableIds ??= new List<int>();
        quantities ??= new List<int>();

        // Pair up ids and quantities; keep only qty > 0
        var lines = consumableIds
            .Select((cid, i) => new { ConsumableId = cid, Qty = i < quantities.Count ? quantities[i] : 0 })
            .Where(l => l.Qty > 0)
            .GroupBy(l => l.ConsumableId)
            .Select(g => new { ConsumableId = g.Key, Qty = g.Sum(x => x.Qty) })
            .ToList();

        var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == supplierId && !s.IsArchived);
        if (supplier == null)
        {
            TempData["ErrorMessage"] = "Please select a supplier.";
            return RedirectToAction("Create");
        }

        if (!lines.Any())
        {
            TempData["ErrorMessage"] = "Enter a quantity for at least one consumable.";
            return RedirectToAction("Create");
        }

        // Orders are grouped by supplier: every item must come from this supplier
        var ids = lines.Select(l => l.ConsumableId).ToList();
        var consumables = await _context.Consumables
            .Where(c => ids.Contains(c.ConsumableId) && !c.IsArchived)
            .ToListAsync();

        var wrongSupplier = consumables.Where(c => c.SupplierId != supplierId).Select(c => c.Name).ToList();
        if (wrongSupplier.Any() || consumables.Count != ids.Count)
        {
            TempData["ErrorMessage"] = wrongSupplier.Any()
                ? $"{string.Join(", ", wrongSupplier)} {(wrongSupplier.Count == 1 ? "is" : "are")} not supplied by {supplier.Name}."
                : "One or more selected consumables are no longer available.";
            return RedirectToAction("Create");
        }

        var managerId = await CurrentLabManagerId();
        if (managerId == null)
        {
            TempData["ErrorMessage"] = "No Lab Manager account found to place this order.";
            return RedirectToAction("Create");
        }

        var placedId = await OrderStatusId("Placed");
        var pendingId = await ItemStatusId("Pending");

        DbOrder? order = null;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            order = new DbOrder
            {
                OrderNumber = await NextOrderNumber(),
                SupplierId = supplierId,
                OrderStatusId = placedId,
                PlacedByUserId = managerId.Value,
                PlacedOn = NowUtc
            };
            foreach (var l in lines)
                order.OrderItems.Add(new DbOrderItem
                {
                    ConsumableId = l.ConsumableId,
                    QuantityOrdered = l.Qty,
                    QuantityReceived = 0,
                    OrderItemStatusId = pendingId
                });

            _context.Orders.Add(order);
            try
            {
                await _context.SaveChangesAsync();
                break;                                  // saved
            }
            catch (DbUpdateException) when (attempt < 2)
            {
                _context.Entry(order).State = EntityState.Detached;   // number taken - retry
                foreach (var i in order.OrderItems) _context.Entry(i).State = EntityState.Detached;
            }
        }

        await Activity.LogAsync("OrderPlaced", "Order", order!.OrderNumber!, $"Supplier: {supplier.Name}");

        var itemRows = string.Join("", lines.Select(l =>
            $"<tr><td>{WebUtility.HtmlEncode(consumables.First(c => c.ConsumableId == l.ConsumableId).Name)}</td><td>{l.Qty}</td></tr>"));
        var emailed = SendSupplierEmail(supplier,
            $"Order {order.OrderNumber} - Nelson Mandela Bay Haematology Laboratory",
            $"<p>Dear {WebUtility.HtmlEncode(supplier.ContactPerson ?? supplier.Name)},</p>" +
            $"<p>Please supply the following for order <strong>{order.OrderNumber}</strong>:</p>" +
            $"<table border='1' cellpadding='6' cellspacing='0'><tr><th>Item</th><th>Quantity</th></tr>{itemRows}</table>" +
            "<p>Kind regards,<br/>Laboratory Manager, NMBHDL</p>");

        TempData["SuccessMessage"] = $"Order {order.OrderNumber} created." + EmailNote(emailed);
        return RedirectToAction("Index");
    }

    // ── Receive one item (full quantity) ────────────────
    public async Task<IActionResult> ReceiveItem(int orderId, int itemId)
    {
        var order = await LoadOrderForUpdate(orderId);
        var item = order?.OrderItems.FirstOrDefault(i => i.OrderItemId == itemId);
        if (order == null || item == null) return NotFound();

        if (!ItemIsOpen(item))
        {
            TempData["WarningMessage"] = "This item has already been received or cancelled.";
            return RedirectToAction("Details", new { id = orderId });
        }

        item.OrderItemStatusId = await ItemStatusId("Received");
        item.OrderItemStatus = await _context.OrderItemStatuses.SingleAsync(s => s.Code == "Received");
        item.QuantityReceived = item.QuantityOrdered;
        item.ReceivedOn = NowUtc;
        item.Consumable.QuantityOnHand += item.QuantityOrdered;     // stock updated automatically

        await RecalculateOrderStatus(order);
        await _context.SaveChangesAsync();                          // one save = all or nothing

        await Activity.LogAsync("ItemReceived", "Order", order.OrderNumber!,
            $"{item.Consumable.Name} ({item.QuantityOrdered} units) - stock now {item.Consumable.QuantityOnHand}");
        TempData["SuccessMessage"] = $"{item.Consumable.Name} received. Stock now {item.Consumable.QuantityOnHand}.";
        return RedirectToAction("Details", new { id = orderId });
    }

    // ── Cancel one item (reason required) ───────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelItem(int orderId, int itemId, string reason)
    {
        reason = reason?.Trim() ?? "";
        if (reason.Length == 0)
        {
            TempData["ErrorMessage"] = "A reason is required to cancel an item.";
            return RedirectToAction("Details", new { id = orderId });
        }
        if (reason.Length > 500) reason = reason[..500];

        var order = await LoadOrderForUpdate(orderId);
        var item = order?.OrderItems.FirstOrDefault(i => i.OrderItemId == itemId);
        if (order == null || item == null) return NotFound();

        if (!ItemIsOpen(item))
        {
            TempData["WarningMessage"] = "This item has already been received or cancelled.";
            return RedirectToAction("Details", new { id = orderId });
        }

        item.OrderItemStatusId = await ItemStatusId("Cancelled");
        item.OrderItemStatus = await _context.OrderItemStatuses.SingleAsync(s => s.Code == "Cancelled");
        item.CancelReason = reason;
        item.CancelledOn = NowUtc;

        await RecalculateOrderStatus(order);
        await _context.SaveChangesAsync();

        await Activity.LogAsync("ItemCancelled", "Order", order.OrderNumber!, $"{item.Consumable.Name} - {reason}");

        var emailed = SendSupplierEmail(order.Supplier,
            $"Order {order.OrderNumber} - item cancelled",
            $"<p>Dear {WebUtility.HtmlEncode(order.Supplier.ContactPerson ?? order.Supplier.Name)},</p>" +
            $"<p>Please cancel <strong>{WebUtility.HtmlEncode(item.Consumable.Name)}</strong> " +
            $"({item.QuantityOrdered} units) on order <strong>{order.OrderNumber}</strong>.</p>" +
            $"<p>Reason: {WebUtility.HtmlEncode(reason)}</p>" +
            "<p>Kind regards,<br/>Laboratory Manager, NMBHDL</p>");

        TempData["SuccessMessage"] = "Item cancelled." + EmailNote(emailed);
        return RedirectToAction("Details", new { id = orderId });
    }

    // ── Cancel the whole order (reason required) ────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelOrder(int orderId, string reason)
    {
        reason = reason?.Trim() ?? "";
        if (reason.Length == 0)
        {
            TempData["ErrorMessage"] = "A reason is required to cancel an order.";
            return RedirectToAction("Details", new { id = orderId });
        }
        if (reason.Length > 500) reason = reason[..500];

        var order = await LoadOrderForUpdate(orderId);
        if (order == null) return NotFound();

        if (order.OrderStatus.Code == "Received" || order.OrderStatus.Code == "Cancelled")
        {
            TempData["WarningMessage"] = "This order is already complete or cancelled.";
            return RedirectToAction("Details", new { id = orderId });
        }

        // Once stock has arrived the order stands - only outstanding items may be cancelled
        if (order.OrderItems.Any(i => i.QuantityReceived > 0 || i.ReceivedOn != null))
        {
            TempData["WarningMessage"] = "Items on this order have already been received, so the order can't be " +
                                         "cancelled. Cancel the outstanding items instead.";
            return RedirectToAction("Details", new { id = orderId });
        }

        var cancelledItemId = await ItemStatusId("Cancelled");
        foreach (var item in order.OrderItems.Where(ItemIsOpen).ToList())
        {
            item.OrderItemStatusId = cancelledItemId;
            item.CancelReason = reason;
            item.CancelledOn = NowUtc;
        }

        order.OrderStatusId = await OrderStatusId("Cancelled");
        order.CancelReason = reason;
        order.CancelledOn = NowUtc;

        await _context.SaveChangesAsync();

        await Activity.LogAsync("OrderCancelled", "Order", order.OrderNumber!, reason);

        var emailed = SendSupplierEmail(order.Supplier,
            $"Order {order.OrderNumber} cancelled",
            $"<p>Dear {WebUtility.HtmlEncode(order.Supplier.ContactPerson ?? order.Supplier.Name)},</p>" +
            $"<p>Order <strong>{order.OrderNumber}</strong> has been cancelled. " +
            "Please do not dispatch any outstanding items.</p>" +
            $"<p>Reason: {WebUtility.HtmlEncode(reason)}</p>" +
            "<p>Kind regards,<br/>Laboratory Manager, NMBHDL</p>");

        TempData["SuccessMessage"] = $"Order {order.OrderNumber} cancelled." + EmailNote(emailed);
        return RedirectToAction("Index");
    }

    // ── Helpers ─────────────────────────────────────────
    private Task<DbOrder?> LoadOrderForUpdate(int orderId) =>
        _context.Orders
            .Include(o => o.Supplier)
            .Include(o => o.OrderStatus)
            .Include(o => o.OrderItems).ThenInclude(i => i.Consumable)
            .Include(o => o.OrderItems).ThenInclude(i => i.OrderItemStatus)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);

    // Spec §5: Partial when some received; Complete when everything is
    // received or cancelled (date complete captured); Cancelled if every
    // item was cancelled.
    private async Task RecalculateOrderStatus(DbOrder order)
    {
        var codes = order.OrderItems.Select(i => i.OrderItemStatus.Code).ToList();

        if (codes.All(c => c == "Cancelled"))
        {
            order.OrderStatusId = await OrderStatusId("Cancelled");
            order.CancelledOn ??= NowUtc;
            order.CancelReason ??= "All items cancelled";
        }
        else if (codes.All(c => c == "Received" || c == "Cancelled"))
        {
            order.OrderStatusId = await OrderStatusId("Received");
            order.ReceivedOn ??= NowUtc;
        }
        else if (codes.Any(c => c == "Received"))
        {
            order.OrderStatusId = await OrderStatusId("PartiallyReceived");
        }
    }

    // Returns true if an e-mail was actually sent.
    private bool SendSupplierEmail(Project.Models.Entities.Supplier supplier, string subject, string html)
    {
        if (!_config.GetValue<bool>("LabManager:SendSupplierEmails"))
        {
            _logger.LogInformation("[EMAIL OFF] To {Supplier}: {Subject}", supplier.Name, subject);
            return false;
        }

        var to = _config["LabManager:SupplierEmailRedirectTo"];
        if (string.IsNullOrWhiteSpace(to)) to = supplier.ContactEmail;
        if (string.IsNullOrWhiteSpace(to)) return false;

        try
        {
            _email.SendEmail(to, subject, html);
            return true;
        }
        catch (Exception ex)
        {
            // Never lose the order because e-mail failed
            _logger.LogWarning(ex, "Supplier e-mail failed for {Supplier}", supplier.Name);
            return false;
        }
    }

    private static string EmailNote(bool emailed) =>
        emailed ? " Supplier has been e-mailed." : " (Supplier e-mail not sent.)";

}//end of class OrderController
