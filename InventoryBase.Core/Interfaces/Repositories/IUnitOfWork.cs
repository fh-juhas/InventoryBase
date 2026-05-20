using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InventoryBase.Core.Entities;

namespace InventoryBase.Core.Interfaces.Repositories
{
    public interface IUnitOfWork : IDisposable
    {
        IRepository<CompanySettings> CompanySettings { get; }
        IRepository<Category> Categories { get; }
        IRepository<Product> Products { get; }
        IRepository<Supplier> Suppliers { get; }
        IRepository<Customer> Customers { get; }
        IRepository<Purchase> Purchases { get; }
        IRepository<PurchaseItem> PurchaseItems { get; }
        IRepository<Sale> Sales { get; }
        IRepository<SaleItem> SaleItems { get; }
        IRepository<StockLedger> StockLedger { get; }
        IRepository<Employee> Employees { get; }
        IRepository<ExpenseTemplate> ExpenseTemplates { get; }
        IRepository<Expense> Expenses { get; }
        IRepository<Unit> Units { get; }

        Task<int> SaveChangesAsync();
    }
}
