using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRLFruitstandESS.Data;
using CRLFruitstandESS.Models;
using CRLFruitstandESS.Models.ViewModels;

namespace CRLFruitstandESS.Controllers
{
    [Authorize(Roles = "CFO,CEO,Admin,Manager")]
    public class AnalyticsController : Controller
    {
        private readonly ApplicationDbContext _db;
        public AnalyticsController(ApplicationDbContext db) => _db = db;

        // ════════════════════════════════════════════
        // MAIN DASHBOARD
        // ════════════════════════════════════════════
        public async Task<IActionResult> Index(string period = "30d")
        {
            var (from, to) = PeriodRange(period);
            var vm = new AnalyticsViewModel { Period = period, DateFrom = from, DateTo = to };
            await PopulateViewModel(vm, from, to);
            return View(vm);
        }

        // ════════════════════════════════════════════
        // DRILL-DOWN API  (returns JSON)
        // ════════════════════════════════════════════

        // Revenue drill-down by category → daily breakdown
        [HttpGet]
        public async Task<IActionResult> DrillRevenue(string category, string period = "30d")
        {
            var (from, to) = PeriodRange(period);
            var query = _db.Revenues.Where(r => !r.IsDeleted && r.TransactionDate >= from && r.TransactionDate <= to);
            if (!string.IsNullOrEmpty(category) && category != "all")
                query = query.Where(r => r.Category == category);

            var rows = await query.OrderBy(r => r.TransactionDate).ToListAsync();

            var grouped = rows.GroupBy(r => r.TransactionDate.Date)
                .Select(g => new { date = g.Key, total = g.Sum(r => r.Amount) })
                .OrderBy(x => x.date).ToList();

            var result = new DrillDownResult
            {
                Title = $"Revenue — {(string.IsNullOrEmpty(category) || category == "all" ? "All Categories" : category)}",
                Type  = "line",
                Labels = grouped.Select(g => g.date.ToString("MMM dd")).ToList(),
                Datasets = new()
                {
                    new DrillDataset
                    {
                        Label = "Revenue",
                        Data  = grouped.Select(g => g.total).ToList(),
                        BackgroundColor = "rgba(59,130,246,0.15)",
                        BorderColor     = "#3b82f6"
                    }
                },
                Rows = rows.Select(r => new DrillRow
                {
                    Date     = r.TransactionDate.ToString("MMM dd, yyyy"),
                    Label    = r.Source,
                    Category = r.Category,
                    Amount   = r.Amount,
                    By       = r.RecordedBy
                }).ToList()
            };
            return Json(result);
        }

        // Expense drill-down by category → daily breakdown
        [HttpGet]
        public async Task<IActionResult> DrillExpense(string category, string period = "30d")
        {
            var (from, to) = PeriodRange(period);
            var query = _db.Expenses.Where(e => !e.IsDeleted && e.ExpenseDate >= from && e.ExpenseDate <= to);
            if (!string.IsNullOrEmpty(category) && category != "all")
                query = query.Where(e => e.Category == category);

            var rows = await query.OrderBy(e => e.ExpenseDate).ToListAsync();

            var grouped = rows.GroupBy(e => e.ExpenseDate.Date)
                .Select(g => new { date = g.Key, total = g.Sum(e => e.Amount) })
                .OrderBy(x => x.date).ToList();

            var result = new DrillDownResult
            {
                Title = $"Expenses — {(string.IsNullOrEmpty(category) || category == "all" ? "All Categories" : category)}",
                Type  = "bar",
                Labels = grouped.Select(g => g.date.ToString("MMM dd")).ToList(),
                Datasets = new()
                {
                    new DrillDataset
                    {
                        Label = "Expenses",
                        Data  = grouped.Select(g => g.total).ToList(),
                        BackgroundColor = "rgba(239,68,68,0.5)",
                        BorderColor     = "#ef4444"
                    }
                },
                Rows = rows.Select(e => new DrillRow
                {
                    Date     = e.ExpenseDate.ToString("MMM dd, yyyy"),
                    Label    = e.Description,
                    Category = e.Category,
                    Amount   = e.Amount,
                    By       = e.RecordedBy
                }).ToList()
            };
            return Json(result);
        }

