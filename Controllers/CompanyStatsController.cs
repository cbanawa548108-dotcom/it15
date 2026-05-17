// Controllers/CompanyStatsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRLFruitstandESS.Data;
using CRLFruitstandESS.Models;

namespace CRLFruitstandESS.Controllers
{
    [Authorize(Roles = "CFO,CEO,Admin,Manager")]
    public class CompanyStatsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly ILogger<CompanyStatsController> _logger;

        public CompanyStatsController(ApplicationDbContext db, ILogger<CompanyStatsController> logger)
        {
            _db     = db;
            _logger = logger;
        }

        // GET /CompanyStats/Index
        public async Task<IActionResult> Index()
        {
            var today        = DateTime.Today;
            var thisMonthStart = new DateTime(today.Year, today.Month, 1);
            var lastMonthStart = thisMonthStart.AddMonths(-1);
            var lastMonthEnd   = thisMonthStart.AddDays(-1);
            var yearStart      = new DateTime(today.Year, 1, 1);
            var sixMonthsAgo   = thisMonthStart.AddMonths(-5);

            // ── Load all data in parallel-safe sequential queries ──────────────────

            // Revenue: all-time + YTD + this month + last month
            var allRevenues = await _db.Revenues
                .Where(r => !r.IsDeleted)
                .Select(r => new { r.TransactionDate, r.Amount, r.Category, r.Source })
                .ToListAsync();

            // Expenses: all-time + YTD + this month + last month
            var allExpenses = await _db.Expenses
                .Where(e => !e.IsDeleted)
                .Select(e => new { e.ExpenseDate, e.Amount, e.Category })
                .ToListAsync();

            // Sales: completed only
            var allSales = await _db.Sales
                .Where(s => s.Status == "Completed")
                .Select(s => new { s.SaleDate, s.TotalAmount })
                .ToListAsync();

            // Sale items for product stats
            var allSaleItems = await _db.SaleItems
                .Where(si => si.Sale.Status == "Completed")
                .Select(si => new
                {
                    si.ProductId,
                    ProductName  = si.Product.Name,
                    ProductEmoji = si.Product.Emoji,
                    Category     = si.Product.Category,
                    si.Quantity,
                    si.Subtotal,
                    si.UnitPrice,
                    CostPrice    = si.Product.CostPrice,
                    SaleDate     = si.Sale.SaleDate
                })
                .ToListAsync();

            // Inventory
            var inventory = await _db.Inventory
                .Include(i => i.Product)
                .Where(i => i.Product != null)
                .Select(i => new
                {
                    i.ProductId,
                    ProductName  = i.Product!.Name,
                    ProductEmoji = i.Product.Emoji,
                    Category     = i.Product.Category,
                    i.Quantity,
                    i.ReorderPoint,
                    i.MinStockLevel,
                    CostPrice    = i.Product.CostPrice,
                    Price        = i.Product.Price,
                    IsActive     = i.Product.IsActive
                })
                .ToListAsync();

            // Spoilage
            var spoilage = await _db.SpoilageRecords
                .Select(s => new
                {
                    s.ProductId,
                    s.Quantity,
                    s.EstimatedLoss,
                    s.Reason,
                    s.SpoilageType,
                    s.IsSellable,
                    s.IsSold,
                    s.SoldRevenue,
                    s.RecordedAt
                })
                .ToListAsync();

            // Suppliers
            var suppliers = await _db.Suppliers
                .Where(s => s.IsActive)
                .Select(s => new { s.Id, s.Name, s.Balance })
                .ToListAsync();

            // Payment methods breakdown
            var paymentTxns = await _db.PaymentTransactions
                .Where(t => t.Status == "paid")
                .Select(t => new { t.Method, t.Amount, t.CreatedAt })
                .ToListAsync();

            // Products
            var products = await _db.Products
                .Select(p => new { p.Id, p.Name, p.Category, p.IsActive, p.Price, p.CostPrice, p.Emoji })
                .ToListAsync();

            // ── Compute all stats in memory ────────────────────────────────────────

            // --- Financial KPIs ---
            var revThisMonth  = allRevenues.Where(r => r.TransactionDate >= thisMonthStart).Sum(r => r.Amount);
            var revLastMonth  = allRevenues.Where(r => r.TransactionDate >= lastMonthStart && r.TransactionDate <= lastMonthEnd).Sum(r => r.Amount);
            var revYTD        = allRevenues.Where(r => r.TransactionDate >= yearStart).Sum(r => r.Amount);
            var revAllTime    = allRevenues.Sum(r => r.Amount);

            var expThisMonth  = allExpenses.Where(e => e.ExpenseDate >= thisMonthStart).Sum(e => e.Amount);
            var expLastMonth  = allExpenses.Where(e => e.ExpenseDate >= lastMonthStart && e.ExpenseDate <= lastMonthEnd).Sum(e => e.Amount);
            var expYTD        = allExpenses.Where(e => e.ExpenseDate >= yearStart).Sum(e => e.Amount);
            var expAllTime    = allExpenses.Sum(e => e.Amount);

            var profitThisMonth = revThisMonth - expThisMonth;
            var profitLastMonth = revLastMonth - expLastMonth;
            var profitYTD       = revYTD - expYTD;
            var profitAllTime   = revAllTime - expAllTime;

            var grossMarginPct  = revYTD > 0 ? (profitYTD / revYTD) * 100m : 0m;
            var revMoMChange    = revLastMonth > 0 ? ((revThisMonth - revLastMonth) / revLastMonth) * 100m : 0m;
            var profitMoMChange = profitLastMonth != 0 ? ((profitThisMonth - profitLastMonth) / Math.Abs(profitLastMonth)) * 100m : 0m;

            // --- Sales KPIs ---
            var salesThisMonth = allSales.Where(s => s.SaleDate >= thisMonthStart).ToList();
            var salesToday     = allSales.Where(s => s.SaleDate.Date == today).ToList();
            var salesYTD       = allSales.Where(s => s.SaleDate >= yearStart).ToList();

            var txnCountThisMonth = salesThisMonth.Count;
            var txnCountToday     = salesToday.Count;
            var avgOrderValue     = salesThisMonth.Count > 0 ? salesThisMonth.Average(s => s.TotalAmount) : 0m;
            var totalSalesYTD     = salesYTD.Sum(s => s.TotalAmount);

            // --- Inventory KPIs ---
            var totalInventoryValue = inventory.Sum(i => i.Quantity * i.CostPrice);
            var totalRetailValue    = inventory.Sum(i => i.Quantity * i.Price);
            var outOfStock          = inventory.Count(i => i.Quantity == 0);
            var criticalStock       = inventory.Count(i => i.Quantity > 0 && i.Quantity <= i.MinStockLevel);
            var lowStock            = inventory.Count(i => i.Quantity > i.MinStockLevel && i.Quantity <= i.ReorderPoint);
            var healthyStock        = inventory.Count(i => i.Quantity > i.ReorderPoint);
            var totalProducts       = products.Count(p => p.IsActive);
            var totalCategories     = products.Where(p => p.IsActive).Select(p => p.Category).Distinct().Count();

            // --- Spoilage KPIs ---
            var spoilageThisMonth   = spoilage.Where(s => s.RecordedAt >= thisMonthStart).ToList();
            var spoilageYTD         = spoilage.Where(s => s.RecordedAt >= yearStart).ToList();
            var totalSpoilageLoss   = spoilageYTD.Sum(s => s.EstimatedLoss);
            var spoilageRecovery    = spoilageYTD.Where(s => s.IsSold).Sum(s => s.SoldRevenue);
            var spoilageByReason    = spoilage.GroupBy(s => s.Reason)
                .Select(g => new { Reason = g.Key, Count = g.Sum(x => x.Quantity), Loss = g.Sum(x => x.EstimatedLoss) })
                .OrderByDescending(x => x.Loss).ToList();

            // --- Supplier KPIs ---
            var totalPayables       = suppliers.Sum(s => s.Balance);
            var suppliersWithDebt   = suppliers.Count(s => s.Balance > 0);

            // --- Payment method breakdown ---
            var payByMethod = paymentTxns
                .GroupBy(t => t.Method)
                .Select(g => new { Method = g.Key, Count = g.Count(), Total = g.Sum(t => t.Amount) })
                .OrderByDescending(x => x.Total).ToList();

            // --- Monthly 6-month trend ---
            var monthlyStats = new List<object>();
            for (int i = 5; i >= 0; i--)
            {
                var mStart = new DateTime(today.Year, today.Month, 1).AddMonths(-i);
                var mEnd   = mStart.AddMonths(1).AddDays(-1);
                var mRev   = allRevenues.Where(r => r.TransactionDate >= mStart && r.TransactionDate <= mEnd).Sum(r => r.Amount);
                var mExp   = allExpenses.Where(e => e.ExpenseDate >= mStart && e.ExpenseDate <= mEnd).Sum(e => e.Amount);
                var mSales = allSales.Where(s => s.SaleDate >= mStart && s.SaleDate <= mEnd).Count();
                monthlyStats.Add(new
                {
                    label   = mStart.ToString("MMM yy"),
                    revenue = mRev,
                    expense = mExp,
                    profit  = mRev - mExp,
                    sales   = mSales
                });
            }

            // --- Top 8 products by revenue (YTD) ---
            var topProducts = allSaleItems
                .Where(si => si.SaleDate >= yearStart)
                .GroupBy(si => new { si.ProductId, si.ProductName, si.ProductEmoji })
                .Select(g => new
                {
                    g.Key.ProductName,
                    g.Key.ProductEmoji,
                    TotalQty     = g.Sum(x => x.Quantity),
                    TotalRevenue = g.Sum(x => x.Subtotal),
                    AvgPrice     = g.Average(x => x.UnitPrice)
                })
                .OrderByDescending(x => x.TotalRevenue)
                .Take(8).ToList();

            // --- Category revenue breakdown (YTD) ---
            var categoryRevenue = allSaleItems
                .Where(si => si.SaleDate >= yearStart)
                .GroupBy(si => si.Category)
                .Select(g => new { Category = g.Key, Revenue = g.Sum(x => x.Subtotal), Qty = g.Sum(x => x.Quantity) })
                .OrderByDescending(x => x.Revenue).ToList();

            // --- Revenue source breakdown (this month) ---
            var revBySource = allRevenues
                .Where(r => r.TransactionDate >= thisMonthStart)
                .GroupBy(r => r.Source.Length > 30 ? r.Source[..30] : r.Source)
                .Select(g => new { Source = g.Key, Amount = g.Sum(x => x.Amount) })
                .OrderByDescending(x => x.Amount).Take(8).ToList();

            // --- Expense category breakdown (YTD) ---
            var expByCategory = allExpenses
                .Where(e => e.ExpenseDate >= yearStart)
                .GroupBy(e => e.Category)
                .Select(g => new { Category = g.Key, Amount = g.Sum(x => x.Amount) })
                .OrderByDescending(x => x.Amount).ToList();

            // --- Daily revenue last 30 days ---
            var last30Start = today.AddDays(-29);
            var dailyRevenue = Enumerable.Range(0, 30).Select(d =>
            {
                var day = last30Start.AddDays(d);
                return new
                {
                    label   = day.ToString("MMM dd"),
                    revenue = allRevenues.Where(r => r.TransactionDate.Date == day).Sum(r => r.Amount),
                    expense = allExpenses.Where(e => e.ExpenseDate.Date == day).Sum(e => e.Amount),
                    sales   = allSales.Where(s => s.SaleDate.Date == day).Count()
                };
            }).ToList();

            // --- Inventory health by category ---
            var invByCategory = inventory
                .GroupBy(i => i.Category)
                .Select(g => new
                {
                    Category = g.Key,
                    TotalQty = g.Sum(x => x.Quantity),
                    Value    = g.Sum(x => x.Quantity * x.CostPrice),
                    Products = g.Count()
                })
                .OrderByDescending(x => x.Value).ToList();

            // --- Operational efficiency ---
            var opEfficiency    = revYTD > 0 ? 100m - (expYTD / revYTD * 100m) : 0m;
            var roi             = expYTD > 0 ? (profitYTD / expYTD) * 100m : 0m;
            var cashFlowRatio   = expYTD > 0 ? revYTD / expYTD : 0m;
            var spoilageRate    = revYTD > 0 ? (totalSpoilageLoss / revYTD) * 100m : 0m;
            var recoveryRate    = totalSpoilageLoss > 0 ? (spoilageRecovery / totalSpoilageLoss) * 100m : 0m;

            // ── Company Health Score (0–100) ───────────────────────────────────────
            // 6 weighted signals, each scored 0–100 then weighted.
            var healthSignals = new List<(string Name, string Description, decimal Score, decimal Weight, string Status)>();

            // 1. Cash Flow Ratio (25 pts) — revenue / expenses
            //    ≥1.5 = 100, 1.2–1.5 = 80, 1.0–1.2 = 60, 0.8–1.0 = 30, <0.8 = 0
            decimal cfScore = cashFlowRatio >= 1.5m ? 100m
                            : cashFlowRatio >= 1.2m ? 80m
                            : cashFlowRatio >= 1.0m ? 60m
                            : cashFlowRatio >= 0.8m ? 30m : 0m;
            string cfStatus = cashFlowRatio >= 1.5m ? "Excellent"
                            : cashFlowRatio >= 1.2m ? "Good"
                            : cashFlowRatio >= 1.0m ? "Marginal"
                            : cashFlowRatio >= 0.8m ? "Warning" : "Critical";
            healthSignals.Add(("Cash Flow", $"Revenue covers expenses {cashFlowRatio:N2}×", cfScore, 25m, cfStatus));

            // 2. Profit Margin YTD (20 pts) — net profit / revenue
            //    ≥20% = 100, 10–20% = 80, 5–10% = 60, 0–5% = 35, negative = 0
            decimal pmScore = grossMarginPct >= 20m ? 100m
                            : grossMarginPct >= 10m ? 80m
                            : grossMarginPct >= 5m  ? 60m
                            : grossMarginPct >= 0m  ? 35m : 0m;
            string pmStatus = grossMarginPct >= 20m ? "Excellent"
                            : grossMarginPct >= 10m ? "Good"
                            : grossMarginPct >= 5m  ? "Marginal"
                            : grossMarginPct >= 0m  ? "Warning" : "Critical";
            healthSignals.Add(("Profit Margin", $"{grossMarginPct:N1}% net margin YTD", pmScore, 20m, pmStatus));

            // 3. Revenue Trend MoM (20 pts) — month-over-month growth
            //    ≥10% = 100, 0–10% = 75, -5–0% = 40, -15–-5% = 15, <-15% = 0
            decimal rtScore = revMoMChange >= 10m  ? 100m
                            : revMoMChange >= 0m   ? 75m
                            : revMoMChange >= -5m  ? 40m
                            : revMoMChange >= -15m ? 15m : 0m;
            string rtStatus = revMoMChange >= 10m  ? "Excellent"
                            : revMoMChange >= 0m   ? "Good"
                            : revMoMChange >= -5m  ? "Marginal"
                            : revMoMChange >= -15m ? "Warning" : "Critical";
            healthSignals.Add(("Revenue Trend", $"{(revMoMChange >= 0 ? "+" : "")}{revMoMChange:N1}% vs last month", rtScore, 20m, rtStatus));

            // 4. Inventory Health (15 pts) — % of products with healthy stock
            int totalInvProducts = outOfStock + criticalStock + lowStock + healthyStock;
            decimal invHealthPct = totalInvProducts > 0 ? (decimal)healthyStock / totalInvProducts * 100m : 100m;
            decimal ihScore = invHealthPct >= 80m ? 100m
                            : invHealthPct >= 60m ? 75m
                            : invHealthPct >= 40m ? 50m
                            : invHealthPct >= 20m ? 25m : 0m;
            string ihStatus = invHealthPct >= 80m ? "Excellent"
                            : invHealthPct >= 60m ? "Good"
                            : invHealthPct >= 40m ? "Marginal"
                            : invHealthPct >= 20m ? "Warning" : "Critical";
            healthSignals.Add(("Inventory Health", $"{invHealthPct:N0}% of products well-stocked", ihScore, 15m, ihStatus));

            // 5. Spoilage Rate (10 pts) — spoilage loss / revenue
            //    ≤2% = 100, 2–5% = 75, 5–10% = 50, 10–20% = 20, >20% = 0
            decimal srScore = spoilageRate <= 2m  ? 100m
                            : spoilageRate <= 5m  ? 75m
                            : spoilageRate <= 10m ? 50m
                            : spoilageRate <= 20m ? 20m : 0m;
            string srStatus = spoilageRate <= 2m  ? "Excellent"
                            : spoilageRate <= 5m  ? "Good"
                            : spoilageRate <= 10m ? "Marginal"
                            : spoilageRate <= 20m ? "Warning" : "Critical";
            healthSignals.Add(("Spoilage Control", $"{spoilageRate:N1}% of revenue lost to spoilage", srScore, 10m, srStatus));

            // 6. Supplier Payables (10 pts) — payables / monthly revenue
            //    ≤0.5× = 100, 0.5–1× = 75, 1–2× = 45, 2–3× = 15, >3× = 0
            decimal payablesRatio = revThisMonth > 0 ? totalPayables / revThisMonth : (totalPayables > 0 ? 99m : 0m);
            decimal spScore = payablesRatio <= 0.5m ? 100m
                            : payablesRatio <= 1m   ? 75m
                            : payablesRatio <= 2m   ? 45m
                            : payablesRatio <= 3m   ? 15m : 0m;
            string spStatus = payablesRatio <= 0.5m ? "Excellent"
                            : payablesRatio <= 1m   ? "Good"
                            : payablesRatio <= 2m   ? "Marginal"
                            : payablesRatio <= 3m   ? "Warning" : "Critical";
            healthSignals.Add(("Supplier Payables", $"₱{totalPayables:N0} owed ({payablesRatio:N1}× monthly revenue)", spScore, 10m, spStatus));

            // Weighted composite score
            decimal totalWeight    = healthSignals.Sum(s => s.Weight);
            decimal compositeScore = healthSignals.Sum(s => s.Score * s.Weight) / totalWeight;

            // Overall verdict
            string overallVerdict, overallEmoji, overallColor, overallBg, overallBorder;
            string[] overallInsights;

            if (compositeScore >= 80m)
            {
                overallVerdict = "Thriving";
                overallEmoji   = "🚀";
                overallColor   = "#34d399";
                overallBg      = "rgba(16,185,129,.08)";
                overallBorder  = "rgba(16,185,129,.35)";
                overallInsights = new[] {
                    "Revenue is growing and covering all expenses comfortably.",
                    "Inventory is well-stocked with minimal stockout risk.",
                    "Spoilage is under control — waste is minimal.",
                    "Supplier obligations are manageable relative to income."
                };
            }
            else if (compositeScore >= 65m)
            {
                overallVerdict = "Doing Well";
                overallEmoji   = "✅";
                overallColor   = "#86efac";
                overallBg      = "rgba(134,239,172,.07)";
                overallBorder  = "rgba(134,239,172,.3)";
                overallInsights = new[] {
                    "The business is profitable and financially stable.",
                    "A few areas need attention but nothing critical.",
                    "Focus on improving the weakest signals to reach peak performance."
                };
            }
            else if (compositeScore >= 50m)
            {
                overallVerdict = "Stable but Watchful";
                overallEmoji   = "⚠️";
                overallColor   = "#fbbf24";
                overallBg      = "rgba(251,191,36,.07)";
                overallBorder  = "rgba(251,191,36,.3)";
                overallInsights = new[] {
                    "The business is breaking even but margins are thin.",
                    "Revenue trends or inventory issues need immediate attention.",
                    "Reduce expenses and spoilage to improve profitability.",
                    "Monitor cash flow closely — a bad month could tip into loss."
                };
            }
            else if (compositeScore >= 30m)
            {
                overallVerdict = "Under Pressure";
                overallEmoji   = "🔴";
                overallColor   = "#f97316";
                overallBg      = "rgba(249,115,22,.07)";
                overallBorder  = "rgba(249,115,22,.35)";
                overallInsights = new[] {
                    "Multiple financial signals are in the warning zone.",
                    "Expenses may be outpacing revenue — review cost structure urgently.",
                    "Supplier payables or spoilage losses are eroding profitability.",
                    "Immediate action needed: cut costs, boost sales, reduce waste.",
                    "Consider renegotiating supplier terms to ease cash pressure."
                };
            }
            else
            {
                overallVerdict = "Critical — Bankruptcy Risk";
                overallEmoji   = "💀";
                overallColor   = "#f87171";
                overallBg      = "rgba(239,68,68,.1)";
                overallBorder  = "rgba(239,68,68,.45)";
                overallInsights = new[] {
                    "The business is in serious financial distress.",
                    "Revenue is not covering expenses — cash reserves are at risk.",
                    "Immediate intervention required: halt non-essential spending.",
                    "Seek financial advice or restructuring options immediately.",
                    "Prioritise collecting receivables and settling critical payables.",
                    "Review all product lines — discontinue unprofitable ones."
                };
            }

            // Specific action items based on worst signals
            var actionItems = new List<string>();
            foreach (var sig in healthSignals.Where(s => s.Status is "Warning" or "Critical").OrderBy(s => s.Score))
            {
                actionItems.Add(sig.Name switch
                {
                    "Cash Flow"         => cashFlowRatio < 1m
                                            ? "🚨 Cash flow is negative — expenses exceed revenue. Cut costs immediately."
                                            : "⚠️ Cash flow is tight. Increase sales volume or reduce operating costs.",
                    "Profit Margin"     => grossMarginPct < 0m
                                            ? "🚨 Operating at a loss. Review pricing and cost structure urgently."
                                            : "⚠️ Thin profit margins. Consider price adjustments or cost reduction.",
                    "Revenue Trend"     => revMoMChange < -15m
                                            ? "🚨 Revenue dropped sharply. Investigate cause — lost customers or seasonal dip?"
                                            : "⚠️ Revenue is declining. Launch promotions or expand product offerings.",
                    "Inventory Health"  => outOfStock > 0
                                            ? $"⚠️ {outOfStock} products are out of stock — reorder immediately to avoid lost sales."
                                            : $"⚠️ {criticalStock} products at critical levels. Place reorders now.",
                    "Spoilage Control"  => spoilageRate > 10m
                                            ? "🚨 High spoilage rate is destroying margins. Review storage and ordering quantities."
                                            : "⚠️ Spoilage is above target. Improve stock rotation and reduce over-ordering.",
                    "Supplier Payables" => payablesRatio > 2m
                                            ? "🚨 Supplier debt is very high relative to revenue. Risk of supply disruption."
                                            : "⚠️ Supplier payables are elevated. Prioritise payments to maintain good terms.",
                    _ => $"⚠️ {sig.Name} needs attention."
                });
            }

            ViewBag.HealthScore     = (int)Math.Round(compositeScore);
            ViewBag.HealthVerdict   = overallVerdict;
            ViewBag.HealthEmoji     = overallEmoji;
            ViewBag.HealthColor     = overallColor;
            ViewBag.HealthBg        = overallBg;
            ViewBag.HealthBorder    = overallBorder;
            ViewBag.HealthInsights  = overallInsights;
            ViewBag.ActionItems     = actionItems;
            ViewBag.HealthSignals   = healthSignals.Select(s => new
            {
                s.Name, s.Description, Score = (int)Math.Round(s.Score),
                Weight = (int)s.Weight, s.Status
            }).ToList();

            // ── Pack into ViewBag ──────────────────────────────────────────────────

            // Financial
            ViewBag.RevThisMonth    = revThisMonth;
            ViewBag.RevLastMonth    = revLastMonth;
            ViewBag.RevYTD          = revYTD;
            ViewBag.RevAllTime      = revAllTime;
            ViewBag.ExpThisMonth    = expThisMonth;
            ViewBag.ExpYTD          = expYTD;
            ViewBag.ProfitThisMonth = profitThisMonth;
            ViewBag.ProfitLastMonth = profitLastMonth;
            ViewBag.ProfitYTD       = profitYTD;
            ViewBag.ProfitAllTime   = profitAllTime;
            ViewBag.GrossMarginPct  = grossMarginPct;
            ViewBag.RevMoMChange    = revMoMChange;
            ViewBag.ProfitMoMChange = profitMoMChange;

            // Sales
            ViewBag.TxnCountThisMonth = txnCountThisMonth;
            ViewBag.TxnCountToday     = txnCountToday;
            ViewBag.AvgOrderValue     = avgOrderValue;
            ViewBag.TotalSalesYTD     = totalSalesYTD;

            // Inventory
            ViewBag.TotalInventoryValue = totalInventoryValue;
            ViewBag.TotalRetailValue    = totalRetailValue;
            ViewBag.OutOfStock          = outOfStock;
            ViewBag.CriticalStock       = criticalStock;
            ViewBag.LowStock            = lowStock;
            ViewBag.HealthyStock        = healthyStock;
            ViewBag.TotalProducts       = totalProducts;
            ViewBag.TotalCategories     = totalCategories;

            // Spoilage
            ViewBag.TotalSpoilageLoss = totalSpoilageLoss;
            ViewBag.SpoilageRecovery  = spoilageRecovery;
            ViewBag.SpoilageRate      = spoilageRate;
            ViewBag.RecoveryRate      = recoveryRate;
            ViewBag.SpoilageByReason  = spoilageByReason;

            // Suppliers
            ViewBag.TotalPayables     = totalPayables;
            ViewBag.SuppliersWithDebt = suppliersWithDebt;
            ViewBag.TotalSuppliers    = suppliers.Count;

            // Operational
            ViewBag.OpEfficiency  = opEfficiency;
            ViewBag.ROI           = roi;
            ViewBag.CashFlowRatio = cashFlowRatio;

            // Chart data (serialised as JSON for JS)
            ViewBag.MonthlyStats    = System.Text.Json.JsonSerializer.Serialize(monthlyStats);
            ViewBag.TopProducts     = System.Text.Json.JsonSerializer.Serialize(topProducts);
            ViewBag.CategoryRevenue = System.Text.Json.JsonSerializer.Serialize(categoryRevenue);
            ViewBag.RevBySource     = System.Text.Json.JsonSerializer.Serialize(revBySource);
            ViewBag.ExpByCategory   = System.Text.Json.JsonSerializer.Serialize(expByCategory);
            ViewBag.DailyRevenue    = System.Text.Json.JsonSerializer.Serialize(dailyRevenue);
            ViewBag.InvByCategory   = System.Text.Json.JsonSerializer.Serialize(invByCategory);
            ViewBag.PayByMethod     = System.Text.Json.JsonSerializer.Serialize(payByMethod);
            ViewBag.SpoilageReasons = System.Text.Json.JsonSerializer.Serialize(
                spoilageByReason.Select(x => new { x.Reason, x.Count, x.Loss }));

            ViewBag.GeneratedAt = DateTime.Now;
            ViewBag.Period      = $"{yearStart:MMM d} – {today:MMM d, yyyy}";

            return View();
        }
    }
}
