namespace CRLFruitstandESS.Services
{
    public class RiskAssessmentResult
    {
        public string RiskCategory { get; set; } = string.Empty;
        public double Probability { get; set; }
        public double Impact { get; set; }
        public double RiskScore { get; set; }
        public double? Volatility { get; set; }
        public string Status { get; set; } = string.Empty; // green, yellow, red
        public List<string> Insights { get; set; } = new();
    }

    public class VolatilityMetrics
    {
        public decimal Mean { get; set; }
        public double StandardDeviation { get; set; }
        public double CoefficientOfVariation { get; set; }
        public bool IsHighlyVolatile { get; set; }
    }

    public class CorrelationMatrix
    {
        public Dictionary<string, Dictionary<string, double>> Correlations { get; set; } = new();
        public List<string> Variables { get; set; } = new();
    }

    public class MonteCarloResult
    {
        public List<decimal> Outcomes { get; set; } = new();
        public decimal Mean { get; set; }
        public double StandardDeviation { get; set; }
        public decimal Percentile5 { get; set; }
        public decimal Percentile50 { get; set; }
        public decimal Percentile95 { get; set; }
        public double ValueAtRisk95 { get; set; } // Probability of loss exceeding VaR
    }

    public interface IRiskAnalysisService
    {
        /// <summary>
        /// Assess risks across multiple categories
        /// </summary>
        Task<List<RiskAssessmentResult>> AssessRisksAsync(
            List<(DateTime date, decimal revenue)> revenueHistory,
            List<(DateTime date, decimal expense)> expenseHistory,
            decimal baselineRevenue);

        /// <summary>
        /// Calculate volatility metrics
        /// </summary>
        Task<VolatilityMetrics> CalculateVolatilityAsync(List<decimal> data);

        /// <summary>
        /// Calculate correlation matrix between variables
        /// </summary>
        Task<CorrelationMatrix> CalculateCorrelationMatrixAsync(
            Dictionary<string, List<decimal>> variables);

        /// <summary>
        /// Run Monte Carlo simulation for profit distribution
        /// </summary>
        Task<MonteCarloResult> RunMonteCarloSimulationAsync(
            decimal meanRevenue, double revenueStdDev,
            decimal meanExpenses, double expenseStdDev,
            int iterations = 1000);

        /// <summary>
        /// Compare actual metrics against industry benchmarks
        /// </summary>
        Task<Dictionary<string, (decimal actual, decimal benchmark, double variance)>> BenchmarkAgainstIndustryAsync(
            decimal grossMargin, decimal expenseRatio, int inventoryTurnover);

        /// <summary>
        /// Generate early warning indicators
        /// </summary>
        Task<List<(string indicator, decimal currentValue, decimal threshold, string status)>> GenerateEarlyWarningIndicatorsAsync(
            decimal dailyAverageSales, decimal dailyAverageExpenses,
            int currentInventoryDays, decimal cashPosition);
    }
}
