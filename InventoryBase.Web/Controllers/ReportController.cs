using InventoryBase.Core.Interfaces.Repositories;
using InventoryBase.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryBase.Web.Controllers;

[Authorize]
public class ReportController : Controller
{
    private readonly IUnitOfWork     _uow;
    private readonly ICompanyService _companySvc;

    public ReportController(IUnitOfWork uow, ICompanyService companySvc)
    { _uow = uow; _companySvc = companySvc; }

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

    // ── Detailed P&L: month-wise for a year ──────────────────────────────
    [HttpGet]
    public async Task<IActionResult> ProfitLossDetail(int? year)
    {
        var y         = year ?? DateTime.Now.Year;
        var company   = await _companySvc.GetAsync();
        var employees = await _uow.Employees.Query().Where(e => e.IsActive).ToListAsync();
        var monthlySalary = employees.Sum(e => e.Salary);
        var maxMonth  = y < DateTime.Now.Year ? 12 : (y == DateTime.Now.Year ? DateTime.Now.Month : 0);

        var yearStart = new DateTime(y, 1, 1);
        var yearEnd   = new DateTime(y + 1, 1, 1);

        var salesByMonth = await _uow.Sales.Query()
            .Where(s => s.SaleDate >= yearStart && s.SaleDate < yearEnd)
            .GroupBy(s => s.SaleDate.Month)
            .Select(g => new { Month = g.Key, Total = g.Sum(x => x.TotalAmount) })
            .ToListAsync();

        var purchByMonth = await _uow.Purchases.Query()
            .Where(p => p.PurchaseDate >= yearStart && p.PurchaseDate < yearEnd)
            .GroupBy(p => p.PurchaseDate.Month)
            .Select(g => new { Month = g.Key, Total = g.Sum(x => x.TotalAmount) })
            .ToListAsync();

        var expByMonth = await _uow.Expenses.Query()
            .Where(e => e.Year == y && e.Status == Core.Enums.ExpenseStatus.Confirmed)
            .GroupBy(e => e.Month)
            .Select(g => new { Month = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync();

        var rows = new List<object>();
        decimal totSales = 0, totPurchases = 0, totExpenses = 0, totSalaries = 0, totNet = 0;

        for (int m = 1; m <= maxMonth; m++)
        {
            var sales     = salesByMonth.FirstOrDefault(x => x.Month == m)?.Total ?? 0;
            var purchases = purchByMonth.FirstOrDefault(x => x.Month == m)?.Total ?? 0;
            var expenses  = expByMonth.FirstOrDefault(x => x.Month == m)?.Total ?? 0;
            var totalCost = purchases + expenses + monthlySalary;
            var net       = sales - totalCost;

            totSales     += sales;
            totPurchases += purchases;
            totExpenses  += expenses;
            totSalaries  += monthlySalary;
            totNet       += net;

            rows.Add(new { monthNum = m, monthName = new DateTime(y, m, 1).ToString("MMMM"), sales, purchases, expenses, salaries = monthlySalary, totalCost, net });
        }

        ViewBag.Rows          = rows;
        ViewBag.Year          = y;
        ViewBag.MaxMonth      = maxMonth;
        ViewBag.Company       = company;
        ViewBag.MonthlySalary = monthlySalary;
        ViewBag.EmployeeCount = employees.Count;
        ViewBag.TotSales      = totSales;
        ViewBag.TotPurchases  = totPurchases;
        ViewBag.TotExpenses   = totExpenses;
        ViewBag.TotSalaries   = totSalaries;
        ViewBag.TotNet        = totNet;
        return View();
    }

    // ── Product stock report (date range) ─────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> ProductReport(string? dateFrom, string? dateTo)
    {
        var dFrom   = DateTime.TryParse(dateFrom, out var df) ? df : new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var dTo     = DateTime.TryParse(dateTo,   out var dt) ? dt.AddDays(1) : DateTime.Now.Date.AddDays(1);
        var company = await _companySvc.GetAsync();

        var products = await _uow.Products.Query()
            .Include(p => p.Category).Include(p => p.Unit)
            .Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();

        var stockMap = await _uow.StockLedger.Query()
            .GroupBy(s => s.ProductId)
            .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Quantity) })
            .ToListAsync();

        var poIds = await _uow.Purchases.Query()
            .Where(p => p.PurchaseDate >= dFrom && p.PurchaseDate < dTo)
            .Select(p => p.Id).ToListAsync();

