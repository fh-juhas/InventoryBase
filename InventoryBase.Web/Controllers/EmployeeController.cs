using InventoryBase.Core.Entities;
using InventoryBase.Core.Interfaces.Repositories;
using InventoryBase.Core.Interfaces.Services;
using InventoryBase.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InventoryBase.Web.Controllers;

[Authorize]
public class EmployeeController : Controller
{
    private readonly IUnitOfWork  _uow;
    private readonly IHashService _hash;

    public EmployeeController(IUnitOfWork uow, IHashService hash)
    { _uow = uow; _hash = hash; }

    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> Data([FromQuery] TabulatorRequest req)
    {
        var q = _uow.Employees.Query().AsQueryable();

        if (!string.IsNullOrWhiteSpace(req.search))
            q = q.Where(e => e.Name.Contains(req.search) ||
                             (e.Role != null && e.Role.Contains(req.search)));

        if (req.status == "active")   q = q.Where(e => e.IsActive);
        if (req.status == "inactive") q = q.Where(e => !e.IsActive);

        q = (req.field, req.dir) switch
        {
            ("name",   "desc") => q.OrderByDescending(e => e.Name),
            ("salary", "asc")  => q.OrderBy(e => e.Salary),
            ("salary", "desc") => q.OrderByDescending(e => e.Salary),
            _                  => q.OrderBy(e => e.Name)
        };

        int total    = await q.CountAsync();
        int lastPage = (int)Math.Ceiling(total / (double)req.size);
        var items    = await q.Skip((req.page - 1) * req.size).Take(req.size).ToListAsync();

        return Json(new TabulatorResponse<object>
        {
            last_page = Math.Max(lastPage, 1),
            data = items.Select(e => new
            {
                hash     = _hash.Encode(e.Id),
                name     = e.Name,
                role     = e.Role ?? "—",
                phone    = e.Phone ?? "—",
                email    = e.Email ?? "—",
                salary   = e.Salary,
                joinDate = e.JoinDate.ToString("dd MMM yyyy"),
                status   = e.IsActive ? "active" : "inactive",
                isActive = e.IsActive
            }).ToList<object>()
        });
    }

    [HttpGet]  public IActionResult Create() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Employee model)
    {
        if (!ModelState.IsValid) return View(model);
        model.IsActive = true; model.CreatedAt = DateTime.Now;
        await _uow.Employees.AddAsync(model);
        await _uow.SaveChangesAsync();
        TempData["Success"] = $"Employee \"{model.Name}\" added.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var realId = _hash.Decode(id);
        if (realId == null) return BadRequest();
        var emp = await _uow.Employees.GetByIdAsync(realId.Value);
        if (emp == null) return NotFound();
        ViewBag.HashId = id;
        return View(emp);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, Employee model)
    {
        var realId = _hash.Decode(id);
        if (realId == null) return BadRequest();
        model.Id = realId.Value;
        if (!ModelState.IsValid) { ViewBag.HashId = id; return View(model); }
        _uow.Employees.Update(model);
        await _uow.SaveChangesAsync();
        TempData["Success"] = $"Employee \"{model.Name}\" updated.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var realId = _hash.Decode(id);
        if (realId == null) return BadRequest();
        var emp = await _uow.Employees.GetByIdAsync(realId.Value);
        if (emp == null) return Json(new { success = false, message = "Not found." });
        emp.IsActive = false;
        _uow.Employees.Update(emp);
        await _uow.SaveChangesAsync();
        return Json(new { success = true });
    }
}
