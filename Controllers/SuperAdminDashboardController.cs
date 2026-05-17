// Controllers/SuperAdminDashboardController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRLFruitstandESS.Data;
using CRLFruitstandESS.Models;

namespace CRLFruitstandESS.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class SuperAdminDashboardController : Controller
    {
        private readonly ApplicationDbContext         _db;
        private readonly UserManager<ApplicationUser>  _userManager;
        private readonly RoleManager<ApplicationRole>  _roleManager;
        private readonly ILogger<SuperAdminDashboardController> _logger;
        private readonly IConfiguration               _config;

        public SuperAdminDashboardController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            ILogger<SuperAdminDashboardController> logger,
            IConfiguration config)
        {
            _db          = db;
            _userManager = userManager;
            _roleManager = roleManager;
            _logger      = logger;
            _config      = config;
        }

        // GET /SuperAdminDashboard/Index
        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;

            // ── System-wide stats ──────────────────────────────────────────────────
            var totalUsers    = await _userManager.Users.CountAsync();
            var activeUsers   = await _userManager.Users.CountAsync(u => u.IsActive);
            var inactiveUsers = totalUsers - activeUsers;

            var allRoles = await _roleManager.Roles.OrderBy(r => r.Name).ToListAsync();
            var roleStats = new List<(string Name, string Description, int UserCount)>();
            foreach (var role in allRoles)
            {
                var users = await _userManager.GetUsersInRoleAsync(role.Name!);
                roleStats.Add((role.Name!, role.Description, users.Count));
            }

            // ── Recent login attempts (last 50) ────────────────────────────────────
            var recentLogins = await _db.LoginAttempts
                .OrderByDescending(l => l.AttemptedAt)
                .Take(50)
                .ToListAsync();

            var failedToday   = recentLogins.Count(l => !l.Succeeded && l.AttemptedAt.Date == today);
            var successToday  = recentLogins.Count(l =>  l.Succeeded && l.AttemptedAt.Date == today);

            // ── Financial snapshot ─────────────────────────────────────────────────
            var yearStart     = new DateTime(today.Year, 1, 1);
            var revYTD        = await _db.Revenues.Where(r => !r.IsDeleted && r.TransactionDate >= yearStart).SumAsync(r => r.Amount);
            var expYTD        = await _db.Expenses.Where(e => !e.IsDeleted && e.ExpenseDate >= yearStart).SumAsync(e => e.Amount);
            var totalSales    = await _db.Sales.CountAsync(s => s.Status == "Completed");
            var totalProducts = await _db.Products.CountAsync(p => p.IsActive);

            // ── All users with roles (for the user table) ──────────────────────────
            var allUsers = await _userManager.Users.OrderBy(u => u.FullName).ToListAsync();
            var userRows = new List<object>();
            foreach (var u in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(u);
                userRows.Add(new
                {
                    u.Id, u.UserName, u.FullName, u.Email,
                    u.Department, u.IsActive, u.CreatedAt, u.LastLoginAt,
                    Roles = roles.ToList()
                });
            }

            // ── System config summary (non-sensitive keys only) ────────────────────
            var dbConn    = _config.GetConnectionString("DefaultConnection") ?? "";
            var dbServer  = dbConn.Contains("Server=") ? dbConn.Split(';').FirstOrDefault(s => s.StartsWith("Server="))?.Replace("Server=","") ?? "?" : "?";
            var dbName    = dbConn.Contains("Database=") ? dbConn.Split(';').FirstOrDefault(s => s.StartsWith("Database="))?.Replace("Database=","") ?? "?" : "?";
            var payMongoMode = (_config["PayMongo:SecretKey"] ?? "").StartsWith("sk_test_") ? "Test / Sandbox" : "Live / Production";
            var smtpHost  = _config["Email:SmtpHost"] ?? "Not configured";

            ViewBag.TotalUsers    = totalUsers;
            ViewBag.ActiveUsers   = activeUsers;
            ViewBag.InactiveUsers = inactiveUsers;
            ViewBag.RoleStats     = roleStats;
            ViewBag.RecentLogins  = recentLogins;
            ViewBag.FailedToday   = failedToday;
            ViewBag.SuccessToday  = successToday;
            ViewBag.RevYTD        = revYTD;
            ViewBag.ExpYTD        = expYTD;
            ViewBag.ProfitYTD     = revYTD - expYTD;
            ViewBag.TotalSales    = totalSales;
            ViewBag.TotalProducts = totalProducts;
            ViewBag.UserRows      = userRows;
            ViewBag.DbServer      = dbServer;
            ViewBag.DbName        = dbName;
            ViewBag.PayMongoMode  = payMongoMode;
            ViewBag.SmtpHost      = smtpHost;
            ViewBag.GeneratedAt   = DateTime.Now;

            return View();
        }

        // POST /SuperAdminDashboard/ForceDeactivate — deactivate any user including Admins
        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> ForceDeactivate(string userId)
        {
            var current = await _userManager.GetUserAsync(User);
            if (current?.Id == userId)
            {
                TempData["Error"] = "You cannot deactivate your own account.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            user.IsActive = false;
            await _userManager.UpdateAsync(user);

            _logger.LogWarning("[SuperAdmin] {SuperAdmin} force-deactivated user {User}",
                current?.UserName, user.UserName);

            TempData["Success"] = $"User '{user.FullName}' has been deactivated.";
            return RedirectToAction(nameof(Index));
        }

        // POST /SuperAdminDashboard/ForceActivate
        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> ForceActivate(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            user.IsActive = true;
            await _userManager.UpdateAsync(user);

            var current = await _userManager.GetUserAsync(User);
            _logger.LogInformation("[SuperAdmin] {SuperAdmin} activated user {User}",
                current?.UserName, user.UserName);

            TempData["Success"] = $"User '{user.FullName}' has been activated.";
            return RedirectToAction(nameof(Index));
        }

        // POST /SuperAdminDashboard/ForceResetPassword
        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> ForceResetPassword(string userId, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            {
                TempData["Error"] = "Password must be at least 8 characters.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var token  = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            var current = await _userManager.GetUserAsync(User);
            if (result.Succeeded)
            {
                _logger.LogWarning("[SuperAdmin] {SuperAdmin} force-reset password for {User}",
                    current?.UserName, user.UserName);
                TempData["Success"] = $"Password for '{user.FullName}' has been reset.";
            }
            else
            {
                TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction(nameof(Index));
        }

        // POST /SuperAdminDashboard/AssignRole — SuperAdmin-only role assignment
        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignRole(string userId, string newRole)
        {
            if (string.IsNullOrWhiteSpace(newRole))
            {
                TempData["Error"] = "Role is required.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var current = await _userManager.GetUserAsync(User);
            if (current?.Id == userId && newRole != "SuperAdmin")
            {
                TempData["Error"] = "You cannot remove your own SuperAdmin role.";
                return RedirectToAction(nameof(Index));
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, newRole);

            _logger.LogWarning("[SuperAdmin] {SuperAdmin} changed role of {User} from [{OldRoles}] to {NewRole}",
                current?.UserName, user.UserName, string.Join(",", currentRoles), newRole);

            TempData["Success"] = $"'{user.FullName}' is now assigned to role '{newRole}'.";
            return RedirectToAction(nameof(Index));
        }

        // POST /SuperAdminDashboard/PurgeLoginAttempts — clear audit log
        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> PurgeLoginAttempts(int daysOlderThan = 30)
        {
            var cutoff = DateTime.UtcNow.AddDays(-daysOlderThan);
            var old    = _db.LoginAttempts.Where(l => l.AttemptedAt < cutoff);
            int count  = await old.CountAsync();
            _db.LoginAttempts.RemoveRange(old);
            await _db.SaveChangesAsync();

            var current = await _userManager.GetUserAsync(User);
            _logger.LogWarning("[SuperAdmin] {SuperAdmin} purged {Count} login attempts older than {Days} days",
                current?.UserName, count, daysOlderThan);

            TempData["Success"] = $"Purged {count} login attempt records older than {daysOlderThan} days.";
            return RedirectToAction(nameof(Index));
        }

        // POST /SuperAdminDashboard/DeleteUser — SuperAdmin can delete any user including Admins
        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            var current = await _userManager.GetUserAsync(User);
            if (current?.Id == userId)
            {
                TempData["Error"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var userName = user.FullName;
            await _userManager.DeleteAsync(user);

            _logger.LogWarning("[SuperAdmin] {SuperAdmin} permanently deleted user {User}",
                current?.UserName, userName);

            TempData["Success"] = $"User '{userName}' has been permanently deleted.";
            return RedirectToAction(nameof(Index));
        }
    }
}
