using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRLFruitstandESS.Data;
using CRLFruitstandESS.Models;
using CRLFruitstandESS.Services;

namespace CRLFruitstandESS.Controllers
{
    /// <summary>
    /// Security Dashboard — Admin-only view of login activity, locked accounts,
    /// and the security headers currently applied to the application.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class SecurityController : Controller
    {
        private readonly ApplicationDbContext        _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly LoginRateLimiter            _rateLimiter;

        public SecurityController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            LoginRateLimiter rateLimiter)
        {
            _db          = db;
            _userManager = userManager;
            _rateLimiter = rateLimiter;
        }

        // GET /Security/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var now        = DateTime.UtcNow;
            var last24h    = now.AddHours(-24);
            var last7d     = now.AddDays(-7);

            // ── Login attempt stats
            var recentAttempts = await _db.LoginAttempts
                .Where(a => a.AttemptedAt >= last24h)
                .OrderByDescending(a => a.AttemptedAt)
                .Take(100)
                .ToListAsync();

            var totalToday    = recentAttempts.Count;
            var failedToday   = recentAttempts.Count(a => !a.Succeeded);
            var successToday  = recentAttempts.Count(a => a.Succeeded);

            // ── Last 7 days for chart
            var last7dAttempts = await _db.LoginAttempts
                .Where(a => a.AttemptedAt >= last7d)
                .ToListAsync();

            var dailyStats = Enumerable.Range(0, 7)
                .Select(i =>
                {
                    var day     = DateTime.UtcNow.Date.AddDays(-6 + i);
                    var dayData = last7dAttempts.Where(a => a.AttemptedAt.Date == day).ToList();
                    return new
                    {
                        Label    = day.ToString("MMM dd"),
                        Success  = dayData.Count(a => a.Succeeded),
                        Failed   = dayData.Count(a => !a.Succeeded)
                    };
                })
                .ToList();

            // ── Currently locked-out users (Identity lockout)
            var allUsers     = await _userManager.Users.ToListAsync();
            var lockedUsers  = allUsers
                .Where(u => u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow)
                .ToList();

            // ── Inactive accounts
            var inactiveUsers = allUsers.Where(u => !u.IsActive).ToList();

            // ── Unique IPs with failures in last 24h
            var suspiciousIps = recentAttempts
                .Where(a => !a.Succeeded)
                .GroupBy(a => a.IpAddress)
                .Select(g => new { Ip = g.Key, Count = g.Count(), LastSeen = g.Max(a => a.AttemptedAt) })
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToList();

            // ── Security headers list (what we apply)
            var securityHeaders = new List<(string Name, string Value, string Description)>
            {
                ("X-Frame-Options",        "DENY",                          "Prevents clickjacking by blocking iframe embedding"),
                ("X-Content-Type-Options", "nosniff",                       "Prevents MIME-type sniffing attacks"),
                ("X-XSS-Protection",       "1; mode=block",                 "Enables legacy browser XSS filter"),
                ("Referrer-Policy",        "strict-origin-when-cross-origin","Limits referrer info sent to external sites"),
                ("Permissions-Policy",     "camera=(), microphone=(), ...", "Disables unused browser features"),
                ("Content-Security-Policy","default-src 'self'; ...",       "Restricts resource origins to prevent XSS"),
            };

            ViewBag.TotalToday     = totalToday;
            ViewBag.FailedToday    = failedToday;
            ViewBag.SuccessToday   = successToday;
            ViewBag.LockedUsers    = lockedUsers;
            ViewBag.InactiveUsers  = inactiveUsers;
            ViewBag.SuspiciousIps  = suspiciousIps;
            ViewBag.SecurityHeaders= securityHeaders;
            ViewBag.DailyStats     = dailyStats;
            ViewBag.RecentAttempts = recentAttempts.Take(20).ToList();

            return View();
        }

        // POST /Security/UnlockUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UnlockUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                await _userManager.SetLockoutEndDateAsync(user, null);
                await _userManager.ResetAccessFailedCountAsync(user);
                TempData["Success"] = $"Account '{user.UserName}' has been unlocked.";
            }
            return RedirectToAction(nameof(Dashboard));
        }

        // POST /Security/ToggleActive
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user != null)
            {
                user.IsActive = !user.IsActive;
                await _userManager.UpdateAsync(user);
                TempData["Success"] = $"Account '{user.UserName}' is now {(user.IsActive ? "active" : "deactivated")}.";
            }
            return RedirectToAction(nameof(Dashboard));
        }
    }
}
