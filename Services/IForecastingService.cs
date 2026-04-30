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

    public interface IForecastingService
    {
        /// <summary>
        /// Generate forecast using simple moving average
        /// </summary>
        Task<ForecastResult> MovingAverageForecastAsync(List<(DateTime date, decimal value)> historicalData,
            int period = 7, int forecastDays = 30);

        /// <summary>
        /// Generate forecast using exponential smoothing
        /// </summary>
        Task<ForecastResult> ExponentialSmoothingForecastAsync(List<(DateTime date, decimal value)> historicalData,
            double alpha = 0.3, int forecastDays = 30);

        /// <summary>
        /// Generate forecast using linear regression
        /// </summary>
        Task<ForecastResult> LinearRegressionForecastAsync(List<(DateTime date, decimal value)> historicalData,
            int forecastDays = 30);

        /// <summary>
        /// Calculate confidence intervals for forecast
        /// </summary>
        Task<(decimal lower68, decimal upper68, decimal lower95, decimal upper95)> CalculateConfidenceIntervalsAsync(
            List<decimal> residuals, decimal forecastValue);

        /// <summary>
        /// Detect and measure seasonality in data
        /// </summary>
        Task<(double seasonalityIndex, bool isHighlySeasonable, List<double> weeklyFactors)> AnalyzeSeasonalityAsync(
            List<(DateTime date, decimal value)> historicalData);

        /// <summary>
        /// Generate insights based on forecast analysis
        /// </summary>
        Task<string> GenerateInsightsAsync(ForecastResult forecast, List<(DateTime date, decimal value)> historicalData);
    }
}
