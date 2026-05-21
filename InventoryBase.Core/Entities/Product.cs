using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InventoryBase.Core.Entities
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SKU { get; set; } = string.Empty;
        public int UnitID { get; set; }
        public decimal CostPrice { get; set; }
        public decimal SalePrice { get; set; }
        public string? Description { get; set; }
        public int CategoryId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public Unit? Unit { get; set; }
        public Category? Category { get; set; }
    }
}
