using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InventoryBase.Core.Entities;

namespace InventoryBase.Core.Interfaces.Services
{
    public interface IExpenseService
    {
        // Templates
        Task<IEnumerable<ExpenseTemplate>> GetTemplatesAsync();
        Task CreateTemplateAsync(ExpenseTemplate template);
        Task DeleteTemplateAsync(int id);

        // Monthly flow
        Task<IEnumerable<Expense>> GetMonthAsync(int month, int year);
        Task GenerateFromTemplatesAsync(int month, int year);
        Task<bool> MonthHasDraftAsync(int month, int year);
        Task UpdateAmountAsync(int expenseId, decimal newAmount);
        Task ConfirmMonthAsync(int month, int year);
        Task<decimal> GetMonthTotalAsync(int month, int year);
    }
}
