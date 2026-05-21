using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InventoryBase.Core.Entities
{
    public class Sale
    {
        public int Id { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
        public DateTime SaleDate { get; set; }
        public int CustomerId { get; set; }
        public decimal TotalAmount { get; set; }
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Customer? Customer { get; set; } 
        public ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
    }

    public class SaleItem
    {
        public int Id { get; set; }
        public int SaleId { get; set; }
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal SubTotal { get; set; }

        public Sale? Sale { get; set; }
        public Product? Product { get; set; }
    }
}
