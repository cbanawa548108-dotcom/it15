namespace CRLFruitstandESS.Services
{
    public interface IKPICalculationService
    {
        /// <summary>
        /// Calculate high-level strategic KPIs from financial data
        /// </summary>
        Task<Dictionary<string, decimal>> CalculateStrategicKPIsAsync(DateTime startDate, DateTime endDate);

        /// <summary>
        /// Compare current period KPIs with previous period
        /// </summary>
        Task<Dictionary<string, (decimal current, decimal previous, double changePercent)>> ComparePeriodsAsync(
            DateTime currentStart, DateTime currentEnd,
            DateTime previousStart, DateTime previousEnd);

        /// <summary>
        /// Calculate overall business health score (0-100)
        /// </summary>
        Task<(double score, string status, List<string> insights)> CalculateHealthScoreAsync(DateTime fromDate);

        /// <summary>
        /// Detect anomalies in financial data
        /// </summary>
        Task<List<(string metric, decimal value, string anomalyType)>> DetectAnomaliesAsync(
            DateTime fromDate, double deviationThreshold = 2.0);
    }
}
