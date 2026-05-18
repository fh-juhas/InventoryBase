using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InventoryBase.Core.Entities;
using InventoryBase.Core.Interfaces.Repositories;
using InventoryBase.Infrastructure.Data;

namespace InventoryBase.Infrastructure.Repositories
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _ctx;

        public UnitOfWork(AppDbContext ctx)
        {
            _ctx = ctx;
            CompanySettings = new Repository<CompanySettings>(ctx);
            Categories = new Repository<Category>(ctx);
            Products = new Repository<Product>(ctx);
            Suppliers = new Repository<Supplier>(ctx);
            Customers = new Repository<Customer>(ctx);
            Purchases = new Repository<Purchase>(ctx);
            PurchaseItems = new Repository<PurchaseItem>(ctx);
            Sales = new Repository<Sale>(ctx);
            SaleItems = new Repository<SaleItem>(ctx);
            StockLedger = new Repository<StockLedger>(ctx);
            Employees = new Repository<Employee>(ctx);
            ExpenseTemplates = new Repository<ExpenseTemplate>(ctx);
            Expenses = new Repository<Expense>(ctx);
        }

        public IRepository<CompanySettings> CompanySettings { get; }
        public IRepository<Category> Categories { get; }
        public IRepository<Product> Products { get; }
        public IRepository<Supplier> Suppliers { get; }
        public IRepository<Customer> Customers { get; }
        public IRepository<Purchase> Purchases { get; }
        public IRepository<PurchaseItem> PurchaseItems { get; }
        public IRepository<Sale> Sales { get; }
        public IRepository<SaleItem> SaleItems { get; }
        public IRepository<StockLedger> StockLedger { get; }
        public IRepository<Employee> Employees { get; }
        public IRepository<ExpenseTemplate> ExpenseTemplates { get; }
        public IRepository<Expense> Expenses { get; }

        public async Task<int> SaveChangesAsync() => await _ctx.SaveChangesAsync();
        public void Dispose() => _ctx.Dispose();
    }
}
