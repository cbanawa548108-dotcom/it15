using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CRLFruitstandESS.Controllers
{
    /// <summary>
    /// Base controller — catches unhandled exceptions in all actions,
    /// logs them, and shows a user-friendly error message via TempData.
    /// </summary>
    public class BaseController : Controller
    {
        private readonly ILogger _logger;
        protected BaseController(ILogger logger) => _logger = logger;

        public override void OnActionExecuted(ActionExecutedContext context)
        {
            if (context.Exception != null && !context.ExceptionHandled)
            {
                _logger.LogError(context.Exception,
                    "Unhandled exception in {Controller}.{Action}",
                    context.RouteData.Values["controller"],
                    context.RouteData.Values["action"]);

                context.ExceptionHandled = true;
                TempData["Error"] = "An unexpected error occurred. Please try again.";

                context.Result = context.Controller is Controller ctrl
                    ? ctrl.RedirectToAction("Index", "Home")
                    : new RedirectResult("/");
            }

            base.OnActionExecuted(context);
        }
    }
}
