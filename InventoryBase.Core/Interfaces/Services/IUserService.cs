using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InventoryBase.Core.Entities;

namespace InventoryBase.Core.Interfaces.Services
{
    public interface IUserService
    {
        Task<IEnumerable<ApplicationUser>> GetAllAsync();
        Task<ApplicationUser?> GetByRowIdAsync(int rowId);
        Task<bool> CreateAsync(string fullName, string email, string password, string role);
        Task<bool> UpdateAsync(int rowId, string fullName, bool isActive);
        Task<bool> DeactivateAsync(int rowId);
        Task<IList<string>> GetRolesAsync(ApplicationUser user);
    }
}
