using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRLFruitstandESS.Data;
using CRLFruitstandESS.Models;
using CRLFruitstandESS.Models.ViewModels;
using ClosedXML.Excel;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Colors;
using iText.Kernel.Font;

namespace CRLFruitstandESS.Controllers
{
    [Authorize(Roles = "CFO,Admin")]
    public class CfoDashboardController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public CfoDashboardController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // ── DASHBOARD ────────────────────────────────
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var weekStart = today.AddDays(-(int)today.DayOfWeek);

            var vm = new CfoDashboardViewModel
            {
                FullName = user?.FullName ?? "CFO",
                LastLoginAt = user?.LastLoginAt ?? DateTime.UtcNow,

                // Today
                TodayRevenue = await _db.Revenues
                    .Where(r => !r.IsDeleted && r.TransactionDate.Date == today)
                    .SumAsync(r => r.Amount),
                TodayExpenses = await _db.Expenses
                    .Where(e => !e.IsDeleted && e.ExpenseDate.Date == today)
                    .SumAsync(e => e.Amount),

                // This month
                MonthRevenue = await _db.Revenues
                    .Where(r => !r.IsDeleted && r.TransactionDate >= monthStart)
                    .SumAsync(r => r.Amount),
                MonthExpenses = await _db.Expenses
                    .Where(e => !e.IsDeleted && e.ExpenseDate >= monthStart)
                    .SumAsync(e => e.Amount),

                // Budget
                TotalBudgetAllocated = await _db.Budgets
                    .Where(b => b.Month == today.Month && b.Year == today.Year)
                    .SumAsync(b => b.AllocatedAmount),
                TotalBudgetSpent = await _db.Budgets
                    .Where(b => b.Month == today.Month && b.Year == today.Year)
                    .SumAsync(b => b.SpentAmount),

                // Recent records
                RecentRevenues = await _db.Revenues
                    .Where(r => !r.IsDeleted)
                    .OrderByDescending(r => r.TransactionDate)
                    .Take(5).ToListAsync(),
                RecentExpenses = await _db.Expenses
                    .Where(e => !e.IsDeleted)
                    .OrderByDescending(e => e.ExpenseDate)
                    .Take(5).ToListAsync(),
                ActiveBudgets = await _db.Budgets
                    .Where(b => b.Month == today.Month && b.Year == today.Year)
                    .ToListAsync(),
            };

            vm.TodayNetProfit = vm.TodayRevenue - vm.TodayExpenses;
            vm.MonthNetProfit = vm.MonthRevenue - vm.MonthExpenses;
            vm.MonthGrossMargin = vm.MonthRevenue > 0
                ? (vm.MonthNetProfit / vm.MonthRevenue) * 100 : 0;
            vm.BudgetUtilizationPercent = vm.TotalBudgetAllocated > 0
                ? (vm.TotalBudgetSpent / vm.TotalBudgetAllocated) * 100 : 0;

            // Weekly chart data (last 7 days)
            for (int i = 6; i >= 0; i--)
            {
                var day = today.AddDays(-i);
                vm.WeeklyRevenue.Add(new ChartDataPoint
                {
                    Label = day.ToString("ddd"),
                    Value = await _db.Revenues
                        .Where(r => !r.IsDeleted && r.TransactionDate.Date == day)
                        .SumAsync(r => r.Amount)
                });
                vm.WeeklyExpenses.Add(new ChartDataPoint
                {
                    Label = day.ToString("ddd"),
                    Value = await _db.Expenses
                        .Where(e => !e.IsDeleted && e.ExpenseDate.Date == day)
                        .SumAsync(e => e.Amount)
                });
            }

            // Monthly trend (last 6 months)
            for (int i = 5; i >= 0; i--)
            {
                var month = today.AddMonths(-i);
                var mStart = new DateTime(month.Year, month.Month, 1);
                var mEnd = mStart.AddMonths(1);
                var rev = await _db.Revenues
                    .Where(r => !r.IsDeleted && r.TransactionDate >= mStart && r.TransactionDate < mEnd)
                    .SumAsync(r => r.Amount);
                var exp = await _db.Expenses
                    .Where(e => !e.IsDeleted && e.ExpenseDate >= mStart && e.ExpenseDate < mEnd)
                    .SumAsync(e => e.Amount);
                vm.MonthlyTrend.Add(new ChartDataPoint
                {
                    Label = month.ToString("MMM"),
                    Value = rev - exp
                });
            }

