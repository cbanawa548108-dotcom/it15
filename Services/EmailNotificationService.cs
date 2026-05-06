using System.Net;
using System.Net.Mail;

namespace CRLFruitstandESS.Services
{
    /// <summary>
    /// Sends email notifications for key system events.
    /// Configure SMTP in appsettings.json under "Email".
    /// If SmtpUser is empty, email sending is silently skipped (dev mode).
    ///
    /// Events covered:
    ///   - Low stock alert → Manager
    ///   - Daily P&L summary → CFO
    ///   - Account locked out → Admin
    ///   - New sale completed → (optional, for high-value sales)
    /// </summary>
    public interface IEmailNotificationService
    {
        Task SendLowStockAlertAsync(string productName, int currentStock, int reorderPoint, string managerEmail);
        Task SendDailySummaryAsync(string recipientEmail, decimal revenue, decimal expenses, decimal profit, int transactions, DateTime date);
        Task SendAccountLockedAlertAsync(string adminEmail, string lockedUsername, string ipAddress);
        Task SendAsync(string to, string subject, string htmlBody);
    }

    public class EmailNotificationService : IEmailNotificationService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailNotificationService> _logger;

        private string SmtpHost    => _config["Email:SmtpHost"]    ?? "smtp.gmail.com";
        private int    SmtpPort    => int.TryParse(_config["Email:SmtpPort"], out var p) ? p : 587;
        private string SmtpUser    => _config["Email:SmtpUser"]    ?? "";
        private string SmtpPass    => _config["Email:SmtpPass"]    ?? "";
        private string FromAddress => _config["Email:FromAddress"] ?? "noreply@crlfruitstand.com";
        private string FromName    => _config["Email:FromName"]    ?? "CRL Fruitstand ESS";

        public EmailNotificationService(IConfiguration config, ILogger<EmailNotificationService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendLowStockAlertAsync(string productName, int currentStock, int reorderPoint, string managerEmail)
        {
            var subject = $"⚠️ Low Stock Alert: {productName}";
            var body = $@"
                <div style='font-family:sans-serif;max-width:500px;'>
                    <h2 style='color:#f59e0b;'>⚠️ Low Stock Alert</h2>
                    <p>The following product has fallen below its reorder point:</p>
                    <table style='width:100%;border-collapse:collapse;'>
                        <tr><td style='padding:8px;font-weight:bold;'>Product</td><td style='padding:8px;'>{productName}</td></tr>
                        <tr style='background:#fef3c7;'><td style='padding:8px;font-weight:bold;'>Current Stock</td><td style='padding:8px;color:#dc2626;font-weight:bold;'>{currentStock} units</td></tr>
                        <tr><td style='padding:8px;font-weight:bold;'>Reorder Point</td><td style='padding:8px;'>{reorderPoint} units</td></tr>
                    </table>
                    <p style='margin-top:16px;'>Please arrange a restock order as soon as possible.</p>
                    <p style='color:#6b7280;font-size:12px;'>— CRL Fruitstand ESS</p>
                </div>";
            await SendAsync(managerEmail, subject, body);
        }

        public async Task SendDailySummaryAsync(string recipientEmail, decimal revenue, decimal expenses, decimal profit, int transactions, DateTime date)
        {
            var profitColor = profit >= 0 ? "#10b981" : "#ef4444";
            var subject = $"📊 Daily P&L Summary — {date:MMM dd, yyyy}";
            var body = $@"
                <div style='font-family:sans-serif;max-width:500px;'>
                    <h2 style='color:#3b82f6;'>📊 Daily Financial Summary</h2>
                    <p style='color:#6b7280;'>{date:dddd, MMMM dd, yyyy}</p>
                    <table style='width:100%;border-collapse:collapse;margin-top:12px;'>
                        <tr style='background:#f0f9ff;'><td style='padding:10px;font-weight:bold;'>Total Revenue</td><td style='padding:10px;color:#10b981;font-weight:bold;font-size:18px;'>₱{revenue:N2}</td></tr>
                        <tr><td style='padding:10px;font-weight:bold;'>Total Expenses</td><td style='padding:10px;color:#ef4444;'>₱{expenses:N2}</td></tr>
                        <tr style='background:#f0fdf4;'><td style='padding:10px;font-weight:bold;'>Net Profit</td><td style='padding:10px;color:{profitColor};font-weight:bold;font-size:18px;'>₱{profit:N2}</td></tr>
                        <tr><td style='padding:10px;font-weight:bold;'>Transactions</td><td style='padding:10px;'>{transactions}</td></tr>
                        <tr><td style='padding:10px;font-weight:bold;'>Avg Order Value</td><td style='padding:10px;'>₱{(transactions > 0 ? revenue / transactions : 0):N2}</td></tr>
                    </table>
                    <p style='color:#6b7280;font-size:12px;margin-top:16px;'>— CRL Fruitstand ESS Automated Report</p>
                </div>";
            await SendAsync(recipientEmail, subject, body);
        }

        public async Task SendAccountLockedAlertAsync(string adminEmail, string lockedUsername, string ipAddress)
        {
            var subject = $"🔒 Account Locked: {lockedUsername}";
            var body = $@"
                <div style='font-family:sans-serif;max-width:500px;'>
                    <h2 style='color:#ef4444;'>🔒 Account Locked Out</h2>
                    <p>An account has been locked due to multiple failed login attempts:</p>
                    <table style='width:100%;border-collapse:collapse;'>
                        <tr style='background:#fef2f2;'><td style='padding:8px;font-weight:bold;'>Username</td><td style='padding:8px;color:#dc2626;font-weight:bold;'>{lockedUsername}</td></tr>
                        <tr><td style='padding:8px;font-weight:bold;'>IP Address</td><td style='padding:8px;font-family:monospace;'>{ipAddress}</td></tr>
                        <tr><td style='padding:8px;font-weight:bold;'>Time</td><td style='padding:8px;'>{DateTime.Now:MMM dd, yyyy HH:mm:ss} UTC</td></tr>
                    </table>
                    <p style='margin-top:16px;'>If this was not an authorized user, consider reviewing the Security Dashboard.</p>
                    <p style='color:#6b7280;font-size:12px;'>— CRL Fruitstand ESS Security</p>
                </div>";
            await SendAsync(adminEmail, subject, body);
        }

        public async Task SendAsync(string to, string subject, string htmlBody)
        {
            // Skip silently if SMTP is not configured
            if (string.IsNullOrEmpty(SmtpUser))
            {
                _logger.LogInformation("[Email] SMTP not configured — skipping email to {To}: {Subject}", to, subject);
                return;
            }

            try
            {
                using var client = new SmtpClient(SmtpHost, SmtpPort)
                {
                    Credentials = new NetworkCredential(SmtpUser, SmtpPass),
                    EnableSsl   = true
                };

                var msg = new MailMessage
                {
                    From       = new MailAddress(FromAddress, FromName),
                    Subject    = subject,
                    Body       = htmlBody,
                    IsBodyHtml = true
                };
                msg.To.Add(to);

                await client.SendMailAsync(msg);
                _logger.LogInformation("[Email] Sent '{Subject}' to {To}", subject, to);
            }
            catch (Exception ex)
            {
                // Never crash the app over a failed email
                _logger.LogWarning(ex, "[Email] Failed to send '{Subject}' to {To}", subject, to);
            }
        }
    }
}
