using InventoryBase.Core.Entities;
using InventoryBase.Core.Interfaces.Repositories;
using InventoryBase.Core.Interfaces.Services;
using InventoryBase.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryBase.Web.Controllers;

[Authorize]
public class CustomerController : Controller
{
    private readonly IUnitOfWork  _uow;
    private readonly IHashService _hash;

    public CustomerController(IUnitOfWork uow, IHashService hash)
    { _uow = uow; _hash = hash; }

    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> Data([FromQuery] TabulatorRequest req)
    {
        try
        {
            var q = _uow.Customers.Query().AsQueryable();

            if (!string.IsNullOrWhiteSpace(req.search))
                q = q.Where(c => c.Name.Contains(req.search) ||
                                 (c.Phone != null && c.Phone.Contains(req.search)) ||
                                 (c.Email != null && c.Email.Contains(req.search)));

            if (req.status == "active")   q = q.Where(c => c.IsActive);
            if (req.status == "inactive") q = q.Where(c => !c.IsActive);

            q = req.dir == "desc"
                ? q.OrderByDescending(c => c.Name)
                : q.OrderBy(c => c.Name);

            int total    = await q.CountAsync();
            int lastPage = (int)Math.Ceiling(total / (double)(req.size > 0 ? req.size : 20));
            var items    = await q.Skip((req.page - 1) * req.size).Take(req.size).ToListAsync();

            return Json(new TabulatorResponse<object>
            {
                last_page = Math.Max(lastPage, 1),
                data = items.Select(c => new
                {
                    hash     = _hash.Encode(c.Id),
                    name     = c.Name,
                    phone    = c.Phone ?? "—",
                    email    = c.Email ?? "—",
                    address  = c.Address ?? "—",
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

    [HttpGet] public IActionResult Create() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Customer model)
    {
        try
        {
            if (!ModelState.IsValid) return View(model);
            model.IsActive  = true;
            model.CreatedAt = DateTime.Now;
            await _uow.Customers.AddAsync(model);
            await _uow.SaveChangesAsync();
            TempData["Success"] = $"Customer \"{model.Name}\" created.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception)
        {
            ModelState.AddModelError("", "An unexpected error occurred while saving the customer. Please try again.");
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
            var c = await _uow.Customers.GetByIdAsync(realId.Value);
            if (c == null) return NotFound();
            ViewBag.HashId = id;
            return View(c);
        }
        catch (Exception)
        {
            TempData["Error"] = "An error occurred loading the customer.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, Customer model)
    {
        try
        {
            var realId = _hash.Decode(id);
            if (realId == null) return BadRequest();
            model.Id = realId.Value;
            if (!ModelState.IsValid) { ViewBag.HashId = id; return View(model); }
            _uow.Customers.Update(model);
            await _uow.SaveChangesAsync();
            TempData["Success"] = $"Customer \"{model.Name}\" updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception)
        {
            ModelState.AddModelError("", "An unexpected error occurred while updating the customer. Please try again.");
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
            if (realId == null) return Json(new { success = false, message = "Invalid id." });
            var hasSales = await _uow.Sales.Query().AnyAsync(s => s.CustomerId == realId.Value);
            if (hasSales)
                return Json(new { success = false, message = "Cannot delete — customer has sale records." });
            var c = await _uow.Customers.GetByIdAsync(realId.Value);
            if (c == null) return Json(new { success = false, message = "Not found." });
            _uow.Customers.Remove(c);
            await _uow.SaveChangesAsync();
            return Json(new { success = true });
        }
        catch (Exception)
        {
            return Json(new { success = false, message = "An unexpected error occurred while deleting the customer." });
        }
    }
}
