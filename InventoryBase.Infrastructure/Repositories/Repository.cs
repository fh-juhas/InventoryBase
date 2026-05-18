using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using InventoryBase.Core.Interfaces.Repositories;
using InventoryBase.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace InventoryBase.Infrastructure.Repositories
{
    public class Repository<T> : IRepository<T> where T : class
    {
        protected readonly AppDbContext _ctx;
        protected readonly DbSet<T> _set;

        public Repository(AppDbContext ctx) { _ctx = ctx; _set = ctx.Set<T>(); }

        public async Task<T?> GetByIdAsync(int id) => await _set.FindAsync(id);
        public async Task<IEnumerable<T>> GetAllAsync() => await _set.ToListAsync();
        public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> p) => await _set.Where(p).ToListAsync();
        public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> p) => await _set.FirstOrDefaultAsync(p);
        public async Task AddAsync(T entity) => await _set.AddAsync(entity);
        public void Update(T entity) => _set.Update(entity);
        public void Remove(T entity) => _set.Remove(entity);
        public void RemoveRange(IEnumerable<T> entities) => _set.RemoveRange(entities);
        public IQueryable<T> Query() => _set.AsQueryable();
    }
}
