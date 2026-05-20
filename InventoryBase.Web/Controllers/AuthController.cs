using InventoryBase.Core.Entities;
using InventoryBase.Web.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace InventoryBase.Web.Controllers;

public class AuthController : Controller
{
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly UserManager<ApplicationUser>  _users;

    public AuthController(SignInManager<ApplicationUser> signIn,
                          UserManager<ApplicationUser>  users)
    { _signIn = signIn; _users = users; }

    [HttpGet] public IActionResult Login()  => View();
    [HttpGet] public IActionResult Register() => View();

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel m)
    {
        if (!ModelState.IsValid) return View(m);
        var r = await _signIn.PasswordSignInAsync(m.Email, m.Password, m.RememberMe, false);
        if (r.Succeeded) return RedirectToAction("Index", "Dashboard");
        ModelState.AddModelError("", "Invalid credentials.");
        return View(m);
    }

    [HttpPost]
    public async Task<IActionResult> Register(string fullName, string email, string password)
    {
        var user = new ApplicationUser
        {
            UserName = email, Email = email, FullName = fullName,
            EmailConfirmed = true, RowId = 1
        };
        var r = await _users.CreateAsync(user, password);
        if (r.Succeeded) { await _users.AddToRoleAsync(user, "Admin"); return RedirectToAction("Login"); }
        foreach (var e in r.Errors) ModelState.AddModelError("", e.Description);
        return View();
    }

    [HttpPost] public async Task<IActionResult> Logout()
    { await _signIn.SignOutAsync(); return RedirectToAction("Login"); }

    public IActionResult AccessDenied() => View();
}
