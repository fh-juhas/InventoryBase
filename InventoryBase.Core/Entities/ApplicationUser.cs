using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace InventoryBase.Core.Entities
{
    public class ApplicationUser: IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Sequential int for URL hashing.
        // IdentityUser.Id is a GUID string — Hashids encodes ints only.
        // Assigned automatically in UserService.CreateAsync.
        public int RowId { get; set; }
    }
}
