using InventoryBase.Core.Interfaces.Repositories;
using InventoryBase.Core.Interfaces.Services;
using InventoryBase.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryBase.Web.Controllers;

[Authorize]
public class StockController : Controller
{
    private readonly IUnitOfWork  _uow;
    private readonly IHashService _hash;

    public StockController(IUnitOfWork uow, IHashService hash)
    { _uow = uow; _hash = hash; }

    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> Data([FromQuery] TabulatorRequest req)
    {
        // Aggregate stock per product
        var stockMap = await _uow.StockLedger.Query()
            .GroupBy(s => s.ProductId)
            .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Quantity) })
            .ToListAsync();

        var products = await _uow.Products.Query()
            .Include(p => p.Category)
            .Include(p => p.Unit)
            .Where(p => p.IsActive)
            .ToListAsync();

        var rows = products.Select(p => {
            var qty    = stockMap.FirstOrDefault(s => s.ProductId == p.Id)?.Qty ?? 0;
            string status = qty <= 0 ? "out" : qty < 10 ? "low" : "in stock";
            return new {
                hash      = _hash.Encode(p.Id),
                name      = p.Name,
                sku       = p.SKU,
                category  = p.Category?.Name ?? "—",
                unit      = p.Unit?.Name ?? "—",
                qty       = qty,
                costPrice = p.CostPrice,
                stockVal  = qty * p.CostPrice,
                status    = status
            };
        }).AsQueryable();

        // Filters
        if (!string.IsNullOrWhiteSpace(req.search))
            rows = rows.Where(r => r.name.Contains(req.search, StringComparison.OrdinalIgnoreCase)
                                || r.sku.Contains(req.search, StringComparison.OrdinalIgnoreCase));

        if (req.status == "low")      rows = rows.Where(r => r.status == "low");
        if (req.status == "out")      rows = rows.Where(r => r.status == "out");
        if (req.status == "in stock") rows = rows.Where(r => r.status == "in stock");

        if (!string.IsNullOrWhiteSpace(req.category))
            rows = rows.Where(r => r.category == req.category);

        // Sort
        rows = (req.field, req.dir) switch {
            ("qty",      "asc")  => rows.OrderBy(r => r.qty),
            ("qty",      "desc") => rows.OrderByDescending(r => r.qty),
            ("name",     "desc") => rows.OrderByDescending(r => r.name),
            ("stockVal", "asc")  => rows.OrderBy(r => r.stockVal),
            ("stockVal", "desc") => rows.OrderByDescending(r => r.stockVal),
            _                    => rows.OrderBy(r => r.name)
        };

        var list     = rows.ToList();
        int total    = list.Count;
        int lastPage = (int)Math.Ceiling(total / (double)(req.size > 0 ? req.size : 20));
        var page     = list.Skip((req.page - 1) * req.size).Take(req.size).ToList<object>();

        return Json(new TabulatorResponse<object> { last_page = Math.Max(lastPage, 1), data = page });
    }

    // Summary stats
    [HttpGet]
    public async Task<IActionResult> Summary()
    {
        var stockMap = await _uow.StockLedger.Query()
            .GroupBy(s => s.ProductId)
            .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Quantity) })
            .ToListAsync();

        var products = await _uow.Products.Query()
            .Where(p => p.IsActive)
            .ToListAsync();

        decimal totalValue = products.Sum(p => {
            var qty = stockMap.FirstOrDefault(s => s.ProductId == p.Id)?.Qty ?? 0;
            return qty * p.CostPrice;
        });
        int lowCount = stockMap.Count(s => s.Qty > 0 && s.Qty < 10);
        int outCount = stockMap.Count(s => s.Qty <= 0);

        return Json(new { totalValue, lowCount, outCount, totalProducts = products.Count });
    }

    // Category list for filter
    [HttpGet]
    public async Task<IActionResult> CategoryList()
    {
        var list = await _uow.Categories.Query()
            .Where(c => c.IsActive).OrderBy(c => c.Name)
            .Select(c => new { id = c.Name, text = c.Name })
            .ToListAsync();
        return Json(list);
    }
}
