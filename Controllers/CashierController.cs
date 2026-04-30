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

        public CashierController(ApplicationDbContext context, UserManager<ApplicationUser> userManager,
            IPayMongoService payMongo, ILogger<CashierController> logger)
        {
            _context  = context;
            _userManager = userManager;
            _payMongo = payMongo;
            _logger   = logger;
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
                StockQuantity = p.Inventory.Quantity,
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
                    product.Inventory.Quantity -= item.Quantity;
                    product.Inventory.LastUpdated = DateTime.Now;
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

            var vm = new ReceiptViewModel
            {
                SaleId = sale.Id,
                CashierName = sale.Cashier.FullName,
                SaleDate = sale.SaleDate,
                Items = sale.SaleItems.ToList(),
                TotalAmount = sale.TotalAmount,
                AmountPaid = sale.AmountPaid,
                Change = sale.Change
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
            try
            {
                var baseUrl    = $"{Request.Scheme}://{Request.Host}";
                var successUrl = $"{baseUrl}/Cashier/PaymentSuccess?saleData={Uri.EscapeDataString(req.SaleDataJson)}";
                var failedUrl  = $"{baseUrl}/Cashier/PaymentFailed";

                var source = await _payMongo.CreateSourceAsync(
                    req.Amount,
                    req.Method.ToLower(),   // "gcash" or "paymaya"
                    successUrl,
                    failedUrl,
                    $"CRL Fruitstand POS — {req.Method}"
                );

                // Save pending transaction
                var txn = new PaymentTransaction
                {
                    Method           = req.Method,
                    Status           = "pending",
                    Amount           = req.Amount,
                    PayMongoSourceId = source.Id,
                    CheckoutUrl      = source.CheckoutUrl,
                    ProcessedBy      = user?.Id ?? ""
                };
                _context.PaymentTransactions.Add(txn);
                await _context.SaveChangesAsync();

                return Json(new { success = true, checkoutUrl = source.CheckoutUrl, txnId = txn.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PayMongo CreateSource failed");
                return Json(new { success = false, message = "Payment gateway error. Please try cash." });
            }
        }

        // ── PayMongo success redirect (customer comes back here after paying)
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> PaymentSuccess(string saleData)
        {
            try
            {
                var req = JsonSerializer.Deserialize<ProcessSaleViewModel>(Uri.UnescapeDataString(saleData));
                if (req == null) return RedirectToAction("POS");

                // Re-use existing ProcessSale logic but mark as digital
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return RedirectToAction("POS");

                using var transaction = await _context.Database.BeginTransactionAsync();

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

                foreach (var item in req.Items!)
                {
                    var product = await _context.Products.Include(p => p.Inventory)
                        .FirstOrDefaultAsync(p => p.Id == item.ProductId);
                    if (product?.Inventory != null)
                    {
                        _context.SaleItems.Add(new SaleItem
                        {
                            SaleId = sale.Id, ProductId = item.ProductId,
                            Quantity = item.Quantity, UnitPrice = item.UnitPrice,
                            Subtotal = item.UnitPrice * item.Quantity
                        });
                        product.Inventory.Quantity -= item.Quantity;
                        product.Inventory.LastUpdated = DateTime.Now;
                    }
                }

                _context.Revenues.Add(new Revenue
                {
                    Source = $"POS Sale ({req.PaymentMethod})",
                    Category = "Direct Sales",
                    Amount = req.TotalAmount,
                    TransactionDate = DateTime.Now,
                    Notes = $"Sale ID: {sale.Id} — paid via {req.PaymentMethod}",
                    RecordedBy = user.Id
                });

                // Mark transaction as paid
                var txn = await _context.PaymentTransactions
                    .Where(t => t.ProcessedBy == user.Id && t.Status == "pending")
                    .OrderByDescending(t => t.CreatedAt).FirstOrDefaultAsync();
                if (txn != null) { txn.Status = "paid"; txn.SaleId = sale.Id; txn.PaidAt = DateTime.UtcNow; }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = $"Payment received via {req.PaymentMethod}!";
                return RedirectToAction("Receipt", new { id = sale.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PaymentSuccess processing failed");
                TempData["Error"] = "Payment was received but order processing failed. Please contact support.";
                return RedirectToAction("POS");
            }
        }

        [HttpGet]
        public IActionResult PaymentFailed()
        {
            TempData["Error"] = "Payment was cancelled or failed. Please try again.";
            return RedirectToAction("POS");
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