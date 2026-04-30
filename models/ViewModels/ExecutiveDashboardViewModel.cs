using CRLFruitstandESS.Models.ViewModels;
using CRLFruitstandESS.Models.Executive;
using CRLFruitstandESS.Services;

namespace CRLFruitstandESS.Models.ViewModels
{
    // ════════════════════════════════════════════════════════
    // EXECUTIVE DASHBOARD VIEW MODEL
    // ════════════════════════════════════════════════════════
    public class ExecutiveDashboardViewModel
    {
        public string FullName { get; set; } = string.Empty;
        public DateTime? LastLoginAt { get; set; }
        public DateTime DashboardDate { get; set; } = DateTime.Today;

        // KPI Summary
        public decimal TotalRevenue { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetProfit { get; set; }
        public decimal GrossMarginPercent { get; set; }
        public decimal OperationalEfficiency { get; set; }
        public decimal ROI { get; set; }
        public double RevenueChangePercent { get; set; }

        // Health Score
        public double BusinessHealthScore { get; set; }
        public string HealthStatus { get; set; } = "healthy";
        public List<string> HealthInsights { get; set; } = new();

        // Alerts & Risks
        public List<ExecutiveAlert> ExecutiveAlerts { get; set; } = new();
        public List<KPITarget> KPITargets { get; set; } = new();
        public List<RiskAssessmentResult> TopRisks { get; set; } = new();

        // Recent Data
        public List<Revenue> RecentRevenues { get; set; } = new();
        public List<Expense> RecentExpenses { get; set; } = new();
    }

    // ════════════════════════════════════════════════════════
    // KPI DASHBOARD VIEW MODEL
    // ════════════════════════════════════════════════════════
    public class KPIDashboardViewModel
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public DateTime GeneratedAt { get; set; }

        public List<KPIMetricDetail> KPIMetrics { get; set; } = new();
        public List<KPITarget> KPITargets { get; set; } = new();
    }

    public class KPIMetricDetail
    {
        public string Name { get; set; } = string.Empty;
        public decimal CurrentValue { get; set; }
        public decimal PreviousValue { get; set; }
        public double ChangePercent { get; set; }
        public string Unit { get; set; } = string.Empty;
        public string Status { get; set; } = "neutral";

        public string StatusColor => Status switch
        {
            "good" => "success",
            "warning" => "warning",
            "danger" => "danger",
            _ => "secondary"
        };

        public string StatusIcon => Status switch
        {
            "good" => "bi-check-circle-fill",
            "warning" => "bi-exclamation-circle-fill",
            "danger" => "bi-x-circle-fill",
            _ => "bi-info-circle-fill"
        };
    }

    // ════════════════════════════════════════════════════════
    // FORECASTING VIEW MODEL
    // ════════════════════════════════════════════════════════
    public class ForecastingViewModel
    {
        public int ForecastDays { get; set; } = 30;
        public int HistoricalDays { get; set; } = 90;
        public DateTime GeneratedAt { get; set; }

        public ForecastResult? MovingAverageForecast { get; set; }
        public ForecastResult? ExponentialSmoothingForecast { get; set; }
        public ForecastResult? PrimaryForecast { get; set; }

        public List<ChartDataPoint> HistoricalData { get; set; } = new();

        // Product demand forecasts
        public List<ProductDemandForecast> ProductDemandForecasts { get; set; } = new();
    }

    public class ProductDemandForecast
    {
        public string  ProductName          { get; set; } = string.Empty;
        public string  Emoji                { get; set; } = "📦";
        public string  Category             { get; set; } = string.Empty;
        public int     TotalQty15Days       { get; set; }
        public double  DailyAvgQty          { get; set; }
        public int     ProjectedQty7Days    { get; set; }
        public decimal ProjectedRevenue7Days { get; set; }
        public decimal UnitPrice            { get; set; }
        public double  GrowthRate           { get; set; }
        public string  Trend                { get; set; } = "stable"; // up | down | stable
    }

    // ════════════════════════════════════════════════════════
    // SCENARIO SIMULATION VIEW MODEL
    // ════════════════════════════════════════════════════════
    public class ScenarioSimulationViewModel
    {
        public decimal BaselineMonthlyRevenue { get; set; }
        public decimal BaselineMonthlyExpenses { get; set; }

        public ScenarioResult? SimulationResult { get; set; }
        public List<SensitivityResult> SensitivityAnalysis { get; set; } = new();

        public List<SavedScenario> SavedScenarios { get; set; } = new();

        // Form fields for simulation
        public double RevenueGrowthPercent { get; set; }
        public double ExpenseGrowthPercent { get; set; }
        public double CogsPercent { get; set; } = 30;
        public int SimulationMonths { get; set; } = 12;
    }

    // ════════════════════════════════════════════════════════
    // RISK ANALYSIS VIEW MODEL
    // ════════════════════════════════════════════════════════
    public class RiskAnalysisViewModel
    {
        public DateTime GeneratedAt { get; set; }

        public List<RiskAssessmentResult> RiskAssessments { get; set; } = new();
        public double OverallRiskScore { get; set; }
        public RiskAssessmentResult? HighestRisk { get; set; }

        public List<RiskRegister> RiskRegister { get; set; } = new();

        public string OverallRiskStatus => OverallRiskScore switch
        {
            >= 0.7 => "Critical",
            >= 0.5 => "High",
            >= 0.3 => "Medium",
            _ => "Low"
        };

        public string OverallRiskColor => OverallRiskScore switch
        {
            >= 0.7 => "danger",
            >= 0.5 => "warning",
            >= 0.3 => "info",
            _ => "success"
        };
    }

}
