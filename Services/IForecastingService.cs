namespace CRLFruitstandESS.Services
{
    public class ForecastDataPoint
    {
        public DateTime Date { get; set; }
        public decimal ForecastedValue { get; set; }
        public decimal LowerBound { get; set; }
        public decimal UpperBound { get; set; }
        public double Confidence { get; set; }
    }

    public class ForecastResult
    {
        public string Method { get; set; } = string.Empty;
        public List<ForecastDataPoint> Forecast { get; set; } = new();
        public double Accuracy { get; set; } // R² value
        public double MAPE { get; set; } // Mean Absolute Percentage Error
        public string Insights { get; set; } = string.Empty;
    }

    /// <summary>
    /// Per-product 30-day revenue forecast anchored to the 5-year CRL Fruitstand dataset.
    /// </summary>
    public class ProductRevenueForecast
    {
        public string  ProductName            { get; set; } = string.Empty;
        public string  Emoji                  { get; set; } = "📦";
        public double  RevenueWeight          { get; set; }   // share of total revenue (0–1)
        public decimal UnitPrice              { get; set; }
        public decimal ProjectedRevenue30Days { get; set; }
        public int     ProjectedQty30Days     { get; set; }
        public decimal AvgDailyRevenue        { get; set; }
        public int     TotalQty15Days         { get; set; }   // from actual DB sales (0 if no data)
        public double  DailyAvgQty            { get; set; }
        public double  GrowthRate             { get; set; }
        public string  Trend                  { get; set; } = "stable"; // up | down | stable
        public string  DataSource             { get; set; } = "Dataset Baseline";
        public double  SpoilageAllowancePct   { get; set; }  // 30.5% from dataset
    }

    public interface IForecastingService
    {
        /// <summary>Generate forecast using simple moving average.</summary>
        Task<ForecastResult> MovingAverageForecastAsync(List<(DateTime date, decimal value)> historicalData,
            int period = 7, int forecastDays = 30);

        /// <summary>Generate forecast using exponential smoothing.</summary>
        Task<ForecastResult> ExponentialSmoothingForecastAsync(List<(DateTime date, decimal value)> historicalData,
            double alpha = 0.3, int forecastDays = 30);

        /// <summary>Generate forecast using linear regression.</summary>
        Task<ForecastResult> LinearRegressionForecastAsync(List<(DateTime date, decimal value)> historicalData,
            int forecastDays = 30);

        /// <summary>
        /// Dataset-anchored per-product revenue forecast for the next N days.
        /// Uses 5-year revenue weights. Blends actual sales data when available.
        /// </summary>
        Task<List<ProductRevenueForecast>> ForecastProductRevenueAsync(
            DateTime startDate, int forecastDays,
            Dictionary<string, (int qty15d, double dailyAvgQty, double growthRate)>? actualSalesData = null);

        /// <summary>Calculate confidence intervals for forecast.</summary>
        Task<(decimal lower68, decimal upper68, decimal lower95, decimal upper95)> CalculateConfidenceIntervalsAsync(
            List<decimal> residuals, decimal forecastValue);

        /// <summary>Detect and measure seasonality in data.</summary>
        Task<(double seasonalityIndex, bool isHighlySeasonable, List<double> weeklyFactors)> AnalyzeSeasonalityAsync(
            List<(DateTime date, decimal value)> historicalData);

        /// <summary>Generate insights based on forecast analysis.</summary>
        Task<string> GenerateInsightsAsync(ForecastResult forecast, List<(DateTime date, decimal value)> historicalData);
    }
}
