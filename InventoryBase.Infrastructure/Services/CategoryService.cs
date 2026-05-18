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
    public class CategoryService : ICategoryService
    {
        private readonly IUnitOfWork _uow;
        public CategoryService(IUnitOfWork uow) => _uow = uow;

        public async Task<IEnumerable<Category>> GetAllAsync() =>
            await _uow.Categories.Query()
                .Include(c => c.ParentCategory)
                .OrderBy(c => c.ParentCategoryId).ThenBy(c => c.Name)
                .ToListAsync();

        public async Task<Category?> GetByIdAsync(int id) =>
            await _uow.Categories.GetByIdAsync(id);

        public async Task<IEnumerable<Category>> GetParentCategoriesAsync() =>
            await _uow.Categories.FindAsync(c => c.ParentCategoryId == null && c.IsActive);

        public async Task CreateAsync(Category category)
        {
            await _uow.Categories.AddAsync(category);
            await _uow.SaveChangesAsync();
        }

        public async Task UpdateAsync(Category category)
        {
            _uow.Categories.Update(category);
            await _uow.SaveChangesAsync();
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var hasChildren = await _uow.Categories.Query().AnyAsync(c => c.ParentCategoryId == id);
            var hasProducts = await _uow.Products.Query().AnyAsync(p => p.CategoryId == id);
            if (hasChildren || hasProducts) return false;

            var cat = await _uow.Categories.GetByIdAsync(id);
            if (cat == null) return false;
            _uow.Categories.Remove(cat);
            await _uow.SaveChangesAsync();
            return true;
        }
    }
}
