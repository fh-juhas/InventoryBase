using InventoryBase.Core.Interfaces.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryBase.Web.Controllers;

[Authorize]
public class ReportController : Controller
{
    private readonly IUnitOfWork _uow;
    public ReportController(IUnitOfWork uow) => _uow = uow;

    public IActionResult Index() => View();

    // ── P&L summary for a month/year ────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> ProfitLoss(int month, int year)
    {
        var firstDay = new DateTime(year, month, 1);
        var lastDay  = firstDay.AddMonths(1).AddDays(-1);

        var revenue = await _uow.Sales.Query()
            .Where(s => s.SaleDate >= firstDay && s.SaleDate <= lastDay)
            .SumAsync(s => (decimal?)s.TotalAmount) ?? 0;

        // COGS = sum of (quantity × cost_price) for items sold
        var cogs = await _uow.SaleItems.Query()
            .Include(i => i.Sale)
            .Include(i => i.Product)
            .Where(i => i.Sale.SaleDate >= firstDay && i.Sale.SaleDate <= lastDay)
            .SumAsync(i => (decimal?)(i.Quantity * i.Product.CostPrice)) ?? 0;

        var expenses = await _uow.Expenses.Query()
            .Where(e => e.Month == month && e.Year == year
                        && e.Status == Core.Enums.ExpenseStatus.Confirmed)
            .SumAsync(e => (decimal?)e.Amount) ?? 0;

        var grossProfit = revenue - cogs;
        var netProfit   = grossProfit - expenses;

        return Json(new { revenue, cogs, grossProfit, expenses, netProfit });
    }

    // ── Top selling products ─────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> TopProducts(int month, int year, int take = 10)
    {
        var firstDay = new DateTime(year, month, 1);
        var lastDay  = firstDay.AddMonths(1).AddDays(-1);

        var rows = await _uow.SaleItems.Query()
            .Include(i => i.Sale)
            .Include(i => i.Product)
            .Where(i => i.Sale.SaleDate >= firstDay && i.Sale.SaleDate <= lastDay)
            .GroupBy(i => new { i.ProductId, i.Product.Name, i.Product.SKU })
            .Select(g => new
            {
                name     = g.Key.Name,
                sku      = g.Key.SKU,
                qtySold  = g.Sum(x => x.Quantity),
                revenue  = g.Sum(x => x.SubTotal)
            })
            .OrderByDescending(x => x.revenue)
            .Take(take)
            .ToListAsync();

        return Json(rows);
    }

    // ── Monthly sales trend (last 12 months) ─────────────────────────────
    [HttpGet]
    public async Task<IActionResult> MonthlySales()
    {
        var from = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-11);

        var rows = await _uow.Sales.Query()
            .Where(s => s.SaleDate >= from)
            .GroupBy(s => new { s.SaleDate.Year, s.SaleDate.Month })
            .Select(g => new
            {
                year  = g.Key.Year,
                month = g.Key.Month,
                total = g.Sum(x => x.TotalAmount)
            })
            .OrderBy(x => x.year).ThenBy(x => x.month)
            .ToListAsync();

        // Fill in months with zero sales
        var labels = Enumerable.Range(0, 12)
            .Select(i => from.AddMonths(i))
            .ToList();

        return Json(new
        {
            labels = labels.Select(d => d.ToString("MMM yy")).ToList(),
            sales  = labels.Select(d => rows.FirstOrDefault(r => r.year == d.Year && r.month == d.Month)?.total ?? 0).ToList()
        });
    }

    // ── Stock summary ────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> StockSummary()
    {
        var stockMap = await _uow.StockLedger.Query()
            .GroupBy(s => s.ProductId)
            .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Quantity) })
            .ToListAsync();

        var products = await _uow.Products.Query()
            .Include(p => p.Category)
            .Where(p => p.IsActive)
            .ToListAsync();

        var rows = products
            .Select(p => {
                var qty = stockMap.FirstOrDefault(s => s.ProductId == p.Id)?.Qty ?? 0;
                return new {
                    name     = p.Name,
                    sku      = p.SKU,
                    category = p.Category?.Name ?? "—",
                    qty      = qty,
                    value    = qty * p.CostPrice,
                    status   = qty <= 0 ? "out" : qty < 10 ? "low" : "ok"
                };
            })
            .OrderBy(r => r.qty)
            .ToList();

        return Json(rows);
    }
}
