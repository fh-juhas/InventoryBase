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
    private readonly IUnitOfWork  _uow;
    private readonly IHashService _hash;

    public SaleController(IUnitOfWork uow, IHashService hash)
    { _uow = uow; _hash = hash; }

    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> Data([FromQuery] TabulatorRequest req)
    {
        var q = _uow.Sales.Query().Include(s => s.Customer).AsQueryable();

        if (!string.IsNullOrWhiteSpace(req.search))
            q = q.Where(s => s.InvoiceNo.Contains(req.search) ||
                             s.Customer.Name.Contains(req.search));

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

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulateViewBagAsync();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [FromForm] Sale    model,
        [FromForm] int[]     productIds,
        [FromForm] decimal[] quantities,
        [FromForm] decimal[] unitPrices)
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
            var last = await _uow.Sales.Query().CountAsync();
            model.InvoiceNo = $"INV-{(last + 1):D4}";
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

        // Write StockLedger entries (negative = stock out)
        foreach (var item in model.Items)
        {
            await _uow.StockLedger.AddAsync(new StockLedger
            {
                ProductId    = item.ProductId,
                Quantity     = -item.Quantity,        // negative = out
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

    [HttpGet]
    public async Task<IActionResult> Details(string id)
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

    // Customer dropdown for Select2
    [HttpGet]
    public async Task<IActionResult> CustomerList()
    {
        var list = await _uow.Customers.Query()
            .Where(c => c.IsActive).OrderBy(c => c.Name)
            .Select(c => new { id = c.Id, text = c.Name })
            .ToListAsync();
        return Json(list);
    }

    // Product search for line items (includes live stock)
    [HttpGet]
    public async Task<IActionResult> ProductSearch(string q)
    {
        var products = await _uow.Products.Query()
            .Include(p => p.Unit)
            .Where(p => p.IsActive && (p.Name.Contains(q) || p.SKU.Contains(q)))
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

    private async Task PopulateViewBagAsync()
    {
        ViewBag.Customers = await _uow.Customers.Query()
            .Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
    }
}
