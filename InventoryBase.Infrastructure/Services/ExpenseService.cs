using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InventoryBase.Core.Entities;
using InventoryBase.Core.Enums;
using InventoryBase.Core.Interfaces.Repositories;
using InventoryBase.Core.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace InventoryBase.Infrastructure.Services
{
    public class ExpenseService : IExpenseService
    {
        private readonly IUnitOfWork _uow;
        public ExpenseService(IUnitOfWork uow) => _uow = uow;

        public async Task<IEnumerable<ExpenseTemplate>> GetTemplatesAsync() =>
            await _uow.ExpenseTemplates.Query()
                .Where(t => t.IsActive).OrderBy(t => t.Name).ToListAsync();

        public async Task CreateTemplateAsync(ExpenseTemplate t)
        {
            await _uow.ExpenseTemplates.AddAsync(t);
            await _uow.SaveChangesAsync();
        }

        public async Task DeleteTemplateAsync(int id)
        {
            var t = await _uow.ExpenseTemplates.GetByIdAsync(id);
            if (t != null) { _uow.ExpenseTemplates.Remove(t); await _uow.SaveChangesAsync(); }
        }

        public async Task<IEnumerable<Expense>> GetMonthAsync(int month, int year) =>
            await _uow.Expenses.Query()
                .Include(e => e.ExpenseTemplate)
                .Where(e => e.Month == month && e.Year == year)
                .OrderBy(e => e.Category).ToListAsync();

        public async Task<bool> MonthHasDraftAsync(int month, int year) =>
            await _uow.Expenses.Query().AnyAsync(e => e.Month == month && e.Year == year);

        // Carry-forward: uses last month's confirmed amounts; falls back to template defaults
        public async Task GenerateFromTemplatesAsync(int month, int year)
        {
            if (await MonthHasDraftAsync(month, year)) return;

            var prevMonth = month == 1 ? 12 : month - 1;
            var prevYear = month == 1 ? year - 1 : year;

            var prevConfirmed = await _uow.Expenses.Query()
                .Where(e => e.Month == prevMonth && e.Year == prevYear
                            && e.Status == ExpenseStatus.Confirmed)
                .ToListAsync();

            foreach (var template in await GetTemplatesAsync())
            {
                var prev = prevConfirmed.FirstOrDefault(e => e.ExpenseTemplateId == template.Id);
                var amount = prev?.Amount ?? template.DefaultAmount;

                await _uow.Expenses.AddAsync(new Expense
                {
                    Month = month,
                    Year = year,
                    Category = template.Name,
                    Description = template.Description,
                    Amount = amount,
                    ExpenseTemplateId = template.Id,
                    Status = ExpenseStatus.Draft
                });
            }
            await _uow.SaveChangesAsync();
        }

        public async Task UpdateAmountAsync(int expenseId, decimal newAmount)
        {
            var e = await _uow.Expenses.GetByIdAsync(expenseId);
            if (e == null || e.Status == ExpenseStatus.Confirmed) return;
            e.Amount = newAmount;
            _uow.Expenses.Update(e);
            await _uow.SaveChangesAsync();
        }

        public async Task DeleteExpenseAsync(int expenseId)
        {
            var e = await _uow.Expenses.GetByIdAsync(expenseId);
            if (e == null || e.Status == ExpenseStatus.Confirmed) return;
            _uow.Expenses.Remove(e);
            await _uow.SaveChangesAsync();
        }

        public async Task AddExpenseAsync(Expense expense)
        {
            await _uow.Expenses.AddAsync(expense);
            await _uow.SaveChangesAsync();
        }

        public async Task ConfirmMonthAsync(int month, int year)
        {
            var drafts = await _uow.Expenses.FindAsync(
                e => e.Month == month && e.Year == year && e.Status == ExpenseStatus.Draft);
            foreach (var d in drafts) { d.Status = ExpenseStatus.Confirmed; _uow.Expenses.Update(d); }
            await _uow.SaveChangesAsync();
        }

        public async Task<decimal> GetMonthTotalAsync(int month, int year) =>
            (await _uow.Expenses.FindAsync(e => e.Month == month && e.Year == year))
            .Sum(e => e.Amount);
    }
}
