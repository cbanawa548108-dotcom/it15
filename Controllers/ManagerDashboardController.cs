using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRLFruitstandESS.Data;
using CRLFruitstandESS.Models;
using CRLFruitstandESS.Models.ViewModels;

namespace CRLFruitstandESS.Controllers
{
    [Authorize(Roles = "Manager,Admin")]
    public class ManagerDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        // Constants for string literals
        private const string COLOR_BLUE = "blue";
        private const string COLOR_GREEN = "green";
        private const string COLOR_ORANGE = "orange";
        private const string COLOR_RED = "red";
        private const string COLOR_PURPLE = "purple";
        private const string COLOR_GOLD = "gold";
        private const string TREND_DAILY = "daily";
        private const string TREND_WEEKLY = "weekly";
        private const string TREND_MONTHLY = "monthly";
        private const string ALERT_DANGER = "danger";
        private const string ALERT_WARNING = "warning";
        private const string STATUS_GOOD = "good";
        private const string STATUS_WARNING = "warning";
        private const string STATUS_DANGER = "danger";
        private const string STATUS_NEUTRAL = "neutral";

        public ManagerDashboardController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
            var user = await _userManager.GetUserAsync(User);
            var today = DateTime.Now.Date;
            var thirtyDaysAgo = today.AddDays(-30);
            var sevenDaysAgo = today.AddDays(-7);

            var vm = new DashboardViewModel
            {
                FullName = user?.FullName ?? "Manager",
                Role = "Manager",
                Department = user?.Department ?? "Operations",
                LastLoginAt = user?.LastLoginAt,
                WelcomeMessage = GetWelcomeMessage(),

                QuickActions = new List<QuickAction>
                {
                    new() { Icon = "bi-box-seam", Label = "Inventory", Url = "/Inventory/Index", Color = COLOR_BLUE },
                    new() { Icon = "bi-truck", Label = "Supplier Deliveries", Url = "/Inventory/Stocks", Color = COLOR_GREEN },
                    new() { Icon = "bi-cash-stack", Label = "Pay Supplier", Url = "/Inventory/PaySupplier", Color = COLOR_ORANGE },
                    new() { Icon = "bi-people", Label = "Suppliers", Url = "/Inventory/Suppliers", Color = COLOR_PURPLE },
                    new() { Icon = "bi-graph-up", Label = "Sales Report", Url = "/ManagerDashboard/Sales", Color = COLOR_GOLD },
                    new() { Icon = "bi-exclamation-triangle", Label = "Low Stock", Url = "/Inventory?status=critical", Color = COLOR_RED }
                }
            };

            // Inventory Stats
            var inventory = await _context.Inventory
                .Include(i => i.Product)
                .ToListAsync();

            vm.TotalProducts = await _context.Products.CountAsync(p => p.IsActive);
            vm.TotalStockItems = inventory.Sum(i => i.Quantity);
            vm.TotalInventoryValue = inventory.Sum(i => i.Quantity * i.Product.CostPrice);
            vm.LowStockCount = inventory.Count(i => i.Quantity > 20 && i.Quantity <= 50);
            vm.CriticalStockCount = inventory.Count(i => i.Quantity <= 20 && i.Quantity > 0);
            vm.OutOfStockCount = inventory.Count(i => i.Quantity == 0);

            // Today's Sales
            var todaysSales = await _context.Sales
                .Where(s => s.SaleDate.Date == today)
                .ToListAsync();
            
            vm.TodaysRevenue = todaysSales.Sum(s => s.TotalAmount);
            vm.TodaysTransactions = todaysSales.Count;

            // Today's Expenses
            vm.TodaysExpenses = await _context.Expenses
                .Where(e => e.ExpenseDate.Date == today)
                .SumAsync(e => (decimal?)e.Amount) ?? 0m;

            // Weekly & Monthly Sales for KPI
            var weeklySales = await _context.Sales
                .Where(s => s.SaleDate >= sevenDaysAgo)
                .SumAsync(s => (decimal?)s.TotalAmount) ?? 0m;
            
            var monthlySales = await _context.Sales
                .Where(s => s.SaleDate >= thirtyDaysAgo)
                .SumAsync(s => (decimal?)s.TotalAmount) ?? 0m;

            vm.WeeklyRevenue = weeklySales;
            vm.MonthlyRevenue = monthlySales;

