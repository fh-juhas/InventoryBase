using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InventoryBase.Core.Enums;

namespace InventoryBase.Core.Entities
{
    public class Expense
    {
        public int Id { get; set; }
        public int Month { get; set; }
        public int Year { get; set; }
        public string Category { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public int? ExpenseTemplateId { get; set; }
        public ExpenseStatus Status { get; set; } = ExpenseStatus.Draft;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ExpenseTemplate? ExpenseTemplate { get; set; }
    }
}
