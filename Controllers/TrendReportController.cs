using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRLFruitstandESS.Data;
using CRLFruitstandESS.Models;
using System.Text.Json;

namespace CRLFruitstandESS.Controllers
{
    [Authorize(Roles = "CFO,CEO,Admin")]
    public class TrendReportController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public TrendReportController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var reports = await _db.TrendReports
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
            return View(reports);
        }

        [HttpGet]
        public IActionResult Create() => View();

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string title, string reportType, string summary,
            DateTime periodFrom, DateTime periodTo)
        {
            var user = await _userManager.GetUserAsync(User);

            // Build chart data from DB
            var revenues = await _db.Revenues
                .Where(r => !r.IsDeleted && r.TransactionDate >= periodFrom && r.TransactionDate <= periodTo)
                .GroupBy(r => r.TransactionDate.Date)
                .Select(g => new { date = g.Key, total = g.Sum(r => r.Amount) })
                .OrderBy(x => x.date).ToListAsync();

            var expenses = await _db.Expenses
                .Where(e => !e.IsDeleted && e.ExpenseDate >= periodFrom && e.ExpenseDate <= periodTo)
                .GroupBy(e => e.ExpenseDate.Date)
                .Select(g => new { date = g.Key, total = g.Sum(e => e.Amount) })
                .OrderBy(x => x.date).ToListAsync();

            var dataJson = JsonSerializer.Serialize(new
            {
                labels  = revenues.Select(r => r.date.ToString("MMM dd")).ToList(),
                revenue = revenues.Select(r => r.total).ToList(),
                expense = expenses.Select(e => e.total).ToList()
            });

            var report = new TrendReport
            {
                Title      = title,
                ReportType = reportType,
                Summary    = summary,
                PeriodFrom = periodFrom,
                PeriodTo   = periodTo,
                DataJson   = dataJson,
                CreatedBy  = user?.FullName ?? User.Identity?.Name ?? "Unknown"
            };

            _db.TrendReports.Add(report);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Trend report created.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> View(int id)
        {
            var report = await _db.TrendReports.FindAsync(id);
            if (report == null) return NotFound();
            return View(report);
        }
        // No Delete action — enforced by omission
    }
}
