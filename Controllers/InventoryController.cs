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

        public InventoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Inventory/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var inventoryQuery = await _context.Inventory
                .Include(i => i.Product)
                .ToListAsync();

            var totalValue = inventoryQuery.Sum(i => i.Quantity * i.Product.CostPrice);
            var lowStock = inventoryQuery.Where(i => i.Quantity > 20 && i.Quantity <= 50).ToList();
            var criticalStock = inventoryQuery.Where(i => i.Quantity <= 20 && i.Quantity > 0).ToList();
            var outOfStock = inventoryQuery.Where(i => i.Quantity == 0).ToList();
            var critical = inventoryQuery.Where(i => i.Quantity <= 20 && i.Quantity > 0).ToList();

            // Combine critical stock and low stock for alerts
            var lowStockAlerts = criticalStock.Concat(lowStock).OrderBy(i => i.Quantity).ToList();

            var vm = new InventoryDashboardViewModel
            {
                TotalProducts = await _context.Products.CountAsync(p => p.IsActive),
                TotalStockItems = inventoryQuery.Sum(i => i.Quantity),
                TotalInventoryValue = totalValue,
                LowStockCount = lowStock.Count + criticalStock.Count,
                OutOfStockCount = outOfStock.Count,
                CriticalStockCount = critical.Count,
                InventoryItems = inventoryQuery.Select(i => MapToInventoryViewModel(i)).ToList(),
                LowStockAlerts = lowStockAlerts.Select(i => MapToInventoryViewModel(i)).ToList()
            };

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

            ViewBag.Categories = categories;
            ViewBag.Search = search;
            ViewBag.SelectedCategory = category;
            ViewBag.SelectedStatus = status;

            return View(inventory.Select(MapToInventoryViewModel));
        }

        // GET: /Inventory/StockIn
        public async Task<IActionResult> StockIn(int? productId = null)
        {
            var products = await _context.Products
                .Where(p => p.IsActive)
                .Include(p => p.Inventory)
                .OrderBy(p => p.Name)
                .ToListAsync();

            ViewBag.Products = products;

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
                        IsStockIn = true
                    };
                    return View(vm);
                }
            }

            return View(new StockInOutViewModel { IsStockIn = true });
        }

        // POST: /Inventory/StockIn
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StockIn(StockInOutViewModel model)
        {
            if (model.Quantity <= 0)
            {
                ModelState.AddModelError("", "Quantity must be greater than 0");
                return await ReloadStockInView(model);
            }

            var inventory = await _context.Inventory
                .Include(i => i.Product)
                .FirstOrDefaultAsync(i => i.ProductId == model.ProductId);

            if (inventory == null)
            {
                // Create inventory record if doesn't exist
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

            var previousStock = inventory.Quantity;
            inventory.Quantity += model.Quantity;
            inventory.LastUpdated = DateTime.Now;

            // Record movement
            var movement = new StockMovement
            {
                ProductId = model.ProductId,
                Type = MovementType.StockIn,
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

            TempData["Success"] = $"Stock-in successful! Added {model.Quantity} units. New stock: {inventory.Quantity}";
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

            // Record movement
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
        public async Task<IActionResult> Movements(int? productId = null, DateTime? from = null, DateTime? to = null)
        {
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

            var movements = await query.Take(100).ToListAsync();
            var products = await _context.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.Name)
                .ToListAsync();

            ViewBag.Products = products;
            ViewBag.SelectedProduct = productId;
            ViewBag.From = from?.ToString("yyyy-MM-dd");
            ViewBag.To = to?.ToString("yyyy-MM-dd");

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

            return View(vm);
        }

        // GET: /Inventory/Valuation
       // GET: /Inventory/Valuation
public async Task<IActionResult> Valuation()
{
    // Fetch inventory with products - THIS IS THE FIX
    var inventory = await _context.Inventory
        .Include(i => i.Product)
        .Where(i => i.Product.IsActive)  // Only active products
        .ToListAsync();

    // Group by category and calculate
    var byCategory = inventory
        .GroupBy(i => i.Product.Category)
        .Select(g => new
        {
            Category = g.Key,
            TotalItems = g.Sum(i => i.Quantity),
            TotalValue = g.Sum(i => i.Quantity * i.Product.CostPrice),  // CostPrice × Quantity
            AvgCost = g.Average(i => i.Product.CostPrice)
        })
        .OrderByDescending(x => x.TotalValue)
        .ToList();

    var totalValue = byCategory.Sum(x => x.TotalValue);
    var totalItems = byCategory.Sum(x => x.TotalItems);

    // IMPORTANT: These must match your View
    ViewBag.TotalValue = totalValue;
    ViewBag.TotalItems = totalItems;
    ViewBag.ByCategory = byCategory;

    return View();
}

        // API: Get product stock (for AJAX)
        [HttpGet]
        public async Task<IActionResult> GetProductStock(int productId)
        {
            var inventory = await _context.Inventory
                .FirstOrDefaultAsync(i => i.ProductId == productId);

            return Json(new
            {
                currentStock = inventory?.Quantity ?? 0,
                minLevel = inventory?.MinStockLevel ?? 10,
                reorderPoint = inventory?.ReorderPoint ?? 20
            });
        }

        // Helper methods
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
                ProductName = i.Product.Name,
                Category = i.Product.Category,
                Emoji = i.Product.Emoji ?? string.Empty,
                CurrentStock = i.Quantity,
                MinStockLevel = i.MinStockLevel,
                MaxStockLevel = i.MaxStockLevel,
                ReorderPoint = i.ReorderPoint,
                Location = i.Location,
                UnitPrice = i.Product.Price,
                CostPrice = i.Product.CostPrice,
                InventoryValue = i.Quantity * i.Product.CostPrice,
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
            // Simple estimation based on average daily sales from last 30 days
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
            ViewBag.Products = products;
            return View(model);
        }

        private async Task<IActionResult> ReloadStockOutView(StockInOutViewModel model)
        {
            var products = await _context.Products
                .Where(p => p.IsActive && p.Inventory != null && p.Inventory.Quantity > 0)
                .Include(p => p.Inventory)
                .OrderBy(p => p.Name)
                .ToListAsync();
            ViewBag.Products = products;
            return View(model);
        }
    }
}