// Controllers/InventoryController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRLFruitstandESS.Data;
using CRLFruitstandESS.Models;
using CRLFruitstandESS.Models.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CRLFruitstandESS.Controllers
{
    [Authorize(Roles = "Manager,Admin")]
    public class InventoryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public InventoryController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private async Task<bool> IsAdmin()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return false;
            return await _userManager.IsInRoleAsync(user, "Admin");
        }

        // GET: /Inventory/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var inventoryQuery = await _context.Inventory
                .Include(i => i.Product)
                .ToListAsync();

            var totalValue = inventoryQuery.Sum(i => i.Quantity * (i.Product?.CostPrice ?? 0m));
            var lowStock = inventoryQuery.Where(i => i.Quantity > 20 && i.Quantity <= 50).ToList();
            var criticalStock = inventoryQuery.Where(i => i.Quantity <= 20 && i.Quantity > 0).ToList();
            var outOfStock = inventoryQuery.Where(i => i.Quantity == 0).ToList();
            var critical = inventoryQuery.Where(i => i.Quantity <= 20 && i.Quantity > 0).ToList();

            var lowStockAlerts = outOfStock.Concat(criticalStock).Concat(lowStock).OrderBy(i => i.Quantity).ToList();

            // Get products with pending deliveries
            var productsWithPendingDeliveries = await _context.SupplierDeliveries
                .Select(d => d.ProductId)
                .Distinct()
                .ToListAsync();

            var vm = new InventoryDashboardViewModel
            {
                TotalProducts = await _context.Products.CountAsync(p => p.IsActive),
                TotalStockItems = inventoryQuery.Sum(i => i.Quantity),
                TotalInventoryValue = totalValue,
                LowStockCount = lowStock.Count + criticalStock.Count + outOfStock.Count,
                OutOfStockCount = outOfStock.Count,
                CriticalStockCount = critical.Count,
                InventoryItems = inventoryQuery.Select(i => MapToInventoryViewModel(i)).ToList(),
                LowStockAlerts = lowStockAlerts.Select(i => MapToInventoryViewModel(i)).ToList()
            };

            ViewBag.IsAdmin = await IsAdmin();
            ViewBag.ProductsWithPendingDeliveries = productsWithPendingDeliveries;

            return View(vm);
        }

        // GET: /Inventory/Index
        public async Task<IActionResult> Index(string search = "", string category = "", string status = "")
        {
            var query = _context.Inventory
                .Include(i => i.Product)
                .AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(i => i.Product.Name.Contains(search));

            if (!string.IsNullOrEmpty(category))
                query = query.Where(i => i.Product.Category == category);

            if (!string.IsNullOrEmpty(status))
            {
                query = status switch
                {
                    "low" => query.Where(i => i.Quantity > 20 && i.Quantity <= 50),
                    "critical" => query.Where(i => i.Quantity <= 20 && i.Quantity > 0),
                    "out" => query.Where(i => i.Quantity == 0),
                    "ok" => query.Where(i => i.Quantity > 50),
                    _ => query
                };
            }

            var inventory = await query.OrderBy(i => i.Product.Name).ToListAsync();
            var categories = await _context.Products
                .Select(p => p.Category)
                .Distinct()
                .Where(c => !string.IsNullOrEmpty(c))
                .ToListAsync();

            // Get products with pending deliveries
            var productsWithPendingDeliveries = await _context.SupplierDeliveries
                .Select(d => d.ProductId)
                .Distinct()
                .ToListAsync();

            ViewBag.Categories = categories;
            ViewBag.Search = search;
            ViewBag.SelectedCategory = category;
            ViewBag.SelectedStatus = status;
            ViewBag.IsAdmin = await IsAdmin();
            ViewBag.ProductsWithPendingDeliveries = productsWithPendingDeliveries;

            return View(inventory.Select(MapToInventoryViewModel));
        }

        // ==================== STOCKS / SUPPLIER DELIVERIES ====================

        // GET: /Inventory/Stocks - Main Stocks page showing deliveries
        public async Task<IActionResult> Stocks()
        {
            var deliveries = await _context.SupplierDeliveries
                .Include(d => d.Supplier)
                .Include(d => d.Product)
                .OrderByDescending(d => d.DeliveryDate)
                .Take(50)
                .ToListAsync();

            var suppliers = await _context.Suppliers
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync();

            ViewBag.Suppliers = suppliers;
            ViewBag.IsAdmin = await IsAdmin();

            return View("Stocks", deliveries);
        }

        // GET: /Inventory/StockIn
        public async Task<IActionResult> StockIn(int? productId = null, int? supplierId = null)
        {
            var suppliers = await _context.Suppliers
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync();

            // If no supplier selected, use the first one by default
            if (!supplierId.HasValue || supplierId.Value <= 0)
            {
                if (suppliers.Any())
                {
                    supplierId = suppliers.First().Id;
                }
            }

            // Get only products with pending deliveries from the selected supplier
            List<Product> products;
            Dictionary<int, int> pendingDeliveryQuantities = new();
            
            if (supplierId.HasValue && supplierId.Value > 0)
            {
                var deliveries = await _context.SupplierDeliveries
                    .Where(d => d.SupplierId == supplierId.Value)
                    .ToListAsync();

                var deliveredProductIds = deliveries.Select(d => d.ProductId).Distinct().ToList();

                products = await _context.Products
                    .Where(p => p.IsActive && deliveredProductIds.Contains(p.Id))
                    .Include(p => p.Inventory)
                    .OrderBy(p => p.Name)
                    .ToListAsync();

                // Get total pending quantity for each product
                foreach (var productId_ in deliveredProductIds)
                {
                    var pendingQty = deliveries
                        .Where(d => d.ProductId == productId_)
                        .Sum(d => d.Quantity);
                    pendingDeliveryQuantities[productId_] = pendingQty;
                }

                ViewBag.PreSelectedSupplierId = supplierId.Value;
            }
            else
            {
                // Fallback: show no products if no supplier
                products = new List<Product>();
            }

            ViewBag.Products = products;
            ViewBag.Suppliers = suppliers;
            ViewBag.PendingDeliveries = pendingDeliveryQuantities;
            ViewBag.IsAdmin = await IsAdmin();

            var vm = new StockInOutViewModel 
            { 
                IsStockIn = true 
            };

            if (productId.HasValue)
            {
                var product = products.FirstOrDefault(p => p.Id == productId);
                if (product != null)
                {
                    vm.ProductId = product.Id;
                    vm.ProductName = product.Name;
                    vm.CurrentStock = product.Inventory?.Quantity ?? 0;
                }
            }

            return View("StockIn", vm);
        }

        // GET: /Inventory/CreateDelivery - Display form to record a new delivery
        public async Task<IActionResult> CreateDelivery()
        {
            var suppliers = await _context.Suppliers
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync();

            var products = await _context.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync();

            ViewBag.Suppliers = suppliers;
            ViewBag.Products = products;
            ViewBag.IsAdmin = await IsAdmin();

            return View(new SupplierDelivery());
        }

        // POST: /Inventory/CreateDelivery - Save new delivery record
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDelivery(SupplierDelivery delivery)
        {
            if (!ModelState.IsValid)
            {
                var suppliers = await _context.Suppliers.Where(s => s.IsActive).ToListAsync();
                var products = await _context.Products.Where(p => p.IsActive).ToListAsync();
                ViewBag.Suppliers = suppliers;
                ViewBag.Products = products;
                ViewBag.IsAdmin = await IsAdmin();
                return View(delivery);
            }

            delivery.DeliveryDate = DateTime.Now;
            delivery.TotalCost = delivery.Quantity * delivery.UnitCost;
            delivery.ReceivedBy = User.Identity?.Name ?? "Unknown";

            _context.SupplierDeliveries.Add(delivery);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Delivery recorded: {delivery.Quantity} units from supplier. Now stock them in!";
            return RedirectToAction(nameof(Stocks));
        }

        // GET: /Inventory/StockFromDelivery/5 - Stock In directly from a delivery
        public async Task<IActionResult> StockFromDelivery(int id)
        {
            var delivery = await _context.SupplierDeliveries
                .Include(d => d.Supplier)
                .Include(d => d.Product!)
                .ThenInclude(p => p.Inventory)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (delivery == null) return NotFound();

            var supplierName = delivery.Supplier?.Name ?? string.Empty;
            var referenceNumber = delivery.ReferenceNumber ?? string.Empty;

            var stockMovements = await _context.StockMovements
                .Where(sm => sm.ReferenceNumber == referenceNumber 
                    && sm.Type == MovementType.StockIn)
                .ToListAsync();

            var alreadyStocked = stockMovements.Any(sm => sm.Notes != null && sm.Notes.Contains(supplierName));

            if (alreadyStocked)
            {
                TempData["Warning"] = "This delivery has already been stocked in!";
                return RedirectToAction(nameof(Stocks));
            }

            var vm = new StockInOutViewModel
            {
                ProductId = delivery.ProductId,
                ProductName = delivery.Product?.Name ?? "Unknown",
                Quantity = delivery.Quantity,
                ReferenceNumber = delivery.ReferenceNumber,
                Notes = $"Delivery from {delivery.Supplier?.Name} - Invoice: {delivery.ReferenceNumber}",
                IsStockIn = true,
                CurrentStock = delivery.Product?.Inventory?.Quantity ?? 0
            };

            ViewBag.SupplierId = delivery.SupplierId;
            ViewBag.SupplierName = delivery.Supplier?.Name;
            ViewBag.UnitCost = delivery.UnitCost;
            ViewBag.DeliveryId = delivery.Id;
            ViewBag.IsAdmin = await IsAdmin();

            return View("StockFromDelivery", vm);
        }

        // POST: /Inventory/StockFromDelivery - Process the stock in + AUTO-CREATE PENDING PAYMENT
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StockFromDelivery(StockInOutViewModel model, int deliveryId, decimal unitCost, int supplierId)
        {
            if (!ModelState.IsValid)
            {
                return await ReloadStockFromDeliveryView(model, deliveryId);
            }

            if (model.Quantity <= 0)
            {
                ModelState.AddModelError("", "Quantity must be greater than 0");
                return await ReloadStockFromDeliveryView(model, deliveryId);
            }

            var delivery = await _context.SupplierDeliveries
                .Include(d => d.Supplier)
                .Include(d => d.Product)
                .FirstOrDefaultAsync(d => d.Id == deliveryId);

            if (delivery == null) return NotFound();

            var supplier = await _context.Suppliers.FindAsync(supplierId);
            var supplierName = supplier?.Name ?? "Unknown";

            var stockMovements = await _context.StockMovements
                .Where(sm => sm.ReferenceNumber == delivery.ReferenceNumber 
                    && sm.Type == MovementType.StockIn)
                .ToListAsync();

            var alreadyStocked = stockMovements.Any(sm => sm.Notes != null && sm.Notes.Contains(supplierName));

            if (alreadyStocked)
            {
                TempData["Warning"] = "This delivery has already been stocked in!";
                return RedirectToAction(nameof(Stocks));
            }

            var inventory = await _context.Inventory
                .Include(i => i.Product)
                .FirstOrDefaultAsync(i => i.ProductId == model.ProductId);

            if (inventory == null)
            {
                var product = await _context.Products.FindAsync(model.ProductId);
                if (product == null) return NotFound();

                inventory = new Inventory
                {
                    ProductId = model.ProductId,
                    Quantity = 0,
                    MinStockLevel = 10,
                    MaxStockLevel = 1000,
                    ReorderPoint = 20
                };
                _context.Inventory.Add(inventory);
            }

            // Check if product has no stock
            if (inventory.Quantity == 0)
            {
                ModelState.AddModelError("", $"⚠️ Warning: {inventory.Product?.Name ?? "This product"} currently has NO stock.");
                return await ReloadStockFromDeliveryView(model, deliveryId);
            }

            var previousStock = inventory.Quantity;
            inventory.Quantity += model.Quantity;
            inventory.LastUpdated = DateTime.Now;

            var movement = new StockMovement
            {
                ProductId = model.ProductId,
                Type = MovementType.StockIn,
                Quantity = model.Quantity,
                PreviousStock = previousStock,
                NewStock = inventory.Quantity,
                Notes = $"Stocked from delivery | Supplier: {supplierName} | Cost: ₱{unitCost} | {model.Notes}",
                ReferenceNumber = model.ReferenceNumber,
                PerformedBy = User.Identity?.Name,
                MovementDate = DateTime.Now
            };

            _context.StockMovements.Add(movement);

            // ========== AUTO-CREATE PENDING PAYMENT ==========
            decimal amountOwed = model.Quantity * unitCost;

            var existingPending = await _context.SupplierPayments
                .FirstOrDefaultAsync(sp => sp.SourceDeliveryId == deliveryId && sp.IsPending);

            if (existingPending == null)
            {
                var pendingPayment = new SupplierPayment
                {
                    SupplierId = supplierId,
                    Amount = amountOwed,
                    PaymentDate = DateTime.Now,
                    PaymentMethod = "Pending",
                    ReferenceNumber = $"DEL-{delivery.ReferenceNumber}",
                    Notes = $"Pending payment for {delivery.Product?.Name} delivery ({model.Quantity} units @ ₱{unitCost})",
                    PaidBy = "System",
                    IsPending = true,
                    SourceDeliveryId = deliveryId
                };

                _context.SupplierPayments.Add(pendingPayment);

                // Update supplier balance
                if (supplier != null)
                {
                    supplier.Balance += amountOwed;
                }
            }
            else
            {
                existingPending.Amount += amountOwed;
                existingPending.Notes += $" | Updated: +₱{amountOwed} for additional {model.Quantity} units";

                // Update supplier balance
                if (supplier != null)
                {
                    supplier.Balance += amountOwed;
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"✅ Stocked in {model.Quantity} units from {supplierName}! New stock: {inventory.Quantity}. ₱{amountOwed:N2} added to supplier balance.";
            return RedirectToAction(nameof(Stocks));
        }

        // POST: /Inventory/StockIn
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StockIn(StockInOutViewModel model, int? SupplierId, decimal? UnitCost)
        {
            if (!ModelState.IsValid)
            {
                return await ReloadStockInView(model);
            }

            if (model.Quantity <= 0)
            {
                ModelState.AddModelError("", "Quantity must be greater than 0");
                return await ReloadStockInView(model);
            }

            if (!SupplierId.HasValue)
            {
                ModelState.AddModelError("", "Please select a supplier");
                return await ReloadStockInView(model);
            }

            // Validate that the product has pending deliveries from the selected supplier
            var hasPendingDelivery = await _context.SupplierDeliveries
                .AnyAsync(d => d.SupplierId == SupplierId.Value && d.ProductId == model.ProductId);

            if (!hasPendingDelivery)
            {
                var product = await _context.Products.FindAsync(model.ProductId);
                var validationSupplier = await _context.Suppliers.FindAsync(SupplierId.Value);
                ModelState.AddModelError("", $"⚠️ This product ({product?.Name}) does not have any pending deliveries from {validationSupplier?.Name}");
                return await ReloadStockInView(model);
            }

            var inventory = await _context.Inventory
                .Include(i => i.Product)
                .FirstOrDefaultAsync(i => i.ProductId == model.ProductId);

            if (inventory == null)
            {
                var product = await _context.Products.FindAsync(model.ProductId);
                if (product == null) return NotFound();

                inventory = new Inventory
                {
                    ProductId = model.ProductId,
                    Quantity = 0,
                    MinStockLevel = 10,
                    MaxStockLevel = 1000,
                    ReorderPoint = 20
                };
                _context.Inventory.Add(inventory);
            }

            // Check if product has no stock
            if (inventory.Quantity == 0)
            {
                ModelState.AddModelError("", $"⚠️ Warning: {inventory.Product?.Name ?? "This product"} currently has NO stock.");
                return await ReloadStockInView(model);
            }

            var previousStock = inventory.Quantity;
            inventory.Quantity += model.Quantity;
            inventory.LastUpdated = DateTime.Now;

            var supplier = await _context.Suppliers.FindAsync(SupplierId.Value);
            var supplierName = supplier?.Name ?? "Unknown";

            var movement = new StockMovement
            {
                ProductId = model.ProductId,
                Type = MovementType.StockIn,
                Quantity = model.Quantity,
                PreviousStock = previousStock,
                NewStock = inventory.Quantity,
                Notes = $"From: {supplierName} | Cost: ₱{UnitCost} | {model.Notes}",
                ReferenceNumber = model.ReferenceNumber,
                PerformedBy = User.Identity?.Name,
                MovementDate = DateTime.Now
            };

            _context.StockMovements.Add(movement);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Stock-in successful! Added {model.Quantity} units from {supplierName}. New stock: {inventory.Quantity}";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Inventory/StockOut
        public async Task<IActionResult> StockOut(int? productId = null)
        {
            var products = await _context.Products
                .Where(p => p.IsActive && p.Inventory != null && p.Inventory.Quantity > 0)
                .Include(p => p.Inventory)
                .OrderBy(p => p.Name)
                .ToListAsync();

            ViewBag.Products = products;
            ViewBag.IsAdmin = await IsAdmin();

            if (productId.HasValue)
            {
                var product = products.FirstOrDefault(p => p.Id == productId);
                if (product != null)
                {
                    var vm = new StockInOutViewModel
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        CurrentStock = product.Inventory?.Quantity ?? 0,
                        IsStockIn = false
                    };
                    return View(vm);
                }
            }

            return View(new StockInOutViewModel { IsStockIn = false });
        }

        // POST: /Inventory/StockOut
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StockOut(StockInOutViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return await ReloadStockOutView(model);
            }

            if (model.Quantity <= 0)
            {
                ModelState.AddModelError("", "Quantity must be greater than 0");
                return await ReloadStockOutView(model);
            }

            var inventory = await _context.Inventory
                .Include(i => i.Product)
                .FirstOrDefaultAsync(i => i.ProductId == model.ProductId);

            if (inventory == null || inventory.Quantity < model.Quantity)
            {
                ModelState.AddModelError("", "Insufficient stock available");
                return await ReloadStockOutView(model);
            }

            var previousStock = inventory.Quantity;
            inventory.Quantity -= model.Quantity;
            inventory.LastUpdated = DateTime.Now;

            var movement = new StockMovement
            {
                ProductId = model.ProductId,
                Type = MovementType.StockOut,
                Quantity = model.Quantity,
                PreviousStock = previousStock,
                NewStock = inventory.Quantity,
                Notes = model.Notes,
                ReferenceNumber = model.ReferenceNumber,
                PerformedBy = User.Identity?.Name,
                MovementDate = DateTime.Now
            };

            _context.StockMovements.Add(movement);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Stock-out successful! Removed {model.Quantity} units. Remaining: {inventory.Quantity}";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Inventory/Movements
        public async Task<IActionResult> Movements(int? productId = null, DateTime? from = null, DateTime? to = null, int page = 1)
        {
            const int pageSize = 20;

            var query = _context.StockMovements
                .Include(sm => sm.Product)
                .OrderByDescending(sm => sm.MovementDate)
                .AsQueryable();

            if (productId.HasValue)
                query = query.Where(sm => sm.ProductId == productId);

            if (from.HasValue)
                query = query.Where(sm => sm.MovementDate >= from);

            if (to.HasValue)
                query = query.Where(sm => sm.MovementDate <= to.Value.AddDays(1));

            int totalItems = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));

            var movements = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var products = await _context.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync();

            ViewBag.Products       = products;
            ViewBag.SelectedProduct = productId;
            ViewBag.From           = from?.ToString("yyyy-MM-dd");
            ViewBag.To             = to?.ToString("yyyy-MM-dd");
            ViewBag.IsAdmin        = await IsAdmin();
            ViewBag.Page           = page;
            ViewBag.TotalPages     = totalPages;
            ViewBag.TotalItems     = totalItems;
            ViewBag.PageSize       = pageSize;

            return View(movements.Select(MapToMovementViewModel));
        }

        // GET: /Inventory/Suppliers
        public async Task<IActionResult> Suppliers()
        {
            var suppliers = await _context.Suppliers
                .Include(s => s.SupplierProducts)
                .OrderBy(s => s.Name)
                .ToListAsync();

            var vm = suppliers.Select(s => new SupplierViewModel
            {
                Id = s.Id,
                Name = s.Name,
                ContactPerson = s.ContactPerson,
                Phone = s.Phone,
                Email = s.Email,
                Address = s.Address,
                City = s.City,
                IsActive = s.IsActive,
                ProductCount = s.SupplierProducts?.Count ?? 0
            });

            ViewBag.IsAdmin = await IsAdmin();

            return View(vm);
        }

        // GET: /Inventory/Valuation
        public async Task<IActionResult> Valuation()
        {
            var inventory = await _context.Inventory
                .Include(i => i.Product)
                .Where(i => i.Product.IsActive)
                .ToListAsync();

            var byCategory = inventory
                .GroupBy(i => i.Product.Category)
                .Select(g => new
                {
                    Category = g.Key,
                    TotalItems = g.Sum(i => i.Quantity),
                    TotalValue = g.Sum(i => i.Quantity * i.Product.CostPrice),
                    AvgCost = g.Average(i => i.Product.CostPrice)
                })
                .OrderByDescending(x => x.TotalValue)
                .ToList();

            var totalValue = byCategory.Sum(x => x.TotalValue);
            var totalItems = byCategory.Sum(x => x.TotalItems);

            ViewBag.TotalValue = totalValue;
            ViewBag.TotalItems = totalItems;
            ViewBag.ByCategory = byCategory;
            ViewBag.IsAdmin = await IsAdmin();

            return View();
        }

        // ==================== SPOILAGE TRACKING ====================

        // GET: /Inventory/RecordSpoilage
        public async Task<IActionResult> RecordSpoilage()
        {
            var products = await _context.Products
                .Where(p => p.IsActive)
                .Include(p => p.Inventory)
                .OrderBy(p => p.Name)
                .ToListAsync();

            // Last 30 days spoilage summary
            var thirtyDaysAgo = DateTime.Today.AddDays(-30);
            var recentSpoilage = await _context.SpoilageRecords
                .Include(s => s.Product)
                .Where(s => s.RecordedAt >= thirtyDaysAgo)
                .OrderByDescending(s => s.RecordedAt)
                .Take(50)
                .ToListAsync();

            var totalLoss30d = recentSpoilage.Sum(s => s.EstimatedLoss);
            var spoilageRate = 0.0;
            var totalStockIn = await _context.StockMovements
                .Where(m => m.Type == MovementType.StockIn && m.MovementDate >= thirtyDaysAgo)
                .SumAsync(m => (int?)m.Quantity) ?? 0;
            if (totalStockIn > 0)
                spoilageRate = recentSpoilage.Sum(s => s.Quantity) / (double)totalStockIn * 100;

            ViewBag.Products       = products;
            ViewBag.RecentSpoilage = recentSpoilage;
            ViewBag.TotalLoss30d   = totalLoss30d;
            ViewBag.SpoilageRate   = Math.Round(spoilageRate, 1);
            ViewBag.BenchmarkRate  = 30.5; // from 5-year dataset
            ViewBag.IsAdmin        = await IsAdmin();

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordSpoilage(int productId, int quantity, string reason, string? notes)
        {
            if (productId <= 0)
            {
                TempData["Error"] = "Please select a valid product.";
                return RedirectToAction(nameof(RecordSpoilage));
            }
            if (quantity <= 0 || quantity > 100000)
            {
                TempData["Error"] = "Quantity must be between 1 and 100,000.";
                return RedirectToAction(nameof(RecordSpoilage));
            }
            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["Error"] = "Reason is required.";
                return RedirectToAction(nameof(RecordSpoilage));
            }
            if (reason.Length > 100)
            {
                TempData["Error"] = "Reason cannot exceed 100 characters.";
                return RedirectToAction(nameof(RecordSpoilage));
            }
            if (notes?.Length > 500)
            {
                TempData["Error"] = "Notes cannot exceed 500 characters.";
                return RedirectToAction(nameof(RecordSpoilage));
            }

            var product = await _context.Products
                .Include(p => p.Inventory)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null)
            {
                TempData["Error"] = "Product not found.";
                return RedirectToAction(nameof(RecordSpoilage));
            }

            var estimatedLoss = quantity * product.CostPrice;

            // Deduct from inventory
            if (product.Inventory != null)
            {
                var prev = product.Inventory.Quantity;
                product.Inventory.Quantity = Math.Max(0, product.Inventory.Quantity - quantity);
                product.Inventory.LastUpdated = DateTime.Now;

                _context.StockMovements.Add(new StockMovement
                {
                    ProductId     = productId,
                    Type          = MovementType.Adjustment,
                    Quantity      = quantity,
                    PreviousStock = prev,
                    NewStock      = product.Inventory.Quantity,
                    Notes         = $"Spoilage write-off — {reason}",
                    PerformedBy   = User.Identity?.Name,
                    MovementDate  = DateTime.Now
                });
            }

            // Record the spoilage event
            _context.SpoilageRecords.Add(new SpoilageRecord
            {
                ProductId     = productId,
                Quantity      = quantity,
                EstimatedLoss = estimatedLoss,
                Reason        = reason,
                RecordedBy    = User.Identity?.Name ?? "Manager",
                RecordedAt    = DateTime.Now,
                Notes         = notes
            });

            // Record as expense
            _context.Expenses.Add(new Expense
            {
                Description     = $"Spoilage: {product.Name} ({quantity} units)",
                Category        = "Spoilage",
                Amount          = estimatedLoss,
                ExpenseDate     = DateTime.Now,
                RecordedBy      = User.Identity?.Name ?? "Manager",
                CreatedAt       = DateTime.Now,
                IsDeleted       = false
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Spoilage recorded: {quantity} units of {product.Name} (₱{estimatedLoss:N2} loss).";
            return RedirectToAction(nameof(RecordSpoilage));
        }

        // ==================== SUPPLIER PAYMENT FEATURES ====================

        // GET: /Inventory/SupplierPayments
        public async Task<IActionResult> SupplierPayments()
        {
            var payments = await _context.SupplierPayments
                .Include(sp => sp.Supplier)
                .OrderByDescending(sp => sp.PaymentDate)
                .Take(50)
                .ToListAsync();

            var vm = payments.Select(p => new SupplierPaymentViewModel
            {
                Id = p.Id,
                SupplierId = p.SupplierId,
                SupplierName = p.Supplier?.Name ?? "Unknown",
                Amount = p.Amount,
                PaymentDate = p.PaymentDate,
                PaymentMethod = p.PaymentMethod,
                ReferenceNumber = p.ReferenceNumber,
                Notes = p.Notes,
                PaidBy = p.PaidBy,
                IsPending = p.IsPending
            });

            ViewBag.IsAdmin = await IsAdmin();
            return View(vm);
        }

        // GET: /Inventory/PaySupplier
        public async Task<IActionResult> PaySupplier(int? id)
        {
            var allSuppliers = await _context.Suppliers
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync();

            // Calculate balance for each supplier
            var supplierBalances = new Dictionary<int, decimal>();

            foreach (var supplier in allSuppliers)
            {
                var deliveries = await _context.SupplierDeliveries
                    .Where(d => d.SupplierId == supplier.Id)
                    .ToListAsync();

                var totalDeliveries = deliveries.Sum(d => d.TotalCost);

                var totalPaid = await _context.SupplierPayments
                    .Where(sp => sp.SupplierId == supplier.Id && !sp.IsPending)
                    .SumAsync(sp => (decimal?)sp.Amount) ?? 0m;

                var balance = totalDeliveries - totalPaid;
                supplierBalances[supplier.Id] = balance;
            }

            var viewModel = new PaySupplierViewModel();
            
            ViewBag.Suppliers = allSuppliers ?? new List<Supplier>();
            ViewBag.SupplierBalances = supplierBalances;
            ViewBag.IsAdmin = await IsAdmin();
            ViewBag.SelectedSupplier = null;
            ViewBag.BalanceDue = (decimal?)null;

            if (id.HasValue && id.Value > 0)
            {
                var supplier = await _context.Suppliers.FindAsync(id.Value);
                if (supplier != null)
                {
                    viewModel.SupplierId = supplier.Id;
                    ViewBag.SelectedSupplier = supplier;
                    
                    if (supplierBalances.ContainsKey(supplier.Id))
                    {
                        ViewBag.BalanceDue = supplierBalances[supplier.Id];
                    }
                    else
                    {
                        ViewBag.BalanceDue = 0m;
                    }
                }
            }

            return View(viewModel);
        }

        // POST: /Inventory/PaySupplier - UPDATED TO HANDLE PENDING PAYMENTS
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PaySupplier(PaySupplierViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                var suppliers = await _context.Suppliers
                    .Where(s => s.IsActive)
                    .OrderBy(s => s.Name)
                    .ToListAsync();
                
                ViewBag.Suppliers = suppliers;
                ViewBag.IsAdmin = await IsAdmin();
                
                if (viewModel.SupplierId > 0)
                {
                    var supplier = await _context.Suppliers.FindAsync(viewModel.SupplierId);
                    if (supplier != null)
                    {
                        ViewBag.SelectedSupplier = supplier;
                        
                        var deliveries = await _context.SupplierDeliveries
                            .Where(d => d.SupplierId == viewModel.SupplierId)
                            .ToListAsync();

                        var totalDeliveries = deliveries.Sum(d => d.TotalCost);

                        var totalPaid = await _context.SupplierPayments
                            .Where(sp => sp.SupplierId == viewModel.SupplierId && !sp.IsPending)
                            .SumAsync(sp => (decimal?)sp.Amount) ?? 0m;

                        ViewBag.BalanceDue = totalDeliveries - totalPaid;
                    }
                }
                
                return View(viewModel);
            }

            var selectedSupplier = await _context.Suppliers.FindAsync(viewModel.SupplierId);
            if (selectedSupplier != null)
            {
                var deliveries = await _context.SupplierDeliveries
                    .Where(d => d.SupplierId == viewModel.SupplierId)
                    .ToListAsync();

                var totalDeliveries = deliveries.Sum(d => d.TotalCost);

                var totalPaid = await _context.SupplierPayments
                    .Where(sp => sp.SupplierId == viewModel.SupplierId && !sp.IsPending)
                    .SumAsync(sp => (decimal?)sp.Amount) ?? 0m;

                var balanceDue = totalDeliveries - totalPaid;

                if (viewModel.Amount > balanceDue)
                {
                    ModelState.AddModelError("Amount", $"Payment amount (₱{viewModel.Amount:N2}) cannot exceed balance due (₱{balanceDue:N2})");
                    
                    var allSuppliers = await _context.Suppliers
                        .Where(s => s.IsActive)
                        .OrderBy(s => s.Name)
                        .ToListAsync();
                    ViewBag.Suppliers = allSuppliers;
                    ViewBag.IsAdmin = await IsAdmin();
                    ViewBag.SelectedSupplier = selectedSupplier;
                    ViewBag.BalanceDue = balanceDue;
                    
                    return View(viewModel);
                }
            }


            // Get pending payments for this supplier
            var pendingPayments = await _context.SupplierPayments
                .Where(sp => sp.SupplierId == viewModel.SupplierId && sp.IsPending)
                .OrderBy(sp => sp.PaymentDate)
                .ToListAsync();

            decimal remainingPayment = viewModel.Amount;

            // Apply payment to pending records first
            foreach (var pending in pendingPayments)
            {
                if (remainingPayment <= 0) break;

                if (remainingPayment >= pending.Amount)
                {
                    // Fully pay this pending - mark as completed
                    pending.IsPending = false;
                    pending.PaymentMethod = viewModel.PaymentMethod;
                    pending.PaidBy = User.Identity?.Name ?? "Unknown";
                    pending.Notes += $" | PAID on {DateTime.Now:yyyy-MM-dd} via {viewModel.PaymentMethod}";
                    if (selectedSupplier != null)
                    {
                        selectedSupplier.Balance -= pending.Amount;
                    }
                    remainingPayment -= pending.Amount;
                }
                else
                {
                    // Partial payment - split the pending
                    pending.Amount -= remainingPayment;

                    // Create new paid record for the partial amount
                    var paidRecord = new SupplierPayment
                    {
                        SupplierId = viewModel.SupplierId,
                        Amount = remainingPayment,
                        PaymentDate = DateTime.Now,
                        PaymentMethod = viewModel.PaymentMethod,
                        ReferenceNumber = viewModel.ReferenceNumber ?? $"PARTIAL-{pending.ReferenceNumber}",
                        Notes = $"Partial payment from pending delivery. Original: {pending.Notes}",
                        PaidBy = User.Identity?.Name ?? "Unknown",
                        IsPending = false,
                        SourceDeliveryId = pending.SourceDeliveryId
                    };

                    _context.SupplierPayments.Add(paidRecord);
                    if (selectedSupplier != null)
                    {
                        selectedSupplier.Balance -= remainingPayment;
                    }
                    remainingPayment = 0;
                }
            }

            // If there's still remaining payment, create new payment record
            if (remainingPayment > 0)
            {
                var payment = new SupplierPayment
                {
                    SupplierId = viewModel.SupplierId,
                    Amount = remainingPayment,
                    PaymentDate = DateTime.Now,
                    PaymentMethod = viewModel.PaymentMethod,
                    ReferenceNumber = viewModel.ReferenceNumber ?? string.Empty,
                    Notes = viewModel.Notes ?? "Additional payment",
                    PaidBy = User.Identity?.Name ?? "Unknown",
                    IsPending = false
                };

                _context.SupplierPayments.Add(payment);
                if (selectedSupplier != null)
                {
                    selectedSupplier.Balance -= remainingPayment;
                }
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Payment of ₱{viewModel.Amount:N2} to supplier recorded successfully! Pending deliveries updated.";
            return RedirectToAction(nameof(SupplierBalances));
        }

        // GET: /Inventory/SupplierBalances - UPDATED TO USE DELIVERIES
        public async Task<IActionResult> SupplierBalances()
        {
            var suppliers = await _context.Suppliers
                .Where(s => s.IsActive)
                .ToListAsync();

            var balances = new List<SupplierBalanceViewModel>();

            foreach (var supplier in suppliers)
            {
                // Get all deliveries for this supplier
                var deliveries = await _context.SupplierDeliveries
                    .Where(d => d.SupplierId == supplier.Id)
                    .ToListAsync();

                var totalDeliveries = deliveries.Sum(d => d.TotalCost);

                // Get completed payments only (not pending)
                var totalPaid = await _context.SupplierPayments
                    .Where(sp => sp.SupplierId == supplier.Id && !sp.IsPending)
                    .SumAsync(sp => (decimal?)sp.Amount) ?? 0m;

                var lastDelivery = deliveries
                    .OrderByDescending(d => d.DeliveryDate)
                    .Select(d => (DateTime?)d.DeliveryDate)
                    .FirstOrDefault();

                var lastPayment = await _context.SupplierPayments
                    .Where(sp => sp.SupplierId == supplier.Id && !sp.IsPending)
                    .OrderByDescending(sp => sp.PaymentDate)
                    .Select(sp => (DateTime?)sp.PaymentDate)
                    .FirstOrDefaultAsync();

                balances.Add(new SupplierBalanceViewModel
                {
                    SupplierId = supplier.Id,
                    SupplierName = supplier.Name,
                    TotalDeliveries = totalDeliveries,
                    TotalPaid = totalPaid,
                    BalanceDue = totalDeliveries - totalPaid,
                    LastDeliveryDate = lastDelivery,
                    LastPaymentDate = lastPayment
                });
            }

            ViewBag.IsAdmin = await IsAdmin();
            return View(balances);
        }

        // DELETE: /Inventory/DeletePayment/5
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> DeletePayment(int id)
        {
            var payment = await _context.SupplierPayments.FindAsync(id);
            if (payment == null) return NotFound();

            _context.SupplierPayments.Remove(payment);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Payment deleted successfully!";
            return RedirectToAction(nameof(SupplierPayments));
        }

        // GET: /Inventory/EditPayment/5
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> EditPayment(int id)
        {
            var payment = await _context.SupplierPayments
                .Include(sp => sp.Supplier)
                .FirstOrDefaultAsync(sp => sp.Id == id);

            if (payment == null) return NotFound();

            var suppliers = await _context.Suppliers
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync();

            ViewBag.Suppliers = suppliers;
            ViewBag.IsAdmin = true;

            return View(payment);
        }

        // POST: /Inventory/EditPayment/5
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditPayment(int id, SupplierPayment payment)
        {
            if (id != payment.Id) return BadRequest();

            var existing = await _context.SupplierPayments.FindAsync(id);
            if (existing == null) return NotFound();

            existing.Amount = payment.Amount;
            existing.PaymentMethod = payment.PaymentMethod;
            existing.ReferenceNumber = payment.ReferenceNumber;
            existing.Notes = payment.Notes;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Payment updated successfully!";
            return RedirectToAction(nameof(SupplierPayments));
        }

        // ==================== HELPER METHODS ====================

        private InventoryViewModel MapToInventoryViewModel(Inventory i)
        {
            string status;
            if (i.Quantity == 0)
                status = "Out of Stock";
            else if (i.Quantity <= 20)
                status = "Critical Stock";
            else if (i.Quantity <= 50)
                status = "Low Stock";
            else
                status = "OK";

            var daysUntilStockout = i.Quantity > 0 ? EstimateDaysUntilStockout(i.ProductId, i.Quantity) : 0;

            return new InventoryViewModel
            {
                ProductId = i.ProductId,
                ProductName = i.Product?.Name ?? string.Empty,
                Category = i.Product?.Category ?? string.Empty,
                Emoji = i.Product?.Emoji ?? string.Empty,
                CurrentStock = i.Quantity,
                MinStockLevel = i.MinStockLevel,
                MaxStockLevel = i.MaxStockLevel,
                ReorderPoint = i.ReorderPoint,
                Location = i.Location,
                UnitPrice = i.Product?.Price ?? 0m,
                CostPrice = i.Product?.CostPrice ?? 0m,
                InventoryValue = i.Quantity * (i.Product?.CostPrice ?? 0m),
                StockStatus = status,
                DaysUntilStockout = daysUntilStockout,
                LastUpdated = i.LastUpdated
            };
        }

        private StockMovementViewModel MapToMovementViewModel(StockMovement sm)
        {
            return new StockMovementViewModel
            {
                Id = sm.Id,
                ProductName = sm.Product?.Name ?? string.Empty,
                Type = sm.Type.ToString(),
                Quantity = sm.Quantity,
                PreviousStock = sm.PreviousStock,
                NewStock = sm.NewStock,
                Notes = sm.Notes ?? string.Empty,
                ReferenceNumber = sm.ReferenceNumber ?? string.Empty,
                PerformedBy = sm.PerformedBy ?? string.Empty,
                MovementDate = sm.MovementDate
            };
        }

        private int EstimateDaysUntilStockout(int productId, int currentStock)
        {
            var thirtyDaysAgo = DateTime.Now.AddDays(-30);
            var totalSales = _context.SaleItems
                .Include(si => si.Sale)
                .Where(si => si.ProductId == productId && si.Sale.SaleDate >= thirtyDaysAgo)
                .Sum(si => (int?)si.Quantity) ?? 0;

            var avgDailySales = totalSales / 30.0;
            if (avgDailySales <= 0) return 999;

            return (int)(currentStock / avgDailySales);
        }

        private async Task<IActionResult> ReloadStockInView(StockInOutViewModel model)
        {
            var products = await _context.Products
                .Where(p => p.IsActive)
                .Include(p => p.Inventory)
                .OrderBy(p => p.Name)
                .ToListAsync();
    
            var suppliers = await _context.Suppliers
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync();
        
            ViewBag.Products = products;
            ViewBag.Suppliers = suppliers;
            ViewBag.IsAdmin = await IsAdmin();
    
            return View("StockIn", model);
        }

        private async Task<IActionResult> ReloadStockOutView(StockInOutViewModel model)
        {
            var products = await _context.Products
                .Where(p => p.IsActive && p.Inventory != null && p.Inventory.Quantity > 0)
                .Include(p => p.Inventory)
                .OrderBy(p => p.Name)
                .ToListAsync();
            ViewBag.Products = products;
            ViewBag.IsAdmin = await IsAdmin();
            return View("StockOut", model);
        }

        private async Task<IActionResult> ReloadStockFromDeliveryView(StockInOutViewModel model, int deliveryId)
        {
            var delivery = await _context.SupplierDeliveries
                .Include(d => d.Supplier)
                .Include(d => d.Product)
                .FirstOrDefaultAsync(d => d.Id == deliveryId);

            ViewBag.SupplierId = delivery?.SupplierId;
            ViewBag.SupplierName = delivery?.Supplier?.Name;
            ViewBag.UnitCost = delivery?.UnitCost;
            ViewBag.DeliveryId = deliveryId;
            ViewBag.IsAdmin = await IsAdmin();

            return View("StockFromDelivery", model);
        }
    }
}