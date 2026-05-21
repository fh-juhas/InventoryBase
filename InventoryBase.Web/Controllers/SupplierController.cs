using InventoryBase.Core.Entities;
using InventoryBase.Core.Interfaces.Repositories;
using InventoryBase.Core.Interfaces.Services;
using InventoryBase.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryBase.Web.Controllers;

[Authorize]
public class SupplierController : Controller
{
    private readonly IUnitOfWork  _uow;
    private readonly IHashService _hash;

    public SupplierController(IUnitOfWork uow, IHashService hash)
    { _uow = uow; _hash = hash; }

    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> Data([FromQuery] TabulatorRequest req)
    {
        var q = _uow.Suppliers.Query().AsQueryable();

        if (!string.IsNullOrWhiteSpace(req.search))
            q = q.Where(s => s.Name.Contains(req.search) ||
                             (s.Phone != null && s.Phone.Contains(req.search)) ||
                             (s.Email != null && s.Email.Contains(req.search)));

        if (req.status == "active")   q = q.Where(s => s.IsActive);
        if (req.status == "inactive") q = q.Where(s => !s.IsActive);

        q = req.dir == "desc"
            ? q.OrderByDescending(s => s.Name)
            : q.OrderBy(s => s.Name);

        int total    = await q.CountAsync();
        int lastPage = (int)Math.Ceiling(total / (double)(req.size > 0 ? req.size : 20));
        var items    = await q.Skip((req.page - 1) * req.size).Take(req.size).ToListAsync();

        return Json(new TabulatorResponse<object>
        {
            last_page = Math.Max(lastPage, 1),
            data = items.Select(s => new
            {
                hash          = _hash.Encode(s.Id),
                name          = s.Name,
                contactPerson = s.ContactPerson ?? "—",
                phone         = s.Phone ?? "—",
                email         = s.Email ?? "—",
                status        = s.IsActive ? "active" : "inactive",
                isActive      = s.IsActive
            }).ToList<object>()
        });
    }

    [HttpGet] public IActionResult Create() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Supplier model)
    {
        if (!ModelState.IsValid) return View(model);
        model.IsActive  = true;
        model.CreatedAt = DateTime.Now;
        await _uow.Suppliers.AddAsync(model);
        await _uow.SaveChangesAsync();
        TempData["Success"] = $"Supplier \"{model.Name}\" created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var realId = _hash.Decode(id);
        if (realId == null) return BadRequest();
        var s = await _uow.Suppliers.GetByIdAsync(realId.Value);
        if (s == null) return NotFound();
        ViewBag.HashId = id;
        return View(s);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, Supplier model)
    {
        var realId = _hash.Decode(id);
        if (realId == null) return BadRequest();
        model.Id = realId.Value;
        if (!ModelState.IsValid) { ViewBag.HashId = id; return View(model); }
        _uow.Suppliers.Update(model);
        await _uow.SaveChangesAsync();
        TempData["Success"] = $"Supplier \"{model.Name}\" updated.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var realId = _hash.Decode(id);
        if (realId == null) return Json(new { success = false, message = "Invalid id." });
        var hasPurchases = await _uow.Purchases.Query().AnyAsync(p => p.SupplierId == realId.Value);
        if (hasPurchases)
            return Json(new { success = false, message = "Cannot delete — supplier has purchase records." });
        var s = await _uow.Suppliers.GetByIdAsync(realId.Value);
        if (s == null) return Json(new { success = false, message = "Not found." });
        _uow.Suppliers.Remove(s);
        await _uow.SaveChangesAsync();
        return Json(new { success = true });
    }
}
