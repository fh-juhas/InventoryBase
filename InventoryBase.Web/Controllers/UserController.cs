using InventoryBase.Core.Interfaces.Services;
using InventoryBase.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryBase.Web.Controllers;

[Authorize(Roles = "Admin")]
public class UserController : Controller
{
    private readonly IUserService  _users;
    private readonly IHashService  _hash;

    public UserController(IUserService users, IHashService hash)
    { _users = users; _hash = hash; }

    public IActionResult Index() => View();

    [HttpGet]
    public async Task<IActionResult> Data([FromQuery] TabulatorRequest req)
    {
        var all = (await _users.GetAllAsync()).ToList();

        if (!string.IsNullOrWhiteSpace(req.search))
            all = all.Where(u =>
                u.FullName.Contains(req.search, StringComparison.OrdinalIgnoreCase) ||
                (u.Email ?? "").Contains(req.search, StringComparison.OrdinalIgnoreCase)).ToList();

        if (req.status == "active")   all = all.Where(u => u.IsActive).ToList();
        if (req.status == "inactive") all = all.Where(u => !u.IsActive).ToList();

        // resolve roles
        var vms = new List<object>();
        foreach (var u in all)
        {
            var roles = await _users.GetRolesAsync(u);
            var role  = roles.FirstOrDefault() ?? "—";
            if (!string.IsNullOrWhiteSpace(req.role) &&
                !role.Equals(req.role, StringComparison.OrdinalIgnoreCase)) continue;
            vms.Add(new
            {
                hash     = _hash.Encode(u.RowId),
                fullName = u.FullName,
                email    = u.Email ?? "",
                role     = role,
                status   = u.IsActive ? "active" : "inactive",
                isActive = u.IsActive
            });
        }

        int total    = vms.Count;
        int lastPage = (int)Math.Ceiling(total / (double)req.size);
        var page     = vms.Skip((req.page - 1) * req.size).Take(req.size).ToList<object>();

        return Json(new TabulatorResponse<object> { last_page = Math.Max(lastPage, 1), data = page });
    }

    // Role summary counts
    [HttpGet]
    public async Task<IActionResult> RoleCounts()
    {
        var all = (await _users.GetAllAsync()).ToList();
        int admins = 0, users = 0;
        foreach (var u in all)
        {
            var roles = await _users.GetRolesAsync(u);
            if (roles.Contains("Admin")) admins++;
            else users++;
        }
        return Json(new { admins, users, total = all.Count });
    }

    [HttpGet]  public IActionResult Create() => View();

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var ok = await _users.CreateAsync(model.FullName, model.Email, model.Password, model.Role);
        if (!ok) { ModelState.AddModelError("", "Failed to create user."); return View(model); }
        TempData["Success"] = "User created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(string id)
    {
        var rowId = _hash.Decode(id);
        if (rowId == null) return BadRequest();
        await _users.DeactivateAsync(rowId.Value);
        return Json(new { success = true });
    }
}
