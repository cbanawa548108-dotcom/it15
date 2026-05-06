// Controllers/CashierController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRLFruitstandESS.Data;
using CRLFruitstandESS.Models;
using CRLFruitstandESS.Models.ViewModels;
using CRLFruitstandESS.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace CRLFruitstandESS.Controllers
{
    [Authorize(Roles = "Cashier,Admin")]
    public class CashierController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPayMongoService _payMongo;
        private readonly ILogger<CashierController> _logger;
        private readonly IConfiguration _config;
        private readonly IEmailNotificationService _email;

        public CashierController(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
            IPayMongoService payMongo, ILogger<CashierController> logger, IConfiguration config,
            IEmailNotificationService email)
        {
            _context     = context;
            _userManager = userManager;
            _payMongo    = payMongo;
            _logger      = logger;
            _config      = config;
            _email       = email;
        }

        // GET: /Cashier/POS
        public async Task<IActionResult> POS()
        {
            var products = await _context.Products
                .Include(p => p.Inventory)
                .Where(p => p.IsActive && p.Inventory != null && p.Inventory.Quantity > 0)
                .OrderBy(p => p.Name)
                .ToListAsync();

            var categories = await _context.Products
                .Select(p => p.Category)
                .Distinct()
                .Where(c => !string.IsNullOrEmpty(c))
                .OrderBy(c => c)
                .ToListAsync();

            ViewBag.Products = products;
            ViewBag.Categories = categories;
            return View();
        }

        // GET: /Cashier/GetProducts (AJAX) - FIXED to use categoryId
        [HttpGet]
        public async Task<IActionResult> GetProducts(string search = "", int categoryId = 0)
        {
            var query = _context.Products
                .Include(p => p.Inventory)
                .Where(p => p.IsActive && p.Inventory != null && p.Inventory.Quantity > 0)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(p => p.Name.Contains(search));

            // Get ordered list of categories for ID mapping
            var categories = await _context.Products
                .Select(p => p.Category)
                .Distinct()
                .Where(c => !string.IsNullOrEmpty(c))
                .OrderBy(c => c)
                .ToListAsync();

            // Filter by category if categoryId > 0 (0 = All)
            if (categoryId > 0 && categoryId <= categories.Count)
            {
                var selectedCategory = categories[categoryId - 1];
                query = query.Where(p => p.Category == selectedCategory);
            }

            var products = await query.Select(p => new {
                p.Id,
                p.Name,
                p.Price,
                StockQuantity = p.Inventory != null ? p.Inventory.Quantity : 0,
                p.Category,
                p.Emoji,
                categoryName = p.Category
            }).ToListAsync();

            return Json(products);
        }

        // POST: /Cashier/ProcessSale
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessSale([FromBody] ProcessSaleViewModel model)
        {
            if (model == null || model.Items == null || !model.Items.Any())
                return Json(new { success = false, message = "Cart is empty." });

            if (model.AmountPaid < model.TotalAmount)
                return Json(new { success = false, message = "Insufficient payment amount." });

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Json(new { success = false, message = "Session expired. Please log in again." });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Validate stock
                foreach (var item in model.Items)
                {
                    var product = await _context.Products
                        .Include(p => p.Inventory)
                        .FirstOrDefaultAsync(p => p.Id == item.ProductId);
                    if (product == null)
                        return Json(new { success = false, message = $"Product not found: {item.ProductId}" });
                    if (product.Inventory == null || product.Inventory.Quantity < item.Quantity)
                        return Json(new { success = false, message = $"Insufficient stock for: {product.Name}" });
                }

                // Create sale record
                var sale = new Sale
                {
                    CashierId = user.Id,
                    SaleDate = DateTime.Now,
                    TotalAmount = model.TotalAmount,
                    AmountPaid = model.AmountPaid,
                    Change = model.AmountPaid - model.TotalAmount,
                    Status = "Completed"
                };
                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                // Create sale items + deduct inventory
                foreach (var item in model.Items)
                {
                    var product = await _context.Products
                        .Include(p => p.Inventory)
                        .FirstOrDefaultAsync(p => p.Id == item.ProductId);

                    _context.SaleItems.Add(new SaleItem
                    {
                        SaleId = sale.Id,
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Subtotal = item.UnitPrice * item.Quantity
                    });

                    // Deduct stock
                    if (product?.Inventory != null)
                    {
                        product.Inventory.Quantity -= item.Quantity;
                        product.Inventory.LastUpdated = DateTime.Now;
                    }
                }

                await _context.SaveChangesAsync();

                // ✨ NEW: Automatically create Revenue record for CFO module
                var revenue = new Revenue
                {
                    Source = "POS Sale",
                    Category = "Direct Sales",
                    Amount = model.TotalAmount,
                    TransactionDate = DateTime.Now,
                    Notes = $"Sale ID: {sale.Id}, Cashier: {user.FullName}",
                    RecordedBy = user.Id,
                    CreatedAt = DateTime.Now,
                    IsDeleted = false
                };
                _context.Revenues.Add(revenue);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                // ── Fire-and-forget low-stock email alerts
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var managerUser = await _userManager.GetUsersInRoleAsync("Manager");
                        var managerEmail = managerUser.FirstOrDefault(u => u.IsActive)?.Email;
                        if (!string.IsNullOrEmpty(managerEmail))
                        {
                            foreach (var item in model.Items)
                            {
                                var inv = await _context.Inventory
                                    .Include(i => i.Product)
                                    .FirstOrDefaultAsync(i => i.ProductId == item.ProductId);
                                if (inv != null && inv.Quantity <= inv.ReorderPoint)
                                    await _email.SendLowStockAlertAsync(inv.Product.Name, inv.Quantity, inv.ReorderPoint, managerEmail);
                            }
                        }
                    }
                    catch { /* never crash the sale over an email */ }
                });

                return Json(new { success = true, saleId = sale.Id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Transaction failed: " + ex.Message });
            }
        }

        // GET: /Cashier/Receipt/{id}
        public async Task<IActionResult> Receipt(int id)
        {
            var sale = await _context.Sales
                .Include(s => s.Cashier)
                .Include(s => s.SaleItems)
                    .ThenInclude(si => si.Product)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (sale == null) return NotFound();

            // Look up the payment method from the linked PaymentTransaction
            var txn = await _context.PaymentTransactions
                .Where(t => t.SaleId == sale.Id && t.Status == "paid")
                .OrderByDescending(t => t.PaidAt)
                .FirstOrDefaultAsync();

            var paymentMethod = txn?.Method; // null = cash

            var vm = new ReceiptViewModel
            {
                SaleId         = sale.Id,
                CashierName    = sale.Cashier?.FullName ?? "Cashier",
                SaleDate       = sale.SaleDate,
                Items          = sale.SaleItems.ToList(),
                TotalAmount    = sale.TotalAmount,
                AmountPaid     = sale.AmountPaid,
                Change         = sale.Change,
                PaymentMethod  = paymentMethod
            };

            return View(vm);
        }

        // ════════════════════════════════════════════
        // PAYMONGO — Create GCash / Maya source
        // ════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDigitalPayment([FromBody] DigitalPaymentRequest req)
        {
            if (req.Amount <= 0)
                return Json(new { success = false, message = "Invalid amount." });

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Json(new { success = false, message = "Session expired. Please log in again." });

            try
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}";

                // ── Normalise method name to what PayMongo API actually accepts
                // PayMongo Sources API only supports "gcash" and "grab_pay".
                // Maya must use the Payment Intents API with type "paymaya".
                var paymongoType = req.Method.ToLower() switch
                {
                    "gcash"   => "gcash",
                    "paymaya" => "paymaya",
                    "maya"    => "paymaya",
                    _         => req.Method.ToLower()
                };

                // ── Save the pending transaction with cart data stored in DB (not URL)
                // Store cart JSON in RawPayMongoResponse (EF knows this column) until
                // the sale is processed, then it gets overwritten with the actual response.
                var txn = new PaymentTransaction
                {
                    Method              = paymongoType,
                    Status              = "pending",
                    Amount              = req.Amount,
                    PaymentMethodType   = paymongoType,
                    IsTestMode          = (_config["PayMongo:SecretKey"] ?? "").StartsWith("sk_test_"),
                    ProcessedBy         = user.Id,
                    RawPayMongoResponse = req.SaleDataJson,
                    CreatedAt           = DateTime.UtcNow,
                    UpdatedAt           = DateTime.UtcNow
                };
                _context.PaymentTransactions.Add(txn);
                await _context.SaveChangesAsync();

                var successUrl = $"{baseUrl}/Cashier/PaymentSuccess?txnId={txn.Id}&method={Uri.EscapeDataString(paymongoType)}";
                var cancelUrl  = $"{baseUrl}/Cashier/PaymentFailed?txnId={txn.Id}";

                // ── Use Checkout Sessions API for both GCash and Maya.
                // This creates a real checkout.paymongo.com/cs_... URL that works for all e-wallets.
                // Deserialise cart to build line_items for the checkout session.
                var cartItems = JsonSerializer.Deserialize<ProcessSaleViewModel>(req.SaleDataJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                var lineItems = cartItems?.Items?.Select(i =>
                    (name: i.ProductName ?? "Item", qty: i.Quantity, unitAmount: i.UnitPrice)
                ).ToList() ?? new List<(string, int, decimal)> { ("CRL Fruitstand Order", 1, req.Amount) };

                var session = await _payMongo.CreateCheckoutSessionAsync(
                    req.Amount,
                    new[] { paymongoType },
                    successUrl,
                    cancelUrl,
                    $"CRL Fruitstand POS — {paymongoType.ToUpper()}",
                    lineItems);

                txn.CheckoutUrl = session.CheckoutUrl;
                await _context.SaveChangesAsync();

                var checkoutUrl = session.CheckoutUrl;

                _logger.LogInformation("[POS] Digital payment created: TxnId={TxnId} Method={Method} Amount={Amount}",
                    txn.Id, paymongoType, req.Amount);

                return Json(new { success = true, checkoutUrl = checkoutUrl, txnId = txn.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayMongo CreateSource failed");
                // Surface the actual PayMongo error message so it's visible in the POS toast
                var detail = ex.Message.Length > 200 ? ex.Message[..200] : ex.Message;
                return Json(new { success = false, message = $"Payment gateway error: {detail}" });
            }
        }

        // ── PayMongo success redirect
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> PaymentSuccess(int? txnId, string? method = null)
        {
            try
            {
                if (!txnId.HasValue)
                {
                    TempData["Error"] = "Invalid payment return. Please contact support.";
                    return RedirectToAction("POS");
                }

                // ── Load the pending transaction (contains cart data + cashier ID)
                var pendingTxn = await _context.PaymentTransactions
                    .FirstOrDefaultAsync(t => t.Id == txnId.Value);

                if (pendingTxn == null)
                {
                    _logger.LogError("PaymentSuccess: txnId={TxnId} not found in DB", txnId);
                    TempData["Error"] = "Transaction not found. Please contact support.";
                    return RedirectToAction("POS");
                }

                // ── Read cart JSON from RawPayMongoResponse (stored there before redirect,
                // EF knows this column so it's always populated correctly).
                var saleDataJson = pendingTxn.RawPayMongoResponse;

                if (string.IsNullOrEmpty(saleDataJson))
                {
                    _logger.LogError("PaymentSuccess: cart data missing for txnId={TxnId}", txnId);
                    TempData["Error"] = "Order data was lost. Please contact support.";
                    return RedirectToAction("POS");
                }

                var req = JsonSerializer.Deserialize<ProcessSaleViewModel>(saleDataJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (req == null || req.Items == null || !req.Items.Any())
                {
                    _logger.LogError("PaymentSuccess: could not deserialise cart for txnId={TxnId}. JSON={Json}",
                        txnId, saleDataJson?[..Math.Min(500, saleDataJson.Length)]);
                    TempData["Error"] = "Order data was corrupted. Please contact support.";
                    return RedirectToAction("POS");
                }

                // ── Resolve cashier from txn record (session may be gone after redirect)
                var user = await _userManager.GetUserAsync(User)
                        ?? await _userManager.FindByIdAsync(pendingTxn.ProcessedBy);

                if (user == null)
                {
                    _logger.LogError("PaymentSuccess: could not resolve cashier for txnId={TxnId}", txnId);
                    TempData["Error"] = "Session expired. Payment received — please contact support.";
                    return RedirectToAction("POS");
                }

                // ── Prevent double-processing if PayMongo calls the URL twice
                if (pendingTxn.Status == "paid" && pendingTxn.SaleId.HasValue)
                {
                    _logger.LogWarning("PaymentSuccess: txnId={TxnId} already processed, redirecting to receipt", txnId);
                    return RedirectToAction("Receipt", new { id = pendingTxn.SaleId.Value });
                }

                using var dbTransaction = await _context.Database.BeginTransactionAsync();

                var sale = new Sale
                {
                    CashierId   = user.Id,
                    SaleDate    = DateTime.Now,
                    TotalAmount = req.TotalAmount,
                    AmountPaid  = req.TotalAmount,
                    Change      = 0,
                    Status      = "Completed"
                };
                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                foreach (var item in req.Items)
                {
                    var product = await _context.Products
                        .Include(p => p.Inventory)
                        .FirstOrDefaultAsync(p => p.Id == item.ProductId);

                    if (product == null) continue;

                    _context.SaleItems.Add(new SaleItem
                    {
                        SaleId    = sale.Id,
                        ProductId = item.ProductId,
                        Quantity  = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Subtotal  = item.UnitPrice * item.Quantity
                    });

                    if (product.Inventory != null)
                    {
                        var prev = product.Inventory.Quantity;
                        product.Inventory.Quantity    = Math.Max(0, prev - item.Quantity);
                        product.Inventory.LastUpdated = DateTime.Now;

                        _context.StockMovements.Add(new StockMovement
                        {
                            ProductId     = item.ProductId,
                            Type          = MovementType.Sale,
                            Quantity      = item.Quantity,
                            PreviousStock = prev,
                            NewStock      = product.Inventory.Quantity,
                            Notes         = $"POS Sale #{sale.Id} via {pendingTxn.Method}",
                            PerformedBy   = user.UserName,
                            MovementDate  = DateTime.Now
                        });
                    }
                }

                var payMethod = method ?? pendingTxn.Method ?? "digital";

                _context.Revenues.Add(new Revenue
                {
                    Source          = $"POS Sale ({payMethod.ToUpper()})",
                    Category        = "Direct Sales",
                    Amount          = req.TotalAmount,
                    TransactionDate = DateTime.Now,
                    Notes           = $"Sale #{sale.Id} — paid via {payMethod} | Cashier: {user.UserName}",
                    RecordedBy      = user.Id,
                    CreatedAt       = DateTime.UtcNow,
                    IsDeleted       = false
                });

                // ── Mark txn as paid, link to sale, clear cart data from RawPayMongoResponse
                pendingTxn.Status              = "paid";
                pendingTxn.SaleId              = sale.Id;
                pendingTxn.PaidAt              = DateTime.UtcNow;
                pendingTxn.UpdatedAt           = DateTime.UtcNow;
                pendingTxn.RawPayMongoResponse = null; // cart JSON no longer needed; webhook will fill this

                await _context.SaveChangesAsync();
                await dbTransaction.CommitAsync();

                _logger.LogInformation("[POS] GCash/Maya sale completed: SaleId={SaleId} TxnId={TxnId} Amount={Amount} Cashier={Cashier}",
                    sale.Id, txnId, req.TotalAmount, user.UserName);

                TempData["Success"] = $"Payment of ₱{req.TotalAmount:N2} received via {payMethod.ToUpper()}!";
                return RedirectToAction("Receipt", new { id = sale.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PaymentSuccess processing failed for txnId={TxnId}", txnId);
                TempData["Error"] = "Payment was received but order processing failed. Please contact support.";
                return RedirectToAction("POS");
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> PaymentFailed(int? txnId)
        {
            if (txnId.HasValue)
            {
                var txn = await _context.PaymentTransactions.FindAsync(txnId.Value);
                if (txn != null && txn.Status == "pending")
                {
                    txn.Status    = "failed";
                    txn.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                }
            }
            TempData["Error"] = "Payment was cancelled or failed. Please try again.";
            return RedirectToAction("POS");
        }

        // POST: /Cashier/VoidSale — void a completed sale and restore inventory
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VoidSale([FromBody] VoidSaleRequest req)
        {
            if (req.SaleId <= 0)
                return Json(new { success = false, message = "Invalid sale ID." });

            var sale = await _context.Sales
                .Include(s => s.SaleItems)
                    .ThenInclude(si => si.Product)
                        .ThenInclude(p => p.Inventory)
                .FirstOrDefaultAsync(s => s.Id == req.SaleId);

            if (sale == null)
                return Json(new { success = false, message = $"Sale #{req.SaleId} not found." });

            if (sale.Status == "Voided")
                return Json(new { success = false, message = $"Sale #{req.SaleId} is already voided." });

            var user = await _userManager.GetUserAsync(User);

            using var txn = await _context.Database.BeginTransactionAsync();
            try
            {
                // Mark sale as voided
                sale.Status = "Voided";

                // Restore inventory for each item
                foreach (var item in sale.SaleItems)
                {
                    if (item.Product?.Inventory != null)
                    {
                        var prev = item.Product.Inventory.Quantity;
                        item.Product.Inventory.Quantity += item.Quantity;
                        item.Product.Inventory.LastUpdated = DateTime.Now;

                        _context.StockMovements.Add(new StockMovement
                        {
                            ProductId     = item.ProductId,
                            Type          = MovementType.Adjustment,
                            Quantity      = item.Quantity,
                            PreviousStock = prev,
                            NewStock      = item.Product.Inventory.Quantity,
                            Notes         = $"Void of Sale #{sale.Id} — {req.Reason}",
                            PerformedBy   = user?.UserName ?? "Cashier",
                            MovementDate  = DateTime.Now
                        });
                    }
                }

                // Mark the revenue record as deleted (soft delete)
                var revenue = await _context.Revenues
                    .FirstOrDefaultAsync(r => r.Notes != null && r.Notes.Contains($"Sale ID: {sale.Id}") && !r.IsDeleted);
                if (revenue != null)
                    revenue.IsDeleted = true;

                // Add a negative revenue entry for the refund
                _context.Revenues.Add(new Revenue
                {
                    Source          = "Void/Refund",
                    Category        = "Refund",
                    Amount          = -sale.TotalAmount,
                    TransactionDate = DateTime.Now,
                    Notes           = $"Void of Sale #{sale.Id} — Reason: {req.Reason} | By: {user?.UserName}",
                    RecordedBy      = user?.Id ?? "system",
                    CreatedAt       = DateTime.Now,
                    IsDeleted       = false
                });

                await _context.SaveChangesAsync();
                await txn.CommitAsync();

                _logger.LogInformation("Sale #{SaleId} voided by {User}. Reason: {Reason}", sale.Id, user?.UserName, req.Reason);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                await txn.RollbackAsync();
                _logger.LogError(ex, "VoidSale failed for SaleId={SaleId}", req.SaleId);
                return Json(new { success = false, message = "Void failed: " + ex.Message });
            }
        }

        // GET: /Cashier/DailySummary
        public async Task<IActionResult> DailySummary(DateTime? date = null)
        {
            var targetDate = date ?? DateTime.Today;

            var sales = await _context.Sales
                .Include(s => s.Cashier)
                .Where(s => s.SaleDate.Date == targetDate.Date && s.Status == "Completed")
                .OrderByDescending(s => s.SaleDate)
                .ToListAsync();

            var vm = new DailySummaryViewModel
            {
                Date = targetDate,
                TotalTransactions = sales.Count,
                TotalSales = sales.Sum(s => s.TotalAmount),
                Sales = sales
            };

            return View(vm);
        }
    }
}