using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRLFruitstandESS.Data;
using CRLFruitstandESS.Models;

namespace CRLFruitstandESS.Controllers.Api
{
    /// <summary>
    /// REST API for Financial data — revenue, expenses, profit summary,
    /// and budget vs. actual comparison.
    /// Accessible by Admin, CFO, and CEO roles.
    /// </summary>
    [ApiController]
    [Route("api/financial")]
    [Authorize(Roles = "Admin,CFO,CEO")]
    public class FinancialApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public FinancialApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ────────────────────────────────────────────────────────
        // GET /api/financial/summary?from=&to=
        // Revenue, expenses, and net profit for the period.
        // ────────────────────────────────────────────────────────
        /// <summary>
        /// Returns total revenue, total expenses, net profit, and gross margin
        /// for the requested date range, plus a daily breakdown.
        /// </summary>
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var start = from?.Date ?? DateTime.Today.AddDays(-29);
            var end   = (to?.Date ?? DateTime.Today).AddDays(1).AddTicks(-1);

            var revenues = await _context.Revenues
                .Where(r => !r.IsDeleted && r.TransactionDate >= start && r.TransactionDate <= end)
                .ToListAsync();

            var expenses = await _context.Expenses
                .Where(e => !e.IsDeleted && e.ExpenseDate >= start && e.ExpenseDate <= end)
                .ToListAsync();

            var totalRevenue  = revenues.Sum(r => r.Amount);
            var totalExpenses = expenses.Sum(e => e.Amount);
            var netProfit     = totalRevenue - totalExpenses;
            var grossMargin   = totalRevenue > 0
                ? Math.Round(netProfit / totalRevenue * 100, 2)
                : 0m;

            // Build a unified daily timeline
            var allDates = revenues.Select(r => r.TransactionDate.Date)
                .Union(expenses.Select(e => e.ExpenseDate.Date))
                .Distinct()
                .OrderBy(d => d);

            var daily = allDates.Select(date => new
            {
                date     = date.ToString("yyyy-MM-dd"),
                revenue  = revenues.Where(r => r.TransactionDate.Date == date).Sum(r => r.Amount),
                expenses = expenses.Where(e => e.ExpenseDate.Date == date).Sum(e => e.Amount),
                profit   = revenues.Where(r => r.TransactionDate.Date == date).Sum(r => r.Amount)
                         - expenses.Where(e => e.ExpenseDate.Date == date).Sum(e => e.Amount)
            });