        var stockIn = await _uow.PurchaseItems.Query()
            .Where(i => poIds.Contains(i.PurchaseId))
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Quantity), Value = g.Sum(x => x.SubTotal) })
            .ToListAsync();

        var saleIds = await _uow.Sales.Query()
            .Where(s => s.SaleDate >= dFrom && s.SaleDate < dTo)
            .Select(s => s.Id).ToListAsync();

        var stockOut = await _uow.SaleItems.Query()
            .Where(i => saleIds.Contains(i.SaleId))
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Qty = g.Sum(x => x.Quantity), Value = g.Sum(x => x.SubTotal) })
            .ToListAsync();

        var rows = products.Select(p => new {
            name         = p.Name,
            sku          = p.SKU,
            category     = p.Category?.Name ?? "—",
            unit         = p.Unit?.Name ?? "—",
            currentStock = stockMap.FirstOrDefault(s => s.ProductId == p.Id)?.Qty ?? 0,
            inQty        = stockIn.FirstOrDefault(s => s.ProductId  == p.Id)?.Qty   ?? 0,
            inValue      = stockIn.FirstOrDefault(s => s.ProductId  == p.Id)?.Value ?? 0,
            outQty       = stockOut.FirstOrDefault(s => s.ProductId == p.Id)?.Qty   ?? 0,
            outValue     = stockOut.FirstOrDefault(s => s.ProductId == p.Id)?.Value ?? 0
        }).ToList();

        ViewBag.Rows     = rows;
        ViewBag.DateFrom = dFrom.ToString("dd MMM yyyy");
        ViewBag.DateTo   = dTo.AddDays(-1).ToString("dd MMM yyyy");
        ViewBag.Company  = company;
        return View();
    }

    // ── Customer revenue report (date range) ──────────────────────────────
    [HttpGet]
    public async Task<IActionResult> CustomerReport(string? dateFrom, string? dateTo)
    {
        var dFrom   = DateTime.TryParse(dateFrom, out var df) ? df : new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var dTo     = DateTime.TryParse(dateTo,   out var dt) ? dt.AddDays(1) : DateTime.Now.Date.AddDays(1);
        var company = await _companySvc.GetAsync();

        var sales = await _uow.Sales.Query()
            .Include(s => s.Customer)
            .Where(s => s.SaleDate >= dFrom && s.SaleDate < dTo)
            .ToListAsync();

        var totalRevenue = sales.Sum(s => s.TotalAmount);

        var rows = sales
            .GroupBy(s => new { s.CustomerId, Name = s.Customer?.Name ?? "Unknown", Phone = s.Customer?.Phone ?? "—" })
            .Select(g => new {
                name     = g.Key.Name,
                phone    = g.Key.Phone,
                invoices = g.Count(),
                total    = g.Sum(s => s.TotalAmount),
                pct      = totalRevenue > 0 ? Math.Round(g.Sum(s => s.TotalAmount) / totalRevenue * 100, 1) : 0m
            })
            .OrderByDescending(x => x.total)
            .ToList<object>();

        ViewBag.Rows          = rows;
        ViewBag.TotalRevenue  = totalRevenue;
        ViewBag.CustomerCount = rows.Count;
        ViewBag.DateFrom      = dFrom.ToString("dd MMM yyyy");
        ViewBag.DateTo        = dTo.AddDays(-1).ToString("dd MMM yyyy");
        ViewBag.Company       = company;
        return View();
    }

    // ── Supplier purchase report (date range) ─────────────────────────────
    [HttpGet]
    public async Task<IActionResult> SupplierReport(string? dateFrom, string? dateTo)
    {
        var dFrom   = DateTime.TryParse(dateFrom, out var df) ? df : new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var dTo     = DateTime.TryParse(dateTo,   out var dt) ? dt.AddDays(1) : DateTime.Now.Date.AddDays(1);
        var company = await _companySvc.GetAsync();

        var purchases = await _uow.Purchases.Query()
            .Include(p => p.Supplier)
            .Where(p => p.PurchaseDate >= dFrom && p.PurchaseDate < dTo)
            .ToListAsync();

        var totalPurchases = purchases.Sum(p => p.TotalAmount);

        var rows = purchases
            .GroupBy(p => new { p.SupplierId, Name = p.Supplier?.Name ?? "Unknown", Phone = p.Supplier?.Phone ?? "—" })
            .Select(g => new {
                name     = g.Key.Name,
                phone    = g.Key.Phone,
                invoices = g.Count(),
                total    = g.Sum(p => p.TotalAmount),
                pct      = totalPurchases > 0 ? Math.Round(g.Sum(p => p.TotalAmount) / totalPurchases * 100, 1) : 0m
            })
            .OrderByDescending(x => x.total)
            .ToList<object>();

        ViewBag.Rows           = rows;
        ViewBag.TotalPurchases = totalPurchases;
        ViewBag.SupplierCount  = rows.Count;
        ViewBag.DateFrom       = dFrom.ToString("dd MMM yyyy");
        ViewBag.DateTo         = dTo.AddDays(-1).ToString("dd MMM yyyy");
        ViewBag.Company        = company;
        return View();
    }
}
