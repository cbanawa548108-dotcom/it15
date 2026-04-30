namespace CRLFruitstandESS.Services
{
    public class RiskAnalysisService : IRiskAnalysisService
    {
        public async Task<List<RiskAssessmentResult>> AssessRisksAsync(
            List<(DateTime date, decimal revenue)> revenueHistory,
            List<(DateTime date, decimal expense)> expenseHistory,
            decimal baselineRevenue)
        {
            var risks = new List<RiskAssessmentResult>();

            // Revenue Risk
            var revenueVolatility = await CalculateVolatilityAsync(
                revenueHistory.Select(x => x.revenue).ToList());

            var revenueRisk = new RiskAssessmentResult
            {
                RiskCategory = "Revenue",
                Probability  = Math.Min(1.0, revenueVolatility.CoefficientOfVariation),
                Impact       = 0.3,
                Volatility   = revenueVolatility.CoefficientOfVariation * 100.0,
                Status       = revenueVolatility.CoefficientOfVariation > 0.2 ? "red" : "yellow"
            };
            revenueRisk.RiskScore = (revenueRisk.Probability * revenueRisk.Impact * 0.6)
                + (revenueVolatility.CoefficientOfVariation * 0.3) + (0.0 * 0.1);
            revenueRisk.Insights.Add($"Revenue volatility (CV): {revenueVolatility.CoefficientOfVariation:P}");
            risks.Add(revenueRisk);

            // Operational Risk
            var expenseVolatility = await CalculateVolatilityAsync(
                expenseHistory.Select(x => x.expense).ToList());

            var operationalRisk = new RiskAssessmentResult
            {
                RiskCategory = "Operational",
                Probability  = Math.Min(1.0, expenseVolatility.CoefficientOfVariation),
                Impact       = 0.25,
                Volatility   = expenseVolatility.CoefficientOfVariation * 100.0,
                Status       = expenseVolatility.CoefficientOfVariation > 0.25 ? "red" : "yellow"
            };
            operationalRisk.RiskScore = (operationalRisk.Probability * operationalRisk.Impact * 0.6)
                + (expenseVolatility.CoefficientOfVariation * 0.3) + (0.0 * 0.1);
            operationalRisk.Insights.Add($"Expense volatility (CV): {expenseVolatility.CoefficientOfVariation:P}");
            risks.Add(operationalRisk);

            // Profitability Risk
            var profitHistory = revenueHistory.Zip(expenseHistory,
                (r, e) => (date: r.date, profit: r.revenue - e.expense)).ToList();
            var profitVolatility = await CalculateVolatilityAsync(
                profitHistory.Select(x => x.profit).ToList());

            var unprofitableDays  = profitHistory.Count(x => x.profit < 0);
            var unprofitableRatio = profitHistory.Count > 0
                ? (double)unprofitableDays / profitHistory.Count
                : 0.0;

            var profitabilityRisk = new RiskAssessmentResult
            {
                RiskCategory = "Profitability",
                Probability  = unprofitableRatio,
                Impact       = 0.4,
                Volatility   = profitVolatility.CoefficientOfVariation * 100.0,
                Status       = unprofitableRatio > 0.1 ? "red"
                             : unprofitableRatio > 0.05 ? "yellow" : "green"
            };
            profitabilityRisk.RiskScore = (profitabilityRisk.Probability * profitabilityRisk.Impact * 0.6)
                + (profitVolatility.CoefficientOfVariation * 0.3) + (0.0 * 0.1);
            profitabilityRisk.Insights.Add($"Unprofitable days: {unprofitableRatio:P1}");
            risks.Add(profitabilityRisk);

            // Liquidity Risk
            // FIX CS1503 Ln 115: convert decimal Average/Sum results to double explicitly
            var avgRevenue    = revenueHistory.Count > 0
                ? (double)revenueHistory.Average(x => x.revenue)
                : 0.0;
            var liquidity30Day = (double)revenueHistory.TakeLast(30).Sum(x => x.revenue);

            var liquidityRisk = new RiskAssessmentResult
            {
                RiskCategory = "Liquidity",
                Probability  = liquidity30Day < avgRevenue * 20.0 ? 0.7 : 0.3,
                Impact       = 0.35,
                Status       = liquidity30Day < avgRevenue * 15.0 ? "red" : "yellow"
            };
            liquidityRisk.RiskScore = liquidityRisk.Probability * liquidityRisk.Impact;
            // FIX: format the original decimal value for display (cast back for the message)
            liquidityRisk.Insights.Add(
                $"30-day revenue accumulation: {revenueHistory.TakeLast(30).Sum(x => x.revenue):C}");
            risks.Add(liquidityRisk);

            // External/Market Risk
            var seasonalVariance = revenueHistory.Count >= 30
                ? await AnalyzeSeasonalVarianceAsync(revenueHistory)
                : 0.0;

            var externalRisk = new RiskAssessmentResult
            {
                RiskCategory = "External",
                Probability  = Math.Min(1.0, seasonalVariance),
                Impact       = 0.25,
                Status       = seasonalVariance > 0.3 ? "yellow" : "green"
            };
            externalRisk.RiskScore = externalRisk.Probability * externalRisk.Impact;
            externalRisk.Insights.Add($"Seasonal variance detected: {seasonalVariance:P}");
            risks.Add(externalRisk);

            return risks;
        }

        public Task<VolatilityMetrics> CalculateVolatilityAsync(List<decimal> data)
        {
            if (data.Count == 0)
                return Task.FromResult(new VolatilityMetrics
                {
                    Mean                   = 0,
                    StandardDeviation      = 0,
                    CoefficientOfVariation = 0,
                    IsHighlyVolatile       = false
                });

            var mean     = data.Average();
            // FIX CS1503 Ln 199: cast result of Math.Pow (double) back to decimal for variance
            var variance = data.Average(x => (decimal)Math.Pow((double)(x - mean), 2));
            var stdDev   = Math.Sqrt((double)variance);          // double — correct
            var cv       = (double)mean != 0 ? stdDev / (double)mean : 0.0;

            return Task.FromResult(new VolatilityMetrics
            {
                Mean                   = mean,       // decimal — matches property type
                StandardDeviation      = stdDev,     // double  — matches property type
                CoefficientOfVariation = cv,         // double  — matches property type
                IsHighlyVolatile       = cv > 0.2
            });
        }

        public Task<CorrelationMatrix> CalculateCorrelationMatrixAsync(
            Dictionary<string, List<decimal>> variables)
        {
            var matrix   = new CorrelationMatrix();
            var varNames = variables.Keys.ToList();

            foreach (var varName in varNames)
            {
                matrix.Variables.Add(varName);
                matrix.Correlations[varName] = new Dictionary<string, double>();

                foreach (var otherName in varNames)
                {
                    var correlation = CalculatePearsonCorrelation(
                        variables[varName], variables[otherName]);
                    matrix.Correlations[varName][otherName] = correlation;
                }
            }

            return Task.FromResult(matrix);
        }

        private double CalculatePearsonCorrelation(List<decimal> x, List<decimal> y)
        {
            if (x.Count != y.Count || x.Count < 2)
                return 0;

            var meanX = x.Average();
            var meanY = y.Average();

            var numerator = x.Zip(y, (xi, yi) =>
                (double)(xi - meanX) * (double)(yi - meanY)).Sum();

            var denomX = Math.Sqrt(x.Select(xi =>
                Math.Pow((double)(xi - meanX), 2)).Sum());
            var denomY = Math.Sqrt(y.Select(yi =>
                Math.Pow((double)(yi - meanY), 2)).Sum());

            return denomX > 0 && denomY > 0 ? numerator / (denomX * denomY) : 0;
        }

        public Task<MonteCarloResult> RunMonteCarloSimulationAsync(
            decimal meanRevenue, double revenueStdDev,
            decimal meanExpenses, double expenseStdDev,
            int iterations = 1000)
        {
            var random   = new Random();
            var outcomes = new List<decimal>();

            for (int i = 0; i < iterations; i++)
            {
                var u1 = random.NextDouble();
                var u2 = random.NextDouble();
                var z  = Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
                var u3 = random.NextDouble();
                var u4 = random.NextDouble();
                var z2 = Math.Sqrt(-2 * Math.Log(u3)) * Math.Cos(2 * Math.PI * u4);

                var simRevenue  = meanRevenue  + (decimal)(z  * revenueStdDev);
                var simExpenses = meanExpenses + (decimal)(z2 * expenseStdDev);
                var profit      = simRevenue - simExpenses;

                outcomes.Add(profit);
            }

            outcomes.Sort();

            var mean     = outcomes.Average();
            var variance = outcomes.Average(x => (decimal)Math.Pow((double)(x - mean), 2));
            var stdDev   = Math.Sqrt((double)variance);

            var result = new MonteCarloResult
            {
                Outcomes          = outcomes,
                Mean              = mean,
                StandardDeviation = stdDev,
                Percentile5       = outcomes[(int)(outcomes.Count * 0.05)],
                Percentile50      = outcomes[outcomes.Count / 2],
                Percentile95      = outcomes[(int)(outcomes.Count * 0.95)],
                ValueAtRisk95     = outcomes.Count(x => x < 0) / (double)outcomes.Count
            };

            return Task.FromResult(result);
        }

        public Task<Dictionary<string, (decimal actual, decimal benchmark, double variance)>> BenchmarkAgainstIndustryAsync(
            decimal grossMargin, decimal expenseRatio, int inventoryTurnover)
        {
            var benchmarks = new Dictionary<string, (decimal, decimal, double)>
            {
                { "Gross Margin %",    (grossMargin,       35m, (double)((grossMargin       - 35m) / 35m) * 100) },
                { "Expense Ratio %",   (expenseRatio,      52m, (double)((expenseRatio      - 52m) / 52m) * 100) },
                { "Inventory Turnover",(inventoryTurnover, 10m, (double)((inventoryTurnover - 10m) / 10m) * 100) }
            };

            return Task.FromResult(benchmarks);
        }

        public Task<List<(string indicator, decimal currentValue, decimal threshold, string status)>>
            GenerateEarlyWarningIndicatorsAsync(
                decimal dailyAverageSales, decimal dailyAverageExpenses,
                int currentInventoryDays, decimal cashPosition)
        {
            var indicators = new List<(string, decimal, decimal, string)>();

            var salesThreshold   = 5000m;
            var salesStatus      = dailyAverageSales < salesThreshold * 0.7m ? "critical"
                                 : dailyAverageSales < salesThreshold ? "warning" : "normal";
            indicators.Add(("Daily Average Sales", dailyAverageSales, salesThreshold, salesStatus));

            var expenseThreshold = 3000m;
            var expenseStatus    = dailyAverageExpenses > expenseThreshold * 1.3m ? "critical"
                                 : dailyAverageExpenses > expenseThreshold ? "warning" : "normal";
            indicators.Add(("Daily Average Expenses", dailyAverageExpenses, expenseThreshold, expenseStatus));

            var inventoryThreshold = 14;
            var inventoryStatus    = currentInventoryDays > inventoryThreshold * 1.5 ? "warning"
                                   : currentInventoryDays > inventoryThreshold ? "caution" : "normal";
            indicators.Add(("Inventory Days Outstanding", currentInventoryDays, inventoryThreshold, inventoryStatus));

            var cashThreshold = 50000m;
            var cashStatus    = cashPosition < cashThreshold * 0.3m ? "critical"
                              : cashPosition < cashThreshold ? "warning" : "normal";
            indicators.Add(("Cash Position", cashPosition, cashThreshold, cashStatus));

            return Task.FromResult(indicators);
        }

        private async Task<double> AnalyzeSeasonalVarianceAsync(
            List<(DateTime date, decimal revenue)> data)
        {
            if (data.Count < 14)
                return 0;

            var byWeek = new Dictionary<int, List<decimal>>();
            foreach (var item in data)
            {
                var week = item.date.DayOfYear / 7;
                if (!byWeek.ContainsKey(week))
                    byWeek[week] = new List<decimal>();
                byWeek[week].Add(item.revenue);
            }

            var weeklyAverages = byWeek.Values
                .Where(w => w.Count > 0)
                .Select(w => w.Average())
                .ToList();

            if (weeklyAverages.Count < 2)
                return 0;

            var mean     = weeklyAverages.Average();
            var variance = weeklyAverages.Average(x =>
                (decimal)Math.Pow((double)(x - mean), 2));
            var stdDev   = Math.Sqrt((double)variance);
            var cv       = (double)mean > 0 ? stdDev / (double)mean : 0.0;

            return cv;
        }
    }
}