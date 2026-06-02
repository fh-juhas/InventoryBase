using InventoryBase.Core.Entities;
using InventoryBase.Core.Interfaces.Repositories;
using InventoryBase.Core.Interfaces.Services;
using InventoryBase.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryBase.Web.Controllers;

[Authorize]
public class ProductController : Controller
{
    private readonly IUnitOfWork  _uow;
    private readonly IHashService _hash;

    public ProductController(IUnitOfWork uow, IHashService hash)
    { _uow = uow; _hash = hash; }

    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> Data([FromQuery] TabulatorRequest req)
    {
        try
        {
            var q = _uow.Products.Query()
                .Include(p => p.Category)
                .Include(p => p.Unit)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(req.search))
                q = q.Where(p => p.Name.Contains(req.search) || p.SKU.Contains(req.search));

            if (req.status == "active")   q = q.Where(p => p.IsActive);
            if (req.status == "inactive") q = q.Where(p => !p.IsActive);

            if (!string.IsNullOrWhiteSpace(req.category))
                q = q.Where(p => p.Category.Name == req.category);

            q = (req.field, req.dir) switch
            {
                ("name",      "desc") => q.OrderByDescending(p => p.Name),
                ("sku",       "asc")  => q.OrderBy(p => p.SKU),
                ("sku",       "desc") => q.OrderByDescending(p => p.SKU),
                ("salePrice", "asc")  => q.OrderBy(p => p.SalePrice),
                ("salePrice", "desc") => q.OrderByDescending(p => p.SalePrice),
                ("costPrice", "asc")  => q.OrderBy(p => p.CostPrice),
                ("costPrice", "desc") => q.OrderByDescending(p => p.CostPrice),
                _                     => q.OrderBy(p => p.Name)
            };

            int total    = await q.CountAsync();
            int lastPage = (int)Math.Ceiling(total / (double)req.size);
            var items    = await q.Skip((req.page - 1) * req.size).Take(req.size).ToListAsync();

            return Json(new TabulatorResponse<object>
            {
                last_page = Math.Max(lastPage, 1),
                data = items.Select(p => new
                {
                    hash      = _hash.Encode(p.Id),
                    name      = p.Name,
                    sku       = p.SKU,
                    category  = p.Category?.Name ?? "—",
                    unit      = p.Unit?.Name ?? "—",
                    costPrice = p.CostPrice,
                    salePrice = p.SalePrice,
                    status    = p.IsActive ? "active" : "inactive",
                    isActive  = p.IsActive
                }).ToList<object>()
            });
        }
        catch (Exception)
        {
            return Json(new TabulatorResponse<object> { last_page = 1, data = new List<object>() });
        }
    }

    [HttpGet]
    public async Task<IActionResult> CategoryList()
    {
        try
        {
            var list = await _uow.Categories.Query()
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => new { id = c.Name, text = c.Name })
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
            await PopulateViewBagAsync();
            return View();
        }
        catch (Exception)
        {
            TempData["Error"] = "An error occurred loading the form.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Product model)
    {
        try
        {
            if (!ModelState.IsValid) { await PopulateViewBagAsync(); return View(model); }
            if (await _uow.Products.Query().AnyAsync(p => p.SKU == model.SKU))
            {
                ModelState.AddModelError("SKU", "SKU already exists.");
                await PopulateViewBagAsync(); return View(model);
            }
            model.IsActive = true; model.CreatedAt = DateTime.Now;
            await _uow.Products.AddAsync(model);
            await _uow.SaveChangesAsync();
            TempData["Success"] = $"Product \"{model.Name}\" created.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception)
        {
            ModelState.AddModelError("", "An unexpected error occurred while saving the product. Please try again.");
            await PopulateViewBagAsync();
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
            var product = await _uow.Products.Query()
                .Include(p => p.Category).Include(p => p.Unit)
                .FirstOrDefaultAsync(p => p.Id == realId.Value);
            if (product == null) return NotFound();
            ViewBag.HashId = id;
            await PopulateViewBagAsync();
            return View(product);
        }
        catch (Exception)
        {
            TempData["Error"] = "An error occurred loading the product.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, Product model)
    {
        try
        {
            var realId = _hash.Decode(id);
            if (realId == null) return BadRequest();
            model.Id = realId.Value;
            if (!ModelState.IsValid) { ViewBag.HashId = id; await PopulateViewBagAsync(); return View(model); }
            if (await _uow.Products.Query().AnyAsync(p => p.SKU == model.SKU && p.Id != model.Id))
            {
                ModelState.AddModelError("SKU", "SKU already in use.");
                ViewBag.HashId = id; await PopulateViewBagAsync(); return View(model);
            }
            _uow.Products.Update(model);
            await _uow.SaveChangesAsync();
            TempData["Success"] = $"Product \"{model.Name}\" updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception)
        {
            ModelState.AddModelError("", "An unexpected error occurred while updating the product. Please try again.");
            ViewBag.HashId = id;
            await PopulateViewBagAsync();
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
            if (await _uow.StockLedger.Query().AnyAsync(s => s.ProductId == realId.Value))
                return Json(new { success = false, message = "Cannot delete — product has stock movements." });
            var product = await _uow.Products.GetByIdAsync(realId.Value);
            if (product == null) return Json(new { success = false, message = "Not found." });
            _uow.Products.Remove(product);
            await _uow.SaveChangesAsync();
            return Json(new { success = true });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An unexpected error occurred while deleting the product." });
        }
    }

    private async Task PopulateViewBagAsync()
    {
        ViewBag.Categories = await _uow.Categories.Query()
            .Where(c => c.IsActive).OrderBy(c => c.Name).ToListAsync();
        ViewBag.Units = await _uow.Units.Query()
            .Where(u => u.IsActive).OrderBy(u => u.Name).ToListAsync();
    }
}
