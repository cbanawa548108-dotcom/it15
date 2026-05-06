using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using CRLFruitstandESS.Data;
using CRLFruitstandESS.Models;
using CRLFruitstandESS.Models.ViewModels;
using CRLFruitstandESS.Services;

namespace CRLFruitstandESS.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser>   _userManager;
        private readonly ILogger<AccountController>     _logger;
        private readonly ApplicationDbContext           _db;
        private readonly LoginRateLimiter               _rateLimiter;
        private readonly IEmailNotificationService      _email;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser>   userManager,
            ILogger<AccountController>     logger,
            ApplicationDbContext           db,
            LoginRateLimiter               rateLimiter,
            IEmailNotificationService      email)
        {
            _signInManager = signInManager;
            _userManager   = userManager;
            _logger        = logger;
            _db            = db;
            _rateLimiter   = rateLimiter;
            _email         = email;
        }

        // GET: /Account/Login
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("RedirectToDashboard");

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
                return View(model);

            // ── IP-based rate limiting (protects against credential stuffing)
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            if (_rateLimiter.IsBlocked(ip))
            {
                var secs = _rateLimiter.BlockedSecondsRemaining(ip);
                ModelState.AddModelError(string.Empty,
                    $"Too many failed attempts from your IP. Please wait {secs} seconds before trying again.");
                return View(model);
            }

            var userAgent = Request.Headers["User-Agent"].ToString();

            var user = await _userManager.FindByNameAsync(model.UserName);

            if (user == null)
            {
                _rateLimiter.RecordFailure(ip);
                await RecordAttemptAsync(model.UserName, ip, userAgent, false, "UserNotFound");
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return View(model);
            }

            if (!user.IsActive)
            {
                _rateLimiter.RecordFailure(ip);
                await RecordAttemptAsync(model.UserName, ip, userAgent, false, "AccountInactive");
                ModelState.AddModelError(string.Empty, "Your account has been deactivated. Contact the administrator.");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                model.UserName, model.Password, model.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _rateLimiter.RecordSuccess(ip);
                await RecordAttemptAsync(model.UserName, ip, userAgent, true, string.Empty);

                user.LastLoginAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                _logger.LogInformation("User {UserName} logged in from {IP} at {Time}.", model.UserName, ip, DateTime.UtcNow);

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                return RedirectToAction("RedirectToDashboard");
            }

            if (result.IsLockedOut)
            {
                _rateLimiter.RecordFailure(ip);
                await RecordAttemptAsync(model.UserName, ip, userAgent, false, "LockedOut");
                _logger.LogWarning("User {UserName} is locked out. IP: {IP}", model.UserName, ip);

                // Notify admin by email
                var adminUser = await _userManager.FindByNameAsync("admin");
                if (adminUser?.Email != null)
                    _ = _email.SendAccountLockedAlertAsync(adminUser.Email, model.UserName, ip);

                ModelState.AddModelError(string.Empty, "Account locked due to multiple failed attempts. Try again in 15 minutes.");
                return View(model);
            }

            _rateLimiter.RecordFailure(ip);
            await RecordAttemptAsync(model.UserName, ip, userAgent, false, "InvalidPassword");
            ModelState.AddModelError(string.Empty, "Invalid username or password.");
            return View(model);
        }

        // ── Helper: persist a login attempt record
        private async Task RecordAttemptAsync(string userName, string ip, string userAgent, bool succeeded, string failReason)
        {
            _db.LoginAttempts.Add(new LoginAttempt
            {
                UserName    = userName,
                IpAddress   = ip,
                UserAgent   = userAgent.Length > 250 ? userAgent[..250] : userAgent,
                Succeeded   = succeeded,
                FailReason  = failReason,
                AttemptedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        // GET: /Account/RedirectToDashboard — role-based redirect
        [Authorize]
        public async Task<IActionResult> RedirectToDashboard()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            var roles = await _userManager.GetRolesAsync(user);
            var role = roles.FirstOrDefault();

            return role switch
            {
                "Admin"   => RedirectToAction("Index", "AdminDashboard"),
                "CFO"     => RedirectToAction("Index", "CfoDashboard"),
                "Manager" => RedirectToAction("Index", "ManagerDashboard"),
                "Cashier" => RedirectToAction("POS", "Cashier"),
                _         => RedirectToAction("AccessDenied")
            };
        }

        // POST: /Account/Logout
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var userName = User.Identity?.Name;
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User {UserName} logged out at {Time}.", userName, DateTime.UtcNow);
            return RedirectToAction("Login");
        }

        // GET: /Account/AccessDenied
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        // ════════════════════════════════════════════
        // FORGOT PASSWORD — OTP FLOW
        // Step 1: Enter email → send 6-digit code
        // Step 2: Enter code → verify
        // Step 3: Set new password
        // ════════════════════════════════════════════

        // In-memory OTP store: email → (code, expiry, userId)
        // For a production app use a DB table or distributed cache.
        private static readonly Dictionary<string, (string Code, DateTime Expiry, string UserId)> _otpStore = new();

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null || !user.IsActive)
            {
                // Don't reveal whether the email exists
                ModelState.AddModelError(string.Empty, "No active account found with that email address.");
                return View(model);
            }

            // Generate a 6-digit OTP
            var code = new Random().Next(100000, 999999).ToString();
            _otpStore[model.Email] = (code, DateTime.UtcNow.AddMinutes(10), user.Id);

            await _email.SendAsync(model.Email,
                "Your CRL Fruitstand Password Reset Code",
                $@"<div style='font-family:sans-serif;max-width:480px;background:#0f172a;padding:32px;border-radius:12px;'>
                    <h2 style='color:#3b82f6;margin:0 0 8px;'>🔐 Password Reset Code</h2>
                    <p style='color:#94a3b8;margin:0 0 24px;font-size:14px;'>CRL Fruitstand Executive Decision Support System</p>
                    <p style='color:#cbd5e1;font-size:14px;margin:0 0 16px;'>Use the code below to reset your password. It expires in <strong>10 minutes</strong>.</p>
                    <div style='background:#1e293b;border:2px solid #3b82f6;border-radius:10px;padding:20px;text-align:center;margin:0 0 24px;'>
                        <span style='font-size:42px;font-weight:900;letter-spacing:12px;color:#f1f5f9;font-family:monospace;'>{code}</span>
                    </div>
                    <p style='color:#64748b;font-size:12px;margin:0;'>If you did not request this, ignore this email. Your password will not change.</p>
                    <p style='color:#64748b;font-size:12px;margin:4px 0 0;'>— CRL Fruitstand ESS Security</p>
                </div>");

            _logger.LogInformation("OTP sent to {Email}", model.Email);

            // Pass email to the verify step via TempData
            TempData["OtpEmail"] = model.Email;
            return RedirectToAction(nameof(VerifyOtp));
        }

        // ── STEP 2: Enter the OTP code
        [HttpGet]
        [AllowAnonymous]
        public IActionResult VerifyOtp()
        {
            var email = TempData["OtpEmail"] as string;
            if (string.IsNullOrEmpty(email))
                return RedirectToAction(nameof(ForgotPassword));

            TempData.Keep("OtpEmail");
            ViewBag.Email = email;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyOtp(string email, string code)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
            {
                ViewBag.Email = email;
                ViewBag.Error = "Please enter the 6-digit code.";
                return View();
            }

            if (!_otpStore.TryGetValue(email, out var entry))
            {
                ViewBag.Email = email;
                ViewBag.Error = "Code not found or already used. Please request a new one.";
                return View();
            }

            if (DateTime.UtcNow > entry.Expiry)
            {
                _otpStore.Remove(email);
                ViewBag.Email = email;
                ViewBag.Error = "This code has expired. Please request a new one.";
                return View();
            }

            if (entry.Code != code.Trim())
            {
                ViewBag.Email = email;
                ViewBag.Error = "Incorrect code. Please try again.";
                return View();
            }

            // Code is valid — remove it so it can't be reused
            _otpStore.Remove(email);

            // Generate the actual reset token now and pass directly to the view
            var user = await _userManager.FindByIdAsync(entry.UserId);
            if (user == null)
            {
                ViewBag.Email = email;
                ViewBag.Error = "User not found. Please try again.";
                return View();
            }

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            return View("ResetPassword", new ResetPasswordViewModel
            {
                UserId = entry.UserId,
                Token  = resetToken
            });
        }

        // ── STEP 3: Set new password
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword(string? userId = null, string? token = null)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
                return RedirectToAction(nameof(ForgotPassword));

            return View(new ResetPasswordViewModel { UserId = userId, Token = token });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                TempData["Error"] = "Invalid reset session. Please start over.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);
            if (result.Succeeded)
            {
                _logger.LogInformation("Password reset via OTP for {UserName}", user.UserName);
                TempData["Success"] = "✅ Password changed successfully. You can now log in.";
                return RedirectToAction(nameof(Login));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }
    }
}