using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRLFruitstandESS.Data;
using CRLFruitstandESS.Models;
using CRLFruitstandESS.Services;
using System.Text.Json;

namespace CRLFruitstandESS.Controllers.Api
{
    /// <summary>
    /// API Controller for real-time Executive Dashboard data
    /// Provides real-time KPI monitoring, alerts, and decision support
    /// </summary>
    [ApiController]
    [Route("api/executive")]
    [Authorize(Roles = "CFO,CEO,Admin")]
    public class ExecutiveDashboardApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IKPICalculationService _kpiService;
        private readonly IRiskAnalysisService _riskService;
        private readonly IForecastingService _forecastingService;
        private readonly IScenarioService _scenarioService;

        public ExecutiveDashboardApiController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IKPICalculationService kpiService,
            IRiskAnalysisService riskService,
            IForecastingService forecastingService,
            IScenarioService scenarioService)
        {
            _context = context;
            _userManager = userManager;
            _kpiService = kpiService;
            _riskService = riskService;
            _forecastingService = forecastingService;
            _scenarioService = scenarioService;
        }

        // ════════════════════════════════════════════════════════
        // REAL-TIME KPI ENDPOINTS
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// Get current day KPI metrics in real-time
        /// </summary>
        [HttpGet("kpi/today")]
        public async Task<IActionResult> GetTodayKPIs()
        {
            try
            {
                var today = DateTime.Today;
                var kpis = await _kpiService.CalculateStrategicKPIsAsync(today, today);

                return Ok(new
                {
                    success = true,
                    timestamp = DateTime.UtcNow,
                    data = kpis,
                    metrics = new
                    {
                        revenue = kpis.ContainsKey("TotalRevenue") ? kpis["TotalRevenue"] : 0m,
                        expenses = kpis.ContainsKey("TotalExpenses") ? kpis["TotalExpenses"] : 0m,
                        profit = kpis.ContainsKey("NetProfit") ? kpis["NetProfit"] : 0m,
                        margin = kpis.ContainsKey("GrossMarginPercent") ? kpis["GrossMarginPercent"] : 0m
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Get KPI comparison between current month and previous month
        /// </summary>
        [HttpGet("kpi/comparison")]
        public async Task<IActionResult> GetKPIComparison()
        {
            try
            {
                var today = DateTime.Today;
                var currentMonthStart = new DateTime(today.Year, today.Month, 1);
                var previousMonthStart = currentMonthStart.AddMonths(-1);
                var previousMonthEnd = currentMonthStart.AddDays(-1);

                var comparison = await _kpiService.ComparePeriodsAsync(
                    currentMonthStart, today,
                    previousMonthStart, previousMonthEnd);

                var result = comparison.Select(kvp => new
                {
                    metric = kvp.Key,
                    current = kvp.Value.current,
                    previous = kvp.Value.previous,
                    changePercent = kvp.Value.changePercent,
                    trend = kvp.Value.changePercent > 0 ? "up" : "down"
                }).ToList();

                return Ok(new
                {
                    success = true,
                    timestamp = DateTime.UtcNow,
                    comparison = result
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Get current business health score
        /// </summary>
        [HttpGet("health-score")]
        public async Task<IActionResult> GetHealthScore()
        {
            try
            {
                var today = DateTime.Today;
                var monthStart = new DateTime(today.Year, today.Month, 1);

                var (score, status, insights) = await _kpiService.CalculateHealthScoreAsync(monthStart);

                return Ok(new
                {
                    success = true,
                    timestamp = DateTime.UtcNow,
                    healthScore = new
                    {
                        score = score,
                        status = status,
                        percentage = score / 100.0,
                        insights = insights
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // ════════════════════════════════════════════════════════
        // REAL-TIME RISK MONITORING
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// Get current risk assessment
        /// </summary>
        [HttpGet("risks/current")]
        public async Task<IActionResult> GetCurrentRisks()
        {
            try
            {
                var today = DateTime.Today;
                var thirtyDaysAgo = today.AddDays(-30);

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

                var risks = new List<object>();
                if (revenueHistory.Count > 0 && expenseHistory.Count > 0)
                {
                    var riskAssessments = await _riskService.AssessRisksAsync(
                        revenueHistory.Select(r => (r.TransactionDate, r.Amount)).ToList(),
                        expenseHistory.Select(e => (e.ExpenseDate, e.Amount)).ToList(),
                        totalRevenue);

                    risks = riskAssessments
                        .OrderByDescending(r => r.RiskScore)
                        .Select(r => new
                        {
                            category = r.RiskCategory,
                            score = r.RiskScore,
                            probability = r.Probability,
                            impact = r.Impact,
                            status = r.Status,
                            insights = r.Insights.Take(3).ToList()
                        })
                        .Cast<object>()
                        .ToList();
                }

                return Ok(new
                {
                    success = true,
                    timestamp = DateTime.UtcNow,
                    overallScore = risks.Any() ? risks.Average(r => (double)((dynamic)r).score) : 0,
                    risks = risks
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Get active alerts
        /// </summary>
        [HttpGet("alerts")]
        public async Task<IActionResult> GetAlerts(bool unresolved = true)
        {
            try
            {
                var query = _context.ExecutiveAlerts.AsQueryable();

                if (unresolved)
                    query = query.Where(a => !a.IsResolved);

                var alerts = await query
                    .OrderByDescending(a => a.CreatedAt)
                    .Select(a => new
                    {
                        id = a.Id,
                        title = a.Title,
                        message = a.Message,
                        severity = a.Severity,
                        category = a.Category,
                        createdAt = a.CreatedAt,
                        isResolved = a.IsResolved
                    })
                    .ToListAsync();

                return Ok(new
                {
                    success = true,
                    timestamp = DateTime.UtcNow,
                    count = alerts.Count,
                    alerts = alerts
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // ════════════════════════════════════════════════════════
        // FORECASTING ENDPOINTS
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// Get 30-day revenue forecast
        /// </summary>
        [HttpGet("forecast/revenue")]
        public async Task<IActionResult> GetRevenueForecast(int forecastDays = 30)
        {
            try
            {
                var today = DateTime.Today;
                var startDate = today.AddDays(-90);

                var revenueHistory = await _context.Revenues
                    .Where(r => !r.IsDeleted && r.TransactionDate >= startDate)
                    .OrderBy(r => r.TransactionDate)
                    .GroupBy(r => r.TransactionDate.Date)
                    .Select(g => new { Date = g.Key, Amount = g.Sum(r => r.Amount) })
                    .ToListAsync();

                var historicalData = revenueHistory
                    .Select(r => (r.Date, r.Amount))
                    .ToList();

                if (historicalData.Count < 7)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Insufficient historical data for forecasting"
                    });
                }

                var forecast = await _forecastingService
                    .ExponentialSmoothingForecastAsync(historicalData, 0.3, forecastDays);

                return Ok(new
                {
                    success = true,
                    timestamp = DateTime.UtcNow,
                    method = forecast.Method,
                    accuracy = forecast.Accuracy,
                    mape = forecast.MAPE,
                    insights = forecast.Insights,
                    forecastPoints = forecast.Forecast.Select(f => new
                    {
                        date = f.Date.ToString("yyyy-MM-dd"),
                        value = f.ForecastedValue,
                        lower = f.LowerBound,
                        upper = f.UpperBound,
                        confidence = f.Confidence
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Get sales forecast with multiple methods
        /// </summary>
        [HttpGet("forecast/compare")]
        public async Task<IActionResult> CompareForecastMethods(int forecastDays = 30)
        {
            try
            {
                var today = DateTime.Today;
                var startDate = today.AddDays(-90);

                var revenueHistory = await _context.Revenues
                    .Where(r => !r.IsDeleted && r.TransactionDate >= startDate)
                    .OrderBy(r => r.TransactionDate)
                    .GroupBy(r => r.TransactionDate.Date)
                    .Select(g => new { Date = g.Key, Amount = g.Sum(r => r.Amount) })
                    .ToListAsync();

                var historicalData = revenueHistory
                    .Select(r => (r.Date, r.Amount))
                    .ToList();

                if (historicalData.Count < 7)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Insufficient historical data"
                    });
                }

                var maForecast = await _forecastingService.MovingAverageForecastAsync(historicalData, 7, forecastDays);
                var expForecast = await _forecastingService.ExponentialSmoothingForecastAsync(historicalData, 0.3, forecastDays);
                var linForecast = await _forecastingService.LinearRegressionForecastAsync(historicalData, forecastDays);

                return Ok(new
                {
                    success = true,
                    timestamp = DateTime.UtcNow,
                    forecasts = new
                    {
                        movingAverage = new
                        {
                            method = maForecast.Method,
                            accuracy = maForecast.Accuracy,
                            mape = maForecast.MAPE,
                            insights = maForecast.Insights,
                            avgDaily = maForecast.Forecast.Any() ? maForecast.Forecast.Average(f => f.ForecastedValue) : 0m
                        },
                        exponentialSmoothing = new
                        {
                            method = expForecast.Method,
                            accuracy = expForecast.Accuracy,
                            mape = expForecast.MAPE,
                            insights = expForecast.Insights,
                            avgDaily = expForecast.Forecast.Any() ? expForecast.Forecast.Average(f => f.ForecastedValue) : 0m
                        },
                        linearRegression = new
                        {
                            method = linForecast.Method,
                            accuracy = linForecast.Accuracy,
                            mape = linForecast.MAPE,
                            insights = linForecast.Insights,
                            avgDaily = linForecast.Forecast.Any() ? linForecast.Forecast.Average(f => f.ForecastedValue) : 0m
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // ════════════════════════════════════════════════════════
        // SCENARIO & WHAT-IF ANALYSIS
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// Run scenario projection
        /// </summary>
        [HttpPost("scenario/project")]
        public async Task<IActionResult> ProjectScenario([FromBody] ScenarioProjectionRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var scenario = await _scenarioService.ProjectScenarioAsync(
                    request.BaselineRevenue,
                    request.BaselineExpenses,
                    request.RevenueGrowthPercent,
                    request.ExpenseGrowthPercent,
                    request.CogsPercent,
                    request.SimulationMonths,
                    request.ScenarioName);

                var sensitivity = await _scenarioService.PerformSensitivityAnalysisAsync(
                    request.BaselineRevenue,
                    request.BaselineExpenses,
                    request.RevenueGrowthPercent,
                    request.ExpenseGrowthPercent,
                    request.CogsPercent,
                    request.SimulationMonths);

                return Ok(new
                {
                    success = true,
                    timestamp = DateTime.UtcNow,
                    scenario = new
                    {
                        name = scenario.ScenarioName,
                        totalRevenue = scenario.TotalProjectedRevenue,
                        totalProfit = scenario.TotalProjectedProfit,
                        averageMargin = scenario.AverageMargin,
                        roi = scenario.ROI,
                        projections = scenario.Projections.Select(p => new
                        {
                            month = p.Month,
                            revenue = p.ProjectedRevenue,
                            cogs = p.ProjectedCOGS,
                            opex = p.ProjectedOpEx,
                            profit = p.ProjectedProfit,
                            margin = p.ProfitMargin,
                            cumulativeProfit = p.CumulativeProfit
                        }).ToList()
                    },
                    sensitivity = sensitivity.Select(s => new
                    {
                        variable = s.Variable,
                        impact = s.ImpactOnProfit,
                        impactPercent = s.ImpactPercentage
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Compare multiple scenarios
        /// </summary>
        [HttpPost("scenario/compare")]
        public async Task<IActionResult> CompareScenarios([FromBody] ScenarioComparisonRequest request)
        {
            try
            {
                if (!ModelState.IsValid)
                    return BadRequest(ModelState);

                var results = await _scenarioService.CompareMultipleScenariosAsync(
                    request.Scenarios,
                    request.BaselineRevenue,
                    request.BaselineExpenses,
                    request.SimulationMonths);

                return Ok(new
                {
                    success = true,
                    timestamp = DateTime.UtcNow,
                    comparison = results.Select(kvp => new
                    {
                        name = kvp.Key,
                        totalRevenue = kvp.Value.TotalProjectedRevenue,
                        totalProfit = kvp.Value.TotalProjectedProfit,
                        averageMargin = kvp.Value.AverageMargin,
                        roi = kvp.Value.ROI
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // ════════════════════════════════════════════════════════
        // ANOMALY DETECTION
        // ════════════════════════════════════════════════════════

        /// <summary>
        /// Detect anomalies in financial data
        /// </summary>
        [HttpGet("anomalies")]
        public async Task<IActionResult> DetectAnomalies()
        {
            try
            {
                var today = DateTime.Today;
                var thirtyDaysAgo = today.AddDays(-30);

                var anomalies = await _kpiService.DetectAnomaliesAsync(thirtyDaysAgo);

                return Ok(new
                {
                    success = true,
                    timestamp = DateTime.UtcNow,
                    count = anomalies.Count,
                    anomalies = anomalies.Select(a => new
                    {
                        metric = a.metric,
                        value = a.value,
                        type = a.anomalyType
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // ════════════════════════════════════════════════════════
        // HELPER CLASSES
        // ════════════════════════════════════════════════════════

        public class ScenarioProjectionRequest
        {
            public decimal BaselineRevenue { get; set; }
            public decimal BaselineExpenses { get; set; }
            public double RevenueGrowthPercent { get; set; }
            public double ExpenseGrowthPercent { get; set; }
            public double CogsPercent { get; set; } = 30;
            public int SimulationMonths { get; set; } = 12;
            public string ScenarioName { get; set; } = "Scenario Analysis";
        }

        public class ScenarioComparisonRequest
        {
            public List<(string name, double revGrowth, double expGrowth, double cogs)> Scenarios { get; set; } = new();
            public decimal BaselineRevenue { get; set; }
            public decimal BaselineExpenses { get; set; }
            public int SimulationMonths { get; set; } = 12;
        }
    }
}
