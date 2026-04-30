using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRLFruitstandESS.Data;
using CRLFruitstandESS.Models;

namespace CRLFruitstandESS.Controllers
{
    [Authorize(Roles = "CFO,Admin")]
    public class TaxController : Controller
    {
        private readonly ApplicationDbContext _db;
        public TaxController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Index(int year = 0)
        {
            if (year == 0) year = DateTime.Today.Year;

            // Pull all revenue + expenses for the year
            var yearStart = new DateTime(year, 1, 1);
            var yearEnd   = new DateTime(year, 12, 31);

            var revenues = await _db.Revenues
                .Where(r => !r.IsDeleted && r.TransactionDate >= yearStart && r.TransactionDate <= yearEnd)
                .ToListAsync();
            var expenses = await _db.Expenses
                .Where(e => !e.IsDeleted && e.ExpenseDate >= yearStart && e.ExpenseDate <= yearEnd)
                .ToListAsync();

            var totalRevenue  = revenues.Sum(r => r.Amount);
            var totalExpenses = expenses.Sum(e => e.Amount);
            var grossIncome   = totalRevenue - totalExpenses;

            // Philippine corporate income tax rates (TRAIN Law / CREATE Act)
            // MSME: 20% if net income ≤ ₱5M, else 25%
            decimal taxRate      = grossIncome <= 5_000_000m ? 0.20m : 0.25m;
            decimal taxableIncome = Math.Max(0, grossIncome);
            decimal estimatedTax  = taxableIncome * taxRate;

            // Quarterly breakdown
            var quarters = new List<object>();
            for (int q = 1; q <= 4; q++)
            {
                var qStart = new DateTime(year, (q - 1) * 3 + 1, 1);
                var qEnd   = qStart.AddMonths(3).AddDays(-1);
                var qRev   = revenues.Where(r => r.TransactionDate >= qStart && r.TransactionDate <= qEnd).Sum(r => r.Amount);
                var qExp   = expenses.Where(e => e.ExpenseDate >= qStart && e.ExpenseDate <= qEnd).Sum(e => e.Amount);
                var qIncome = qRev - qExp;
                quarters.Add(new
                {
                    Quarter     = $"Q{q} {year}",
                    Revenue     = qRev,
                    Expenses    = qExp,
                    GrossIncome = qIncome,
                    EstTax      = Math.Max(0, qIncome) * taxRate
                });
            }

            ViewBag.Year          = year;
            ViewBag.TotalRevenue  = totalRevenue;
            ViewBag.TotalExpenses = totalExpenses;
            ViewBag.GrossIncome   = grossIncome;
            ViewBag.TaxRate       = taxRate * 100;
            ViewBag.EstimatedTax  = estimatedTax;
            ViewBag.Quarters      = quarters;
            ViewBag.AvailableYears = Enumerable.Range(DateTime.Today.Year - 3, 5).Reverse().ToList();

            return View();
        }
    }
}