            // Supplier Payments
            var suppliers = await _context.Suppliers
                .Where(s => s.IsActive)
                .ToListAsync();

            vm.UpcomingPayments = new List<SupplierPaymentDue>();
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
                    vm.UpcomingPayments.Add(new SupplierPaymentDue
                    {
                        SupplierId = supplier.Id,
                        SupplierName = supplier.Name,
                        BalanceDue = balanceDue,
                        LastDeliveryDate = deliveries.OrderByDescending(d => d.DeliveryDate).FirstOrDefault()?.DeliveryDate
                    });
                    vm.TotalOutstandingPayables += balanceDue;
                }
            }
            
            vm.SuppliersWithBalanceDue = vm.UpcomingPayments.Count;

            // Top Selling Products (Last 30 days)
            var topProducts = await _context.SaleItems
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

            foreach (var product in topProducts)
            {
                var stock = await _context.Inventory
                    .FirstOrDefaultAsync(i => i.ProductId == product.ProductId);
                product.CurrentStock = stock?.Quantity ?? 0;
            }
            
            vm.TopSellingProducts = topProducts;

            // Low Performing Products (Last 30 days - bottom 5)
            // Fix: EF Core can't translate Include() inside Select() — use two queries
            var allActiveProducts = await _context.Products
                .Where(p => p.IsActive)
                .Select(p => new { p.Id, p.Name, p.Emoji, p.Category })
                .ToListAsync();

            var soldByProduct = await _context.SaleItems
                .Where(si => si.Sale.SaleDate >= thirtyDaysAgo)
                .GroupBy(si => si.ProductId)
                .Select(g => new { ProductId = g.Key, Sold = g.Sum(si => si.Quantity) })
                .ToListAsync();

            var soldDict = soldByProduct.ToDictionary(x => x.ProductId, x => x.Sold);

            var lowProducts = allActiveProducts
                .Select(p => new { p.Id, p.Name, p.Emoji, p.Category, Sold = soldDict.GetValueOrDefault(p.Id, 0) })
                .OrderBy(p => p.Sold)
                .Take(5)
                .ToList();

            vm.LowPerformingProducts = new List<TopSellingProduct>();
            foreach (var p in lowProducts)
            {
                var stock = await _context.Inventory.FirstOrDefaultAsync(i => i.ProductId == p.Id);
                vm.LowPerformingProducts.Add(new TopSellingProduct
                {
                    ProductId = p.Id,
                    ProductName = p.Name,
                    Emoji = p.Emoji ?? "📦",
                    QuantitySold = p.Sold,
                    CurrentStock = stock?.Quantity ?? 0
                });
            }

            // Sales Trends for Charts (Last 30 days daily)
            vm.SalesTrends = await GetSalesTrendsAsync(thirtyDaysAgo, today, TREND_DAILY);
            
            // Category Performance for Pie Chart
            vm.CategoryPerformance = await _context.SaleItems
                .Include(si => si.Product)
                .Include(si => si.Sale)
                .Where(si => si.Sale.SaleDate >= thirtyDaysAgo)
                .GroupBy(si => si.Product.Category)
                .Select(g => new CategoryPerformance
                {
                    Category = g.Key,
                    Revenue = g.Sum(si => si.Quantity * si.UnitPrice),
                    ItemsSold = g.Sum(si => si.Quantity)
                })
                .ToListAsync();

            // Calculate percentages
            var totalCatRevenue = vm.CategoryPerformance.Sum(c => c.Revenue);
            foreach (var cat in vm.CategoryPerformance)
            {
                cat.PercentageOfTotalRevenue = totalCatRevenue > 0 
                    ? (double)(cat.Revenue / totalCatRevenue) * 100 
                    : 0;
            }

            // Inventory Movement Tracking (Last 30 days)
            vm.RecentStockMovements = await _context.StockMovements
                .Include(sm => sm.Product)
                .Where(sm => sm.MovementDate >= thirtyDaysAgo)
                .OrderByDescending(sm => sm.MovementDate)
                .Take(20)
                .Select(sm => new StockMovementView
                {
                    ProductName = sm.Product.Name,
                    Emoji = sm.Product.Emoji ?? "📦",
                    Type = sm.Type.ToString(),
                    Quantity = sm.Quantity,
                    Date = sm.MovementDate,
                    Reference = sm.ReferenceNumber ?? string.Empty
                })
                .ToListAsync();

            // Inventory Forecasting
            vm.LowStockForecasts = new List<InventoryForecast>();
            foreach (var item in inventory.Where(i => i.Quantity <= 50))
            {
                var sales = await _context.SaleItems
                    .Include(si => si.Sale)
                    .Where(si => si.ProductId == item.ProductId && si.Sale.SaleDate >= thirtyDaysAgo)
                    .SumAsync(si => (int?)si.Quantity) ?? 0;
                
                var avgDailySales = sales / 30.0;
                var daysUntilStockout = avgDailySales > 0 
                    ? (int)(item.Quantity / avgDailySales) 
                    : 999;

                vm.LowStockForecasts.Add(new InventoryForecast
                {
                    ProductId = item.ProductId,
                    ProductName = item.Product.Name,
                    Emoji = item.Product.Emoji ?? "📦",
                    CurrentStock = item.Quantity,
                    AverageDailySales = Math.Round(avgDailySales, 2),
                    DaysUntilStockout = daysUntilStockout,
                    PredictionConfidence = sales > 10 ? 85 : 60
                });
            }

            // Reorder Suggestions (EOQ Algorithm)
            vm.ReorderSuggestions = new List<ReorderSuggestion>();
            foreach (var item in inventory.Where(i => i.Quantity <= 100))
            {
                // Get average monthly sales
                var totalSalesQuantity = await _context.SaleItems
                    .Include(si => si.Sale)
                    .Where(si => si.ProductId == item.ProductId && si.Sale.SaleDate >= thirtyDaysAgo)
                    .SumAsync(si => (int?)si.Quantity) ?? 0;

                if (totalSalesQuantity > 0)
                {
                    // Simple EOQ calculation: √(2DS/H)
                    // D = Annual demand (sales for month * 12)
                    // S = Ordering cost (assume ₱500)
                    // H = Holding cost per unit (assume 10% of cost price)
                    var annualDemand = totalSalesQuantity * 12;
                    var orderingCost = 500m;
                    var holdingCost = item.Product.CostPrice * 0.10m;

                    if (holdingCost > 0)
                    {
                        var eoq = Math.Sqrt((double)(2 * annualDemand * orderingCost / holdingCost));
                        var suggestedQty = Math.Max((int)eoq, 50); // Minimum order 50 units

                        // Find preferred supplier
                        var supplierProduct = await _context.SupplierProducts
                            .Include(sp => sp.Supplier)
                            .Where(sp => sp.ProductId == item.ProductId)
                            .FirstOrDefaultAsync();

                        if (supplierProduct != null)
                        {
                            vm.ReorderSuggestions.Add(new ReorderSuggestion
                            {
                                ProductId = item.ProductId,
                                ProductName = item.Product.Name,
                                CurrentStock = item.Quantity,
                                SuggestedOrderQuantity = suggestedQty,
                                EstimatedCost = suggestedQty * item.Product.CostPrice,
                                SupplierId = supplierProduct.SupplierId,
                                SupplierName = supplierProduct.Supplier.Name,
                                SuggestedOrderDate = DateTime.Now
                            });
                        }
                    }
                }
            }

            // Alerts
            vm.Alerts = new List<DashboardAlert>();
            
            if (vm.OutOfStockCount > 0)
            {
                vm.Alerts.Add(new DashboardAlert
                {
                    Type = ALERT_DANGER,
                    Title = "Out of Stock",
                    Message = $"{vm.OutOfStockCount} products are out of stock",
                    ActionUrl = "/Inventory?status=out",
                    ActionText = "View Inventory",
                    CreatedAt = DateTime.Now
                });
            }

            if (vm.CriticalStockCount > 0)
            {
                vm.Alerts.Add(new DashboardAlert
                {
                    Type = ALERT_WARNING,
                    Title = "Critical Stock Level",
                    Message = $"{vm.CriticalStockCount} products need immediate reordering",
                    ActionUrl = "/Inventory?status=critical",
                    ActionText = "View Critical",
                    CreatedAt = DateTime.Now
                });
            }

            if (vm.UpcomingPayments.Any(p => p.BalanceDue > 10000))
            {
                vm.Alerts.Add(new DashboardAlert
                {
                    Type = ALERT_WARNING,
                    Title = "Large Payment Due",
                    Message = $"You have suppliers with >₱10,000 balance due",
                    ActionUrl = "/Inventory/SupplierBalances",
                    ActionText = "View Balances",
                    CreatedAt = DateTime.Now
                });
            }

            // ====== KPI CALCULATIONS ======
            // Profit Margin
            if (vm.TodaysRevenue > 0)
            {
                vm.ProfitMargin = (double)((vm.TodaysRevenue - vm.TodaysExpenses) / vm.TodaysRevenue * 100);
            }

            // Inventory Turnover Ratio (Cost of Goods Sold / Average Inventory Value)
            var lastMonthInventoryValue = inventory.Sum(i => i.Quantity * i.Product.CostPrice);
            if (lastMonthInventoryValue > 0)
            {
                var cogs = await _context.SaleItems
                    .Include(si => si.Product)
                    .Include(si => si.Sale)
                    .Where(si => si.Sale.SaleDate >= thirtyDaysAgo)
                    .SumAsync(si => (decimal?)(si.Quantity * si.Product.CostPrice)) ?? 0m;
                
                vm.InventoryTurnoverRatio = (double)(cogs / lastMonthInventoryValue);
            }

            // Growth Rates (Week over Week, Month over Month)
            var previousWeekRevenue = await _context.Sales
                .Where(s => s.SaleDate >= sevenDaysAgo.AddDays(-7) && s.SaleDate < sevenDaysAgo)
                .SumAsync(s => (decimal?)s.TotalAmount) ?? 0m;

            if (previousWeekRevenue > 0)
            {
                vm.WeeklyGrowthRate = (double)((vm.WeeklyRevenue - previousWeekRevenue) / previousWeekRevenue * 100);
            }

            var previousMonthRevenue = await _context.Sales
                .Where(s => s.SaleDate >= thirtyDaysAgo.AddMonths(-1) && s.SaleDate < thirtyDaysAgo)
                .SumAsync(s => (decimal?)s.TotalAmount) ?? 0m;

            if (previousMonthRevenue > 0)
            {
                vm.MonthlyGrowthRate = (double)((vm.MonthlyRevenue - previousMonthRevenue) / previousMonthRevenue * 100);
            }

            // Stock-out Frequency (how many times products went out of stock in the period)
            vm.StockOutFrequency = await _context.StockMovements
                .Include(sm => sm.Product)
                .Where(sm => sm.MovementDate >= thirtyDaysAgo && sm.Type == MovementType.StockOut)
                .CountAsync();

            // Average Order Value
            vm.AverageOrderValue = vm.TodaysTransactions > 0 
                ? vm.TodaysRevenue / vm.TodaysTransactions 
                : 0;

            // Best and Worst Selling Categories
            if (vm.CategoryPerformance.Any())
            {
                vm.BestSellingCategory = vm.CategoryPerformance
                    .OrderByDescending(c => c.Revenue)
                    .FirstOrDefault()?.Category ?? "N/A";

                vm.WorstSellingCategory = vm.CategoryPerformance
                    .OrderBy(c => c.Revenue)
                    .FirstOrDefault()?.Category ?? "N/A";
            }

            // Total Stock Movements
            vm.TotalStockMovements = await _context.StockMovements
                .Where(sm => sm.MovementDate >= thirtyDaysAgo)
                .CountAsync();

            // Average Daily Sales
            if (thirtyDaysAgo < today)
            {
                var totalSales = await _context.SaleItems
                    .Include(si => si.Sale)
                    .Where(si => si.Sale.SaleDate >= thirtyDaysAgo)
                    .SumAsync(si => (decimal?)(si.Quantity * si.UnitPrice)) ?? 0m;

                vm.AverageDailySales = totalSales / (decimal)(today - thirtyDaysAgo).TotalDays;
            }

            // Build KPI Metrics List for UI display
            vm.KPIMetrics = new List<KPIMetric>
            {
                new KPIMetric
                {
                    Name = "Profit Margin",
                    Value = $"{vm.ProfitMargin:F1}%",
                    NumericValue = vm.ProfitMargin,
                    Unit = "%",
                    Status = GetProfitMarginStatus(vm.ProfitMargin),
                    Icon = "bi-graph-up",
                    Color = GetProfitMarginColor(vm.ProfitMargin),
                    Target = "20%+",
                    Trend = vm.ProfitMargin > 15 ? "increasing" : "stable"
                },
                new KPIMetric
                {
                    Name = "Inventory Turnover",
                    Value = $"{vm.InventoryTurnoverRatio:F2}x",
                    NumericValue = vm.InventoryTurnoverRatio,
                    Unit = "x",
                    Status = GetInventoryTurnoverStatus(vm.InventoryTurnoverRatio),
                    Icon = "bi-arrow-repeat",
                    Color = GetInventoryTurnoverColor(vm.InventoryTurnoverRatio),
                    Target = "2.0x+",
                    Trend = "stable"
                },
                new KPIMetric
                {
                    Name = "Weekly Growth",
                    Value = $"{vm.WeeklyGrowthRate:F1}%",
                    NumericValue = vm.WeeklyGrowthRate,
                    Unit = "%",
                    Status = GetGrowthStatus(vm.WeeklyGrowthRate),
                    Icon = "bi-graph-up-arrow",
                    Color = GetGrowthColor(vm.WeeklyGrowthRate),
                    Target = ">0%",
                    Trend = vm.WeeklyGrowthRate > 0 ? "increasing" : "decreasing"
                },
                new KPIMetric
                {
                    Name = "Monthly Growth",
                    Value = $"{vm.MonthlyGrowthRate:F1}%",
                    NumericValue = vm.MonthlyGrowthRate,
                    Unit = "%",
                    Status = GetGrowthStatus(vm.MonthlyGrowthRate),
                    Icon = "bi-graph-up",
                    Color = GetGrowthColor(vm.MonthlyGrowthRate),
                    Target = ">0%",
                    Trend = vm.MonthlyGrowthRate > 0 ? "increasing" : "decreasing"
                },
                new KPIMetric
                {
                    Name = "Avg Order Value",
                    Value = $"₱{vm.AverageOrderValue:N0}",
                    NumericValue = (double)vm.AverageOrderValue,
                    Unit = "₱",
                    Status = GetOrderValueStatus(vm.AverageOrderValue),
                    Icon = "bi-receipt",
                    Color = COLOR_PURPLE
                },
                new KPIMetric
                {
                    Name = "Stock-Out Events",
                    Value = $"{vm.StockOutFrequency}",
                    NumericValue = vm.StockOutFrequency,
                    Unit = "events",
                    Status = GetStockOutStatus(vm.StockOutFrequency),
                    Icon = "bi-exclamation-triangle",
                    Color = GetStockOutColor(vm.StockOutFrequency)
                }
            };

            return View(vm);
            }
            catch (Exception ex)
            {
                var emptyVm = new DashboardViewModel
                {
                    FullName = User.Identity?.Name ?? "Manager",
                    Role = "Manager",
                    WelcomeMessage = "Error loading dashboard: " + ex.Message
                };
                TempData["Error"] = "Dashboard error: " + ex.Message;
                return View(emptyVm);
            }
        }

        // AJAX: Get Sales Data for Charts
        [HttpGet]
        public async Task<IActionResult> GetSalesData(string period = "daily")
        {
            var today = DateTime.Now.Date;
            DateTime startDate;
            string format;

            switch (period.ToLower())
            {
                case TREND_WEEKLY:
                    startDate = today.AddDays(-84); // 12 weeks
                    format = TREND_WEEKLY;
                    break;
                case TREND_MONTHLY:
                    startDate = today.AddMonths(-12);
                    format = TREND_MONTHLY;
                    break;
                default: // daily
                    startDate = today.AddDays(-30);
                    format = TREND_DAILY;
                    break;
            }

            var trends = await GetSalesTrendsAsync(startDate, today, format);
            return Json(trends);
        }

        // AJAX: Get Inventory Movement Data
        [HttpGet]
        public async Task<IActionResult> GetInventoryMovementData(int days = 30)
        {
            if (days < 1 || days > 365)
            {
                return BadRequest("Days must be between 1 and 365");
            }
            var startDate = DateTime.Now.AddDays(-days);
            
            var movements = await _context.StockMovements
                .Include(sm => sm.Product)
                .Where(sm => sm.MovementDate >= startDate)
                .GroupBy(sm => new { sm.MovementDate.Date, sm.Type })
                .Select(g => new
                {
                    Date = g.Key.Date,
                    Type = g.Key.Type,
                    TotalQuantity = g.Sum(sm => sm.Quantity)
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            var stockIn = movements.Where(m => m.Type == MovementType.StockIn)
                .GroupBy(m => m.Date)
                .Select(g => new { Date = g.Key, Quantity = g.Sum(x => x.TotalQuantity) });
            
            var stockOut = movements.Where(m => m.Type == MovementType.StockOut)
                .GroupBy(m => m.Date)
                .Select(g => new { Date = g.Key, Quantity = g.Sum(x => x.TotalQuantity) });

            return Json(new { stockIn, stockOut });
        }

        private async Task<List<SalesTrend>> GetSalesTrendsAsync(DateTime startDate, DateTime endDate, string format)
        {
            var sales = await _context.Sales
                .Where(s => s.SaleDate >= startDate && s.SaleDate <= endDate.AddDays(1))
                .ToListAsync();

            if (format == TREND_DAILY)
            {
                return sales
                    .GroupBy(s => s.SaleDate.Date)
                    .Select(g => new SalesTrend
                    {
                        Date = g.Key,
                        Revenue = g.Sum(s => s.TotalAmount),
                        TransactionCount = g.Count()
                    })
                    .OrderBy(s => s.Date)
                    .ToList();
            }
            else if (format == TREND_WEEKLY)
            {
                return sales
                    .GroupBy(s => new { Year = s.SaleDate.Year, Week = GetWeekNumber(s.SaleDate) })
                    .Select(g => new SalesTrend
                    {
                        Date = FirstDayOfWeek(g.Key.Year, g.Key.Week),
                        Revenue = g.Sum(s => s.TotalAmount),
                        TransactionCount = g.Count()
                    })
                    .OrderBy(s => s.Date)
                    .ToList();
            }
            else // monthly
            {
                return sales
                    .GroupBy(s => new { s.SaleDate.Year, s.SaleDate.Month })
                    .Select(g => new SalesTrend
                    {
                        Date = new DateTime(g.Key.Year, g.Key.Month, 1),
                        Revenue = g.Sum(s => s.TotalAmount),
                        TransactionCount = g.Count()
                    })
                    .OrderBy(s => s.Date)
                    .ToList();
            }
        }

        private int GetWeekNumber(DateTime date)
        {
            var culture = System.Globalization.CultureInfo.CurrentCulture;
            return culture.Calendar.GetWeekOfYear(date, 
                System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday);
        }

        // Sales Report Page
        [HttpGet]
        public async Task<IActionResult> SalesReport(string period = "daily")
        {
            var today = DateTime.Now.Date;
            DateTime startDate;
            string format;

            switch (period.ToLower())
            {
                case TREND_WEEKLY:
                    startDate = today.AddDays(-84); // 12 weeks
                    format = TREND_WEEKLY;
                    break;
                case TREND_MONTHLY:
                    startDate = today.AddMonths(-12);
                    format = TREND_MONTHLY;
                    break;
                default: // daily
                    startDate = today.AddDays(-30);
                    format = TREND_DAILY;
                    break;
            }

            var vm = new SalesReportViewModel
            {
                Period = period
            };

            // Get all sales in the period
            var sales = await _context.Sales
                .Include(s => s.SaleItems)
                .ThenInclude(si => si.Product)
                .Where(s => s.SaleDate >= startDate && s.SaleDate <= today.AddDays(1))
                .ToListAsync();

            // Overall Stats
            vm.TotalRevenue = sales.Sum(s => s.TotalAmount);
            vm.TotalTransactions = sales.Count;
            vm.AverageTransactionValue = sales.Count > 0 
                ? vm.TotalRevenue / vm.TotalTransactions 
                : 0;

            // Best Day
            if (sales.Any())
            {
                var bestDay = sales
                    .GroupBy(s => s.SaleDate.Date)
                    .Select(g => new { Date = g.Key, Revenue = g.Sum(s => s.TotalAmount) })
                    .OrderByDescending(x => x.Revenue)
                    .FirstOrDefault();

                vm.BestDay = new SalesReportViewModel.BestDayData
                {
                    Date = bestDay?.Date ?? today,
                    Revenue = bestDay?.Revenue ?? 0
                };
            }

            // Top Products
            vm.TopProducts = await _context.SaleItems
                .Include(si => si.Product)
                .Include(si => si.Sale)
                .Where(si => si.Sale.SaleDate >= startDate && si.Sale.SaleDate <= today.AddDays(1))
                .GroupBy(si => new { si.ProductId, si.Product.Name, si.Product.Emoji })
                .Select(g => new SalesReportViewModel.SalesProductData
                {
                    ProductId = g.Key.ProductId,
                    ProductName = g.Key.Name,
                    Emoji = g.Key.Emoji ?? "📦",
                    QuantitySold = g.Sum(si => si.Quantity),
                    Revenue = g.Sum(si => si.Quantity * si.UnitPrice)
                })
                .OrderByDescending(p => p.Revenue)
                .Take(10)
                .ToListAsync();

            // Low Performing Products
            var allProducts = await _context.Products
                .Where(p => p.IsActive)
                .ToListAsync();

            var productSalesDict = await _context.SaleItems
                .Include(si => si.Sale)
                .Where(si => si.Sale.SaleDate >= startDate && si.Sale.SaleDate <= today.AddDays(1))
                .GroupBy(si => si.ProductId)
                .Select(g => new { ProductId = g.Key, Revenue = g.Sum(si => si.Quantity * si.UnitPrice) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Revenue);

            vm.LowProducts = allProducts
                .Select(p => new SalesReportViewModel.SalesProductData
                {
                    ProductId = p.Id,
                    ProductName = p.Name,
                    Emoji = p.Emoji ?? "📦",
                    Revenue = productSalesDict.ContainsKey(p.Id) ? productSalesDict[p.Id] : 0,
                    QuantitySold = 0 // Can be calculated if needed
                })
                .OrderBy(p => p.Revenue)
                .Take(10)
                .ToList();

            // Sales Trends
            var trends = await GetSalesTrendsAsync(startDate, today, format);
            vm.Trends = trends.Select(t => new SalesReportViewModel.SalesTrendData
            {
                Date = t.Date,
                Revenue = t.Revenue,
                TransactionCount = t.TransactionCount
            }).ToList();

            return View(vm);
        }

        private static DateTime FirstDayOfWeek(int year, int weekNumber)
        {
            var jan1 = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var daysOffset = DayOfWeek.Monday - jan1.DayOfWeek;
            var firstMonday = jan1.AddDays(daysOffset);
            if (daysOffset > 0) firstMonday = firstMonday.AddDays(-7);
            return firstMonday.AddDays(weekNumber * 7);
        }

        private static string GetWelcomeMessage()
        {
            var hour = DateTime.Now.Hour;
            return hour switch
            {
                < 12 => "Good morning! Let's manage operations efficiently today.",
                < 17 => "Good afternoon! Track inventory and sales performance.",
                < 21 => "Good evening! Review today's operations summary.",
                _ => "Working late? Take care of yourself!"
            };
        }

        // Helper methods for KPI status and color determination
        private string GetProfitMarginStatus(double margin) => 
            margin >= 20 ? STATUS_GOOD : margin >= 10 ? STATUS_WARNING : STATUS_DANGER;

        private string GetProfitMarginColor(double margin) => 
            margin >= 20 ? COLOR_GREEN : margin >= 10 ? COLOR_ORANGE : COLOR_RED;

        private string GetInventoryTurnoverStatus(double ratio) => 
            ratio >= 2 ? STATUS_GOOD : ratio >= 1 ? STATUS_WARNING : STATUS_DANGER;

        private string GetInventoryTurnoverColor(double ratio) => 
            ratio >= 2 ? COLOR_BLUE : ratio >= 1 ? COLOR_ORANGE : COLOR_RED;

        private string GetGrowthStatus(double rate) => 
            rate > 0 ? STATUS_GOOD : rate > -5 ? STATUS_WARNING : STATUS_DANGER;

        private string GetGrowthColor(double rate) => 
            rate > 0 ? COLOR_GREEN : rate > -5 ? COLOR_ORANGE : COLOR_RED;

        private string GetOrderValueStatus(decimal value) => 
            value >= 1000 ? STATUS_GOOD : value >= 500 ? STATUS_WARNING : STATUS_NEUTRAL;

        private string GetStockOutStatus(int frequency) => 
            frequency < 5 ? STATUS_GOOD : frequency < 10 ? STATUS_WARNING : STATUS_DANGER;

        private string GetStockOutColor(int frequency) => 
            frequency < 5 ? COLOR_GREEN : frequency < 10 ? COLOR_ORANGE : COLOR_RED;
    }
}