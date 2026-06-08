using InventoryBase.Core.Entities;
using InventoryBase.Core.Enums;
using InventoryBase.Core.Interfaces.Repositories;
using InventoryBase.Core.Interfaces.Services;
using InventoryBase.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryBase.Web.Controllers;

[Authorize]
public class PurchaseController : Controller
{
    private readonly IUnitOfWork  _uow;
    private readonly IHashService _hash;

    public PurchaseController(IUnitOfWork uow, IHashService hash)
    { _uow = uow; _hash = hash; }

    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> Data([FromQuery] TabulatorRequest req)
    {
        try
        {
            var q = _uow.Purchases.Query().Include(p => p.Supplier).AsQueryable();

            if (!string.IsNullOrWhiteSpace(req.search))
                q = q.Where(p => p.InvoiceNo.Contains(req.search) ||
                                 p.Supplier.Name.Contains(req.search));

            if (!string.IsNullOrEmpty(req.dateFrom) && DateTime.TryParse(req.dateFrom, out var dFrom))
                q = q.Where(p => p.PurchaseDate >= dFrom);
            if (!string.IsNullOrEmpty(req.dateTo) && DateTime.TryParse(req.dateTo, out var dTo))
                q = q.Where(p => p.PurchaseDate < dTo.AddDays(1));

            q = (req.field, req.dir) switch {
                ("purchaseDate", "asc")  => q.OrderBy(p => p.PurchaseDate),
                ("totalAmount",  "asc")  => q.OrderBy(p => p.TotalAmount),
                ("totalAmount",  "desc") => q.OrderByDescending(p => p.TotalAmount),
                _                        => q.OrderByDescending(p => p.PurchaseDate)
            };

            int total    = await q.CountAsync();
            int lastPage = (int)Math.Ceiling(total / (double)(req.size > 0 ? req.size : 20));
            var items    = await q.Skip((req.page - 1) * req.size).Take(req.size).ToListAsync();

            return Json(new TabulatorResponse<object>
            {
                last_page = Math.Max(lastPage, 1),
                data = items.Select(p => new
                {
                    hash         = _hash.Encode(p.Id),
                    invoiceNo    = p.InvoiceNo,
                    supplier     = p.Supplier.Name,
                    purchaseDate = p.PurchaseDate.ToString("dd MMM yyyy"),
                    totalAmount  = p.TotalAmount,
                    note         = p.Note ?? "—"
                }).ToList<object>()
            });
        }
        catch (Exception)
        {
            return Json(new TabulatorResponse<object> { last_page = 1, data = new List<object>() });
        }
    }

