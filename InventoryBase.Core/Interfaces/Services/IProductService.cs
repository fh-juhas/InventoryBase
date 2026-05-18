using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InventoryBase.Core.Entities;

namespace InventoryBase.Core.Interfaces.Services
{
    public interface IProductService
    {
        Task<IEnumerable<Product>> GetAllAsync();
        Task<Product?> GetByIdAsync(int id);
        Task<bool> SKUExistsAsync(string sku, int? excludeId = null);
        Task CreateAsync(Product product);
        Task UpdateAsync(Product product);
        Task<bool> DeleteAsync(int id);
    }
}
