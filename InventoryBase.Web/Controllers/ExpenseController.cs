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

    [HttpGet]
    public async Task<IActionResult> Data([FromQuery] TabulatorRequest req)
    {
        try
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
        catch (Exception)
        {
            return Json(new TabulatorResponse<object> { last_page = 1, data = new List<object>() });
        }
    }

    [HttpGet]
    public async Task<IActionResult> MonthSummary(int month, int year)
    {
        try
        {
            bool hasDraft = await _expense.MonthHasDraftAsync(month, year);
            decimal total = await _expense.GetMonthTotalAsync(month, year);
            return Json(new { hasDraft, total });
        }
        catch (Exception)
        {
            return Json(new { hasDraft = false, total = 0 });
        }
    }

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
        try
        {
            var realId = _hash.Decode(id);
            if (realId == null) return Json(new { success = false, message = "Invalid id." });
            await _expense.UpdateAmountAsync(realId.Value, amount);
            return Json(new { success = true });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An unexpected error occurred while updating the amount." });
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteExpense(string id)
    {
        try
        {
            var realId = _hash.Decode(id);
            if (realId == null) return Json(new { success = false, message = "Invalid id." });
            await _expense.DeleteExpenseAsync(realId.Value);
            return Json(new { success = true });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddExpense(int month, int year, string category, string? description, decimal amount)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(category))
                return Json(new { success = false, message = "Category is required." });
            await _expense.AddExpenseAsync(new Expense
            {
                Month = month, Year = year,
                Category = category, Description = description,
                Amount = amount, Status = Core.Enums.ExpenseStatus.Draft
            });
            return Json(new { success = true });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An unexpected error occurred." });
        }
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Templates()
    {
        try
        {
            return View(await _expense.GetTemplatesAsync());
        }
        catch (Exception)
        {
            TempData["Error"] = "An error occurred loading expense templates.";
            return RedirectToAction(nameof(Index));
        }
    }

    [Authorize(Roles = "Admin"), HttpGet]
    public async Task<IActionResult> TemplateData([FromQuery] TabulatorRequest req)
    {
        try
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
        catch (Exception)
        {
            return Json(new TabulatorResponse<object> { last_page = 1, data = new List<object>() });
        }
    }

    [Authorize(Roles = "Admin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTemplate(ExpenseTemplate model)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please fill in all required fields for the template.";
                return RedirectToAction(nameof(Templates));
            }
            await _expense.CreateTemplateAsync(model);
            TempData["Success"] = "Template created.";
            return RedirectToAction(nameof(Templates));
        }
        catch (Exception)
        {
            TempData["Error"] = "An unexpected error occurred while creating the template.";
            return RedirectToAction(nameof(Templates));
        }
    }

    [Authorize(Roles = "Admin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTemplate(string id)
    {
        try
        {
            var realId = _hash.Decode(id);
            if (realId == null) return Json(new { success = false, message = "Invalid id." });
            await _expense.DeleteTemplateAsync(realId.Value);
            return Json(new { success = true });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An unexpected error occurred while deleting the template." });
        }
    }
}
