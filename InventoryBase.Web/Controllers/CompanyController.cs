using InventoryBase.Core.Entities;
using InventoryBase.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryBase.Web.Controllers;

[Authorize(Roles = "Admin")]
public class CompanyController : Controller
{
    private readonly ICompanyService _company;
    private readonly ILogger<CompanyController> _logger;
    public CompanyController(ICompanyService company, ILogger<CompanyController> logger)
    {
        _company = company;
        _logger = logger;
    }

    public async Task<IActionResult> Settings()
    {
        try
        {
            return View(await _company.GetAsync());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load company settings.");
            TempData["Error"] = "An error occurred loading company settings.";
            return RedirectToAction("Index", "Dashboard");
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(CompanySettings model, IFormFile? logoFile)
    {
        try
        {
            if (!ModelState.IsValid) return View(model);
            await _company.SaveAsync(model, logoFile?.OpenReadStream(), logoFile?.FileName);
            TempData["Success"] = "Company settings saved.";
            return RedirectToAction(nameof(Settings));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save company settings (logo file: {FileName})", logoFile?.FileName);
            ModelState.AddModelError("", "An unexpected error occurred while saving company settings. Please try again.");
            return View(model);
        }
    }
}
