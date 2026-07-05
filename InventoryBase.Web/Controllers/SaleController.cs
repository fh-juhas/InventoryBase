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
public class SaleController : Controller
{
    private readonly IUnitOfWork     _uow;
    private readonly IHashService    _hash;
    private readonly ICompanyService _companySvc;

    public SaleController(IUnitOfWork uow, IHashService hash, ICompanyService companySvc)
    { _uow = uow; _hash = hash; _companySvc = companySvc; }

    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> NextInvoiceNo()
    {
        try
        {
            var year   = DateTime.Now.Year % 100;
            var prefix = $"INV-{year:D2}-";
            var last   = await _uow.Sales.Query()
                .Where(s => s.InvoiceNo.StartsWith(prefix))
                .OrderByDescending(s => s.InvoiceNo)
                .Select(s => s.InvoiceNo)
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
    public async Task<IActionResult> Data([FromQuery] TabulatorRequest req)
    {
        try
        {
            var q = _uow.Sales.Query().Include(s => s.Customer).AsQueryable();

            if (!string.IsNullOrWhiteSpace(req.search))
                q = q.Where(s => s.InvoiceNo.Contains(req.search) ||
                                 s.Customer.Name.Contains(req.search));

            if (!string.IsNullOrEmpty(req.dateFrom) && DateTime.TryParse(req.dateFrom, out var dFrom))
                q = q.Where(s => s.SaleDate >= dFrom);
            if (!string.IsNullOrEmpty(req.dateTo) && DateTime.TryParse(req.dateTo, out var dTo))
                q = q.Where(s => s.SaleDate < dTo.AddDays(1));

            q = (req.field, req.dir) switch {
                ("saleDate",    "asc")  => q.OrderBy(s => s.SaleDate),
                ("totalAmount", "asc")  => q.OrderBy(s => s.TotalAmount),
                ("totalAmount", "desc") => q.OrderByDescending(s => s.TotalAmount),
                _                       => q.OrderByDescending(s => s.SaleDate)
            };

            int total    = await q.CountAsync();
            int lastPage = (int)Math.Ceiling(total / (double)(req.size > 0 ? req.size : 20));
            var items    = await q.Skip((req.page - 1) * req.size).Take(req.size).ToListAsync();

            return Json(new TabulatorResponse<object>
            {
                last_page = Math.Max(lastPage, 1),
                data = items.Select(s => new
                {
                    hash        = _hash.Encode(s.Id),
                    invoiceNo   = s.InvoiceNo,
                    customer    = s.Customer.Name,
                    saleDate    = s.SaleDate.ToString("dd MMM yyyy"),
                    totalAmount = s.TotalAmount,
                    note        = s.Note ?? "—"
                }).ToList<object>()
            });
        }
        catch (Exception)
        {
            return Json(new TabulatorResponse<object> { last_page = 1, data = new List<object>() });
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
        [FromForm] Sale      model,
        [FromForm] int[]     productIds,
        [FromForm] decimal[] quantities,
        [FromForm] decimal[] unitPrices)
    {
        try
        {
            if (!ModelState.IsValid) { await PopulateViewBagAsync(); return View(model); }
            if (productIds.Length == 0)
            {
                ModelState.AddModelError("", "Add at least one product line.");
                await PopulateViewBagAsync(); return View(model);
            }

            // Check available stock for each line
            for (int i = 0; i < productIds.Length; i++)
            {
                var pid       = productIds[i];
                var requested = quantities[i];
                var available = await _uow.StockLedger.Query()
                    .Where(s => s.ProductId == pid)
                    .SumAsync(s => (decimal?)s.Quantity) ?? 0;

                if (available < requested)
                {
                    var product = await _uow.Products.GetByIdAsync(pid);
                    ModelState.AddModelError("", $"Insufficient stock for \"{product?.Name}\" — available: {available}");
                    await PopulateViewBagAsync(); return View(model);
                }
            }

            if (string.IsNullOrWhiteSpace(model.InvoiceNo))
            {
                var year   = DateTime.Now.Year % 100;
                var prefix = $"INV-{year:D2}-";
                var last   = await _uow.Sales.Query()
                    .Where(s => s.InvoiceNo.StartsWith(prefix))
                    .OrderByDescending(s => s.InvoiceNo)
                    .Select(s => s.InvoiceNo)
                    .FirstOrDefaultAsync();
                int seq = 1;
                if (last != null && int.TryParse(last.Replace(prefix, ""), out var n)) seq = n + 1;
                model.InvoiceNo = $"{prefix}{seq:D4}";
            }

            model.CreatedAt = DateTime.Now;
            model.Items     = new List<SaleItem>();

            for (int i = 0; i < productIds.Length; i++)
            {
                var qty   = quantities[i];
                var price = unitPrices[i];
                model.Items.Add(new SaleItem
                {
                    ProductId = productIds[i],
                    Quantity  = qty,
                    UnitPrice = price,
                    SubTotal  = qty * price
                });
            }
            model.TotalAmount = model.Items.Sum(x => x.SubTotal);

            await _uow.Sales.AddAsync(model);
            await _uow.SaveChangesAsync();

            foreach (var item in model.Items)
            {
                await _uow.StockLedger.AddAsync(new StockLedger
                {
                    ProductId    = item.ProductId,
                    Quantity     = -item.Quantity,
                    MovementType = StockMovementType.Sale,
                    ReferenceId  = model.Id,
                    Note         = $"Sale {model.InvoiceNo}",
                    CreatedAt    = DateTime.Now
                });
            }
            await _uow.SaveChangesAsync();

            TempData["Success"] = $"Sale {model.InvoiceNo} saved.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception)
        {
            ModelState.AddModelError("", "An unexpected error occurred while saving the sale. Please try again.");
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
            var sale = await _uow.Sales.Query()
                .Include(s => s.Customer)
                .Include(s => s.Items).ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(s => s.Id == realId.Value);
            if (sale == null) return NotFound();
            ViewBag.HashId = id;
            return View(sale);
        }
        catch (Exception)
        {
            TempData["Error"] = "An error occurred loading the sale details.";
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
            var sale = await _uow.Sales.Query()
                .Include(s => s.Customer)
                .Include(s => s.Items).ThenInclude(i => i.Product).ThenInclude(p => p!.Unit)
                .FirstOrDefaultAsync(s => s.Id == realId.Value);
            if (sale == null) return NotFound();

            var stockMap = await _uow.StockLedger.Query()
                .GroupBy(s => s.ProductId)
                .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Quantity) })
                .ToListAsync();

            await PopulateViewBagAsync();
            ViewBag.HashId = id;
            ViewBag.ExistingItems = sale.Items.Select(i => new
            {
                id        = i.ProductId,
                text      = $"{i.Product!.SKU} — {i.Product.Name}",
                unit      = i.Product.Unit?.Name ?? "—",
                qty       = i.Quantity,
                unitPrice = i.UnitPrice,
                stock     = stockMap.FirstOrDefault(s => s.ProductId == i.ProductId)?.Qty ?? 0
            }).ToList();
            return View(sale);
        }
        catch (Exception)
        {
            TempData["Error"] = "An error occurred loading the sale.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        string id,
        [FromForm] Sale      model,
        [FromForm] int[]     productIds,
        [FromForm] decimal[] quantities,
        [FromForm] decimal[] unitPrices)
    {
        try
        {
            var realId = _hash.Decode(id);
            if (realId == null) return BadRequest();
            var sale = await _uow.Sales.Query()
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == realId.Value);
            if (sale == null) return NotFound();

            if (productIds.Length == 0)
            {
                ModelState.AddModelError("", "Add at least one product line.");
                await PopulateViewBagAsync(); ViewBag.HashId = id;
                return View(model);
            }

            // Check stock: effective stock = current stock + old sale quantities returned
            for (int i = 0; i < productIds.Length; i++)
            {
                var pid    = productIds[i];
                var newQty = quantities[i];
                var currentStock = await _uow.StockLedger.Query()
                    .Where(s => s.ProductId == pid)
                    .SumAsync(s => (decimal?)s.Quantity) ?? 0;
                var oldQty = sale.Items.Where(it => it.ProductId == pid).Sum(it => it.Quantity);
                if (currentStock + oldQty < newQty)
                {
                    var product = await _uow.Products.GetByIdAsync(pid);
                    ModelState.AddModelError("", $"Insufficient stock for \"{product?.Name}\" — available: {currentStock + oldQty}");
                    await PopulateViewBagAsync(); ViewBag.HashId = id;
                    return View(model);
                }
            }

            // Remove old items and stock ledger entries
            _uow.SaleItems.RemoveRange(sale.Items.ToList());
            var oldLedger = (await _uow.StockLedger.FindAsync(
                s => s.MovementType == StockMovementType.Sale && s.ReferenceId == sale.Id)).ToList();
            _uow.StockLedger.RemoveRange(oldLedger);

            // Update header fields
            sale.SaleDate   = model.SaleDate;
            sale.CustomerId = model.CustomerId;
            sale.Note       = model.Note;

            // Build new items
            var newItems = new List<SaleItem>();
            for (int i = 0; i < productIds.Length; i++)
            {
                var qty   = quantities[i];
                var price = unitPrices[i];
                var item  = new SaleItem
                {
                    SaleId    = sale.Id,
                    ProductId = productIds[i],
                    Quantity  = qty,
                    UnitPrice = price,
                    SubTotal  = qty * price
                };
                newItems.Add(item);
                await _uow.SaleItems.AddAsync(item);
            }
            sale.TotalAmount = newItems.Sum(x => x.SubTotal);
            _uow.Sales.Update(sale);
            await _uow.SaveChangesAsync();

            foreach (var item in newItems)
            {
                await _uow.StockLedger.AddAsync(new StockLedger
                {
                    ProductId    = item.ProductId,
                    Quantity     = -item.Quantity,
                    MovementType = StockMovementType.Sale,
                    ReferenceId  = sale.Id,
                    Note         = $"Sale {sale.InvoiceNo}",
                    CreatedAt    = DateTime.Now
                });
            }
            await _uow.SaveChangesAsync();

            TempData["Success"] = $"Sale {sale.InvoiceNo} updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception)
        {
            ModelState.AddModelError("", "An unexpected error occurred while updating the sale.");
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
            var sale = await _uow.Sales.Query()
                .Include(s => s.Items)
                .FirstOrDefaultAsync(s => s.Id == realId.Value);
            if (sale == null) return Json(new { success = false, message = "Not found." });

            var ledger = (await _uow.StockLedger.FindAsync(
                s => s.MovementType == StockMovementType.Sale && s.ReferenceId == realId.Value)).ToList();
            _uow.StockLedger.RemoveRange(ledger);
            _uow.SaleItems.RemoveRange(sale.Items.ToList());
            _uow.Sales.Remove(sale);
            await _uow.SaveChangesAsync();
            return Json(new { success = true });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> CustomerList()
    {
        try
        {
            var list = await _uow.Customers.Query()
                .Where(c => c.IsActive).OrderBy(c => c.Name)
                .Select(c => new { id = c.Id, text = c.Name })
                .ToListAsync();
            return Json(list);
        }
        catch (Exception)
        {
            return Json(new List<object>());
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickAddCustomer(string name, string? phone, string? email, string? address)
    {
        try
        {
            name = name?.Trim() ?? "";
            if (string.IsNullOrEmpty(name))
                return Json(new { ok = false, message = "Name is required." });

            var customer = new Customer
            {
                Name = name,
                Phone = phone?.Trim() ?? "",
                Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
                Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            await _uow.Customers.AddAsync(customer);
            await _uow.SaveChangesAsync();

            var text = string.IsNullOrEmpty(customer.Phone) ? customer.Name : $"{customer.Name} · {customer.Phone}";
            return Json(new { ok = true, id = customer.Id, text, name = customer.Name, phone = customer.Phone });
        }
        catch (Exception)
        {
            return Json(new { ok = false, message = "Failed to add customer." });
        }
    }

    [HttpGet]
    public async Task<IActionResult> ProductSearch(string q)
    {
        try
        {
            q = q?.Trim() ?? "";
            var products = await _uow.Products.Query()
                .Include(p => p.Unit)
                .Where(p => p.IsActive && (string.IsNullOrEmpty(q) || p.Name.Contains(q) || p.SKU.Contains(q)))
                .Take(20).ToListAsync();

            var stockMap = await _uow.StockLedger.Query()
                .GroupBy(s => s.ProductId)
                .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Quantity) })
                .ToListAsync();

            return Json(products.Select(p => new {
                id    = p.Id,
                text  = $"{p.SKU} — {p.Name}",
                unit  = p.Unit?.Name ?? "",
                price = p.SalePrice,
                stock = stockMap.FirstOrDefault(s => s.ProductId == p.Id)?.Qty ?? 0
            }));
        }
        catch (Exception)
        {
            return Json(new List<object>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> Invoice(string id)
    {
        try
        {
            var realId = _hash.Decode(id);
            if (realId == null) return BadRequest();
            var sale = await _uow.Sales.Query()
                .Include(s => s.Customer)
                .Include(s => s.Items).ThenInclude(i => i.Product).ThenInclude(p => p!.Unit)
                .FirstOrDefaultAsync(s => s.Id == realId.Value);
            if (sale == null) return NotFound();
            ViewBag.Company = await _companySvc.GetAsync();
            return View(sale);
        }
        catch (Exception)
        {
            TempData["Error"] = "An error occurred loading the invoice.";
            return RedirectToAction(nameof(Index));
        }
    }

    private async Task PopulateViewBagAsync()
    {
        ViewBag.Customers = await _uow.Customers.Query()
            .Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
    }
}
