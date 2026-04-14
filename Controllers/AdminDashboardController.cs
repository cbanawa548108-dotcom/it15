using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CRLFruitstandESS.Models;
using CRLFruitstandESS.Models.ViewModels;

namespace CRLFruitstandESS.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminDashboardController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminDashboardController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var vm = new DashboardViewModel
            {
                FullName = user?.FullName ?? "Administrator",
                Role = "Admin",
                Department = user?.Department ?? "IT",
                LastLoginAt = user?.LastLoginAt,
                WelcomeMessage = "Full system access — manage users, roles, and all modules."
            };
            return View(vm);
        }
    }
}