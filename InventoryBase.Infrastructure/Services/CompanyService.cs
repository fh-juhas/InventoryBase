using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InventoryBase.Core.Entities;
using InventoryBase.Core.Interfaces.Repositories;
using InventoryBase.Core.Interfaces.Services;

namespace InventoryBase.Infrastructure.Services
{
    public class CompanyService : ICompanyService
    {
        private readonly IUnitOfWork _uow;
        private readonly string _webRootPath;

        // webRootPath injected as a string — keeps Core clean of IWebHostEnvironment (ASP.NET type)
        public CompanyService(IUnitOfWork uow, string webRootPath)
        {
            _uow = uow;
            _webRootPath = webRootPath;
        }

        public async Task<CompanySettings> GetAsync()
        {
            var s = await _uow.CompanySettings.GetByIdAsync(1);
            return s ?? new CompanySettings { Id = 1 };
        }

        public async Task SaveAsync(CompanySettings settings, Stream? logoStream, string? logoFileName)
        {
            var existing = await _uow.CompanySettings.GetByIdAsync(1)
                           ?? new CompanySettings { Id = 1 };

            existing.CompanyName = settings.CompanyName;
            existing.Address = settings.Address;
            existing.Phone = settings.Phone;
            existing.Email = settings.Email;
            existing.UpdatedAt = DateTime.UtcNow;

            if (logoStream != null && !string.IsNullOrEmpty(logoFileName))
            {
                var dir = Path.Combine(_webRootPath, "uploads", "logos");
                Directory.CreateDirectory(dir);
                var name = $"logo_{Guid.NewGuid()}{Path.GetExtension(logoFileName)}";
                await using var fileStream = new FileStream(Path.Combine(dir, name), FileMode.Create);
                await logoStream.CopyToAsync(fileStream);
                existing.LogoPath = $"/uploads/logos/{name}";
            }

            _uow.CompanySettings.Update(existing);
            await _uow.SaveChangesAsync();
        }
    }
}
