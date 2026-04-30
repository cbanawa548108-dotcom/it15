using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRLFruitstandESS.Data;
using CRLFruitstandESS.Models;
using CRLFruitstandESS.Models.ViewModels;

namespace CRLFruitstandESS.Controllers
{
    [Authorize(Roles = "Cashier,Admin,Manager")]
    public class CashierDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CashierDashboardController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var today = DateTime.Now.Date;

            var vm = new DashboardViewModel
            {
                FullName = user?.FullName ?? "Cashier",
                Role = "Cashier",
                Department = user?.Department ?? "Sales",
                LastLoginAt = user?.LastLoginAt,
                WelcomeMessage = GetWelcomeMessage(),

                // Quick Actions for Cashier
                QuickActions = new List<QuickAction>
                {
                    new() { Icon = "bi-cart-plus", Label = "New Sale", Url = "/Cashier", Color = "green" },
                    new() { Icon = "bi-receipt", Label = "Recent Sales", Url = "/Cashier/History", Color = "blue" },
                    new() { Icon = "bi-box-arrow-in-down", Label = "Stock In", Url = "/Inventory/StockIn", Color = "purple" },
                    new() { Icon = "bi-calculator", Label = "Calculator", Url = "#", Color = "gold" }
                }
            };

            // Today's Stats
            var userName = user?.UserName;
            var todaysSales = await _context.Sales
                .Include(s => s.Cashier)
                .Where(s => s.SaleDate.Date == today && s.Cashier.UserName == userName)
                .ToListAsync();

            vm.TodaysRevenue = todaysSales.Sum(s => s.TotalAmount);
            vm.TodaysTransactions = todaysSales.Count;

            // Low stock alerts for cashier awareness
            var lowStock = await _context.Inventory
                .Include(i => i.Product)
                .Where(i => i.Quantity <= 20)
                .CountAsync();

            if (lowStock > 0)
            {
                vm.Alerts = new List<DashboardAlert>
                {
                    new()
                    {
                        Type = "info",
                        Title = "Low Stock Notice",
                        Message = $"{lowStock} items are running low. Inform manager if customers ask.",
                        CreatedAt = DateTime.Now
                    }
                };
            }

            return View(vm);
        }

        private static string GetWelcomeMessage()
        {
            var hour = DateTime.Now.Hour;
            return hour switch
            {
                < 12 => "Good morning! Ready to serve customers?",
                < 17 => "Good afternoon! Keep up the great service.",
                < 21 => "Good evening! Finish strong.",
                _ => "Late shift? You've got this!"
            };
        }
    }
}