            return Ok(new
            {
                success       = true,
                timestamp     = DateTime.UtcNow,
                period        = new { from = start.ToString("yyyy-MM-dd"), to = end.Date.ToString("yyyy-MM-dd") },
                totalRevenue,
                totalExpenses,
                netProfit,
                grossMarginPercent = grossMargin,
                dailyBreakdown = daily
            });
        }

        // ────────────────────────────────────────────────────────
        // GET /api/financial/revenue?from=&to=&category=&page=1&pageSize=50
        // Paginated revenue records.
        // ────────────────────────────────────────────────────────
        /// <summary>
        /// Returns paginated revenue records with optional category and date filters.
        /// </summary>
        [HttpGet("revenue")]
        public async Task<IActionResult> GetRevenue(
            [FromQuery] DateTime? from     = null,
            [FromQuery] DateTime? to       = null,
            [FromQuery] string?   category = null,
            [FromQuery] int page           = 1,
            [FromQuery] int pageSize       = 50)
        {
            pageSize = Math.Clamp(pageSize, 1, 200);
            page     = Math.Max(1, page);

            var start = from?.Date ?? DateTime.Today.AddDays(-29);
            var end   = (to?.Date ?? DateTime.Today).AddDays(1).AddTicks(-1);

            var query = _context.Revenues
                .Where(r => !r.IsDeleted && r.TransactionDate >= start && r.TransactionDate <= end);

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(r => r.Category == category);

            var total = await query.CountAsync();

            var records = await query
                .OrderByDescending(r => r.TransactionDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Category breakdown for the full (unfiltered) period
            var allRevenues = await _context.Revenues
                .Where(r => !r.IsDeleted && r.TransactionDate >= start && r.TransactionDate <= end)
                .ToListAsync();

            var byCategory = allRevenues
                .GroupBy(r => r.Category)
                .Select(g => new { category = g.Key, total = g.Sum(r => r.Amount) })
                .OrderByDescending(x => x.total)
                .ToList();

            return Ok(new
            {
                success    = true,
                timestamp  = DateTime.UtcNow,
                periodTotal = allRevenues.Sum(r => r.Amount),
                byCategory,
                pagination = new { page, pageSize, total, totalPages = (int)Math.Ceiling(total / (double)pageSize) },
                data = records.Select(r => new
                {
                    id              = r.Id,
                    source          = r.Source,
                    category        = r.Category,
                    amount          = r.Amount,
                    transactionDate = r.TransactionDate.ToString("yyyy-MM-dd"),
                    notes           = r.Notes,
                    recordedBy      = r.RecordedBy,
                    createdAt       = r.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
                })
            });
        }

        // ────────────────────────────────────────────────────────
        // GET /api/financial/expenses?from=&to=&category=&page=1&pageSize=50
        // Paginated expense records.
        // ────────────────────────────────────────────────────────
        /// <summary>
        /// Returns paginated expense records with optional category and date filters.
        /// </summary>
        [HttpGet("expenses")]
        public async Task<IActionResult> GetExpenses(
            [FromQuery] DateTime? from     = null,
            [FromQuery] DateTime? to       = null,
            [FromQuery] string?   category = null,
            [FromQuery] int page           = 1,
            [FromQuery] int pageSize       = 50)
        {
            pageSize = Math.Clamp(pageSize, 1, 200);
            page     = Math.Max(1, page);

            var start = from?.Date ?? DateTime.Today.AddDays(-29);
            var end   = (to?.Date ?? DateTime.Today).AddDays(1).AddTicks(-1);

            var query = _context.Expenses
                .Where(e => !e.IsDeleted && e.ExpenseDate >= start && e.ExpenseDate <= end);

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(e => e.Category == category);

            var total = await query.CountAsync();

            var records = await query
                .OrderByDescending(e => e.ExpenseDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Category breakdown for the full period
            var allExpenses = await _context.Expenses
                .Where(e => !e.IsDeleted && e.ExpenseDate >= start && e.ExpenseDate <= end)
                .ToListAsync();

            var byCategory = allExpenses
                .GroupBy(e => e.Category)
                .Select(g => new { category = g.Key, total = g.Sum(e => e.Amount) })
                .OrderByDescending(x => x.total)
                .ToList();

            return Ok(new
            {
                success     = true,
                timestamp   = DateTime.UtcNow,
                periodTotal = allExpenses.Sum(e => e.Amount),
                byCategory,
                pagination  = new { page, pageSize, total, totalPages = (int)Math.Ceiling(total / (double)pageSize) },
                data = records.Select(e => new
                {
                    id          = e.Id,
                    description = e.Description,
                    category    = e.Category,
                    amount      = e.Amount,
                    expenseDate = e.ExpenseDate.ToString("yyyy-MM-dd"),
                    supplier    = e.Supplier,
                    notes       = e.Notes,
                    recordedBy  = e.RecordedBy,
                    createdAt   = e.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss")
                })
            });
        }

        // ────────────────────────────────────────────────────────
        // GET /api/financial/budget?year=&month=
        // Budget vs. actual comparison for a given month.
        // ────────────────────────────────────────────────────────
        /// <summary>
        /// Returns budget allocations vs. actual expenses for the given month,
        /// including variance and utilization percent per category.
        /// </summary>
        [HttpGet("budget")]
        public async Task<IActionResult> GetBudget(
            [FromQuery] int? year  = null,
            [FromQuery] int? month = null)
        {
            var targetYear  = year  ?? DateTime.Today.Year;
            var targetMonth = month ?? DateTime.Today.Month;

            var periodStart = new DateTime(targetYear, targetMonth, 1);
            var periodEnd   = periodStart.AddMonths(1).AddTicks(-1);

            var budgets = await _context.Budgets
                .Where(b => b.Year == targetYear && b.Month == targetMonth)
                .OrderBy(b => b.Category)
                .ToListAsync();

            var actualExpenses = await _context.Expenses
                .Where(e => !e.IsDeleted && e.ExpenseDate >= periodStart && e.ExpenseDate <= periodEnd)
                .ToListAsync();

            var totalAllocated = budgets.Sum(b => b.AllocatedAmount);
            var totalSpent     = budgets.Sum(b => b.SpentAmount);
            var totalActual    = actualExpenses.Sum(e => e.Amount);

            var items = budgets.Select(b => new
            {
                id              = b.Id,
                title           = b.Title,
                category        = b.Category,
                allocated       = b.AllocatedAmount,
                spent           = b.SpentAmount,
                remaining       = b.RemainingAmount,
                utilizationPct  = Math.Round(b.UtilizationPercent, 2),
                actualExpenses  = actualExpenses.Where(e => e.Category == b.Category).Sum(e => e.Amount),
                notes           = b.Notes
            });

            return Ok(new
            {
                success   = true,
                timestamp = DateTime.UtcNow,
                period    = new
                {
                    year  = targetYear,
                    month = targetMonth,
                    label = periodStart.ToString("MMMM yyyy")
                },
                totals = new
                {
                    totalAllocated,
                    totalSpent,
                    totalRemaining    = totalAllocated - totalSpent,
                    overallUtilization = totalAllocated > 0
                        ? Math.Round(totalSpent / totalAllocated * 100, 2)
                        : 0m,
                    actualExpensesFromLedger = totalActual
                },
                budgetItems = items
            });
        }

        // ────────────────────────────────────────────────────────
        // GET /api/financial/monthly-trend?months=6
        // Month-over-month revenue, expense, and profit trend.
        // ────────────────────────────────────────────────────────
        /// <summary>
        /// Returns a month-over-month trend of revenue, expenses, and net profit
        /// for the last N months (default 6, max 24).
        /// </summary>
        [HttpGet("monthly-trend")]
        public async Task<IActionResult> GetMonthlyTrend([FromQuery] int months = 6)
        {
            months = Math.Clamp(months, 1, 24);

            var since = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
                .AddMonths(-(months - 1));

            var revenues = await _context.Revenues
                .Where(r => !r.IsDeleted && r.TransactionDate >= since)
                .ToListAsync();

            var expenses = await _context.Expenses
                .Where(e => !e.IsDeleted && e.ExpenseDate >= since)
                .ToListAsync();

            var trend = Enumerable.Range(0, months)
                .Select(i =>
                {
                    var monthStart = since.AddMonths(i);
                    var monthEnd   = monthStart.AddMonths(1).AddTicks(-1);
                    var rev  = revenues.Where(r => r.TransactionDate >= monthStart && r.TransactionDate <= monthEnd).Sum(r => r.Amount);
                    var exp  = expenses.Where(e => e.ExpenseDate  >= monthStart && e.ExpenseDate  <= monthEnd).Sum(e => e.Amount);
                    return new
                    {
                        month    = monthStart.ToString("yyyy-MM"),
                        label    = monthStart.ToString("MMM yyyy"),
                        revenue  = rev,
                        expenses = exp,
                        profit   = rev - exp,
                        margin   = rev > 0 ? Math.Round((rev - exp) / rev * 100, 2) : 0m
                    };
                })
                .ToList();

            return Ok(new
            {
                success   = true,
                timestamp = DateTime.UtcNow,
                months,
                data      = trend
            });
        }

        // ────────────────────────────────────────────────────────
        // GET /api/financial/expense-categories
        // Distinct expense categories used in the system.
        // ────────────────────────────────────────────────────────
        /// <summary>Returns the list of distinct expense categories recorded in the system.</summary>
        [HttpGet("expense-categories")]
        public async Task<IActionResult> GetExpenseCategories()
        {
            var categories = await _context.Expenses
                .Where(e => !e.IsDeleted)
                .Select(e => e.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            return Ok(new { success = true, data = categories });
        }

        // ────────────────────────────────────────────────────────
        // GET /api/financial/revenue-categories
        // Distinct revenue categories used in the system.
        // ────────────────────────────────────────────────────────
        /// <summary>Returns the list of distinct revenue categories recorded in the system.</summary>
        [HttpGet("revenue-categories")]
        public async Task<IActionResult> GetRevenueCategories()
        {
            var categories = await _context.Revenues
                .Where(r => !r.IsDeleted)
                .Select(r => r.Category)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            return Ok(new { success = true, data = categories });
        }
    }
}
