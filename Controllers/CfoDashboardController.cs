using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CRLFruitstandESS.Models;
using CRLFruitstandESS.Models.ViewModels;

namespace CRLFruitstandESS.Controllers
{
    [Authorize(Roles = "CFO")]
    public class CfoDashboardController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public CfoDashboardController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var vm = new DashboardViewModel
            {
                FullName = user?.FullName ?? "CFO",
                Role = "CFO",
                Department = user?.Department ?? "Finance",
                LastLoginAt = user?.LastLoginAt,
                WelcomeMessage = "Financial overview — monitor revenue, expenses, and P&L reports."
            };
            return View(vm);
        }
    }
}