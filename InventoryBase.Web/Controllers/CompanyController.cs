using InventoryBase.Core.Entities;
using InventoryBase.Core.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryBase.Web.Controllers;

[Authorize(Roles = "Admin")]
public class CompanyController : Controller
{
    private readonly ICompanyService _company;
    public CompanyController(ICompanyService company) => _company = company;

    public async Task<IActionResult> Settings() => View(await _company.GetAsync());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Settings(CompanySettings model, IFormFile? logoFile)
    {
        if (!ModelState.IsValid) return View(model);
        await _company.SaveAsync(model, logoFile?.OpenReadStream(), logoFile?.FileName);
        TempData["Success"] = "Company settings saved.";
        return RedirectToAction(nameof(Settings));
    }
}