        // Product drill-down → daily sales qty + revenue
        [HttpGet]
        public async Task<IActionResult> DrillProduct(int productId, string period = "30d")
        {
            var (from, to) = PeriodRange(period);
            var product = await _db.Products.FindAsync(productId);

            var items = await _db.SaleItems
                .Include(si => si.Sale)
                .Where(si => si.ProductId == productId
                          && si.Sale.SaleDate >= from
                          && si.Sale.SaleDate <= to
                          && si.Sale.Status == "Completed")
                .OrderBy(si => si.Sale.SaleDate)
                .ToListAsync();

            var grouped = items.GroupBy(si => si.Sale.SaleDate.Date)
                .Select(g => new { date = g.Key, qty = g.Sum(si => si.Quantity), rev = g.Sum(si => si.Subtotal) })
                .OrderBy(x => x.date).ToList();

            var result = new DrillDownResult
            {
                Title = $"Product: {product?.Name ?? productId.ToString()}",
                Type  = "bar",
                Labels = grouped.Select(g => g.date.ToString("MMM dd")).ToList(),
                Datasets = new()
                {
                    new DrillDataset { Label = "Qty Sold",  Data = grouped.Select(g => (decimal)g.qty).ToList(), BackgroundColor = "rgba(16,185,129,0.5)", BorderColor = "#10b981" },
                    new DrillDataset { Label = "Revenue ₱", Data = grouped.Select(g => g.rev).ToList(),          BackgroundColor = "rgba(59,130,246,0.5)",  BorderColor = "#3b82f6" }
                },
                Rows = items.Select(si => new DrillRow
                {
                    Date     = si.Sale.SaleDate.ToString("MMM dd, yyyy HH:mm"),
                    Label    = product?.Name ?? "",
                    Category = $"Qty: {si.Quantity}",
                    Amount   = si.Subtotal,
                    By       = si.Sale.CashierId
                }).ToList()
            };
            return Json(result);
        }

        // Monthly profit drill-down → weekly breakdown
        [HttpGet]
        public async Task<IActionResult> DrillMonth(int year, int month)
        {
            var start = new DateTime(year, month, 1);
            var end   = start.AddMonths(1).AddDays(-1);

            var revenues = await _db.Revenues
                .Where(r => !r.IsDeleted && r.TransactionDate >= start && r.TransactionDate <= end)
                .GroupBy(r => r.TransactionDate.Date)
                .Select(g => new { date = g.Key, total = g.Sum(r => r.Amount) })
                .ToListAsync();

            var expenses = await _db.Expenses
                .Where(e => !e.IsDeleted && e.ExpenseDate >= start && e.ExpenseDate <= end)
                .GroupBy(e => e.ExpenseDate.Date)
                .Select(g => new { date = g.Key, total = g.Sum(e => e.Amount) })
                .ToListAsync();

            var allDates = revenues.Select(r => r.date)
                .Union(expenses.Select(e => e.date))
                .OrderBy(d => d).ToList();

            var result = new DrillDownResult
            {
                Title = $"Daily P&L — {start:MMMM yyyy}",
                Type  = "bar",
                Labels = allDates.Select(d => d.ToString("dd")).ToList(),
                Datasets = new()
                {
                    new DrillDataset { Label = "Revenue",  Data = allDates.Select(d => revenues.FirstOrDefault(r => r.date == d)?.total ?? 0).ToList(),  BackgroundColor = "rgba(16,185,129,0.5)", BorderColor = "#10b981" },
                    new DrillDataset { Label = "Expenses", Data = allDates.Select(d => expenses.FirstOrDefault(e => e.date == d)?.total ?? 0).ToList(), BackgroundColor = "rgba(239,68,68,0.5)",  BorderColor = "#ef4444" }
                }
            };
            return Json(result);
        }

        // ════════════════════════════════════════════
        // PRIVATE HELPERS
        // ════════════════════════════════════════════
        private async Task PopulateViewModel(AnalyticsViewModel vm, DateTime from, DateTime to)
        {
            // Pull a 6-month window so monthly trend + period data come from ONE query each
            var sixMonthsAgo = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-5);

            // ── Run queries sequentially (DbContext is not thread-safe)
            var allRevenues  = await _db.Revenues
                .Where(r => !r.IsDeleted && r.TransactionDate >= sixMonthsAgo)
                .Select(r => new { r.TransactionDate, r.Amount, r.Category, r.Source, r.RecordedBy })
                .ToListAsync();

            var allExpenses  = await _db.Expenses
                .Where(e => !e.IsDeleted && e.ExpenseDate >= sixMonthsAgo)
                .Select(e => new { e.ExpenseDate, e.Amount, e.Category, e.Description, e.RecordedBy })
                .ToListAsync();

            var sales        = await _db.Sales
                .Where(s => s.SaleDate >= from && s.SaleDate <= to && s.Status == "Completed")
                .Select(s => new { s.TotalAmount })
                .ToListAsync();

            var saleItems    = await _db.SaleItems
                .Where(si => si.Sale.SaleDate >= from && si.Sale.SaleDate <= to && si.Sale.Status == "Completed")
                .Select(si => new
                {
                    si.ProductId,
                    ProductName = si.Product != null ? si.Product.Name : "Unknown",
                    si.Quantity,
                    si.Subtotal
                })
                .ToListAsync();

