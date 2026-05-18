using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InventoryBase.Core.Enums;

namespace InventoryBase.Core.Entities
{
    public class StockLedger
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public decimal Quantity { get; set; }  // +ve = stock in, -ve = stock out
        public StockMovementType MovementType { get; set; }
        public int? ReferenceId { get; set; }  // PurchaseId or SaleId
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Product Product { get; set; } = null!;
    }
}
