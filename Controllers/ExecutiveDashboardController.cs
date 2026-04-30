using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRLFruitstandESS.Data;
using CRLFruitstandESS.Models;
using CRLFruitstandESS.Models.ViewModels;
using CRLFruitstandESS.Models.Executive;
using CRLFruitstandESS.Services;
using System.Text.Json;

namespace CRLFruitstandESS.Controllers
{
    [Authorize(Roles = "CFO,CEO,Admin")]
    public class ExecutiveDashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IForecastingService _forecastingService;
        private readonly IKPICalculationService _kpiService;
        private readonly IRiskAnalysisService _riskService;
        private readonly IScenarioService _scenarioService;

        public ExecutiveDashboardController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IForecastingService forecastingService,
            IKPICalculationService kpiService,
            IRiskAnalysisService riskService,
            IScenarioService scenarioService)
        {
            _context = context;
            _userManager = userManager;
            _forecastingService = forecastingService;
            _kpiService = kpiService;
            _riskService = riskService;
            _scenarioService = scenarioService;
        }

        // ════════════════════════════════════════════
        // MAIN DASHBOARD
        // ════════════════════════════════════════════
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Local);
            var thirtyDaysAgo = today.AddDays(-30);

            var vm = new ExecutiveDashboardViewModel
            {
                FullName = user?.FullName ?? "Executive",
                LastLoginAt = user?.LastLoginAt ?? DateTime.UtcNow,
                DashboardDate = today
            };

            // Fetch current period KPIs
            var currentKPIs = await _kpiService.CalculateStrategicKPIsAsync(monthStart, today);
            var previousMonthStart = monthStart.AddMonths(-1);
            var previousMonthEnd = monthStart.AddDays(-1);
            var previousKPIs = await _kpiService.CalculateStrategicKPIsAsync(previousMonthStart, previousMonthEnd);

            // KPI Summary — all values are decimal, store as decimal
            vm.TotalRevenue          = currentKPIs.ContainsKey("TotalRevenue")          ? currentKPIs["TotalRevenue"]          : 0m;
            vm.TotalExpenses         = currentKPIs.ContainsKey("TotalExpenses")         ? currentKPIs["TotalExpenses"]         : 0m;
            vm.NetProfit             = currentKPIs.ContainsKey("NetProfit")             ? currentKPIs["NetProfit"]             : 0m;
            vm.GrossMarginPercent    = currentKPIs.ContainsKey("GrossMarginPercent")    ? currentKPIs["GrossMarginPercent"]    : 0m;
            vm.OperationalEfficiency = currentKPIs.ContainsKey("OperationalEfficiency") ? currentKPIs["OperationalEfficiency"] : 0m;
            vm.ROI                   = currentKPIs.ContainsKey("ROI")                   ? currentKPIs["ROI"]                   : 0m;

            // Calculate month-over-month change — convert to double to match ViewModel property type
           if (previousKPIs.ContainsKey("TotalRevenue") && previousKPIs["TotalRevenue"] > 0m)
{
    decimal revenueChangePct = (vm.TotalRevenue - previousKPIs["TotalRevenue"])
                               / previousKPIs["TotalRevenue"] * 100m;
    vm.RevenueChangePercent = Convert.ToDouble(revenueChangePct); // ← this line
}

            // Health Score
            var (healthScore, status, insights) = await _kpiService.CalculateHealthScoreAsync(monthStart);
            vm.BusinessHealthScore = healthScore;
            vm.HealthStatus        = status;
            vm.HealthInsights      = insights;

            // Active Alerts
            vm.ExecutiveAlerts = await _context.ExecutiveAlerts
                .Where(a => !a.IsResolved)
                .OrderByDescending(a => a.CreatedAt)
                .Take(5)
                .ToListAsync();

            // Active KPI Targets
            vm.KPITargets = await _context.KPITargets
                .Where(k => k.Month == today.Month && k.Year == today.Year)
                .ToListAsync();

            // Risk Summary
            var revenueHistory = await _context.Revenues
                .Where(r => !r.IsDeleted && r.TransactionDate >= thirtyDaysAgo)
                .OrderBy(r => r.TransactionDate)
                .Select(r => new { r.TransactionDate, r.Amount })
                .ToListAsync();

            var expenseHistory = await _context.Expenses
                .Where(e => !e.IsDeleted && e.ExpenseDate >= thirtyDaysAgo)
                .OrderBy(e => e.ExpenseDate)
                .Select(e => new { e.ExpenseDate, e.Amount })
                .ToListAsync();

            if (revenueHistory.Count > 0 && expenseHistory.Count > 0)
            {
                var risks = await _riskService.AssessRisksAsync(
                    revenueHistory.Select(r => (r.TransactionDate, r.Amount)).ToList(),
                    expenseHistory.Select(e => (e.ExpenseDate, e.Amount)).ToList(),
                    vm.TotalRevenue);

                vm.TopRisks = risks.OrderByDescending(r => r.RiskScore).Take(3).ToList();
            }

            // Recent transactions
            vm.RecentRevenues = await _context.Revenues
                .Where(r => !r.IsDeleted)
                .OrderByDescending(r => r.TransactionDate)
                .Take(5)
                .ToListAsync();

            vm.RecentExpenses = await _context.Expenses
                .Where(e => !e.IsDeleted)
                .OrderByDescending(e => e.ExpenseDate)
                .Take(5)
                .ToListAsync();

            return View(vm);
        }

        // ════════════════════════════════════════════
        // KPI DASHBOARD
        // ════════════════════════════════════════════
        public async Task<IActionResult> KPIDashboard(int month = 0, int year = 0)
        {
            var today = DateTime.Today;
            if (month == 0) month = today.Month;
            if (year == 0) year  = today.Year;

            var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Local);
            var monthEnd   = monthStart.AddMonths(1).AddDays(-1);

            var vm = new KPIDashboardViewModel
            {
                Month       = month,
                Year        = year,
                GeneratedAt = DateTime.Now
            };

            // Strategic KPIs
            var kpis              = await _kpiService.CalculateStrategicKPIsAsync(monthStart, monthEnd);
            var previousMonthStart = monthStart.AddMonths(-1);
            var previousMonthEnd  = monthStart.AddDays(-1);
            var previousKPIs      = await _kpiService.CalculateStrategicKPIsAsync(previousMonthStart, previousMonthEnd);

            // Build KPI cards with comparison
            foreach (var key in kpis.Keys)
            {
                var currentValue  = kpis[key];
                var previousValue = previousKPIs.ContainsKey(key) ? previousKPIs[key] : 0m;

                // Keep arithmetic in decimal, cast to double only for the DTO field
                var changeDecimal = previousValue != 0m
                    ? ((currentValue - previousValue) / previousValue) * 100m
                    : 0m;

                vm.KPIMetrics.Add(new KPIMetricDetail
                {
                    Name          = FormatKPIName(key),
                    CurrentValue  = currentValue,
                    PreviousValue = previousValue,
                    ChangePercent = (double)changeDecimal,   // explicit cast — fixes CS0266
                    Unit          = GetKPIUnit(key),
                    Status        = DetermineKPIStatus(key, currentValue)
                });
            }

            // KPI Targets
            vm.KPITargets = await _context.KPITargets
                .Where(k => k.Month == month && k.Year == year)
                .ToListAsync();

            return View(vm);
        }

        // ════════════════════════════════════════════
        // SALES FORECASTING
        // ════════════════════════════════════════════
        public async Task<IActionResult> Forecasting(int forecastDays = 30)
        {
            var today          = DateTime.Today;
            var historicalDays = 90;
            var startDate      = today.AddDays(-historicalDays);

            var vm = new ForecastingViewModel
            {
                ForecastDays    = forecastDays,
                HistoricalDays  = historicalDays,
                GeneratedAt     = DateTime.Now
            };

            // Get historical revenue data
            var revenueHistory = await _context.Revenues
                .Where(r => !r.IsDeleted && r.TransactionDate >= startDate)
                .OrderBy(r => r.TransactionDate)
                .GroupBy(r => r.TransactionDate.Date)
                .Select(g => new { Date = g.Key, Amount = g.Sum(r => r.Amount) })
                .ToListAsync();

            var historicalData = revenueHistory
                .Select(r => (r.Date, r.Amount))
                .ToList();

            if (historicalData.Count >= 7)
            {
                // Moving Average Forecast
                vm.MovingAverageForecast = await _forecastingService
                    .MovingAverageForecastAsync(historicalData, 7, forecastDays);

                // Exponential Smoothing Forecast
                vm.ExponentialSmoothingForecast = await _forecastingService
                    .ExponentialSmoothingForecastAsync(historicalData, 0.3, forecastDays);

                // Use exponential smoothing as primary forecast
                vm.PrimaryForecast = vm.ExponentialSmoothingForecast;
            }

            vm.HistoricalData = historicalData
                .Select(h => new ChartDataPoint
                {
                    Label = h.Date.ToString("MMM dd"),
                    Value = h.Amount
                })
                .ToList();

            // ── Product demand forecast (last 15 days → project next 7)
            var fifteenDaysAgo = today.AddDays(-15);
            var eightDaysAgo   = today.AddDays(-8);

            var recentItems = await _context.SaleItems
                .Include(si => si.Product)
                .Where(si => si.Sale.SaleDate >= fifteenDaysAgo && si.Sale.Status == "Completed")
                .Select(si => new
                {
                    si.ProductId,
                    ProductName = si.Product.Name,
                    Emoji       = si.Product.Emoji ?? "📦",
                    Category    = si.Product.Category,
                    UnitPrice   = si.Product.Price,
                    si.Quantity,
                    SaleDate    = si.Sale.SaleDate
                })
                .ToListAsync();

            var productGroups = recentItems
                .GroupBy(x => new { x.ProductId, x.ProductName, x.Emoji, x.Category, x.UnitPrice })
                .Select(g =>
                {
                    // Split into first 8 days and last 7 days to detect trend
                    var first8  = g.Where(x => x.SaleDate < today.AddDays(-7)).Sum(x => x.Quantity);
                    var last7   = g.Where(x => x.SaleDate >= today.AddDays(-7)).Sum(x => x.Quantity);
                    var total15 = g.Sum(x => x.Quantity);
                    var dailyAvg = total15 / 15.0;
                    var projected7 = (int)Math.Round(dailyAvg * 7);

                    double growthRate = first8 > 0
                        ? ((double)(last7 - first8) / first8) * 100.0
                        : 0;

                    string trend = growthRate > 10 ? "up" : growthRate < -10 ? "down" : "stable";

                    return new ProductDemandForecast
                    {
                        ProductName           = g.Key.ProductName,
                        Emoji                 = g.Key.Emoji,
                        Category              = g.Key.Category,
                        UnitPrice             = g.Key.UnitPrice,
                        TotalQty15Days        = total15,
                        DailyAvgQty           = Math.Round(dailyAvg, 1),
                        ProjectedQty7Days     = projected7,
                        ProjectedRevenue7Days = projected7 * g.Key.UnitPrice,
                        GrowthRate            = Math.Round(growthRate, 1),
                        Trend                 = trend
                    };
                })
                .OrderByDescending(p => p.ProjectedRevenue7Days)
                .Take(10)
                .ToList();

            vm.ProductDemandForecasts = productGroups;

            return View(vm);
        }

        // ════════════════════════════════════════════
        // SCENARIO SIMULATION
        // ════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> ScenarioSimulation()
        {
            var today      = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Local);

            var vm = new ScenarioSimulationViewModel();

            vm.BaselineMonthlyRevenue = await _context.Revenues
                .Where(r => !r.IsDeleted && r.TransactionDate >= monthStart)
                .SumAsync(r => r.Amount);

            vm.BaselineMonthlyExpenses = await _context.Expenses
                .Where(e => !e.IsDeleted && e.ExpenseDate >= monthStart)
                .SumAsync(e => e.Amount);

            vm.SavedScenarios = await _context.SavedScenarios.ToListAsync();

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SimulateScenario(
            decimal baselineRevenue,
            double revenueGrowth,
            double expenseGrowth,
            double cogsPercent,
            int months)
        {
            var today      = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Local);

            var baselineExpenses = await _context.Expenses
                .Where(e => !e.IsDeleted && e.ExpenseDate >= monthStart)
                .SumAsync(e => e.Amount);

            var result = await _scenarioService.ProjectScenarioAsync(
                baselineRevenue, baselineExpenses,
                revenueGrowth, expenseGrowth, cogsPercent, months);

            var sensitivity = await _scenarioService.PerformSensitivityAnalysisAsync(
                baselineRevenue, baselineExpenses,
                revenueGrowth, expenseGrowth, cogsPercent, months);

            var vm = new ScenarioSimulationViewModel
            {
                SimulationResult        = result,
                SensitivityAnalysis     = sensitivity,
                BaselineMonthlyRevenue  = baselineRevenue,
                BaselineMonthlyExpenses = baselineExpenses,
                SavedScenarios          = await _context.SavedScenarios.ToListAsync()
            };

            return View("ScenarioSimulation", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveScenario(
            string scenarioName,
            string description,
            decimal baselineRevenue,
            double revenueGrowth,
            double expenseGrowth,
            double cogsPercent,
            int months)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(scenarioName))
                {
                    TempData["Error"] = "Scenario name is required.";
                    return RedirectToAction(nameof(ScenarioSimulation));
                }

                var user       = await _userManager.GetUserAsync(User);
                var today      = DateTime.Today;
                var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Local);

                // Use current month expenses as baseline if not provided
                if (baselineRevenue <= 0)
                    baselineRevenue = await _context.Revenues
                        .Where(r => !r.IsDeleted && r.TransactionDate >= monthStart)
                        .SumAsync(r => r.Amount);

                var baselineExpenses = await _context.Expenses
                    .Where(e => !e.IsDeleted && e.ExpenseDate >= monthStart)
                    .SumAsync(e => e.Amount);

                // Ensure months is valid
                if (months <= 0) months = 12;
                if (cogsPercent <= 0) cogsPercent = 30;

                var result = await _scenarioService.ProjectScenarioAsync(
                    baselineRevenue, baselineExpenses,
                    revenueGrowth, expenseGrowth, cogsPercent, months);

                var scenario = new SavedScenario
                {
                    ScenarioName           = scenarioName,
                    Description            = description ?? "",
                    RevenueGrowthPercent   = revenueGrowth,
                    ExpenseIncreasePercent = expenseGrowth,
                    CostOfGoodsSoldPercent = cogsPercent,
                    SimulationMonths       = months,
                    CreatedAt              = DateTime.UtcNow,
                    CreatedBy              = user?.Id ?? "System",
                    ResultsJson            = JsonSerializer.Serialize(result)
                };

                _context.SavedScenarios.Add(scenario);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Scenario '{scenarioName}' saved successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Failed to save scenario: {ex.Message}";
            }

            return RedirectToAction(nameof(ScenarioSimulation));
        }

        // ════════════════════════════════════════════
        // SCENARIO COMPARE (side-by-side)
        // ════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> CompareScenarios(int? id1 = null, int? id2 = null)
        {
            var all = await _context.SavedScenarios.OrderByDescending(s => s.CreatedAt).ToListAsync();
            ViewBag.AllScenarios = all;

            if (id1.HasValue && id2.HasValue)
            {
                var s1 = all.FirstOrDefault(s => s.Id == id1);
                var s2 = all.FirstOrDefault(s => s.Id == id2);
                if (s1 != null && s2 != null)
                {
                    var r1 = JsonSerializer.Deserialize<ScenarioResult>(s1.ResultsJson ?? "{}");
                    var r2 = JsonSerializer.Deserialize<ScenarioResult>(s2.ResultsJson ?? "{}");
                    ViewBag.Scenario1 = s1; ViewBag.Result1 = r1;
                    ViewBag.Scenario2 = s2; ViewBag.Result2 = r2;
                }
            }

            ViewBag.Id1 = id1; ViewBag.Id2 = id2;
            return View();
        }

        // ════════════════════════════════════════════
        // SCENARIO EXPORT (PDF)
        // ════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> ExportScenario(int id)
        {
            var scenario = await _context.SavedScenarios.FindAsync(id);
            if (scenario == null) return NotFound();

            var result = JsonSerializer.Deserialize<ScenarioResult>(scenario.ResultsJson ?? "{}");

            using var ms     = new System.IO.MemoryStream();
            var writer       = new iText.Kernel.Pdf.PdfWriter(ms);
            var pdf          = new iText.Kernel.Pdf.PdfDocument(writer);
            var doc          = new iText.Layout.Document(pdf);
            var blue         = new iText.Kernel.Colors.DeviceRgb(59, 130, 246);
            var dark         = new iText.Kernel.Colors.DeviceRgb(30, 41, 59);
            var white        = new iText.Kernel.Colors.DeviceRgb(241, 245, 249);
            var green        = new iText.Kernel.Colors.DeviceRgb(16, 185, 129);
            var muted        = new iText.Kernel.Colors.DeviceRgb(100, 116, 139);

            doc.Add(new iText.Layout.Element.Paragraph("CRL Fruitstand ESS — Scenario Report")
                .SetFontSize(18).SetFontColor(blue));
            doc.Add(new iText.Layout.Element.Paragraph(scenario.ScenarioName)
                .SetFontSize(13).SetFontColor(white));
            doc.Add(new iText.Layout.Element.Paragraph($"Generated: {DateTime.Now:MMMM dd, yyyy h:mm tt}")
                .SetFontSize(9).SetFontColor(muted).SetMarginBottom(12));

            // Parameters
            doc.Add(new iText.Layout.Element.Paragraph("Simulation Parameters").SetFontSize(11).SetFontColor(white));
            var paramTable = new iText.Layout.Element.Table(2).UseAllAvailableWidth();
            void AddRow(string label, string val) {
                paramTable.AddCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph(label).SetFontSize(9).SetFontColor(muted)).SetBackgroundColor(dark).SetPadding(5));
                paramTable.AddCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph(val).SetFontSize(9).SetFontColor(white)).SetBackgroundColor(dark).SetPadding(5));
            }
            AddRow("Revenue Growth", $"{scenario.RevenueGrowthPercent}%");
            AddRow("Expense Growth", $"{scenario.ExpenseIncreasePercent}%");
            AddRow("COGS %",         $"{scenario.CostOfGoodsSoldPercent}%");
            AddRow("Period",         $"{scenario.SimulationMonths} months");
            AddRow("Description",    scenario.Description ?? "—");
            doc.Add(paramTable);
            doc.Add(new iText.Layout.Element.Paragraph(" "));

            if (result?.Projections != null)
            {
                doc.Add(new iText.Layout.Element.Paragraph("Monthly Projections").SetFontSize(11).SetFontColor(white));
                var t = new iText.Layout.Element.Table(5).UseAllAvailableWidth();
                foreach (var h in new[]{"Month","Revenue","Expenses","Profit","Margin%"})
                    t.AddHeaderCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph(h).SetFontSize(8).SetFontColor(white)).SetBackgroundColor(dark).SetPadding(5));
                foreach (var p in result.Projections)
                {
                    t.AddCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph($"M{p.Month}").SetFontSize(8).SetFontColor(muted)).SetPadding(4));
                    t.AddCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph($"₱{p.ProjectedRevenue:N0}").SetFontSize(8).SetFontColor(white)).SetPadding(4));
                    t.AddCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph($"₱{p.ProjectedOpEx:N0}").SetFontSize(8).SetFontColor(white)).SetPadding(4));
                    t.AddCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph($"₱{p.ProjectedProfit:N0}").SetFontSize(8).SetFontColor(p.ProjectedProfit >= 0 ? green : new iText.Kernel.Colors.DeviceRgb(239,68,68))).SetPadding(4));
                    t.AddCell(new iText.Layout.Element.Cell().Add(new iText.Layout.Element.Paragraph($"{p.ProfitMargin:F1}%").SetFontSize(8).SetFontColor(muted)).SetPadding(4));
                }
                doc.Add(t);
            }

            doc.Close();
            return File(ms.ToArray(), "application/pdf", $"Scenario_{scenario.ScenarioName.Replace(" ","_")}_{DateTime.Now:yyyyMMdd}.pdf");
        }

        // ════════════════════════════════════════════
        // RISK ANALYSIS
        // ════════════════════════════════════════════
        public async Task<IActionResult> RiskAnalysis()
        {
            var today         = DateTime.Today;
            var thirtyDaysAgo = today.AddDays(-30);

            var vm = new RiskAnalysisViewModel
            {
                GeneratedAt = DateTime.Now
            };

            var revenueHistory = await _context.Revenues
                .Where(r => !r.IsDeleted && r.TransactionDate >= thirtyDaysAgo)
                .OrderBy(r => r.TransactionDate)
                .Select(r => new { r.TransactionDate, r.Amount })
                .ToListAsync();

            var expenseHistory = await _context.Expenses
                .Where(e => !e.IsDeleted && e.ExpenseDate >= thirtyDaysAgo)
                .OrderBy(e => e.ExpenseDate)
                .Select(e => new { e.ExpenseDate, e.Amount })
                .ToListAsync();

            var totalRevenue = revenueHistory.Sum(r => r.Amount);

            if (revenueHistory.Count > 0 && expenseHistory.Count > 0)
            {
                var risks = await _riskService.AssessRisksAsync(
                    revenueHistory.Select(r => (r.TransactionDate, r.Amount)).ToList(),
                    expenseHistory.Select(e => (e.ExpenseDate, e.Amount)).ToList(),
                    totalRevenue);

                vm.RiskAssessments  = risks;
                vm.OverallRiskScore = risks.Count > 0 ? risks.Average(r => r.RiskScore) : 0;
                vm.HighestRisk      = risks.OrderByDescending(r => r.RiskScore).FirstOrDefault();
            }

            // Risk Register
            vm.RiskRegister = await _context.RiskRegisters
                .Where(r => r.Status != "closed")
                .OrderByDescending(r => r.Priority)
                .ToListAsync();

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRiskAlert(
            string title, string message, string severity, string category)
        {
            var alert = new ExecutiveAlert
            {
                Title     = title,
                Message   = message,
                Severity  = severity,
                Category  = category,
                CreatedAt = DateTime.UtcNow
            };

            _context.ExecutiveAlerts.Add(alert);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Alert created successfully!";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResolveAlert(int id)
        {
            var alert = await _context.ExecutiveAlerts.FindAsync(id);
            if (alert != null)
            {
                alert.IsResolved  = true;
                alert.ResolvedAt  = DateTime.UtcNow;
                var user          = await _userManager.GetUserAsync(User);
                alert.AcknowledgedBy = user?.FullName ?? "Unknown";

                await _context.SaveChangesAsync();
                TempData["Success"] = "Alert resolved!";
            }

            return RedirectToAction(nameof(Index));
        }

        // ════════════════════════════════════════════
        // ANOMALY DETECTION
        // ════════════════════════════════════════════
        public async Task<IActionResult> AnomalyDetection()
        {
            var today = DateTime.Today;
            var thirtyDaysAgo = today.AddDays(-30);

            var vm = new Dictionary<string, object>
            {
                { "GeneratedAt", DateTime.Now }
            };

            // Detect revenue anomalies
            var anomalies = await _kpiService.DetectAnomaliesAsync(thirtyDaysAgo);
            vm["Anomalies"] = anomalies;

            // High-impact anomalies
            var highImpact = anomalies
                .Where(a => Math.Abs(a.value) > 100000m)
                .OrderByDescending(a => Math.Abs(a.value))
                .ToList();
            vm["HighImpactAnomalies"] = highImpact;

            ViewBag.AnomalyCount = anomalies.Count;
            ViewBag.CriticalCount = highImpact.Count;

            return View(vm);
        }

        // ════════════════════════════════════════════
        // PERFORMANCE SUMMARY REPORT
        // ════════════════════════════════════════════
        public async Task<IActionResult> PerformanceReport(int months = 3)
        {
            var today = DateTime.Today;
            var startDate = today.AddMonths(-months);

            // Get historical KPIs
            var kpiHistory = new List<object>();
            for (int i = months; i >= 0; i--)
            {
                var periodStart = today.AddMonths(-i);
                var periodEnd = periodStart.AddMonths(1).AddDays(-1);

                var kpis = await _kpiService.CalculateStrategicKPIsAsync(periodStart, periodEnd);
                kpiHistory.Add(new
                {
                    period = periodStart.ToString("MMM yyyy"),
                    revenue = kpis.ContainsKey("TotalRevenue") ? kpis["TotalRevenue"] : 0m,
                    expenses = kpis.ContainsKey("TotalExpenses") ? kpis["TotalExpenses"] : 0m,
                    profit = kpis.ContainsKey("NetProfit") ? kpis["NetProfit"] : 0m,
                    margin = kpis.ContainsKey("GrossMarginPercent") ? kpis["GrossMarginPercent"] : 0m
                });
            }

            ViewBag.KPIHistory = kpiHistory;
            ViewBag.ReportPeriod = $"Last {months} Months";

            return View();
        }

        // ════════════════════════════════════════════
        // KPI ALERTS & NOTIFICATIONS
        // ════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetKPIAlert(
            string kpiName, decimal threshold, string operator_type, string frequency)
        {
            var user = await _userManager.GetUserAsync(User);

            // In a production system, this would be stored in a KPIAlert table
            var alert = new ExecutiveAlert
            {
                Title = $"KPI Alert: {kpiName}",
                Message = $"Alert set when {kpiName} is {operator_type} {threshold:C}",
                Severity = "info",
                Category = "kpi-monitoring",
                CreatedAt = DateTime.UtcNow
            };

            _context.ExecutiveAlerts.Add(alert);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"KPI alert for {kpiName} has been set successfully!";
            return RedirectToAction(nameof(Index));
        }

        // ════════════════════════════════════════════
        // DECISION SUPPORT ANALYSIS
        // ════════════════════════════════════════════
        public async Task<IActionResult> DecisionSupport()
        {
            var today = DateTime.Today;
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var thirtyDaysAgo = today.AddDays(-30);

            var vm = new Dictionary<string, object>();

            // Current KPIs
            var currentKPIs = await _kpiService.CalculateStrategicKPIsAsync(monthStart, today);
            vm["CurrentKPIs"] = currentKPIs;

            // Health score and insights
            var (healthScore, status, insights) = await _kpiService.CalculateHealthScoreAsync(monthStart);
            vm["HealthScore"] = healthScore;
            vm["HealthStatus"] = status;
            vm["HealthInsights"] = insights;

            // Risk assessment
            var revenueHistory = await _context.Revenues
                .Where(r => !r.IsDeleted && r.TransactionDate >= thirtyDaysAgo)
                .OrderBy(r => r.TransactionDate)
                .Select(r => new { r.TransactionDate, r.Amount })
                .ToListAsync();

            var expenseHistory = await _context.Expenses
                .Where(e => !e.IsDeleted && e.ExpenseDate >= thirtyDaysAgo)
                .OrderBy(e => e.ExpenseDate)
                .Select(e => new { e.ExpenseDate, e.Amount })
                .ToListAsync();

            if (revenueHistory.Count > 0 && expenseHistory.Count > 0)
            {
                var risks = await _riskService.AssessRisksAsync(
                    revenueHistory.Select(r => (r.TransactionDate, r.Amount)).ToList(),
                    expenseHistory.Select(e => (e.ExpenseDate, e.Amount)).ToList(),
                    currentKPIs.ContainsKey("TotalRevenue") ? currentKPIs["TotalRevenue"] : 0m);

                vm["Risks"] = risks;
                vm["OverallRiskScore"] = risks.Average(r => r.RiskScore);
            }

            // Forecast
            var historicalRevenueData = await _context.Revenues
                .Where(r => !r.IsDeleted && r.TransactionDate >= thirtyDaysAgo)
                .OrderBy(r => r.TransactionDate)
                .GroupBy(r => r.TransactionDate.Date)
                .Select(g => new { Date = g.Key, Amount = g.Sum(r => r.Amount) })
                .ToListAsync();

            var forecastData = historicalRevenueData
                .Select(r => (r.Date, r.Amount))
                .ToList();

            if (forecastData.Count >= 7)
            {
                var forecast = await _forecastingService
                    .ExponentialSmoothingForecastAsync(forecastData, 0.3, 30);
                vm["Forecast"] = forecast;
            }

            // Recommendations based on analysis
            var recommendations = GenerateRecommendations(healthScore, (double)vm.GetValueOrDefault("OverallRiskScore", 0.0));
            vm["Recommendations"] = recommendations;

            return View(vm);
        }

        private List<string> GenerateRecommendations(double healthScore, double riskScore)
        {
            var recommendations = new List<string>();

            if (healthScore < 50)
            {
                recommendations.Add("🚨 Critical: Business health is low. Review operational efficiency immediately.");
            }
            else if (healthScore < 70)
            {
                recommendations.Add("⚠️ Warning: Business health needs improvement. Focus on revenue growth.");
            }
            else
            {
                recommendations.Add("✓ Business health is good. Continue current strategies.");
            }

            if (riskScore > 0.7)
            {
                recommendations.Add("🛡️ High risk detected. Implement risk mitigation strategies immediately.");
            }
            else if (riskScore > 0.5)
            {
                recommendations.Add("⚠️ Moderate risk levels. Monitor key metrics closely.");
            }

            recommendations.Add("📊 Review forecasts monthly and adjust strategies based on trends.");
            recommendations.Add("💡 Use scenario analysis to test strategic initiatives before implementation.");

            return recommendations;
        }

        // ════════════════════════════════════════════
        // HELPER METHODS
        // ════════════════════════════════════════════
        private static string FormatKPIName(string kpiKey) => kpiKey switch
        {
            "TotalRevenue"          => "Total Revenue",
            "TotalExpenses"         => "Total Expenses",
            "NetProfit"             => "Net Profit",
            "GrossProfit"           => "Gross Profit",
            "GrossMarginPercent"    => "Gross Margin %",
            "OperationalEfficiency" => "Operational Efficiency",
            "ROI"                   => "Return on Investment",
            "CashFlowRatio"         => "Cash Flow Ratio",
            _                       => kpiKey
        };

        private static string GetKPIUnit(string kpiKey) => kpiKey switch
        {
            "GrossMarginPercent"    => "%",
            "OperationalEfficiency" => "%",
            "ROI"                   => "%",
            "CashFlowRatio"         => "x",
            _                       => "₱"
        };

        private static string DetermineKPIStatus(string kpiKey, decimal value) => kpiKey switch
        {
            "NetProfit"             => value > 0m  ? "good" : "danger",
            "GrossMarginPercent"    => value >= 30m ? "good" : value >= 20m ? "warning" : "danger",
            "OperationalEfficiency" => value >= 70m ? "good" : value >= 50m ? "warning" : "danger",
            "ROI"                   => value >= 20m ? "good" : value >= 10m ? "warning" : "danger",
            _                       => "neutral"
        };
    }
}