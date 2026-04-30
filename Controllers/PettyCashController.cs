using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRLFruitstandESS.Data;
using CRLFruitstandESS.Models;

namespace CRLFruitstandESS.Controllers
{
    public class PettyCashController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public PettyCashController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // ── CASHIER: submit a claim
        [Authorize(Roles = "Cashier,Admin")]
        public IActionResult Submit() => View();

        [HttpPost][ValidateAntiForgeryToken]
        [Authorize(Roles = "Cashier,Admin")]
        public async Task<IActionResult> Submit(string description, string category, decimal amount, string? notes)
        {
            var user = await _userManager.GetUserAsync(User);
            _db.PettyCashClaims.Add(new PettyCashClaim
            {
                Description     = description,
                Category        = category,
                Amount          = amount,
                Notes           = notes,
                SubmittedBy     = user?.Id ?? "",
                SubmittedByName = user?.FullName ?? User.Identity?.Name ?? ""
            });
            await _db.SaveChangesAsync();
            TempData["Success"] = "Claim submitted for CFO review.";
            return RedirectToAction("Index", "CashierDashboard");
        }

        // ── CFO: review queue
        [Authorize(Roles = "CFO,Admin")]
        public async Task<IActionResult> Review()
        {
            var claims = await _db.PettyCashClaims
                .OrderByDescending(c => c.SubmittedAt)
                .ToListAsync();
            ViewBag.PendingCount = claims.Count(c => c.Status == "Pending");
            return View(claims);
        }

        [HttpPost][ValidateAntiForgeryToken]
        [Authorize(Roles = "CFO,Admin")]
        public async Task<IActionResult> Approve(int id, string reviewNotes)
        {
            var claim = await _db.PettyCashClaims.FindAsync(id);
            if (claim == null) return NotFound();

            claim.Status      = "Approved";
            claim.ReviewedBy  = User.Identity?.Name;
            claim.ReviewedAt  = DateTime.UtcNow;
            claim.ReviewNotes = reviewNotes;

            // Auto-create expense record
            _db.Expenses.Add(new Expense
            {
                Description = $"Petty Cash: {claim.Description}",
                Category    = claim.Category,
                Amount      = claim.Amount,
                ExpenseDate = DateTime.Today,
                Notes       = $"Approved petty cash claim #{claim.Id} from {claim.SubmittedByName}",
                RecordedBy  = User.Identity?.Name ?? ""
            });

            await _db.SaveChangesAsync();
            TempData["Success"] = "Claim approved and expense recorded.";
            return RedirectToAction(nameof(Review));
        }

        [HttpPost][ValidateAntiForgeryToken]
        [Authorize(Roles = "CFO,Admin")]
        public async Task<IActionResult> Reject(int id, string reviewNotes)
        {
            var claim = await _db.PettyCashClaims.FindAsync(id);
            if (claim == null) return NotFound();

            claim.Status      = "Rejected";
            claim.ReviewedBy  = User.Identity?.Name;
            claim.ReviewedAt  = DateTime.UtcNow;
            claim.ReviewNotes = reviewNotes;

            await _db.SaveChangesAsync();
            TempData["Success"] = "Claim rejected.";
            return RedirectToAction(nameof(Review));
        }
    }
}
