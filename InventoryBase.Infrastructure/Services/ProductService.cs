using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InventoryBase.Core.Entities;
using InventoryBase.Core.Interfaces.Repositories;
using InventoryBase.Core.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace InventoryBase.Infrastructure.Services
{
    public class ProductService : IProductService
    {
        private readonly IUnitOfWork _uow;
        public ProductService(IUnitOfWork uow) => _uow = uow;

        public async Task<IEnumerable<Product>> GetAllAsync() =>
            await _uow.Products.Query()
                .Include(p => p.Category)
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync();

        public async Task<Product?> GetByIdAsync(int id) =>
            await _uow.Products.Query()
                .Include(p => p.Category)
                .FirstOrDefaultAsync(p => p.Id == id);

        public async Task<bool> SKUExistsAsync(string sku, int? excludeId = null) =>
            await _uow.Products.Query()
                .AnyAsync(p => p.SKU == sku && (excludeId == null || p.Id != excludeId));

        public async Task CreateAsync(Product product)
        {
            await _uow.Products.AddAsync(product);
            await _uow.SaveChangesAsync();
        }

        public async Task UpdateAsync(Product product)
        {
            _uow.Products.Update(product);
            await _uow.SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var hasStock = await _uow.StockLedger.Query().AnyAsync(s => s.ProductId == id);
            if (hasStock) return false;

            var product = await _uow.Products.GetByIdAsync(id);
            if (product == null) return false;
            _uow.Products.Remove(product);
            await _uow.SaveChangesAsync();
            return true;
        }
    }
}
