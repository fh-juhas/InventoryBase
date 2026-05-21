using InventoryBase.Core.Entities;
using InventoryBase.Core.Enums;
using InventoryBase.Core.Interfaces.Services;
using InventoryBase.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryBase.Web.Controllers;

[Authorize]
public class ExpenseController : Controller
{
    private readonly IExpenseService _expense;
    private readonly IHashService    _hash;

    public ExpenseController(IExpenseService expense, IHashService hash)
    { _expense = expense; _hash = hash; }

    public IActionResult Index(int? month, int? year)
    {
        ViewBag.Month = month ?? DateTime.Now.Month;
        ViewBag.Year  = year  ?? DateTime.Now.Year;
        return View();
    }

    // ── Tabulator AJAX ──────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Data([FromQuery] TabulatorRequest req)
    {
        int month = req.month ?? DateTime.Now.Month;
        int year  = req.year  ?? DateTime.Now.Year;

        var all = (await _expense.GetMonthAsync(month, year)).ToList();

        if (!string.IsNullOrWhiteSpace(req.search))
            all = all.Where(e => e.Category.Contains(req.search,
                StringComparison.OrdinalIgnoreCase)).ToList();

        if (req.status == "draft")     all = all.Where(e => e.Status == ExpenseStatus.Draft).ToList();
        if (req.status == "confirmed") all = all.Where(e => e.Status == ExpenseStatus.Confirmed).ToList();

        int total    = all.Count;
        int lastPage = (int)Math.Ceiling(total / (double)(req.size > 0 ? req.size : 20));
        var page     = all.Skip((req.page - 1) * req.size).Take(req.size).ToList();

        return Json(new TabulatorResponse<object>
        {
            last_page = Math.Max(lastPage, 1),
            data = page.Select(e => new
            {
                hash        = _hash.Encode(e.Id),
                category    = e.Category,
                description = e.Description ?? "—",
                amount      = e.Amount,
                status      = e.Status.ToString().ToLower(),
                isDraft     = e.Status == ExpenseStatus.Draft
            }).ToList<object>()
        });
    }

    // ── Month summary (hasDraft, total) ────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> MonthSummary(int month, int year)
    {
        bool hasDraft = await _expense.MonthHasDraftAsync(month, year);
        decimal total = await _expense.GetMonthTotalAsync(month, year);
        return Json(new { hasDraft, total });
    }

    // ── FIX: was [ValidateAntiForgeryToken] + RedirectToAction → now returns JSON
    // so the JS button handler can reload the table without a page refresh.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(int month, int year)
    {
        try
        {
            await _expense.GenerateFromTemplatesAsync(month, year);
            return Json(new { success = true, message = $"Expenses generated for {month}/{year}." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // ── FIX: same — return JSON not redirect
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int month, int year)
    {
        try
        {
            await _expense.ConfirmMonthAsync(month, year);
            return Json(new { success = true, message = $"Expenses for {month}/{year} confirmed and locked." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAmount(string id, decimal amount)
    {
        var realId = _hash.Decode(id);
        if (realId == null) return Json(new { success = false, message = "Invalid id." });
        await _expense.UpdateAmountAsync(realId.Value, amount);
        return Json(new { success = true });
    }

    // ── Templates ───────────────────────────────────────────────────────
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Templates() => View(await _expense.GetTemplatesAsync());

    [Authorize(Roles = "Admin"), HttpGet]
    public async Task<IActionResult> TemplateData([FromQuery] TabulatorRequest req)
    {
        var all = (await _expense.GetTemplatesAsync()).ToList();
        if (!string.IsNullOrWhiteSpace(req.search))
            all = all.Where(t => t.Name.Contains(req.search, StringComparison.OrdinalIgnoreCase)).ToList();
        int total    = all.Count;
        int lastPage = (int)Math.Ceiling(total / (double)(req.size > 0 ? req.size : 20));
        var page     = all.Skip((req.page - 1) * req.size).Take(req.size).ToList();
        return Json(new TabulatorResponse<object>
        {
            last_page = Math.Max(lastPage, 1),
            data = page.Select(t => new
            {
                hash          = _hash.Encode(t.Id),
                name          = t.Name,
                description   = t.Description ?? "—",
                defaultAmount = t.DefaultAmount
            }).ToList<object>()
        });
    }

    [Authorize(Roles = "Admin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTemplate(ExpenseTemplate model)
    {
        if (!ModelState.IsValid) return RedirectToAction(nameof(Templates));
        await _expense.CreateTemplateAsync(model);
        TempData["Success"] = "Template created.";
        return RedirectToAction(nameof(Templates));
    }

    [Authorize(Roles = "Admin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTemplate(string id)
    {
        var realId = _hash.Decode(id);
        if (realId == null) return Json(new { success = false, message = "Invalid id." });
        await _expense.DeleteTemplateAsync(realId.Value);
        return Json(new { success = true });
    }
}
