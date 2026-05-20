
using InventoryBase.Core.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace InventoryBase.Infrastructure.Data
{
    public class AppDbContext : IdentityDbContext<ApplicationUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<CompanySettings> CompanySettings => Set<CompanySettings>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Purchase> Purchases => Set<Purchase>();
        public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
        public DbSet<Sale> Sales => Set<Sale>();
        public DbSet<SaleItem> SaleItems => Set<SaleItem>();
        public DbSet<StockLedger> StockLedger => Set<StockLedger>();
        public DbSet<Employee> Employees => Set<Employee>();
        public DbSet<ExpenseTemplate> ExpenseTemplates => Set<ExpenseTemplate>();
        public DbSet<Expense> Expenses => Set<Expense>();
        public DbSet<Unit> Units => Set<Unit>();
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Self-referencing Category tree
            builder.Entity<Category>()
                .HasOne(c => c.ParentCategory)
                .WithMany(c => c.SubCategories)
                .HasForeignKey(c => c.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // Expense → Template: set null when template deleted
            builder.Entity<Expense>()
                .HasOne(e => e.ExpenseTemplate)
                .WithMany()
                .HasForeignKey(e => e.ExpenseTemplateId)
                .OnDelete(DeleteBehavior.SetNull);

            // Decimal precision
            builder.Entity<Product>(e =>
            {
                e.Property(p => p.CostPrice).HasPrecision(18, 2);
                e.Property(p => p.SalePrice).HasPrecision(18, 2);
            });

            builder.Entity<Purchase>()
                .Property(p => p.TotalAmount).HasPrecision(18, 2);

            builder.Entity<Sale>()
                .Property(s => s.TotalAmount).HasPrecision(18, 2);

            builder.Entity<Employee>()
                .Property(e => e.Salary).HasPrecision(18, 2);
            builder.Entity<ExpenseTemplate>()
                .Property(e => e.DefaultAmount).HasPrecision(18, 2);
            builder.Entity<Expense>()
                .Property(e => e.Amount).HasPrecision(18, 2);
            builder.Entity<PurchaseItem>(e =>
            {
                e.Property(p => p.Quantity).HasPrecision(18, 3);
                e.Property(p => p.UnitCost).HasPrecision(18, 2);
                e.Property(p => p.SubTotal).HasPrecision(18, 2);
            });
            builder.Entity<SaleItem>(e =>
            {
                e.Property(p => p.Quantity).HasPrecision(18, 3);
                e.Property(p => p.UnitPrice).HasPrecision(18, 2);
                e.Property(p => p.SubTotal).HasPrecision(18, 2);
            });
            builder.Entity<StockLedger>()
                .Property(s => s.Quantity).HasPrecision(18, 3);

            // No seed data — first Admin created via /Auth/Register
        }
    }
}