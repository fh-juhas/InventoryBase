using InventoryBase.Core.Entities;
using InventoryBase.Core.Interfaces.Repositories;
using InventoryBase.Core.Interfaces.Services;

namespace InventoryBase.Infrastructure.Services;

public class CompanyService : ICompanyService
{
    private readonly IUnitOfWork _uow;
    private readonly string _webRootPath;

    public CompanyService(IUnitOfWork uow, string webRootPath)
    {
        _uow = uow;
        _webRootPath = webRootPath;
    }

    public async Task<CompanySettings> GetAsync()
    {
        // Grab the first row — there's only ever one
        var s = await _uow.CompanySettings.FirstOrDefaultAsync(_ => true);
        return s ?? new CompanySettings();
    }

    public async Task SaveAsync(CompanySettings settings, Stream? logoStream, string? logoFileName)
    {
        var existing = await _uow.CompanySettings.FirstOrDefaultAsync(_ => true);
        bool isNew = existing == null;

        if (isNew)
            existing = new CompanySettings();  // don't set Id — let DB auto-generate

        existing!.CompanyName = settings.CompanyName;
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

        if (isNew)
            await _uow.CompanySettings.AddAsync(existing);
        else
            _uow.CompanySettings.Update(existing);

        await _uow.SaveChangesAsync();
    }
}