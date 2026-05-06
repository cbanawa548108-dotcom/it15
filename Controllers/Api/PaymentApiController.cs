using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using CRLFruitstandESS.Data;
using CRLFruitstandESS.Models;
using CRLFruitstandESS.Services;

namespace CRLFruitstandESS.Controllers.Api
{
    [ApiController]
    [Route("api/payment")]
    public class PaymentApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IPayMongoService _payMongo;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<PaymentApiController> _logger;
        private readonly IConfiguration _config;

        public PaymentApiController(
            ApplicationDbContext context,
            IPayMongoService payMongo,
            UserManager<ApplicationUser> userManager,
            ILogger<PaymentApiController> logger,
            IConfiguration config)
        {
            _context     = context;
            _payMongo    = payMongo;
            _userManager = userManager;
            _logger      = logger;
            _config      = config;
        }

        private bool IsTestMode()
        {
            var key = _config["PayMongo:SecretKey"] ?? "";
            return key.StartsWith("sk_test_");
        }

        // POST /api/payment/intent
        [HttpPost("intent")]
        [Authorize(Roles = "Cashier,Admin")]
        public async Task<IActionResult> CreateIntent([FromBody] CreateIntentRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

            if (req.Amount <= 0)
                return BadRequest(new { success = false, message = "Amount must be greater than zero." });

            var user = await _userManager.GetUserAsync(User);

            try
            {
                var intent = await _payMongo.CreatePaymentIntentAsync(
                    req.Amount,
                    req.Description ?? $"CRL Fruitstand - PHP {req.Amount:N2}",
                    "card"
                );

                var txn = new PaymentTransaction
                {
                    SaleId           = req.SaleId,
                    Method           = "card",
                    Status           = "pending",
                    Amount           = req.Amount,
                    PayMongoIntentId = intent.Id,
                    IsTestMode       = IsTestMode(),
                    ProcessedBy      = user?.Id ?? string.Empty,
                    CreatedAt        = DateTime.UtcNow,
                    UpdatedAt        = DateTime.UtcNow
                };
                _context.PaymentTransactions.Add(txn);
                await _context.SaveChangesAsync();

                _logger.LogInformation("[PayMongo] Intent created: {IntentId} | Amount: {Amount} | Test: {Test} | TxnId: {TxnId}",
                    intent.Id, req.Amount, IsTestMode(), txn.Id);

                return Ok(new
                {
                    success   = true,
                    txnId     = txn.Id,
                    intentId  = intent.Id,
                    clientKey = intent.ClientKey,
                    amount    = intent.Amount,
                    currency  = intent.Currency,
                    status    = intent.Status,
                    isTestMode = txn.IsTestMode
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PayMongo] CreateIntent failed for amount {Amount}", req.Amount);
                return StatusCode(502, new { success = false, message = "Payment gateway error.", detail = ex.Message });
            }
        }

        // POST /api/payment/intent/{intentId}/attach
        [HttpPost("intent/{intentId}/attach")]
        [Authorize(Roles = "Cashier,Admin")]
        public async Task<IActionResult> AttachMethod(string intentId, [FromBody] AttachMethodRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

            if (string.IsNullOrWhiteSpace(req.PaymentMethodId) || string.IsNullOrWhiteSpace(req.ClientKey))
                return BadRequest(new { success = false, message = "paymentMethodId and clientKey are required." });

            try
            {
                var ok = await _payMongo.AttachPaymentMethodAsync(intentId, req.PaymentMethodId, req.ClientKey);

                if (ok)
                {
                    var intent = await _payMongo.GetPaymentIntentAsync(intentId);
                    var isPaid = intent?.Status == "succeeded";

                    var txn = await _context.PaymentTransactions
                        .FirstOrDefaultAsync(t => t.PayMongoIntentId == intentId);

                    if (txn != null)
                    {
                        if (isPaid)
                        {
                            txn.Status    = "paid";
                            txn.PaidAt    = DateTime.UtcNow;
                            txn.UpdatedAt = DateTime.UtcNow;
                        }
                        txn.PaymentMethodType = "card";
                        await _context.SaveChangesAsync();

                        _logger.LogInformation("[PayMongo] Method attached: {IntentId} | Status: {Status} | TxnId: {TxnId}",
                            intentId, txn.Status, txn.Id);
                    }

                    return Ok(new
                    {
                        success = true,
                        status  = intent?.Status ?? "unknown",
                        paid    = isPaid
                    });
                }

                return StatusCode(502, new { success = false, message = "Failed to attach payment method." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PayMongo] AttachMethod failed for intent {IntentId}", intentId);
                return StatusCode(502, new { success = false, message = ex.Message });
            }
        }

        // POST /api/payment/source
        [HttpPost("source")]
        [Authorize(Roles = "Cashier,Admin")]
        public async Task<IActionResult> CreateSource([FromBody] CreateSourceRequest req)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { success = false, errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

            if (req.Amount <= 0)
                return BadRequest(new { success = false, message = "Amount must be greater than zero." });

            var method = req.Method?.ToLower();
            if (method != "gcash" && method != "paymaya")
                return BadRequest(new { success = false, message = "Method must be 'gcash' or 'paymaya'." });

            var user = await _userManager.GetUserAsync(User);

            try
            {
                var baseUrl    = $"{Request.Scheme}://{Request.Host}";
                var successUrl = !string.IsNullOrWhiteSpace(req.SuccessUrl)
                    ? req.SuccessUrl
                    : $"{baseUrl}/api/payment/source/success";
                var failedUrl  = !string.IsNullOrWhiteSpace(req.FailedUrl)
                    ? req.FailedUrl
                    : $"{baseUrl}/api/payment/source/failed";

                var source = await _payMongo.CreateSourceAsync(
                    req.Amount,
                    method,
                    successUrl,
                    failedUrl,
                    req.Description ?? $"CRL Fruitstand - {method.ToUpper()} PHP {req.Amount:N2}"
                );

                var txn = new PaymentTransaction
                {
                    SaleId            = req.SaleId,
                    Method            = method,
                    Status            = "pending",
                    Amount            = req.Amount,
                    PayMongoSourceId  = source.Id,
                    CheckoutUrl       = source.CheckoutUrl,
                    PaymentMethodType = method,
                    IsTestMode        = IsTestMode(),
                    ProcessedBy       = user?.Id ?? string.Empty,
                    CreatedAt         = DateTime.UtcNow,
                    UpdatedAt         = DateTime.UtcNow
                };
                _context.PaymentTransactions.Add(txn);
                await _context.SaveChangesAsync();

                _logger.LogInformation("[PayMongo] Source created: {SourceId} | Method: {Method} | Amount: {Amount} | Test: {Test} | TxnId: {TxnId}",
                    source.Id, method, req.Amount, IsTestMode(), txn.Id);

                return Ok(new
                {
                    success     = true,
                    txnId       = txn.Id,
                    sourceId    = source.Id,
                    checkoutUrl = source.CheckoutUrl,
                    method,
                    amount      = req.Amount,
                    status      = source.Status,
                    isTestMode  = txn.IsTestMode
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PayMongo] CreateSource failed - method={Method} amount={Amount}", method, req.Amount);
                return StatusCode(502, new { success = false, message = "Payment gateway error.", detail = ex.Message });
            }
        }

        // GET /api/payment/status/{txnId}
        [HttpGet("status/{txnId:int}")]
        [Authorize(Roles = "Cashier,Admin,Manager,CFO,CEO")]
        public async Task<IActionResult> GetStatus(int txnId)
        {
            var txn = await _context.PaymentTransactions
                .Include(t => t.Sale)
                .FirstOrDefaultAsync(t => t.Id == txnId);

            if (txn == null)
                return NotFound(new { success = false, message = $"Transaction #{txnId} not found." });

            // For card intents - refresh live from PayMongo
            if (!string.IsNullOrEmpty(txn.PayMongoIntentId) && txn.Status == "pending")
            {
                try
                {
                    var intent = await _payMongo.GetPaymentIntentAsync(txn.PayMongoIntentId);
                    if (intent != null && intent.Status == "succeeded")
                    {
                        txn.Status    = "paid";
                        txn.PaidAt    = DateTime.UtcNow;
                        txn.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[PayMongo] Could not refresh intent status for txn {TxnId}", txnId);
                }
            }

            return Ok(new
            {
                success           = true,
                txnId             = txn.Id,
                saleId            = txn.SaleId,
                method            = txn.Method,
                status            = txn.Status,
                amount            = txn.Amount,
                paymentMethodType = txn.PaymentMethodType,
                cardLast4         = txn.CardLast4,
                cardBrand         = txn.CardBrand,
                referenceNumber   = txn.ReferenceNumber,
                failureCode       = txn.FailureCode,
                failureMessage    = txn.FailureMessage,
                checkoutUrl       = txn.CheckoutUrl,
                isTestMode        = txn.IsTestMode,
                createdAt         = txn.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                paidAt            = txn.PaidAt?.ToString("yyyy-MM-dd HH:mm:ss"),
                updatedAt         = txn.UpdatedAt?.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }

        // POST /api/payment/confirm/{txnId}
        [HttpPost("confirm/{txnId:int}")]
        [Authorize(Roles = "Cashier,Admin")]
        public async Task<IActionResult> ConfirmPayment(int txnId, [FromBody] ConfirmPaymentRequest? req)
        {
            var txn = await _context.PaymentTransactions.FindAsync(txnId);
            if (txn == null)
                return NotFound(new { success = false, message = $"Transaction #{txnId} not found." });

            if (txn.Status == "paid")
                return Ok(new { success = true, message = "Already marked as paid.", txnId, status = "paid" });

            txn.Status    = "paid";
            txn.PaidAt    = DateTime.UtcNow;
            txn.UpdatedAt = DateTime.UtcNow;

            if (req?.SaleId.HasValue == true)
                txn.SaleId = req.SaleId.Value;

            if (!string.IsNullOrWhiteSpace(req?.ReferenceNumber))
                txn.ReferenceNumber = req.ReferenceNumber;

            await _context.SaveChangesAsync();

            _logger.LogInformation("[PayMongo] Payment manually confirmed: TxnId={TxnId} SaleId={SaleId}", txnId, txn.SaleId);

            return Ok(new
            {
                success = true,
                message = "Payment confirmed.",
                txnId,
                saleId  = txn.SaleId,
                status  = txn.Status,
                paidAt  = txn.PaidAt?.ToString("yyyy-MM-dd HH:mm:ss")
            });
        }

        // POST /api/payment/webhook
        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> Webhook()
        {
            Request.EnableBuffering();
            using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync();
            Request.Body.Position = 0;

            var signature = Request.Headers["Paymongo-Signature"].FirstOrDefault() ?? string.Empty;
            if (!_payMongo.VerifyWebhookSignature(rawBody, signature))
            {
                _logger.LogWarning("[PayMongo] Webhook signature mismatch.");
                return Unauthorized(new { success = false, message = "Invalid webhook signature." });
            }

            try
            {
                using var doc  = JsonDocument.Parse(rawBody);
                var root       = doc.RootElement;
                var eventType  = root.GetProperty("data").GetProperty("attributes").GetProperty("type").GetString();
                var eventData  = root.GetProperty("data").GetProperty("attributes").GetProperty("data");
                var attributes = eventData.GetProperty("attributes");

                _logger.LogInformation("[PayMongo] Webhook event: {EventType}", eventType);

                switch (eventType)
                {
                    // GCash / Maya source became chargeable
                    case "source.chargeable":
                    {
                        var sourceId = eventData.GetProperty("id").GetString();
                        var txn = await _context.PaymentTransactions
                            .FirstOrDefaultAsync(t => t.PayMongoSourceId == sourceId);

                        if (txn != null && txn.Status == "pending")
                        {
                            txn.Status            = "paid";
                            txn.PaidAt            = DateTime.UtcNow;
                            txn.UpdatedAt         = DateTime.UtcNow;
                            txn.RawPayMongoResponse = rawBody.Length > 4000 ? rawBody.Substring(0, 4000) : rawBody;

                            // Capture amount from webhook
                            if (attributes.TryGetProperty("amount", out var amtProp))
                                txn.Amount = amtProp.GetDecimal() / 100;

                            await _context.SaveChangesAsync();
                            _logger.LogInformation("[PayMongo] Source chargeable: {SourceId} -> TxnId={TxnId}", sourceId, txn.Id);
                        }
                        break;
                    }

                    // Card payment intent succeeded
                    case "payment.paid":
                    {
                        var paymentId = eventData.GetProperty("id").GetString();
                        var intentId  = attributes.TryGetProperty("payment_intent_id", out var piProp)
                            ? piProp.GetString() : null;

                        var txn = !string.IsNullOrEmpty(intentId)
                            ? await _context.PaymentTransactions.FirstOrDefaultAsync(t => t.PayMongoIntentId == intentId)
                            : null;

                        if (txn != null)
                        {
                            txn.Status              = "paid";
                            txn.PaidAt              = DateTime.UtcNow;
                            txn.UpdatedAt           = DateTime.UtcNow;
                            txn.PayMongoPaymentId   = paymentId;
                            txn.RawPayMongoResponse = rawBody.Length > 4000 ? rawBody.Substring(0, 4000) : rawBody;

                            // Reference number
                            if (attributes.TryGetProperty("external_reference_number", out var refProp))
                                txn.ReferenceNumber = refProp.GetString();

                            // Card details
                            if (attributes.TryGetProperty("source", out var srcProp))
                            {
                                if (srcProp.TryGetProperty("type", out var typeProp))
                                    txn.PaymentMethodType = typeProp.GetString();

                                if (srcProp.TryGetProperty("card_last4", out var last4Prop))
                                    txn.CardLast4 = last4Prop.GetString();

                                if (srcProp.TryGetProperty("brand", out var brandProp))
                                    txn.CardBrand = brandProp.GetString();
                            }

                            await _context.SaveChangesAsync();
                            _logger.LogInformation("[PayMongo] Payment paid: {PaymentId} -> TxnId={TxnId}", paymentId, txn.Id);
                        }
                        break;
                    }

                    // Payment failed
                    case "payment.failed":
                    {
                        var intentId = attributes.TryGetProperty("payment_intent_id", out var piProp)
                            ? piProp.GetString() : null;

                        var txn = !string.IsNullOrEmpty(intentId)
                            ? await _context.PaymentTransactions.FirstOrDefaultAsync(t => t.PayMongoIntentId == intentId)
                            : null;

                        if (txn != null)
                        {
                            txn.Status              = "failed";
                            txn.UpdatedAt           = DateTime.UtcNow;
                            txn.RawPayMongoResponse = rawBody.Length > 4000 ? rawBody.Substring(0, 4000) : rawBody;

                            // Failure reason
                            if (attributes.TryGetProperty("last_payment_error", out var errProp))
                            {
                                if (errProp.TryGetProperty("code", out var codeProp))
                                    txn.FailureCode = codeProp.GetString();
                                if (errProp.TryGetProperty("message", out var msgProp))
                                    txn.FailureMessage = msgProp.GetString();
                            }

                            await _context.SaveChangesAsync();
                            _logger.LogInformation("[PayMongo] Payment failed: IntentId={IntentId} -> TxnId={TxnId} Reason={Reason}",
                                intentId, txn.Id, txn.FailureCode);
                        }
                        break;
                    }

                    default:
                        _logger.LogInformation("[PayMongo] Unhandled event: {EventType}", eventType);
                        break;
                }

                return Ok(new { received = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PayMongo] Webhook processing error");
                return Ok(new { received = true, warning = "Processing error logged." });
            }
        }

        // GET /api/payment/transactions
        [HttpGet("transactions")]
        [Authorize(Roles = "Admin,Manager,CFO,CEO,Cashier")]
        public async Task<IActionResult> GetTransactions(
            [FromQuery] DateTime? from     = null,
            [FromQuery] DateTime? to       = null,
            [FromQuery] string?   status   = null,
            [FromQuery] string?   method   = null,
            [FromQuery] bool?     testOnly = null,
            [FromQuery] int page           = 1,
            [FromQuery] int pageSize       = 20)
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            page     = Math.Max(1, page);

            var start = from?.Date ?? DateTime.Today.AddDays(-29);
            var end   = (to?.Date ?? DateTime.Today).AddDays(1).AddTicks(-1);

            var query = _context.PaymentTransactions
                .Where(t => t.CreatedAt >= start && t.CreatedAt <= end);

            if (!string.IsNullOrWhiteSpace(status))
                query = query.Where(t => t.Status == status.ToLower());

            if (!string.IsNullOrWhiteSpace(method))
                query = query.Where(t => t.Method == method.ToLower());

            if (testOnly.HasValue)
                query = query.Where(t => t.IsTestMode == testOnly.Value);

            var total = await query.CountAsync();

            var records = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var allForPeriod = await _context.PaymentTransactions
                .Where(t => t.CreatedAt >= start && t.CreatedAt <= end)
                .ToListAsync();

            return Ok(new
            {
                success   = true,
                timestamp = DateTime.UtcNow,
                summary = new
                {
                    totalTransactions = allForPeriod.Count,
                    totalPaid         = allForPeriod.Where(t => t.Status == "paid").Sum(t => t.Amount),
                    totalPending      = allForPeriod.Where(t => t.Status == "pending").Sum(t => t.Amount),
                    totalFailed       = allForPeriod.Count(t => t.Status == "failed"),
                    testModeCount     = allForPeriod.Count(t => t.IsTestMode),
                    liveModeCount     = allForPeriod.Count(t => !t.IsTestMode),
                    byMethod = allForPeriod
                        .GroupBy(t => t.Method)
                        .Select(g => new { method = g.Key, count = g.Count(), total = g.Sum(t => t.Amount) })
                        .OrderByDescending(x => x.total)
                },
                pagination = new { page, pageSize, total, totalPages = (int)Math.Ceiling(total / (double)pageSize) },
                data = records.Select(t => new
                {
                    id                = t.Id,
                    saleId            = t.SaleId,
                    method            = t.Method,
                    status            = t.Status,
                    amount            = t.Amount,
                    paymentMethodType = t.PaymentMethodType,
                    cardLast4         = t.CardLast4,
                    cardBrand         = t.CardBrand,
                    intentId          = t.PayMongoIntentId,
                    sourceId          = t.PayMongoSourceId,
                    paymentId         = t.PayMongoPaymentId,
                    referenceNumber   = t.ReferenceNumber,
                    failureCode       = t.FailureCode,
                    failureMessage    = t.FailureMessage,
                    checkoutUrl       = t.CheckoutUrl,
                    isTestMode        = t.IsTestMode,
                    processedBy       = t.ProcessedBy,
                    createdAt         = t.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    paidAt            = t.PaidAt?.ToString("yyyy-MM-dd HH:mm:ss"),
                    updatedAt         = t.UpdatedAt?.ToString("yyyy-MM-dd HH:mm:ss")
                })
            });
        }

        // GET /api/payment/summary
        [HttpGet("summary")]
        [Authorize(Roles = "Admin,Manager,CFO,CEO")]
        public async Task<IActionResult> GetSummary(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var start = from?.Date ?? DateTime.Today.AddDays(-29);
            var end   = (to?.Date ?? DateTime.Today).AddDays(1).AddTicks(-1);

            var txns = await _context.PaymentTransactions
                .Where(t => t.Status == "paid" && t.PaidAt >= start && t.PaidAt <= end)
                .ToListAsync();

            var byMethod = txns
                .GroupBy(t => t.Method)
                .Select(g => new
                {
                    method       = g.Key,
                    count        = g.Count(),
                    total        = g.Sum(t => t.Amount),
                    percentShare = txns.Count > 0
                        ? Math.Round((decimal)g.Count() / txns.Count * 100, 2)
                        : 0m
                })
                .OrderByDescending(x => x.total)
                .ToList();

            var daily = txns
                .GroupBy(t => t.PaidAt!.Value.Date)
                .Select(g => new
                {
                    date  = g.Key.ToString("yyyy-MM-dd"),
                    count = g.Count(),
                    total = g.Sum(t => t.Amount)
                })
                .OrderBy(x => x.date)
                .ToList();

            return Ok(new
            {
                success      = true,
                timestamp    = DateTime.UtcNow,
                period       = new { from = start.ToString("yyyy-MM-dd"), to = end.Date.ToString("yyyy-MM-dd") },
                totalRevenue = txns.Sum(t => t.Amount),
                totalCount   = txns.Count,
                testModeCount = txns.Count(t => t.IsTestMode),
                liveModeCount = txns.Count(t => !t.IsTestMode),
                byMethod,
                dailyBreakdown = daily
            });
        }

        // GET /api/payment/source/success
        [HttpGet("source/success")]
        [AllowAnonymous]
        public async Task<IActionResult> SourceSuccess([FromQuery] string? id)
        {
            // Try to mark the source transaction as paid
            if (!string.IsNullOrEmpty(id))
            {
                var txn = await _context.PaymentTransactions
                    .FirstOrDefaultAsync(t => t.PayMongoSourceId == id && t.Status == "pending");
                if (txn != null)
                {
                    txn.Status    = "paid";
                    txn.PaidAt    = DateTime.UtcNow;
                    txn.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("[PayMongo] Source success redirect: {SourceId} -> TxnId={TxnId}", id, txn.Id);
                }
            }

            return Ok(new
            {
                success  = true,
                message  = "Payment completed. You may close this window.",
                sourceId = id
            });
        }

        // GET /api/payment/source/failed
        [HttpGet("source/failed")]
        [AllowAnonymous]
        public async Task<IActionResult> SourceFailed([FromQuery] string? id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                var txn = await _context.PaymentTransactions
                    .FirstOrDefaultAsync(t => t.PayMongoSourceId == id && t.Status == "pending");
                if (txn != null)
                {
                    txn.Status    = "failed";
                    txn.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("[PayMongo] Source failed redirect: {SourceId} -> TxnId={TxnId}", id, txn.Id);
                }
            }

            return Ok(new
            {
                success  = false,
                message  = "Payment was cancelled or failed. Please try again.",
                sourceId = id
            });
        }
    }

    // Request DTOs
    public class CreateIntentRequest
    {
        [Range(0.01, 9999999, ErrorMessage = "Amount must be greater than zero.")]
        public decimal Amount      { get; set; }

        [StringLength(255)]
        public string? Description { get; set; }

        public int?    SaleId      { get; set; }
    }

    public class AttachMethodRequest
    {
        [Required(ErrorMessage = "paymentMethodId is required.")]
        [StringLength(100)]
        public string PaymentMethodId { get; set; } = string.Empty;

        [Required(ErrorMessage = "clientKey is required.")]
        [StringLength(200)]
        public string ClientKey       { get; set; } = string.Empty;
    }

    public class CreateSourceRequest
    {
        [Range(0.01, 9999999, ErrorMessage = "Amount must be greater than zero.")]
        public decimal Amount      { get; set; }

        [Required(ErrorMessage = "Payment method is required.")]
        [RegularExpression("^(gcash|paymaya)$", ErrorMessage = "Method must be 'gcash' or 'paymaya'.")]
        public string  Method      { get; set; } = string.Empty;

        [StringLength(255)]
        public string? Description { get; set; }

        public int?    SaleId      { get; set; }

        [Url(ErrorMessage = "SuccessUrl must be a valid URL.")]
        public string? SuccessUrl  { get; set; }

        [Url(ErrorMessage = "FailedUrl must be a valid URL.")]
        public string? FailedUrl   { get; set; }
    }

    public class ConfirmPaymentRequest
    {
        public int?    SaleId          { get; set; }

        [StringLength(100)]
        public string? ReferenceNumber { get; set; }
    }
}
