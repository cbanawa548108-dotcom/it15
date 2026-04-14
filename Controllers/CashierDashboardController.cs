using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CRLFruitstandESS.Models;
using CRLFruitstandESS.Models.ViewModels;

namespace CRLFruitstandESS.Controllers
{
    [Authorize(Roles = "Cashier,Admin")]
    public class CashierDashboardController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public CashierDashboardController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var vm = new DashboardViewModel
            {
                FullName = user?.FullName ?? "Cashier",
                Role = "Cashier",
                Department = user?.Department ?? "Sales",
                LastLoginAt = user?.LastLoginAt,
                WelcomeMessage = "POS terminal — process transactions and manage your shift."
            };
            return View(vm);
        }
    }
}