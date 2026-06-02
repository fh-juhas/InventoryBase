using InventoryBase.Core.Entities;
using InventoryBase.Core.Interfaces.Repositories;
using InventoryBase.Core.Interfaces.Services;
using InventoryBase.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryBase.Web.Controllers;

[Authorize]
public class CategoryController : Controller
{
    private readonly IUnitOfWork  _uow;
    private readonly IHashService _hash;

    public CategoryController(IUnitOfWork uow, IHashService hash)
    { _uow = uow; _hash = hash; }

    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> Data([FromQuery] TabulatorRequest req)
    {
        try
        {
            var q = _uow.Categories.Query().Include(c => c.ParentCategory).AsQueryable();

            if (!string.IsNullOrWhiteSpace(req.search))
                q = q.Where(c => c.Name.Contains(req.search));

            if (req.status == "active")   q = q.Where(c => c.IsActive);
            if (req.status == "inactive") q = q.Where(c => !c.IsActive);

            q = (req.field, req.dir) switch
            {
                ("name",   "desc") => q.OrderByDescending(c => c.Name),
                ("parent", "asc")  => q.OrderBy(c => c.ParentCategory!.Name),
                ("parent", "desc") => q.OrderByDescending(c => c.ParentCategory!.Name),
                _                  => q.OrderBy(c => c.Name)
            };

            int total    = await q.CountAsync();
            int lastPage = (int)Math.Ceiling(total / (double)req.size);
            var items    = await q.Skip((req.page - 1) * req.size).Take(req.size).ToListAsync();

            return Json(new TabulatorResponse<object>
            {
                last_page = Math.Max(lastPage, 1),
                data = items.Select(c => new
                {
                    hash     = _hash.Encode(c.Id),
                    name     = c.Name,
                    parent   = c.ParentCategory?.Name ?? "— top level —",
                    status   = c.IsActive ? "active" : "inactive",
                    isActive = c.IsActive
                }).ToList<object>()
            });
        }
        catch (Exception)
        {
            return Json(new TabulatorResponse<object> { last_page = 1, data = new List<object>() });
        }
    }

    [HttpGet]
    public async Task<IActionResult> ParentList()
    {
        try
        {
            var list = await _uow.Categories.Query()
                .Where(c => c.ParentCategoryId == null && c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => new { id = c.Id, text = c.Name })
                .ToListAsync();
            return Json(list);
        }
        catch (Exception)
        {
            return Json(new List<object>());
        }
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        try
        {
            ViewBag.Parents = await GetParentsAsync();
            return View();
        }
        catch (Exception)
        {
            TempData["Error"] = "An error occurred loading the form.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Category model)
    {
        try
        {
            if (!ModelState.IsValid) { ViewBag.Parents = await GetParentsAsync(); return View(model); }
            await _uow.Categories.AddAsync(model);
            await _uow.SaveChangesAsync();
            TempData["Success"] = "Category created.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception)
        {
            ModelState.AddModelError("", "An unexpected error occurred while saving the category. Please try again.");
            ViewBag.Parents = await GetParentsAsync();
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
            var cat = await _uow.Categories.GetByIdAsync(realId.Value);
            if (cat == null) return NotFound();
            ViewBag.HashId  = id;
            ViewBag.Parents = await GetParentsAsync();
            return View(cat);
        }
        catch (Exception)
        {
            TempData["Error"] = "An error occurred loading the category.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, Category model)
    {
        try
        {
            var realId = _hash.Decode(id);
            if (realId == null) return BadRequest();
            model.Id = realId.Value;
            if (!ModelState.IsValid) { ViewBag.HashId = id; ViewBag.Parents = await GetParentsAsync(); return View(model); }
            _uow.Categories.Update(model);
            await _uow.SaveChangesAsync();
            TempData["Success"] = "Category updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception)
        {
            ModelState.AddModelError("", "An unexpected error occurred while updating the category. Please try again.");
            ViewBag.HashId  = id;
            ViewBag.Parents = await GetParentsAsync();
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
            var hasChildren = await _uow.Categories.Query().AnyAsync(c => c.ParentCategoryId == realId.Value);
            var hasProducts = await _uow.Products.Query().AnyAsync(p => p.CategoryId == realId.Value);
            if (hasChildren || hasProducts)
                return Json(new { success = false, message = "Cannot delete — has sub-categories or products." });
            var cat = await _uow.Categories.GetByIdAsync(realId.Value);
            if (cat == null) return Json(new { success = false, message = "Not found." });
            _uow.Categories.Remove(cat);
            await _uow.SaveChangesAsync();
            return Json(new { success = true });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An unexpected error occurred while deleting the category." });
        }
    }

    private async Task<IEnumerable<Category>> GetParentsAsync() =>
        await _uow.Categories.FindAsync(c => c.ParentCategoryId == null && c.IsActive);
}
