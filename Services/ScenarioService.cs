namespace CRLFruitstandESS.Services
{
    public class ScenarioService : IScenarioService
    {
        public Task<ScenarioResult> ProjectScenarioAsync(
            decimal baselineMonthlyRevenue, decimal baselineMonthlyExpenses,
            double revenueGrowthPercent, double expenseGrowthPercent, double cogsPercent,
            int simulationMonths, string scenarioName = "")
        {
            var result = new ScenarioResult { ScenarioName = scenarioName };
            var revenueGrowthFactor = 1.0 + (revenueGrowthPercent / 100.0);
            var expenseGrowthFactor = 1.0 + (expenseGrowthPercent / 100.0);
            var cogsFactor = cogsPercent / 100.0;

            decimal cumulativeProfit = 0;

            for (int month = 1; month <= simulationMonths; month++)
            {
                var projectedRevenue = baselineMonthlyRevenue *
                    (decimal)Math.Pow(revenueGrowthFactor, month - 1);
                var projectedCOGS = projectedRevenue * (decimal)cogsFactor;
                var projectedOpEx = baselineMonthlyExpenses *
                    (decimal)Math.Pow(expenseGrowthFactor, month - 1);
                var projectedProfit = projectedRevenue - projectedCOGS - projectedOpEx;
                var profitMargin = projectedRevenue > 0
                    ? ((projectedRevenue - projectedCOGS) / projectedRevenue) * 100
                    : 0;

                cumulativeProfit += projectedProfit;

                var projection = new ScenarioProjection
                {
                    Month = month,
                    ProjectedRevenue = projectedRevenue,
                    ProjectedCOGS = projectedCOGS,
                    ProjectedOpEx = projectedOpEx,
                    ProjectedProfit = projectedProfit,
                    ProfitMargin = (double)profitMargin,
                    CumulativeProfit = cumulativeProfit
                };

                result.Projections.Add(projection);
                result.TotalProjectedRevenue += projectedRevenue;
                result.TotalProjectedProfit += projectedProfit;
            }

            if (result.Projections.Count > 0)
            {
                result.AverageMargin = result.Projections.Average(p => p.ProfitMargin);
                result.ROI = baselineMonthlyExpenses * simulationMonths > 0
                    ? (double)(result.TotalProjectedProfit / (baselineMonthlyExpenses * simulationMonths)) * 100
                    : 0;
            }

            return Task.FromResult(result);
        }

        public async Task<Dictionary<string, ScenarioResult>> CompareMultipleScenariosAsync(
            List<(string name, double revGrowth, double expGrowth, double cogs)> scenarios,
            decimal baselineRevenue, decimal baselineExpenses, int months)
        {
            var results = new Dictionary<string, ScenarioResult>();

            foreach (var scenario in scenarios)
            {
                var result = await ProjectScenarioAsync(
                    baselineRevenue, baselineExpenses,
                    scenario.revGrowth, scenario.expGrowth, scenario.cogs,
                    months, scenario.name);
                results[scenario.name] = result;
            }

            return results;
        }

        public Task<List<SensitivityResult>> PerformSensitivityAnalysisAsync(
            decimal baselineRevenue, decimal baselineExpenses,
            double revenueGrowthPercent, double expenseGrowthPercent, double cogsPercent,
            int simulationMonths)
        {
            var baseResult = ProjectScenarioAsync(
                baselineRevenue, baselineExpenses,
                revenueGrowthPercent, expenseGrowthPercent, cogsPercent,
                simulationMonths).Result;

            var baseProfitValue = baseResult.TotalProjectedProfit;
            var results = new List<SensitivityResult>();

            // Test revenue growth sensitivity (±10%)
            var revUpResult = ProjectScenarioAsync(
                baselineRevenue, baselineExpenses,
                revenueGrowthPercent + 10, expenseGrowthPercent, cogsPercent,
                simulationMonths).Result;
            var revDownResult = ProjectScenarioAsync(
                baselineRevenue, baselineExpenses,
                revenueGrowthPercent - 10, expenseGrowthPercent, cogsPercent,
                simulationMonths).Result;

            var revImpact = ((revUpResult.TotalProjectedProfit - revDownResult.TotalProjectedProfit) / 2);
            results.Add(new SensitivityResult
            {
                Variable = "Revenue Growth",
                ImpactOnProfit = (double)revImpact,
                ImpactPercentage = baseProfitValue != 0 ? (double)(revImpact / baseProfitValue) * 100 : 0
            });

            // Test expense growth sensitivity (±10%)
            var expUpResult = ProjectScenarioAsync(
                baselineRevenue, baselineExpenses,
                revenueGrowthPercent, expenseGrowthPercent + 10, cogsPercent,
                simulationMonths).Result;
            var expDownResult = ProjectScenarioAsync(
                baselineRevenue, baselineExpenses,
                revenueGrowthPercent, expenseGrowthPercent - 10, cogsPercent,
                simulationMonths).Result;

            var expImpact = ((expDownResult.TotalProjectedProfit - expUpResult.TotalProjectedProfit) / 2);
            results.Add(new SensitivityResult
            {
                Variable = "Expense Growth",
                ImpactOnProfit = (double)expImpact,
                ImpactPercentage = baseProfitValue != 0 ? (double)(expImpact / baseProfitValue) * 100 : 0
            });

            // Test COGS sensitivity (±5%)
            var cogsUpResult = ProjectScenarioAsync(
                baselineRevenue, baselineExpenses,
                revenueGrowthPercent, expenseGrowthPercent, cogsPercent + 5,
                simulationMonths).Result;
            var cogsDownResult = ProjectScenarioAsync(
                baselineRevenue, baselineExpenses,
                revenueGrowthPercent, expenseGrowthPercent, cogsPercent - 5,
                simulationMonths).Result;

            var cogsImpact = ((cogsDownResult.TotalProjectedProfit - cogsUpResult.TotalProjectedProfit) / 2);
            results.Add(new SensitivityResult
            {
                Variable = "COGS Percentage",
                ImpactOnProfit = (double)cogsImpact,
                ImpactPercentage = baseProfitValue != 0 ? (double)(cogsImpact / baseProfitValue) * 100 : 0
            });

            // Sort by impact percentage descending
            results = results.OrderByDescending(r => Math.Abs(r.ImpactPercentage)).ToList();

            return Task.FromResult(results);
        }

        public Task<List<(string variable, double impact)>> GenerateTornadoChartAsync(
            List<SensitivityResult> sensitivityResults)
        {
            var tornadoData = sensitivityResults
                .OrderByDescending(s => Math.Abs(s.ImpactPercentage))
                .Select(s => (s.Variable, Math.Abs(s.ImpactPercentage)))
                .ToList();

            return Task.FromResult(tornadoData);
        }
    }
}
