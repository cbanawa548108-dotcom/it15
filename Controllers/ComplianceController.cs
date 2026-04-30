using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRLFruitstandESS.Data;
using CRLFruitstandESS.Models;

namespace CRLFruitstandESS.Controllers
{
    [Authorize(Roles = "CFO,CEO,Admin")]
    public class ComplianceController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public ComplianceController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // ── INDEX: compliance monitor dashboard
        public async Task<IActionResult> Index()
        {
            await AutoFlagAsync(); // run auto-detection on every load
            var flags = await _db.ComplianceFlags
                .OrderByDescending(f => f.FlaggedAt)
                .ToListAsync();
            ViewBag.OpenCount     = flags.Count(f => !f.IsResolved);
            ViewBag.CriticalCount = flags.Count(f => !f.IsResolved && f.Severity == "critical");
            return View(flags);
        }

        // ── RESOLVE a flag
        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> Resolve(int id)
        {
            var flag = await _db.ComplianceFlags.FindAsync(id);
            if (flag != null)
            {
                flag.IsResolved = true;
                flag.ResolvedBy = User.Identity?.Name;
                flag.ResolvedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
                TempData["Success"] = "Flag resolved.";
            }
            return RedirectToAction(nameof(Index));
        }

        // ── AUTO-DETECT compliance issues
        private async Task AutoFlagAsync()
        {
            var today = DateTime.Today;
            var existing = await _db.ComplianceFlags
                .Where(f => !f.IsResolved)
                .Select(f => f.ReferenceId)
                .ToListAsync();

            var newFlags = new List<ComplianceFlag>();

            // 1. Large cash transactions without notes (RA 10173 data privacy — large POS)
            var largeSales = await _db.Sales
                .Where(s => s.TotalAmount > 10000 && s.Status == "Completed"
                         && s.SaleDate >= today.AddDays(-7))
                .ToListAsync();
            foreach (var s in largeSales)
            {
                var refId = $"SALE-{s.Id}";
                if (!existing.Contains(refId))
                    newFlags.Add(new ComplianceFlag
                    {
                        Module = "POS", Severity = "warning",
                        Description = $"Large cash transaction ₱{s.TotalAmount:N0} on {s.SaleDate:MMM dd} — verify customer data handling per RA 10173.",
                        ReferenceId = refId, PolicyReference = "RA 10173 §12",
                        FlaggedBy = "System"
                    });
            }

            // 2. Expenses without supplier reference over ₱5,000
            var bigExpenses = await _db.Expenses
                .Where(e => !e.IsDeleted && e.Amount > 5000 && string.IsNullOrEmpty(e.Supplier)
                         && e.ExpenseDate >= today.AddDays(-30))
                .ToListAsync();
            foreach (var e in bigExpenses)
            {
                var refId = $"EXP-{e.Id}";
                if (!existing.Contains(refId))
                    newFlags.Add(new ComplianceFlag
                    {
                        Module = "Finance", Severity = "warning",
                        Description = $"Expense ₱{e.Amount:N0} ({e.Category}) has no supplier reference — may require documentation.",
                        ReferenceId = refId, PolicyReference = "Internal Policy §4.2",
                        FlaggedBy = "System"
                    });
            }

            // 3. Budget over-utilization (>100%)
            var budgets = await _db.Budgets
                .Where(b => b.Month == today.Month && b.Year == today.Year && b.AllocatedAmount > 0)
                .ToListAsync();
            foreach (var b in budgets.Where(b => b.SpentAmount > b.AllocatedAmount))
            {
                var refId = $"BUD-{b.Id}";
                if (!existing.Contains(refId))
                    newFlags.Add(new ComplianceFlag
                    {
                        Module = "Finance", Severity = "critical",
                        Description = $"Budget '{b.Title}' exceeded: spent ₱{b.SpentAmount:N0} of ₱{b.AllocatedAmount:N0} allocated.",
                        ReferenceId = refId, PolicyReference = "Internal Budget Policy",
                        FlaggedBy = "System"
                    });
            }

            // 4. Voided sales (potential fraud indicator)
            var voidedSales = await _db.Sales
                .Where(s => s.Status == "Voided" && s.SaleDate >= today.AddDays(-7))
                .ToListAsync();
            foreach (var s in voidedSales)
            {
                var refId = $"VOID-{s.Id}";
                if (!existing.Contains(refId))
                    newFlags.Add(new ComplianceFlag
                    {
                        Module = "POS", Severity = "info",
                        Description = $"Voided sale #{s.Id} on {s.SaleDate:MMM dd HH:mm} — ₱{s.TotalAmount:N0}. Review for accuracy.",
                        ReferenceId = refId, PolicyReference = "RA 10173 §11",
                        FlaggedBy = "System"
                    });
            }

            if (newFlags.Any())
            {
                _db.ComplianceFlags.AddRange(newFlags);
                await _db.SaveChangesAsync();
            }
        }
    }
}
