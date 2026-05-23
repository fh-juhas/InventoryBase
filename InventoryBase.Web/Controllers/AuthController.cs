using InventoryBase.Core.Entities;
using InventoryBase.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
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

    [HttpGet] public IActionResult Login()    => View();
    [HttpGet] public IActionResult Register() => View();
    //Admin@12345
    public    IActionResult AccessDenied()    => View();

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel m)
    {
        if (!ModelState.IsValid) return View(m);
        var r = await _signIn.PasswordSignInAsync(m.Email, m.Password, m.RememberMe, false);
        if (r.Succeeded) return RedirectToAction("Index", "Dashboard");
        ModelState.AddModelError("", "Invalid email or password.");
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

    [HttpPost]
    public async Task<IActionResult> Logout()
    { await _signIn.SignOutAsync(); return RedirectToAction("Login"); }

    // ── Change own password (any logged-in user) ────────────────────────
    [Authorize, HttpGet]
    public IActionResult ChangePassword() => View();

    [Authorize, HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel m)
    {
        if (!ModelState.IsValid) return View(m);
        var user = await _users.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login");

        var result = await _users.ChangePasswordAsync(user, m.CurrentPassword, m.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            return View(m);
        }
        await _signIn.RefreshSignInAsync(user);
        TempData["Success"] = "Password changed successfully.";
        return RedirectToAction("Index", "Dashboard");
    }

    // ── Admin reset any user's password (no email needed) ───────────────
    [Authorize(Roles = "Admin"), HttpGet]
    public async Task<IActionResult> ResetPassword(string id)
    {
        // id is the user's hashed RowId — passed from User list
        var user = await _users.FindByIdAsync(id); // id = IdentityUser GUID
        if (user == null) return NotFound();
        ViewBag.UserId   = id;
        ViewBag.UserName = user.FullName ?? user.Email;
        return View();
    }

    [Authorize(Roles = "Admin"), HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string id, AdminResetPasswordViewModel m)
    {
        if (!ModelState.IsValid) { ViewBag.UserId = id; return View(m); }
        var user = await _users.FindByIdAsync(id);
        if (user == null) return NotFound();

        // remove old password and set new one
        var token  = await _users.GeneratePasswordResetTokenAsync(user);
        var result = await _users.ResetPasswordAsync(user, token, m.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
            ViewBag.UserId   = id;
            ViewBag.UserName = user.FullName ?? user.Email;
            return View(m);
        }
        TempData["Success"] = $"Password reset for {user.FullName ?? user.Email}.";
        return RedirectToAction("Index", "User");
    }
}