    [HttpGet]
    public async Task<IActionResult> NextInvoiceNo()
    {
        try
        {
            var year   = DateTime.Now.Year % 100;
            var prefix = $"PO-{year:D2}-";
            var last   = await _uow.Purchases.Query()
                .Where(p => p.InvoiceNo.StartsWith(prefix))
                .OrderByDescending(p => p.InvoiceNo)
                .Select(p => p.InvoiceNo)
                .FirstOrDefaultAsync();
            int next = 1;
            if (last != null)
            {
                var seq = last.Replace(prefix, "");
                if (int.TryParse(seq, out var n)) next = n + 1;
            }
            return Json(new { invoiceNo = $"{prefix}{next:D4}" });
        }
        catch (Exception)
        {
            return StatusCode(500);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        try
        {
            await PopulateViewBagAsync();
            return View();
        }
        catch (Exception)
        {
            TempData["Error"] = "An error occurred loading the form.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [FromForm] Purchase  model,
        [FromForm] int[]     productIds,
        [FromForm] decimal[] quantities,
        [FromForm] decimal[] unitCosts)
    {
        try
        {
            if (!ModelState.IsValid) { await PopulateViewBagAsync(); return View(model); }
            if (productIds.Length == 0)
            {
                ModelState.AddModelError("", "Add at least one product line.");
                await PopulateViewBagAsync(); return View(model);
            }

            if (string.IsNullOrWhiteSpace(model.InvoiceNo))
            {
                var year   = DateTime.Now.Year % 100;
                var prefix = $"PO-{year:D2}-";
                var last   = await _uow.Purchases.Query()
                    .Where(s => s.InvoiceNo.StartsWith(prefix))
                    .OrderByDescending(s => s.InvoiceNo)
                    .Select(s => s.InvoiceNo)
                    .FirstOrDefaultAsync();
                int seq = 1;
                if (last != null && int.TryParse(last.Replace(prefix, ""), out var n)) seq = n + 1;
                model.InvoiceNo = $"{prefix}{seq:D4}";
            }

            model.CreatedAt = DateTime.Now;
            model.Items     = new List<PurchaseItem>();

            for (int i = 0; i < productIds.Length; i++)
            {
                var qty  = quantities[i];
                var cost = unitCosts[i];
                model.Items.Add(new PurchaseItem
                {
                    ProductId = productIds[i],
                    Quantity  = qty,
                    UnitCost  = cost,
                    SubTotal  = qty * cost
                });
            }
            model.TotalAmount = model.Items.Sum(x => x.SubTotal);

            await _uow.Purchases.AddAsync(model);
            await _uow.SaveChangesAsync();

            foreach (var item in model.Items)
            {
                await _uow.StockLedger.AddAsync(new StockLedger
                {
                    ProductId    = item.ProductId,
                    Quantity     = item.Quantity,
                    MovementType = StockMovementType.Purchase,
                    ReferenceId  = model.Id,
                    Note         = $"Purchase {model.InvoiceNo}",
                    CreatedAt    = DateTime.Now
                });
            }
            await _uow.SaveChangesAsync();

            TempData["Success"] = $"Purchase {model.InvoiceNo} saved.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception)
        {
            ModelState.AddModelError("", "An unexpected error occurred while saving the purchase. Please try again.");
            await PopulateViewBagAsync();
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        try
        {
            var realId = _hash.Decode(id);
            if (realId == null) return BadRequest();
            var purchase = await _uow.Purchases.Query()
                .Include(p => p.Supplier)
                .Include(p => p.Items).ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(p => p.Id == realId.Value);
            if (purchase == null) return NotFound();
            ViewBag.HashId = id;
            return View(purchase);
        }
        catch (Exception)
        {
            TempData["Error"] = "An error occurred loading the purchase details.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        try
        {
            var realId = _hash.Decode(id);
            if (realId == null) return BadRequest();
            var purchase = await _uow.Purchases.Query()
                .Include(p => p.Supplier)
                .Include(p => p.Items).ThenInclude(i => i.Product).ThenInclude(p => p!.Unit)
                .FirstOrDefaultAsync(p => p.Id == realId.Value);
            if (purchase == null) return NotFound();
            await PopulateViewBagAsync();
            ViewBag.HashId = id;
            ViewBag.ExistingItems = purchase.Items.Select(i => new
            {
                id       = i.ProductId,
                text     = $"{i.Product!.SKU} — {i.Product.Name}",
                unit     = i.Product.Unit?.Name ?? "—",
                qty      = i.Quantity,
                unitCost = i.UnitCost
            }).ToList();
            return View(purchase);
        }
        catch (Exception)
        {
            TempData["Error"] = "An error occurred loading the purchase.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string id,
        [FromForm] Purchase  model,
        [FromForm] int[]     productIds,
        [FromForm] decimal[] quantities,
        [FromForm] decimal[] unitCosts)
    {
        try
        {
            var realId = _hash.Decode(id);
            if (realId == null) return BadRequest();
            var purchase = await _uow.Purchases.Query()
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == realId.Value);
            if (purchase == null) return NotFound();

            if (productIds.Length == 0)
            {
                ModelState.AddModelError("", "Add at least one product line.");
                await PopulateViewBagAsync(); ViewBag.HashId = id;
                return View(model);
            }

            // Remove old items and stock ledger entries
            _uow.PurchaseItems.RemoveRange(purchase.Items.ToList());
            var oldLedger = (await _uow.StockLedger.FindAsync(
                s => s.MovementType == StockMovementType.Purchase && s.ReferenceId == purchase.Id)).ToList();
            _uow.StockLedger.RemoveRange(oldLedger);

            // Update header fields
            purchase.PurchaseDate = model.PurchaseDate;
            purchase.SupplierId   = model.SupplierId;
            purchase.Note         = model.Note;

            // Build new items
            var newItems = new List<PurchaseItem>();
            for (int i = 0; i < productIds.Length; i++)
            {
                var qty  = quantities[i];
                var cost = unitCosts[i];
                var item = new PurchaseItem
                {
                    PurchaseId = purchase.Id,
                    ProductId  = productIds[i],
                    Quantity   = qty,
                    UnitCost   = cost,
                    SubTotal   = qty * cost
                };
                newItems.Add(item);
                await _uow.PurchaseItems.AddAsync(item);
            }
            purchase.TotalAmount = newItems.Sum(x => x.SubTotal);
            _uow.Purchases.Update(purchase);
            await _uow.SaveChangesAsync();

            foreach (var item in newItems)
            {
                await _uow.StockLedger.AddAsync(new StockLedger
                {
                    ProductId    = item.ProductId,
                    Quantity     = item.Quantity,
                    MovementType = StockMovementType.Purchase,
                    ReferenceId  = purchase.Id,
                    Note         = $"Purchase {purchase.InvoiceNo}",
                    CreatedAt    = DateTime.Now
                });
            }
            await _uow.SaveChangesAsync();

            TempData["Success"] = $"Purchase {purchase.InvoiceNo} updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception)
        {
            ModelState.AddModelError("", "An unexpected error occurred while updating the purchase.");
            await PopulateViewBagAsync(); ViewBag.HashId = id;
            return View(model);
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        try
        {
            var realId = _hash.Decode(id);
            if (realId == null) return Json(new { success = false, message = "Invalid id." });
            var purchase = await _uow.Purchases.Query()
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == realId.Value);
            if (purchase == null) return Json(new { success = false, message = "Not found." });

            var ledger = (await _uow.StockLedger.FindAsync(
                s => s.MovementType == StockMovementType.Purchase && s.ReferenceId == realId.Value)).ToList();
            _uow.StockLedger.RemoveRange(ledger);
            _uow.PurchaseItems.RemoveRange(purchase.Items.ToList());
            _uow.Purchases.Remove(purchase);
            await _uow.SaveChangesAsync();
            return Json(new { success = true });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> SupplierList()
    {
        try
        {
            var list = await _uow.Suppliers.Query()
                .Where(s => s.IsActive).OrderBy(s => s.Name)
                .Select(s => new { id = s.Id, text = s.Name })
                .ToListAsync();
            return Json(list);
        }
        catch (Exception)
        {
            return Json(new List<object>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> ProductSearch(string q)
    {
        try
        {
            q = q?.Trim() ?? "";
            var list = await _uow.Products.Query()
                .Include(p => p.Unit)
                .Where(p => p.IsActive && (string.IsNullOrEmpty(q) || p.Name.Contains(q) || p.SKU.Contains(q)))
                .Take(20)
                .Select(p => new { id = p.Id, text = $"{p.SKU} — {p.Name}", unit = p.Unit.Name, cost = p.CostPrice })
                .ToListAsync();
            return Json(list);
        }
        catch (Exception)
        {
            return Json(new List<object>());
        }
    }

    private async Task PopulateViewBagAsync()
    {
        ViewBag.Suppliers = await _uow.Suppliers.Query()
            .Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
    }
}
