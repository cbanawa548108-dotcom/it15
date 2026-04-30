using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRLFruitstandESS.Data;
using CRLFruitstandESS.Models;
using CRLFruitstandESS.Models.ViewModels;

namespace CRLFruitstandESS.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;

        public AdminDashboardController(
            ApplicationDbContext context, 
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var today = DateTime.Now.Date;
            var thirtyDaysAgo = today.AddDays(-30);

            var vm = new DashboardViewModel
            {
                FullName = user?.FullName ?? "Administrator",
                Role = "Admin",
                Department = user?.Department ?? "IT",
                LastLoginAt = user?.LastLoginAt,
                WelcomeMessage = "Full system access — monitor all operations and users.",

                // Quick Actions for Admin
                QuickActions = new List<QuickAction>
                {
                    new() { Icon = "bi-people", Label = "User Management", Url = "/Admin/Users", Color = "blue" },
                    new() { Icon = "bi-shield", Label = "Roles & Permissions", Url = "/Admin/Roles", Color = "purple" },
                    new() { Icon = "bi-box-seam", Label = "Inventory", Url = "/Inventory/Index", Color = "green" },
                    new() { Icon = "bi-cash-stack", Label = "Supplier Payments", Url = "/Inventory/SupplierBalances", Color = "orange" },
                    new() { Icon = "bi-graph-up", Label = "Financial Reports", Url = "/CfoDashboard/Reports", Color = "gold" },
                    new() { Icon = "bi-gear", Label = "System Settings", Url = "/Admin/Settings", Color = "red" }
                }
            };

            // System Overview Stats
            vm.TotalProducts = await _context.Products.CountAsync(p => p.IsActive);
            vm.TotalStockItems = await _context.Inventory.SumAsync(i => i.Quantity);
            
            // User Stats
            var totalUsers = await _userManager.Users.CountAsync();
            var activeUsers = await _userManager.Users.CountAsync(u => u.LastLoginAt >= today.AddDays(-7));

            // Today's Sales
            var todaysSales = await _context.Sales
                .Where(s => s.SaleDate.Date == today)
                .ToListAsync();
            
            vm.TodaysRevenue = todaysSales.Sum(s => s.TotalAmount);
            vm.TodaysTransactions = todaysSales.Count;

            // Inventory Issues
            var inventory = await _context.Inventory.ToListAsync();
            vm.LowStockCount = inventory.Count(i => i.Quantity > 20 && i.Quantity <= 50);
            vm.CriticalStockCount = inventory.Count(i => i.Quantity <= 20 && i.Quantity > 0);
            vm.OutOfStockCount = inventory.Count(i => i.Quantity == 0);

            // Supplier Payments Overview
            var suppliers = await _context.Suppliers.Where(s => s.IsActive).ToListAsync();
            foreach (var supplier in suppliers)
            {
                var deliveries = await _context.SupplierDeliveries
                    .Where(d => d.SupplierId == supplier.Id)
                    .ToListAsync();
                
                var totalDeliveries = deliveries.Sum(d => d.TotalCost);
                var totalPaid = await _context.SupplierPayments
                    .Where(sp => sp.SupplierId == supplier.Id && !sp.IsPending)
                    .SumAsync(sp => (decimal?)sp.Amount) ?? 0m;
                
                var balanceDue = totalDeliveries - totalPaid;
                
                if (balanceDue > 0)
                {
                    vm.TotalOutstandingPayables += balanceDue;
                    vm.SuppliersWithBalanceDue++;
                }
            }

            // Top Selling Products
            vm.TopSellingProducts = await _context.SaleItems
                .Include(si => si.Product)
                .Include(si => si.Sale)
                .Where(si => si.Sale.SaleDate >= thirtyDaysAgo)
                .GroupBy(si => new { si.ProductId, si.Product.Name, si.Product.Emoji })
                .Select(g => new TopSellingProduct
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.Name,
                    Emoji = g.Key.Emoji ?? "📦",
                    QuantitySold = g.Sum(si => si.Quantity),
                    Revenue = g.Sum(si => si.Quantity * si.UnitPrice)
                })
                .OrderByDescending(p => p.Revenue)
                .Take(5)
                .ToListAsync();

            // Audit Logs - Get recent stock movements and payments
            var auditLogs = new List<AuditLog>();

            // Get recent stock movements (last 20)
            var recentStockMovements = await _context.StockMovements
                .Include(sm => sm.Product)
                .OrderByDescending(sm => sm.MovementDate)
                .Take(20)
                .ToListAsync();

            foreach (var movement in recentStockMovements)
            {
                auditLogs.Add(new AuditLog
                {
                    Action = movement.Type == MovementType.StockIn ? "Create" : "Update",
                    Entity = $"{movement.Product?.Name ?? "Product"}",
                    EntityId = movement.ProductId,
                    User = movement.PerformedBy ?? "System",
                    Timestamp = movement.MovementDate,
                    Details = $"{movement.Type}: {movement.Quantity} units"
                });
            }

            // Get recent supplier payments (last 10)
            var recentPayments = await _context.SupplierPayments
                .Include(sp => sp.Supplier)
                .OrderByDescending(sp => sp.PaymentDate)
                .Take(10)
                .ToListAsync();

            foreach (var payment in recentPayments)
            {
                auditLogs.Add(new AuditLog
                {
                    Action = "Update",
                    Entity = payment.Supplier?.Name ?? "Supplier",
                    EntityId = payment.SupplierId,
                    User = "System",
                    Timestamp = payment.PaymentDate,
                    Details = $"Payment: ₱{payment.Amount:N2}"
                });
            }

            // Sort by timestamp descending and take top 20
            vm.AuditLogs = auditLogs
                .OrderByDescending(al => al.Timestamp)
                .Take(20)
                .ToList();

            // System Alerts
            vm.Alerts = new List<DashboardAlert>();
            
            if (vm.OutOfStockCount > 0)
            {
                vm.Alerts.Add(new DashboardAlert
                {
                    Type = "danger",
                    Title = "Critical: Out of Stock",
                    Message = $"{vm.OutOfStockCount} products are completely out of stock",
                    ActionUrl = "/Inventory?status=out",
                    ActionText = "Fix Now",
                    CreatedAt = DateTime.Now
                });
            }

            if (vm.SuppliersWithBalanceDue > 0)
            {
                vm.Alerts.Add(new DashboardAlert
                {
                    Type = "info",
                    Title = "Supplier Payments",
                    Message = $"₱{vm.TotalOutstandingPayables:N2} total outstanding to {vm.SuppliersWithBalanceDue} suppliers",
                    ActionUrl = "/Inventory/SupplierBalances",
                    ActionText = "View Details",
                    CreatedAt = DateTime.Now
                });
            }

            return View(vm);
        }

        private string GetWelcomeMessage()
        {
            var hour = DateTime.Now.Hour;
            return hour switch
            {
                < 12 => "Good morning! System is running smoothly.",
                < 17 => "Good afternoon! Monitor all operations.",
                < 21 => "Good evening! Review system health.",
                _ => "Late night admin session. All systems operational."
            };
        }
    }
}