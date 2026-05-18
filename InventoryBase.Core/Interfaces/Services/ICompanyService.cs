using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InventoryBase.Core.Entities;

namespace InventoryBase.Core.Interfaces.Services
{
    public interface ICompanyService
    {
        Task<CompanySettings> GetAsync();
        // logoStream + fileName passed separately so Core has no dependency on IFormFile (ASP.NET type)
        Task SaveAsync(CompanySettings settings, Stream? logoStream, string? logoFileName);
    }
}
