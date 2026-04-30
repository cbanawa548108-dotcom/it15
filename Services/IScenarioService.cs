namespace CRLFruitstandESS.Services
{
    public class ScenarioProjection
    {
        public int Month { get; set; }
        public decimal ProjectedRevenue { get; set; }
        public decimal ProjectedCOGS { get; set; }
        public decimal ProjectedOpEx { get; set; }
        public decimal ProjectedProfit { get; set; }
        public double ProfitMargin { get; set; }
        public decimal CumulativeProfit { get; set; }
    }

    public class ScenarioResult
    {
        public string ScenarioName { get; set; } = string.Empty;
        public List<ScenarioProjection> Projections { get; set; } = new();
        public decimal TotalProjectedRevenue { get; set; }
        public decimal TotalProjectedProfit { get; set; }
        public double AverageMargin { get; set; }
        public double ROI { get; set; }
    }

    public class SensitivityResult
    {
        public string Variable { get; set; } = string.Empty;
        public double ImpactOnProfit { get; set; }
        public double ImpactPercentage { get; set; }
    }

    public interface IScenarioService
    {
        /// <summary>
        /// Project scenario with given parameters
        /// </summary>
        Task<ScenarioResult> ProjectScenarioAsync(
            decimal baselineMonthlyRevenue, decimal baselineMonthlyExpenses,
            double revenueGrowthPercent, double expenseGrowthPercent, double cogsPercent,
            int simulationMonths, string scenarioName = "");

        /// <summary>
        /// Compare multiple scenarios
        /// </summary>
        Task<Dictionary<string, ScenarioResult>> CompareMultipleScenariosAsync(
            List<(string name, double revGrowth, double expGrowth, double cogs)> scenarios,
            decimal baselineRevenue, decimal baselineExpenses, int months);

        /// <summary>
        /// Perform sensitivity analysis on a scenario
        /// </summary>
        Task<List<SensitivityResult>> PerformSensitivityAnalysisAsync(
            decimal baselineRevenue, decimal baselineExpenses,
            double revenueGrowthPercent, double expenseGrowthPercent, double cogsPercent,
            int simulationMonths);

        /// <summary>
        /// Calculate tornado chart data showing variable importance
        /// </summary>
        Task<List<(string variable, double impact)>> GenerateTornadoChartAsync(
            List<SensitivityResult> sensitivityResults);
    }
}