            // Expense breakdown by category
            var colors = new[] { "#2d7ef7","#f0b429","#45c896","#f87171","#c084fc","#fb923c" };
            var expGroups = await _db.Expenses
                .Where(e => !e.IsDeleted && e.ExpenseDate >= monthStart)
                .GroupBy(e => e.Category)
                .Select(g => new { Category = g.Key, Total = g.Sum(e => e.Amount) })
                .ToListAsync();
            int ci = 0;
            foreach (var g in expGroups)
            {
                vm.ExpenseBreakdown.Add(new PieDataPoint
                {
                    Label = g.Category,
                    Value = g.Total,
                    Color = colors[ci++ % colors.Length]
                });
            }

            return View(vm);
        }

        // ════════════════════════════════════════════
        //  REVENUE
        // ════════════════════════════════════════════

        public async Task<IActionResult> Revenue(string period = "month", DateTime? from = null, DateTime? to = null)
        {
            var query = _db.Revenues.Where(r => !r.IsDeleted);
            var today = DateTime.Today;

            query = period switch
            {
                "today" => query.Where(r => r.TransactionDate.Date == today),
                "week"  => query.Where(r => r.TransactionDate >= today.AddDays(-7)),
                "month" => query.Where(r => r.TransactionDate >= new DateTime(today.Year, today.Month, 1)),
                "year"  => query.Where(r => r.TransactionDate.Year == today.Year),
                "custom" when from.HasValue && to.HasValue =>
                    query.Where(r => r.TransactionDate.Date >= from.Value.Date && r.TransactionDate.Date <= to.Value.Date),
                _ => query.Where(r => r.TransactionDate >= new DateTime(today.Year, today.Month, 1))
            };

            var revenues = await query.OrderByDescending(r => r.TransactionDate).ToListAsync();
            var vm = new RevenueViewModel
            {
                Revenues = revenues,
                TotalRevenue = revenues.Sum(r => r.Amount),
                FilterPeriod = period,
                DateFrom = from,
                DateTo = to
            };
            return View(vm);
        }

        [HttpGet]
        public IActionResult AddRevenue() => View(new RevenueFormViewModel { TransactionDate = DateTime.Today });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRevenue(RevenueFormViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);
            var revenue = new Revenue
            {
                Source = vm.Source,
                Category = vm.Category,
                Amount = vm.Amount,
                TransactionDate = vm.TransactionDate,
                Notes = vm.Notes,
                RecordedBy = User.Identity?.Name ?? ""
            };
            _db.Revenues.Add(revenue);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Revenue record added successfully.";
            return RedirectToAction(nameof(Revenue));
        }

        [HttpGet]
        public async Task<IActionResult> EditRevenue(int id)
        {
            var r = await _db.Revenues.FindAsync(id);
            if (r == null) return NotFound();
            return View(new RevenueFormViewModel
            {
                Id = r.Id, Source = r.Source, Category = r.Category,
                Amount = r.Amount, TransactionDate = r.TransactionDate, Notes = r.Notes
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRevenue(RevenueFormViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);
            var r = await _db.Revenues.FindAsync(vm.Id);
            if (r == null) return NotFound();
            r.Source = vm.Source; r.Category = vm.Category;
            r.Amount = vm.Amount; r.TransactionDate = vm.TransactionDate; r.Notes = vm.Notes;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Revenue record updated.";
            return RedirectToAction(nameof(Revenue));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRevenue(int id)
        {
            var r = await _db.Revenues.FindAsync(id);
            if (r != null) { r.IsDeleted = true; await _db.SaveChangesAsync(); }
            TempData["Success"] = "Revenue record deleted.";
            return RedirectToAction(nameof(Revenue));
        }

        // ════════════════════════════════════════════
        //  EXPENSES
        // ════════════════════════════════════════════

        public async Task<IActionResult> Expenses(string period = "month", DateTime? from = null, DateTime? to = null)
        {
            var query = _db.Expenses.Where(e => !e.IsDeleted);
            var today = DateTime.Today;

            query = period switch
            {
                "today"  => query.Where(e => e.ExpenseDate.Date == today),
                "week"   => query.Where(e => e.ExpenseDate >= today.AddDays(-7)),
                "month"  => query.Where(e => e.ExpenseDate >= new DateTime(today.Year, today.Month, 1)),
                "year"   => query.Where(e => e.ExpenseDate.Year == today.Year),
                "custom" when from.HasValue && to.HasValue =>
                    query.Where(e => e.ExpenseDate.Date >= from.Value.Date && e.ExpenseDate.Date <= to.Value.Date),
                _ => query.Where(e => e.ExpenseDate >= new DateTime(today.Year, today.Month, 1))
            };

            var expenses = await query.OrderByDescending(e => e.ExpenseDate).ToListAsync();
            var vm = new ExpenseViewModel
            {
                Expenses = expenses,
                TotalExpenses = expenses.Sum(e => e.Amount),
                FilterPeriod = period,
                DateFrom = from,
                DateTo = to
            };
            return View(vm);
        }

        [HttpGet]
        public IActionResult AddExpense() => View(new ExpenseFormViewModel { ExpenseDate = DateTime.Today });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddExpense(ExpenseFormViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);
            var expense = new Expense
            {
                Description = vm.Description, Category = vm.Category,
                Amount = vm.Amount, ExpenseDate = vm.ExpenseDate,
                Supplier = vm.Supplier, Notes = vm.Notes,
                RecordedBy = User.Identity?.Name ?? ""
            };
            _db.Expenses.Add(expense);

            // Update matching budget spent
            var budget = await _db.Budgets.FirstOrDefaultAsync(b =>
                b.Category == vm.Category &&
                b.Month == vm.ExpenseDate.Month &&
                b.Year == vm.ExpenseDate.Year);
            if (budget != null) { budget.SpentAmount += vm.Amount; }

            await _db.SaveChangesAsync();
            TempData["Success"] = "Expense record added successfully.";
            return RedirectToAction(nameof(Expenses));
        }

        [HttpGet]
        public async Task<IActionResult> EditExpense(int id)
        {
            var e = await _db.Expenses.FindAsync(id);
            if (e == null) return NotFound();
            return View(new ExpenseFormViewModel
            {
                Id = e.Id, Description = e.Description, Category = e.Category,
                Amount = e.Amount, ExpenseDate = e.ExpenseDate,
                Supplier = e.Supplier, Notes = e.Notes
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditExpense(ExpenseFormViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);
            var e = await _db.Expenses.FindAsync(vm.Id);
            if (e == null) return NotFound();

            // Reverse old budget impact
            var oldBudget = await _db.Budgets.FirstOrDefaultAsync(b =>
                b.Category == e.Category && b.Month == e.ExpenseDate.Month && b.Year == e.ExpenseDate.Year);
            if (oldBudget != null) oldBudget.SpentAmount -= e.Amount;

            e.Description = vm.Description; e.Category = vm.Category;
            e.Amount = vm.Amount; e.ExpenseDate = vm.ExpenseDate;
            e.Supplier = vm.Supplier; e.Notes = vm.Notes;

            // Apply new budget impact
            var newBudget = await _db.Budgets.FirstOrDefaultAsync(b =>
                b.Category == vm.Category && b.Month == vm.ExpenseDate.Month && b.Year == vm.ExpenseDate.Year);
            if (newBudget != null) newBudget.SpentAmount += vm.Amount;

            await _db.SaveChangesAsync();
            TempData["Success"] = "Expense record updated.";
            return RedirectToAction(nameof(Expenses));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteExpense(int id)
        {
            var e = await _db.Expenses.FindAsync(id);
            if (e != null) { e.IsDeleted = true; await _db.SaveChangesAsync(); }
            TempData["Success"] = "Expense deleted.";
            return RedirectToAction(nameof(Expenses));
        }

        // ════════════════════════════════════════════
        //  BUDGET
        // ════════════════════════════════════════════

        public async Task<IActionResult> Budget(int month = 0, int year = 0)
        {
            if (month == 0) month = DateTime.Today.Month;
            if (year == 0) year = DateTime.Today.Year;

            var budgets = await _db.Budgets
                .Where(b => b.Month == month && b.Year == year)
                .ToListAsync();

            var vm = new BudgetViewModel
            {
                Budgets = budgets,
                TotalAllocated = budgets.Sum(b => b.AllocatedAmount),
                TotalSpent = budgets.Sum(b => b.SpentAmount),
                TotalRemaining = budgets.Sum(b => b.AllocatedAmount - b.SpentAmount),
                FilterMonth = month,
                FilterYear = year
            };
            return View(vm);
        }

        [HttpGet]
        public IActionResult AddBudget() => View(new BudgetFormViewModel());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddBudget(BudgetFormViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);
            var budget = new Budget
            {
                Title = vm.Title, Category = vm.Category,
                AllocatedAmount = vm.AllocatedAmount,
                Month = vm.Month, Year = vm.Year,
                Notes = vm.Notes, CreatedBy = User.Identity?.Name ?? ""
            };
            _db.Budgets.Add(budget);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Budget plan created.";
            return RedirectToAction(nameof(Budget));
        }

        [HttpGet]
        public async Task<IActionResult> EditBudget(int id)
        {
            var b = await _db.Budgets.FindAsync(id);
            if (b == null) return NotFound();
            return View(new BudgetFormViewModel
            {
                Id = b.Id, Title = b.Title, Category = b.Category,
                AllocatedAmount = b.AllocatedAmount,
                Month = b.Month, Year = b.Year, Notes = b.Notes
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditBudget(BudgetFormViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);
            var b = await _db.Budgets.FindAsync(vm.Id);
            if (b == null) return NotFound();
            b.Title = vm.Title; b.Category = vm.Category;
            b.AllocatedAmount = vm.AllocatedAmount;
            b.Month = vm.Month; b.Year = vm.Year; b.Notes = vm.Notes;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Budget updated.";
            return RedirectToAction(nameof(Budget));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteBudget(int id)
        {
            var b = await _db.Budgets.FindAsync(id);
            if (b != null) { _db.Budgets.Remove(b); await _db.SaveChangesAsync(); }
            TempData["Success"] = "Budget deleted.";
            return RedirectToAction(nameof(Budget));
        }

        // ════════════════════════════════════════════
        //  REPORTS
        // ════════════════════════════════════════════

        public async Task<IActionResult> Reports(string type = "Monthly", int month = 0, int year = 0, DateTime? date = null)
        {
            if (month == 0) month = DateTime.Today.Month;
            if (year == 0) year = DateTime.Today.Year;
            if (date == null) date = DateTime.Today;

            var vm = new FinancialReportViewModel
            {
                ReportType = type,
                ReportMonth = month,
                ReportYear = year,
                ReportDate = date
            };

            if (type == "Daily")
            {
                var d = date.Value.Date;
                vm.Revenues = await _db.Revenues
                    .Where(r => !r.IsDeleted && r.TransactionDate.Date == d)
                    .OrderBy(r => r.TransactionDate).ToListAsync();
                vm.Expenses = await _db.Expenses
                    .Where(e => !e.IsDeleted && e.ExpenseDate.Date == d)
                    .OrderBy(e => e.ExpenseDate).ToListAsync();
            }
            else
            {
                var mStart = new DateTime(year, month, 1);
                var mEnd = mStart.AddMonths(1);
                vm.Revenues = await _db.Revenues
                    .Where(r => !r.IsDeleted && r.TransactionDate >= mStart && r.TransactionDate < mEnd)
                    .OrderBy(r => r.TransactionDate).ToListAsync();
                vm.Expenses = await _db.Expenses
                    .Where(e => !e.IsDeleted && e.ExpenseDate >= mStart && e.ExpenseDate < mEnd)
                    .OrderBy(e => e.ExpenseDate).ToListAsync();

                // Daily breakdown for monthly view
                for (var day = mStart; day < mEnd && day <= DateTime.Today; day = day.AddDays(1))
                {
                    var dayRev = vm.Revenues.Where(r => r.TransactionDate.Date == day).Sum(r => r.Amount);
                    var dayExp = vm.Expenses.Where(e => e.ExpenseDate.Date == day).Sum(e => e.Amount);
                    if (dayRev > 0 || dayExp > 0)
                    {
                        vm.DailyBreakdown.Add(new DailyFinancialSummary
                        {
                            Date = day, Revenue = dayRev,
                            Expenses = dayExp, NetProfit = dayRev - dayExp
                        });
                    }
                }
            }

            vm.TotalRevenue = vm.Revenues.Sum(r => r.Amount);
            vm.TotalExpenses = vm.Expenses.Sum(e => e.Amount);
            vm.NetProfit = vm.TotalRevenue - vm.TotalExpenses;
            vm.GrossMarginPercent = vm.TotalRevenue > 0
                ? (vm.NetProfit / vm.TotalRevenue) * 100 : 0;

            // Expense by category
            var totalExp = vm.TotalExpenses;
            vm.ExpenseByCategory = vm.Expenses
                .GroupBy(e => e.Category)
                .Select(g => new ExpenseCategorySummary
                {
                    Category = g.Key,
                    Total = g.Sum(e => e.Amount),
                    Percent = totalExp > 0 ? (g.Sum(e => e.Amount) / totalExp) * 100 : 0
                })
                .OrderByDescending(x => x.Total)
                .ToList();

            return View(vm);
        }

        // ── EXPORT TO EXCEL ──────────────────────────
        public async Task<IActionResult> ExportExcel(string type = "Monthly", int month = 0, int year = 0, DateTime? date = null)
        {
            if (month == 0) month = DateTime.Today.Month;
            if (year == 0) year = DateTime.Today.Year;
            if (date == null) date = DateTime.Today;

            using var wb = new XLWorkbook();

            // Revenue sheet
            var revSheet = wb.Worksheets.Add("Revenue");
            revSheet.Cell(1, 1).Value = "Date";
            revSheet.Cell(1, 2).Value = "Source";
            revSheet.Cell(1, 3).Value = "Category";
            revSheet.Cell(1, 4).Value = "Amount (₱)";
            revSheet.Cell(1, 5).Value = "Notes";

            var headerRow = revSheet.Row(1);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e4fa8");
            headerRow.Style.Font.FontColor = XLColor.White;

            IQueryable<Revenue> revQuery = _db.Revenues.Where(r => !r.IsDeleted);
            IQueryable<Expense> expQuery = _db.Expenses.Where(e => !e.IsDeleted);

            if (type == "Daily")
            {
                revQuery = revQuery.Where(r => r.TransactionDate.Date == date!.Value.Date);
                expQuery = expQuery.Where(e => e.ExpenseDate.Date == date!.Value.Date);
            }
            else
            {
                var mStart = new DateTime(year, month, 1);
                var mEnd = mStart.AddMonths(1);
                revQuery = revQuery.Where(r => r.TransactionDate >= mStart && r.TransactionDate < mEnd);
                expQuery = expQuery.Where(e => e.ExpenseDate >= mStart && e.ExpenseDate < mEnd);
            }

            var revenues = await revQuery.OrderBy(r => r.TransactionDate).ToListAsync();
            var expenses = await expQuery.OrderBy(e => e.ExpenseDate).ToListAsync();

            int row = 2;
            foreach (var r in revenues)
            {
                revSheet.Cell(row, 1).Value = r.TransactionDate.ToString("yyyy-MM-dd");
                revSheet.Cell(row, 2).Value = r.Source;
                revSheet.Cell(row, 3).Value = r.Category;
                revSheet.Cell(row, 4).Value = (double)r.Amount;
                revSheet.Cell(row, 5).Value = r.Notes ?? "";
                row++;
            }
            revSheet.Cell(row, 3).Value = "TOTAL";
            revSheet.Cell(row, 3).Style.Font.Bold = true;
            revSheet.Cell(row, 4).Value = (double)revenues.Sum(r => r.Amount);
            revSheet.Cell(row, 4).Style.Font.Bold = true;
            revSheet.Columns().AdjustToContents();

            // Expense sheet
            var expSheet = wb.Worksheets.Add("Expenses");
            expSheet.Cell(1, 1).Value = "Date";
            expSheet.Cell(1, 2).Value = "Description";
            expSheet.Cell(1, 3).Value = "Category";
            expSheet.Cell(1, 4).Value = "Amount (₱)";
            expSheet.Cell(1, 5).Value = "Supplier";
            expSheet.Cell(1, 6).Value = "Notes";

            var expHeader = expSheet.Row(1);
            expHeader.Style.Font.Bold = true;
            expHeader.Style.Fill.BackgroundColor = XLColor.FromHtml("#ef4444");
            expHeader.Style.Font.FontColor = XLColor.White;

            row = 2;
            foreach (var e in expenses)
            {
                expSheet.Cell(row, 1).Value = e.ExpenseDate.ToString("yyyy-MM-dd");
                expSheet.Cell(row, 2).Value = e.Description;
                expSheet.Cell(row, 3).Value = e.Category;
                expSheet.Cell(row, 4).Value = (double)e.Amount;
                expSheet.Cell(row, 5).Value = e.Supplier ?? "";
                expSheet.Cell(row, 6).Value = e.Notes ?? "";
                row++;
            }
            expSheet.Cell(row, 3).Value = "TOTAL";
            expSheet.Cell(row, 3).Style.Font.Bold = true;
            expSheet.Cell(row, 4).Value = (double)expenses.Sum(e => e.Amount);
            expSheet.Cell(row, 4).Style.Font.Bold = true;
            expSheet.Columns().AdjustToContents();

            // Summary sheet
            var sumSheet = wb.Worksheets.Add("Summary");
            sumSheet.Cell(1, 1).Value = "CRL FRUITSTAND ESS — FINANCIAL REPORT";
            sumSheet.Cell(1, 1).Style.Font.Bold = true;
            sumSheet.Cell(1, 1).Style.Font.FontSize = 14;
            sumSheet.Cell(2, 1).Value = $"Period: {(type == "Daily" ? date!.Value.ToString("MMMM dd, yyyy") : new DateTime(year, month, 1).ToString("MMMM yyyy"))}";
            sumSheet.Cell(3, 1).Value = $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}";
            sumSheet.Cell(5, 1).Value = "Total Revenue";    sumSheet.Cell(5, 2).Value = (double)revenues.Sum(r => r.Amount);
            sumSheet.Cell(6, 1).Value = "Total Expenses";   sumSheet.Cell(6, 2).Value = (double)expenses.Sum(e => e.Amount);
            sumSheet.Cell(7, 1).Value = "Net Profit";
            var netProfit = revenues.Sum(r => r.Amount) - expenses.Sum(e => e.Amount);
            sumSheet.Cell(7, 2).Value = (double)netProfit;
            sumSheet.Cell(7, 1).Style.Font.Bold = true;
            sumSheet.Cell(7, 2).Style.Font.Bold = true;
            sumSheet.Cell(7, 2).Style.Font.FontColor = netProfit >= 0 ? XLColor.Green : XLColor.Red;
            sumSheet.Columns().AdjustToContents();

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            ms.Seek(0, SeekOrigin.Begin);

            var filename = $"CRL_Financial_{type}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return File(ms.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                filename);
        }

        // ── EXPORT TO PDF ────────────────────────────
        public async Task<IActionResult> ExportPdf(string type = "Monthly", int month = 0, int year = 0, DateTime? date = null)
        {
            if (month == 0) month = DateTime.Today.Month;
            if (year == 0) year = DateTime.Today.Year;
            if (date == null) date = DateTime.Today;

            IQueryable<Revenue> revQuery = _db.Revenues.Where(r => !r.IsDeleted);
            IQueryable<Expense> expQuery = _db.Expenses.Where(e => !e.IsDeleted);

            if (type == "Daily")
            {
                revQuery = revQuery.Where(r => r.TransactionDate.Date == date!.Value.Date);
                expQuery = expQuery.Where(e => e.ExpenseDate.Date == date!.Value.Date);
            }
            else
            {
                var mStart = new DateTime(year, month, 1);
                var mEnd = mStart.AddMonths(1);
                revQuery = revQuery.Where(r => r.TransactionDate >= mStart && r.TransactionDate < mEnd);
                expQuery = expQuery.Where(e => e.ExpenseDate >= mStart && e.ExpenseDate < mEnd);
            }

            var revenues = await revQuery.OrderBy(r => r.TransactionDate).ToListAsync();
            var expenses = await expQuery.OrderBy(e => e.ExpenseDate).ToListAsync();
            var totalRev = revenues.Sum(r => r.Amount);
            var totalExp = expenses.Sum(e => e.Amount);
            var netProfit = totalRev - totalExp;

            using var ms = new MemoryStream();
            var writer = new PdfWriter(ms);
            var pdf = new PdfDocument(writer);
            var doc = new Document(pdf);

            var headerColor = new DeviceRgb(30, 79, 168);
            var profitColor = netProfit >= 0 ? new DeviceRgb(69, 200, 150) : new DeviceRgb(239, 68, 68);

            // Title
            doc.Add(new Paragraph("CRL FRUITSTAND ESS")
                .SetFontSize(20).SetFontColor(headerColor));
            doc.Add(new Paragraph("Financial Report")
                .SetFontSize(14).SetFontColor(headerColor));
            doc.Add(new Paragraph($"Period: {(type == "Daily" ? date!.Value.ToString("MMMM dd, yyyy") : new DateTime(year, month, 1).ToString("MMMM yyyy"))}")
                .SetFontSize(11));
            doc.Add(new Paragraph($"Generated: {DateTime.Now:MMMM dd, yyyy hh:mm tt}")
                .SetFontSize(9).SetFontColor(ColorConstants.GRAY));
            doc.Add(new Paragraph(" "));

            // Summary box
            var summaryTable = new Table(2).UseAllAvailableWidth();
            void AddSummaryRow(string label, string value, bool bold = false)
            {
                summaryTable.AddCell(new Cell().Add(new Paragraph(label).SetFontSize(11)));
                var paragraph = new Paragraph(value).SetFontSize(11);
                summaryTable.AddCell(new Cell().Add(paragraph));
            }
            AddSummaryRow("Total Revenue", $"₱{totalRev:N2}");
            AddSummaryRow("Total Expenses", $"₱{totalExp:N2}");
            AddSummaryRow("Net Profit", $"₱{netProfit:N2}", true);
            AddSummaryRow("Gross Margin", $"{(totalRev > 0 ? (netProfit / totalRev * 100) : 0):N1}%");
            doc.Add(summaryTable);
            doc.Add(new Paragraph(" "));

            // Revenue table
            doc.Add(new Paragraph("Revenue Records").SetFontSize(13).SetFontColor(headerColor));
            var revTable = new Table(new float[] { 2, 3, 2, 2 }).UseAllAvailableWidth();
            foreach (var h in new[] { "Date", "Source", "Category", "Amount (₱)" })
                revTable.AddHeaderCell(new Cell().Add(new Paragraph(h).SetFontSize(9))
                    .SetBackgroundColor(headerColor).SetFontColor(ColorConstants.WHITE));
            foreach (var r in revenues)
            {
                revTable.AddCell(r.TransactionDate.ToString("MM/dd/yyyy"));
                revTable.AddCell(r.Source);
                revTable.AddCell(r.Category);
                revTable.AddCell($"₱{r.Amount:N2}");
            }
            revTable.AddCell(new Cell(1, 3).Add(new Paragraph("TOTAL")));
            revTable.AddCell(new Cell().Add(new Paragraph($"₱{totalRev:N2}")));
            doc.Add(revTable);
            doc.Add(new Paragraph(" "));

            // Expense table
            doc.Add(new Paragraph("Expense Records").SetFontSize(13).SetFontColor(new DeviceRgb(239, 68, 68)));
            var expTable = new Table(new float[] { 2, 3, 2, 2 }).UseAllAvailableWidth();
            foreach (var h in new[] { "Date", "Description", "Category", "Amount (₱)" })
                expTable.AddHeaderCell(new Cell().Add(new Paragraph(h).SetFontSize(9))
                    .SetBackgroundColor(new DeviceRgb(239, 68, 68)).SetFontColor(ColorConstants.WHITE));
            foreach (var e in expenses)
            {
                expTable.AddCell(e.ExpenseDate.ToString("MM/dd/yyyy"));
                expTable.AddCell(e.Description);
                expTable.AddCell(e.Category);
                expTable.AddCell($"₱{e.Amount:N2}");
            }
            expTable.AddCell(new Cell(1, 3).Add(new Paragraph("TOTAL")));
            expTable.AddCell(new Cell().Add(new Paragraph($"₱{totalExp:N2}")));
            doc.Add(expTable);

            doc.Close();

            var filename = $"CRL_Financial_{type}_{DateTime.Now:yyyyMMdd_HHmm}.pdf";
            return File(ms.ToArray(), "application/pdf", filename);
        }
    }
}