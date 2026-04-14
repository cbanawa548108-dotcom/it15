using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using CRLFruitstandESS.Models;
using CRLFruitstandESS.Models.ViewModels;

namespace CRLFruitstandESS.Controllers
{
    [Authorize(Roles = "Manager")]
    public class ManagerDashboardController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public ManagerDashboardController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var vm = new DashboardViewModel
            {
                FullName = user?.FullName ?? "Manager",
                Role = "Manager",
                Department = user?.Department ?? "Operations",
                LastLoginAt = user?.LastLoginAt,
                WelcomeMessage = "Operations overview — track inventory, staff, and daily sales."
            };
            return View(vm);
        }
    }
}