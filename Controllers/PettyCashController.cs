using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using CRLFruitstandESS.Data;
using CRLFruitstandESS.Models;

namespace CRLFruitstandESS.Controllers
{
    // ── Request ViewModel for petty cash submission
    public class PettyCashSubmitViewModel
    {
        [Required(ErrorMessage = "Description is required.")]
        [StringLength(200, MinimumLength = 3, ErrorMessage = "Description must be between 3 and 200 characters.")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Category is required.")]
        [StringLength(50, ErrorMessage = "Category cannot exceed 50 characters.")]
        public string Category { get; set; } = string.Empty;

        [Required(ErrorMessage = "Amount is required.")]
        [Range(1, 100000, ErrorMessage = "Amount must be between ₱1 and ₱100,000.")]
        public decimal Amount { get; set; }

        [StringLength(500, ErrorMessage = "Notes cannot exceed 500 characters.")]
        public string? Notes { get; set; }
    }

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
        public async Task<IActionResult> Submit(PettyCashSubmitViewModel vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            var user = await _userManager.GetUserAsync(User);
            _db.PettyCashClaims.Add(new PettyCashClaim
            {
                Description     = vm.Description.Trim(),
                Category        = vm.Category.Trim(),
                Amount          = vm.Amount,
                Notes           = vm.Notes?.Trim(),
                SubmittedBy     = user?.Id ?? "",
                SubmittedByName = user?.FullName ?? User.Identity?.Name ?? ""
            });
            await _db.SaveChangesAsync();
            TempData["Success"] = "Claim submitted for CFO review.";
            return RedirectToAction("Index", "CashierDashboard");
        }

        // ── CFO: review queue
        [Authorize(Roles = "CFO,Admin")]
        public async Task<IActionResult> Review(int page = 1, string status = "all")
        {
            const int pageSize = 15;

            var query = _db.PettyCashClaims.OrderByDescending(c => c.SubmittedAt).AsQueryable();

            if (status != "all")
                query = query.Where(c => c.Status == status);

            int totalItems  = await query.CountAsync();
            int totalPages  = (int)Math.Ceiling(totalItems / (double)pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            var claims = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.PendingCount = await _db.PettyCashClaims.CountAsync(c => c.Status == "Pending");
            ViewBag.Page         = page;
            ViewBag.TotalPages   = totalPages;
            ViewBag.TotalItems   = totalItems;
            ViewBag.PageSize     = pageSize;
            ViewBag.Status       = status;

            return View(claims);
        }

        [HttpPost][ValidateAntiForgeryToken]
        [Authorize(Roles = "CFO,Admin")]
        public async Task<IActionResult> Approve(int id, string? reviewNotes)
        {
            if (id <= 0) { TempData["Error"] = "Invalid claim."; return RedirectToAction(nameof(Review)); }

            var claim = await _db.PettyCashClaims.FindAsync(id);
            if (claim == null) return NotFound();

            claim.Status      = "Approved";
            claim.ReviewedBy  = User.Identity?.Name;
            claim.ReviewedAt  = DateTime.UtcNow;
            claim.ReviewNotes = reviewNotes?.Trim();

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
        public async Task<IActionResult> Reject(int id, string? reviewNotes)
        {
            if (id <= 0) { TempData["Error"] = "Invalid claim."; return RedirectToAction(nameof(Review)); }
            if (string.IsNullOrWhiteSpace(reviewNotes))
            {
                TempData["Error"] = "A reason is required when rejecting a claim.";
                return RedirectToAction(nameof(Review));
            }

            var claim = await _db.PettyCashClaims.FindAsync(id);
            if (claim == null) return NotFound();

            claim.Status      = "Rejected";
            claim.ReviewedBy  = User.Identity?.Name;
            claim.ReviewedAt  = DateTime.UtcNow;
            claim.ReviewNotes = reviewNotes.Trim();

            await _db.SaveChangesAsync();
            TempData["Success"] = "Claim rejected.";
            return RedirectToAction(nameof(Review));
        }
    }
}
