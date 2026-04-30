using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CRLFruitstandESS.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        public HomeController(ILogger<HomeController> logger) => _logger = logger;

        public IActionResult Index() => View();
        public IActionResult About() => View();
        public IActionResult Features() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error(int? statusCode = null)
        {
            var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            if (feature?.Error != null)
                _logger.LogError(feature.Error, "Unhandled exception at {Path}", feature.Path);

            ViewBag.StatusCode = statusCode ?? 500;
            ViewBag.Title = statusCode switch
            {
                404 => "Page Not Found",
                403 => "Access Denied",
                401 => "Unauthorized",
                _   => "Something Went Wrong"
            };
            ViewBag.Message = statusCode switch
            {
                404 => "The page you're looking for doesn't exist or has been moved.",
                403 => "You don't have permission to access this resource.",
                401 => "You need to be logged in to access this page.",
                _   => "An unexpected error occurred. Please try again or contact support."
            };

            return View();
        }
    }
}
