using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InventoryBase.Core.Entities;
using InventoryBase.Core.Interfaces.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InventoryBase.Infrastructure.Services
{
    public class UserService : IUserService
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public UserService(UserManager<ApplicationUser> userManager) => _userManager = userManager;

        public async Task<IEnumerable<ApplicationUser>> GetAllAsync() =>
            await _userManager.Users.OrderBy(u => u.FullName).ToListAsync();

        public async Task<ApplicationUser?> GetByRowIdAsync(int rowId) =>
            await _userManager.Users.FirstOrDefaultAsync(u => u.RowId == rowId);

        public async Task<bool> CreateAsync(string fullName, string email, string password, string role)
        {
            var maxRowId = await _userManager.Users.MaxAsync(u => (int?)u.RowId) ?? 0;
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = fullName,
                EmailConfirmed = true,
                RowId = maxRowId + 1
            };
            var result = await _userManager.CreateAsync(user, password);
            if (!result.Succeeded) return false;
            await _userManager.AddToRoleAsync(user, role);
            return true;
        }

        public async Task<bool> UpdateAsync(int rowId, string fullName, bool isActive)
        {
            var user = await GetByRowIdAsync(rowId);
            if (user == null) return false;
            user.FullName = fullName;
            user.IsActive = isActive;
            return (await _userManager.UpdateAsync(user)).Succeeded;
        }

        public async Task<bool> DeactivateAsync(int rowId)
        {
            var user = await GetByRowIdAsync(rowId);
            if (user == null) return false;
            user.IsActive = false;
            return (await _userManager.UpdateAsync(user)).Succeeded;
        }

        public async Task<IList<string>> GetRolesAsync(ApplicationUser user) =>
            await _userManager.GetRolesAsync(user);
    }
}
