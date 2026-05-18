using InventoryBase.Core.Entities;
using InventoryBase.Core.Interfaces.Repositories;
using InventoryBase.Core.Interfaces.Services;
using InventoryBase.Infrastructure.Data;
using InventoryBase.Infrastructure.Repositories;
using InventoryBase.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
var builder = WebApplication.CreateBuilder(args);

// ── Database
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(o =>
{
    o.Password.RequireDigit = false;
    o.Password.RequiredLength = 6;
    o.Password.RequireNonAlphanumeric = false;
    o.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Auth/Login";
    o.AccessDeniedPath = "/Auth/AccessDenied";
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
});

// ── Unit of Work
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// ── Services
builder.Services.AddScoped<IHashService, HashService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IExpenseService, ExpenseService>();

// CompanyService needs webRootPath — resolved after app is built (see below)
// Register as factory so IWebHostEnvironment is resolved at runtime
builder.Services.AddScoped<ICompanyService>(sp =>
{
    var uow = sp.GetRequiredService<IUnitOfWork>();
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    return new CompanyService(uow, env.WebRootPath);
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

// ── Ensure DB migrated + roles created (no data seeded)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    await db.Database.MigrateAsync();
    foreach (var role in new[] { "Admin", "User" })
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
}


app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute("default", "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();