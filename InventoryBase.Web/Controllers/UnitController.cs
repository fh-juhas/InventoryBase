using InventoryBase.Core.Entities;
using InventoryBase.Core.Interfaces.Repositories;
using InventoryBase.Core.Interfaces.Services;
using InventoryBase.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryBase.Web.Controllers;

[Authorize]
public class UnitController : Controller
{
    private readonly IUnitOfWork  _uow;
    private readonly IHashService _hash;

    public UnitController(IUnitOfWork uow, IHashService hash)
    { _uow = uow; _hash = hash; }

    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> Data([FromQuery] TabulatorRequest req)
    {
        try
        {
            var q = _uow.Units.Query().AsQueryable();

            if (!string.IsNullOrWhiteSpace(req.search))
                q = q.Where(u => u.Name.Contains(req.search));

            if (req.status == "active")   q = q.Where(u => u.IsActive);
            if (req.status == "inactive") q = q.Where(u => !u.IsActive);

            q = (req.field, req.dir) switch
            {
                ("name", "desc") => q.OrderByDescending(u => u.Name),
                _                => q.OrderBy(u => u.Name)
            };

            int total    = await q.CountAsync();
            int lastPage = (int)Math.Ceiling(total / (double)req.size);
            var items    = await q.Skip((req.page - 1) * req.size).Take(req.size).ToListAsync();

            return Json(new TabulatorResponse<object>
            {
                last_page = Math.Max(lastPage, 1),
                data = items.Select(u => new
                {
                    hash     = _hash.Encode(u.Id),
                    name     = u.Name,
                    isActive = u.IsActive,
                    status   = u.IsActive ? "active" : "inactive"
                }).ToList<object>()
            });
        }
        catch (Exception)
        {
            return Json(new TabulatorResponse<object> { last_page = 1, data = new List<object>() });
        }
    }

    [HttpGet] public IActionResult Create() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Unit model)
    {
        try
        {
            if (!ModelState.IsValid) return View(model);
            var exists = await _uow.Units.Query()
                .AnyAsync(u => u.Name.ToLower() == model.Name.ToLower());
            if (exists) { ModelState.AddModelError("Name", "Name already exists."); return View(model); }
            model.IsActive = true;
            await _uow.Units.AddAsync(model);
            await _uow.SaveChangesAsync();
            TempData["Success"] = $"Unit \"{model.Name}\" created.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception)
        {
            ModelState.AddModelError("", "An unexpected error occurred while saving the unit. Please try again.");
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        try
        {
            var realId = _hash.Decode(id);
            if (realId == null) return BadRequest();
            var unit = await _uow.Units.GetByIdAsync(realId.Value);
            if (unit == null) return NotFound();
            ViewBag.HashId = id;
            return View(unit);
        }
        catch (Exception)
        {
            TempData["Error"] = "An error occurred loading the unit.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, Unit model)
    {
        try
        {
            var realId = _hash.Decode(id);
            if (realId == null) return BadRequest();
            model.Id = realId.Value;
            if (!ModelState.IsValid) { ViewBag.HashId = id; return View(model); }
            var exists = await _uow.Units.Query()
                .AnyAsync(u => u.Name.ToLower() == model.Name.ToLower() && u.Id != model.Id);
            if (exists) { ModelState.AddModelError("Name", "Name already exists."); ViewBag.HashId = id; return View(model); }
            _uow.Units.Update(model);
            await _uow.SaveChangesAsync();
            TempData["Success"] = $"Unit \"{model.Name}\" updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception)
        {
            ModelState.AddModelError("", "An unexpected error occurred while updating the unit. Please try again.");
            ViewBag.HashId = id;
            return View(model);
        }
    }

    [Authorize(Roles = "Admin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        try
        {
            var realId = _hash.Decode(id);
            if (realId == null) return BadRequest();
            var hasProducts = await _uow.Products.Query().AnyAsync(p => p.UnitID == realId.Value);
            if (hasProducts)
                return Json(new { success = false, message = "Cannot delete — unit is assigned to products." });
            var unit = await _uow.Units.GetByIdAsync(realId.Value);
            if (unit == null) return Json(new { success = false, message = "Not found." });
            _uow.Units.Remove(unit);
            await _uow.SaveChangesAsync();
            return Json(new { success = true });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An unexpected error occurred while deleting the unit." });
        }
    }
}