            var inventory    = await _db.Inventory
                .Where(i => i.Product != null)
                .Select(i => new
                {
                    ProductName  = i.Product != null ? i.Product.Name : "Unknown",
                    i.Quantity,
                    i.ReorderPoint
                })
                .ToListAsync();

            // Filter period slice in memory (already fast — data is loaded)
            var revenues = allRevenues.Where(r => r.TransactionDate >= from && r.TransactionDate <= to).ToList();
            var expenses = allExpenses.Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to).ToList();

            // ── KPI tiles
            vm.TotalRevenue  = revenues.Sum(r => r.Amount);
            vm.TotalExpenses = expenses.Sum(e => e.Amount);
            vm.NetProfit     = vm.TotalRevenue - vm.TotalExpenses;
            vm.GrossMargin   = vm.TotalRevenue > 0 ? (vm.NetProfit / vm.TotalRevenue) * 100 : 0;
            vm.TotalSales    = sales.Count;
            vm.AvgOrderValue = sales.Count > 0 ? sales.Average(s => s.TotalAmount) : 0;
            vm.TotalProducts = inventory.Count;
            vm.LowStockCount = inventory.Count(i => i.Quantity <= i.ReorderPoint);

            // ── Daily trend (in-memory grouping, no extra queries)
            var revByDay = revenues.GroupBy(r => r.TransactionDate.Date).ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));
            var expByDay = expenses.GroupBy(e => e.ExpenseDate.Date).ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));
            var days = (int)(to - from).TotalDays + 1;
            for (int i = 0; i < days; i++)
            {
                var day = from.AddDays(i).Date;
                vm.TrendLabels.Add(day.ToString("MMM dd"));
                vm.TrendRevenue.Add(revByDay.GetValueOrDefault(day, 0));
                vm.TrendExpenses.Add(expByDay.GetValueOrDefault(day, 0));
            }

            // ── Category breakdowns
            foreach (var g in expenses.GroupBy(e => e.Category).OrderByDescending(g => g.Sum(e => e.Amount)))
            { vm.ExpenseCatLabels.Add(g.Key); vm.ExpenseCatValues.Add(g.Sum(e => e.Amount)); }

            foreach (var g in revenues.GroupBy(r => r.Category).OrderByDescending(g => g.Sum(r => r.Amount)))
            { vm.RevenueCatLabels.Add(g.Key); vm.RevenueCatValues.Add(g.Sum(r => r.Amount)); }

            // ── Top 8 products
            var topProducts = saleItems
                .GroupBy(si => si.ProductName)
                .Select(g => new { Name = g.Key, Qty = g.Sum(si => si.Quantity), Rev = g.Sum(si => si.Subtotal) })
                .OrderByDescending(x => x.Qty).Take(8).ToList();
            vm.TopProductLabels = topProducts.Select(p => p.Name ?? "Unknown").ToList();
            vm.TopProductQty    = topProducts.Select(p => p.Qty).ToList();
            vm.TopProductRev    = topProducts.Select(p => p.Rev).ToList();

            // ── Monthly trend — group the already-loaded data, zero DB hits
            var revByMonth = allRevenues.GroupBy(r => new { r.TransactionDate.Year, r.TransactionDate.Month })
                .ToDictionary(g => g.Key, g => g.Sum(r => r.Amount));
            var expByMonth = allExpenses.GroupBy(e => new { e.ExpenseDate.Year, e.ExpenseDate.Month })
                .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

            for (int i = 5; i >= 0; i--)
            {
                var mStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-i);
                var key    = new { mStart.Year, mStart.Month };
                var mRev   = revByMonth.GetValueOrDefault(key, 0);
                var mExp   = expByMonth.GetValueOrDefault(key, 0);
                vm.MonthlyLabels.Add(mStart.ToString("MMM yy"));
                vm.MonthlyRevenue.Add(mRev);
                vm.MonthlyExpense.Add(mExp);
                vm.MonthlyProfit.Add(mRev - mExp);
            }

            // ── Stock levels
            foreach (var inv in inventory.OrderByDescending(i => i.Quantity).Take(10))
            {
                vm.StockLabels.Add(inv.ProductName ?? "Unknown");
                vm.StockQty.Add(inv.Quantity);
                vm.StockReorder.Add(inv.ReorderPoint);
            }
        }

        private static (DateTime from, DateTime to) PeriodRange(string period)
        {
            var today = DateTime.Today;
            return period switch
            {
                "7d"  => (today.AddDays(-6),  today),
                "30d" => (today.AddDays(-29), today),
                "90d" => (today.AddDays(-89), today),
                "ytd" => (new DateTime(today.Year, 1, 1), today),
                _     => (today.AddDays(-29), today)
            };
        }

        // ════════════════════════════════════════════
        // FORECAST ACCURACY BENCHMARKER
        // ════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> ForecastAccuracy()
        {
            var today = DateTime.Today;
            // Compare last month's forecast (using 30-day MA) vs actual
            var twoMonthsAgo = today.AddDays(-60);
            var oneMonthAgo  = today.AddDays(-30);

            var historical = await _db.Revenues
                .Where(r => !r.IsDeleted && r.TransactionDate >= twoMonthsAgo && r.TransactionDate < oneMonthAgo)
                .GroupBy(r => r.TransactionDate.Date)
                .Select(g => new { date = g.Key, total = g.Sum(r => r.Amount) })
                .OrderBy(x => x.date).ToListAsync();

            var actual = await _db.Revenues
                .Where(r => !r.IsDeleted && r.TransactionDate >= oneMonthAgo && r.TransactionDate <= today)
                .GroupBy(r => r.TransactionDate.Date)
                .Select(g => new { date = g.Key, total = g.Sum(r => r.Amount) })
                .OrderBy(x => x.date).ToListAsync();

            // Simple MA forecast from historical
            decimal avgForecast = historical.Any() ? historical.Average(x => x.total) : 0;
            decimal avgActual   = actual.Any() ? actual.Average(x => x.total) : 0;
            double mape = avgForecast > 0
                ? Math.Abs((double)((avgActual - avgForecast) / avgForecast)) * 100
                : 0;
            double accuracy = Math.Max(0, 100 - mape);

            var result = new
            {
                forecastAvg = avgForecast,
                actualAvg   = avgActual,
                mape        = Math.Round(mape, 2),
                accuracy    = Math.Round(accuracy, 2),
                labels      = actual.Select(a => a.date.ToString("MMM dd")).ToList(),
                actualData  = actual.Select(a => a.total).ToList(),
                forecastData= actual.Select(_ => avgForecast).ToList()
            };
            return Json(result);
        }

        // ════════════════════════════════════════════
        // STOCK DEMAND PREDICTOR
        // ════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> StockDemand()
        {
            var thirtyDaysAgo = DateTime.Today.AddDays(-30);

            var velocity = await _db.SaleItems
                .Where(si => si.Sale.SaleDate >= thirtyDaysAgo && si.Sale.Status == "Completed")
                .GroupBy(si => new { si.ProductId, si.Product.Name })
                .Select(g => new
                {
                    productId   = g.Key.ProductId,
                    name        = g.Key.Name,
                    totalQty    = g.Sum(si => si.Quantity),
                    dailyAvg    = (double)g.Sum(si => si.Quantity) / 30.0
                })
                .OrderByDescending(x => x.dailyAvg)
                .ToListAsync();

            var inventory = await _db.Inventory
                .Select(i => new { i.ProductId, i.Quantity, i.ReorderPoint })
                .ToListAsync();

            var predictions = velocity.Select(v =>
            {
                var inv = inventory.FirstOrDefault(i => i.ProductId == v.productId);
                var currentStock = inv?.Quantity ?? 0;
                var daysLeft = v.dailyAvg > 0 ? currentStock / v.dailyAvg : 999;
                return new
                {
                    v.name,
                    v.totalQty,
                    dailyAvg    = Math.Round(v.dailyAvg, 1),
                    currentStock,
                    daysLeft    = Math.Round(daysLeft, 0),
                    status      = daysLeft <= 3 ? "critical" : daysLeft <= 7 ? "warning" : "ok",
                    reorderPoint= inv?.ReorderPoint ?? 0
                };
            }).ToList();

            return Json(predictions);
        }

        // ════════════════════════════════════════════
        // HEAT MAP DATA (sales by hour × day-of-week)
        // ════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> HeatMapData(string period = "30d")
        {
            var (from, to) = PeriodRange(period);
            var sales = await _db.Sales
                .Where(s => s.SaleDate >= from && s.SaleDate <= to && s.Status == "Completed")
                .Select(s => new { s.SaleDate, s.TotalAmount })
                .ToListAsync();

            // Build 7×24 matrix [dayOfWeek][hour] = total amount
            var matrix = new decimal[7, 24];
            foreach (var s in sales)
                matrix[(int)s.SaleDate.DayOfWeek, s.SaleDate.Hour] += s.TotalAmount;

            var rows = new List<object>();
            var days = new[] { "Sun","Mon","Tue","Wed","Thu","Fri","Sat" };
            for (int d = 0; d < 7; d++)
                for (int h = 0; h < 24; h++)
                    if (matrix[d, h] > 0)
                        rows.Add(new { day = days[d], hour = h, value = matrix[d, h] });

            return Json(rows);
        }
    }
}
