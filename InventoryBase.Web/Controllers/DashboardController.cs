using InventoryBase.Core.Interfaces.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryBase.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly IUnitOfWork _uow;
    public DashboardController(IUnitOfWork uow) => _uow = uow;

    public IActionResult Index() => View();

    // ── Stats cards ─────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Stats()
    {
        var today = DateTime.Today;
        var firstOfMonth = new DateTime(today.Year, today.Month, 1);

        var todaySales = await _uow.Sales.Query()
            .Where(s => s.SaleDate.Date == today)
            .SumAsync(s => (decimal?)s.TotalAmount) ?? 0;

        var monthSales = await _uow.Sales.Query()
            .Where(s => s.SaleDate >= firstOfMonth)
            .SumAsync(s => (decimal?)s.TotalAmount) ?? 0;

        var monthPurchases = await _uow.Purchases.Query()
            .Where(p => p.PurchaseDate >= firstOfMonth)
            .SumAsync(p => (decimal?)p.TotalAmount) ?? 0;

        var stockValue = await _uow.StockLedger.Query()
            .GroupBy(s => s.ProductId)
            .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Quantity) })
            .Join(_uow.Products.Query(), s => s.ProductId, p => p.Id,
                  (s, p) => s.Qty * p.CostPrice)
            .SumAsync(v => (decimal?)v) ?? 0;

        var activeProducts = await _uow.Products.Query().CountAsync(p => p.IsActive);
        var lowStockCount  = await _uow.StockLedger.Query()
            .GroupBy(s => s.ProductId)
            .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Quantity) })
            .Join(_uow.Products.Query().Where(p => p.IsActive),
                  s => s.ProductId, p => p.Id,
                  (s, p) => new { s.Qty, p.ReorderLevel })
            .CountAsync(x => x.Qty > 0 && x.Qty <= x.ReorderLevel);

        return Json(new
        {
            todaySales     = todaySales,
            monthSales     = monthSales,
            monthPurchases = monthPurchases,
            stockValue     = stockValue,
            activeProducts = activeProducts,
            lowStockCount  = lowStockCount
        });
    }

    // ── Chart: last 30 days sales vs purchases ───────────────────────────────
    [HttpGet]
    public async Task<IActionResult> ChartData()
    {
        var from = DateTime.Today.AddDays(-29);

        var sales = await _uow.Sales.Query()
            .Where(s => s.SaleDate.Date >= from)
            .GroupBy(s => s.SaleDate.Date)
            .Select(g => new { date = g.Key, total = g.Sum(x => x.TotalAmount) })
            .ToListAsync();

        var purchases = await _uow.Purchases.Query()
            .Where(p => p.PurchaseDate.Date >= from)
            .GroupBy(p => p.PurchaseDate.Date)
            .Select(g => new { date = g.Key, total = g.Sum(x => x.TotalAmount) })
            .ToListAsync();

        var labels = Enumerable.Range(0, 30)
            .Select(i => from.AddDays(i))
            .ToList();

        return Json(new
        {
            labels    = labels.Select(d => d.ToString("dd MMM")).ToList(),
            sales     = labels.Select(d => sales.FirstOrDefault(s => s.date == d)?.total ?? 0).ToList(),
            purchases = labels.Select(d => purchases.FirstOrDefault(p => p.date == d)?.total ?? 0).ToList()
        });
    }

    // ── Low stock table (top 8) ──────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> LowStock()
    {
        var rows = await _uow.StockLedger.Query()
            .GroupBy(s => s.ProductId)
            .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Quantity) })
            .Join(_uow.Products.Query().Include(p => p.Unit),
                  s => s.ProductId, p => p.Id,
                  (s, p) => new { p.Name, p.SKU, Unit = p.Unit.Name, qty = s.Qty, reorderLevel = p.ReorderLevel })
            .Where(x => x.qty >= 0 && x.qty <= x.reorderLevel)
            .OrderBy(x => x.qty)
            .Take(8)
            .ToListAsync();

        return Json(rows);
    }

    // ── Recent sales ────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> RecentSales()
    {
        var rows = await _uow.Sales.Query()
            .Include(s => s.Customer)
            .OrderByDescending(s => s.CreatedAt)
            .Take(6)
            .Select(s => new
            {
                invoice  = s.InvoiceNo,
                customer = s.Customer.Name,
                amount   = s.TotalAmount,
                date     = s.SaleDate.ToString("dd MMM")
            })
            .ToListAsync();

        return Json(rows);
    }
}
