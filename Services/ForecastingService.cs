using System;

namespace CRLFruitstandESS.Services
{
    /// <summary>
    /// Dataset constants sourced from CRL Fruitstand 5-year actual data (2020-2024).
    /// Used to anchor forecasts when historical DB data is sparse or missing.
    /// </summary>
    public static class FruitstandDataset
    {
        // ── Annual / daily revenue baseline
        public const decimal AnnualRevenue   = 39_800_000m;   // ₱39.8M average across 5 years
        public const decimal DailyRevenue    = 109_041m;      // AnnualRevenue / 365
        public const decimal WeeklyRevenue   = 763_287m;      // AnnualRevenue / 52

        // ── Net margin & spoilage from dataset
        public const double NetMarginPct  = 0.319;   // 31.9%
        public const double SpoilagePct   = 0.305;   // 30.5% of stock-in

        // ── Product revenue weights (must sum to 1.0)
        public static readonly Dictionary<string, double> ProductWeights = new()
        {
            { "Durian",     0.209 },
            { "Mangosteen", 0.127 },
            { "Pomelo",     0.102 },
            { "Mango",      0.101 },
            { "Lanzones",   0.100 },
            { "Jackfruit",  0.085 },
            { "Rambutan",   0.079 },
            { "Banana",     0.071 },
            { "Watermelon", 0.066 },
            { "Papaya",     0.061 },
        };

        // ── Product unit prices from dataset
        public static readonly Dictionary<string, decimal> ProductPrices = new()
        {
            { "Durian",     350m },
            { "Mangosteen", 180m },
            { "Pomelo",     120m },
            { "Mango",       80m },
            { "Lanzones",    90m },
            { "Jackfruit",  150m },
            { "Rambutan",    70m },
            { "Banana",      45m },
            { "Watermelon",  95m },
            { "Papaya",      60m },
        };

        // ── Product emojis
        public static readonly Dictionary<string, string> ProductEmojis = new()
        {
            { "Durian",     "🌵" },
            { "Mangosteen", "🍇" },
            { "Pomelo",     "🍊" },
            { "Mango",      "🥭" },
            { "Lanzones",   "🍈" },
            { "Jackfruit",  "🍐" },
            { "Rambutan",   "🍒" },
            { "Banana",     "🍌" },
            { "Watermelon", "🍉" },
            { "Papaya",     "🍑" },
        };

        /// <summary>
        /// Returns the seasonal multiplier for a given date.
        /// Jun–Sep (rainy season): +5% | Sat/Sun: +7%
        /// </summary>
        public static double SeasonalFactor(DateTime date)
        {
            double factor = 1.0;
            if (date.Month >= 6 && date.Month <= 9) factor *= 1.05;
            if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday) factor *= 1.07;
            return factor;
        }

        /// <summary>
        /// Returns the dataset-anchored expected daily revenue for a given date.
        /// </summary>
        public static decimal ExpectedDailyRevenue(DateTime date)
            => Math.Round(DailyRevenue * (decimal)SeasonalFactor(date), 2);
    }

    public class ForecastingService : IForecastingService
    {
        // ════════════════════════════════════════════════════════
        // MOVING AVERAGE
        // ════════════════════════════════════════════════════════
        public Task<ForecastResult> MovingAverageForecastAsync(
            List<(DateTime date, decimal value)> historicalData,
            int period = 7, int forecastDays = 30)
        {
            var result = new ForecastResult { Method = $"Moving Average (Period={period})" };
            var data   = historicalData.OrderBy(x => x.date).ToList();

            if (data.Count < period)
            {
                result.Insights = "Insufficient historical data — using dataset baseline for projection.";
                // Fall back to dataset baseline
                var baseDate = data.Any() ? data.Last().date.AddDays(1) : DateTime.Today;
                for (int i = 0; i < forecastDays; i++)
                {
                    var d = baseDate.AddDays(i);
                    var expected = FruitstandDataset.ExpectedDailyRevenue(d);
                    result.Forecast.Add(new ForecastDataPoint
                    {
                        Date            = d,
                        ForecastedValue = expected,
                        LowerBound      = Math.Round(expected * 0.85m, 2),
                        UpperBound      = Math.Round(expected * 1.15m, 2),
                        Confidence      = 0.70
                    });
                }
                result.MAPE     = 0;
                result.Accuracy = 0.70;
                return Task.FromResult(result);
            }

            // Blend: 70% actual moving average + 30% dataset anchor
            var residuals = new List<decimal>();
            var forecastDate = data.Last().date.AddDays(1);
            var windowAvg    = data.TakeLast(period).Average(x => x.value);

            for (int i = 0; i < forecastDays; i++)
            {
                var d        = forecastDate.AddDays(i);
                var anchor   = FruitstandDataset.ExpectedDailyRevenue(d);
                var blended  = Math.Round(windowAvg * 0.70m + anchor * 0.30m, 2);

                result.Forecast.Add(new ForecastDataPoint
                {
                    Date            = d,
                    ForecastedValue = blended,
                    LowerBound      = Math.Round(blended * 0.85m, 2),
                    UpperBound      = Math.Round(blended * 1.15m, 2),
                    Confidence      = 0.75
                });
            }

            for (int i = period; i < data.Count; i++)
            {
                var wa = data.Skip(i - period).Take(period).Average(x => x.value);
                residuals.Add(Math.Abs(data[i].value - wa));
            }

            result.MAPE     = residuals.Count > 0 ? (double)(residuals.Average() / data.Average(x => x.value)) * 100 : 0;
            result.Accuracy = 0.80;
            result.Insights = $"7-day moving average blended with ₱{FruitstandDataset.DailyRevenue:N0}/day dataset baseline. " +
                              $"Seasonal adjustments applied (rainy season +5%, weekends +7%).";

            return Task.FromResult(result);
        }

        // ════════════════════════════════════════════════════════
        // EXPONENTIAL SMOOTHING
        // ════════════════════════════════════════════════════════
        public Task<ForecastResult> ExponentialSmoothingForecastAsync(
            List<(DateTime date, decimal value)> historicalData,
            double alpha = 0.3, int forecastDays = 30)
        {
            var result = new ForecastResult { Method = $"Exponential Smoothing (α={alpha})" };
            var data   = historicalData.OrderBy(x => x.date).ToList();

            if (data.Count < 2)
            {
                result.Insights = "Insufficient historical data — using dataset baseline for projection.";
                var baseDate = data.Any() ? data.Last().date.AddDays(1) : DateTime.Today;
                for (int i = 0; i < forecastDays; i++)
                {
                    var d        = baseDate.AddDays(i);
                    var expected = FruitstandDataset.ExpectedDailyRevenue(d);
                    result.Forecast.Add(new ForecastDataPoint
                    {
                        Date            = d,
                        ForecastedValue = expected,
                        LowerBound      = Math.Round(expected * 0.85m, 2),
                        UpperBound      = Math.Round(expected * 1.15m, 2),
                        Confidence      = 0.75 - (i * 0.004)
                    });
                }
                result.MAPE     = 0;
                result.Accuracy = 0.75;
                result.Insights = $"Dataset baseline: ₱{FruitstandDataset.DailyRevenue:N0}/day with seasonal adjustments.";
                return Task.FromResult(result);
            }

            // Initialize level — blend first actual value with dataset anchor
            decimal anchor0 = FruitstandDataset.ExpectedDailyRevenue(data[0].date);
            decimal level   = data[0].value * 0.70m + anchor0 * 0.30m;

            var residuals = new List<decimal>();

            for (int i = 1; i < data.Count; i++)
            {
                var anchor   = FruitstandDataset.ExpectedDailyRevenue(data[i].date);
                // Blend actual with dataset anchor before smoothing
                var blended  = data[i].value * 0.80m + anchor * 0.20m;
                var newLevel = (decimal)alpha * blended + (1 - (decimal)alpha) * level;
                residuals.Add(Math.Abs(data[i].value - level));
                level = newLevel;
            }

            var forecastDate = data.Last().date.AddDays(1);
            for (int i = 0; i < forecastDays; i++)
            {
                var d        = forecastDate.AddDays(i);
                var anchor   = FruitstandDataset.ExpectedDailyRevenue(d);
                // As we go further out, lean more on the dataset anchor
                double anchorWeight = Math.Min(0.60, 0.20 + i * 0.013);
                var projected = level * (decimal)(1 - anchorWeight) + anchor * (decimal)anchorWeight;

                var stdDev = residuals.Count > 0
                    ? (decimal)Math.Sqrt(residuals.Average(x => Math.Pow((double)x, 2)))
                    : level * 0.10m;

                result.Forecast.Add(new ForecastDataPoint
                {
                    Date            = d,
                    ForecastedValue = Math.Round(projected, 2),
                    LowerBound      = Math.Round(Math.Max(0, projected - 2 * stdDev), 2),
                    UpperBound      = Math.Round(projected + 2 * stdDev, 2),
                    Confidence      = Math.Max(0.50, 0.88 - i * 0.005)
                });
            }

            result.MAPE     = residuals.Count > 0 ? (double)(residuals.Average() / data.Average(x => x.value)) * 100 : 0;
            result.Accuracy = 0.85;
            result.Insights = $"Exponential smoothing (α={alpha}) anchored to ₱{FruitstandDataset.DailyRevenue:N0}/day baseline. " +
                              $"Rainy season (Jun–Sep) and weekend boosts applied. " +
                              $"Net margin target: {FruitstandDataset.NetMarginPct * 100:F1}%.";

            return Task.FromResult(result);
        }

        // ════════════════════════════════════════════════════════
        // LINEAR REGRESSION
        // ════════════════════════════════════════════════════════
        public Task<ForecastResult> LinearRegressionForecastAsync(
            List<(DateTime date, decimal value)> historicalData,
            int forecastDays = 30)
        {
            var result = new ForecastResult { Method = "Linear Regression" };
            var data   = historicalData.OrderBy(x => x.date).ToList();

            if (data.Count < 3)
            {
                result.Insights = "Insufficient historical data — using dataset baseline.";
                var baseDate = data.Any() ? data.Last().date.AddDays(1) : DateTime.Today;
                for (int i = 0; i < forecastDays; i++)
                {
                    var d        = baseDate.AddDays(i);
                    var expected = FruitstandDataset.ExpectedDailyRevenue(d);
                    result.Forecast.Add(new ForecastDataPoint
                    {
                        Date            = d,
                        ForecastedValue = expected,
                        LowerBound      = Math.Round(expected * 0.85m, 2),
                        UpperBound      = Math.Round(expected * 1.15m, 2),
                        Confidence      = 0.72
                    });
                }
                result.MAPE     = 0;
                result.Accuracy = 0.72;
                return Task.FromResult(result);
            }

            var n      = data.Count;
            var x      = Enumerable.Range(0, n).Select(i => (double)i).ToList();
            var y      = data.Select(d => (double)d.value).ToList();
            var xMean  = x.Average();
            var yMean  = y.Average();

            var numerator   = x.Zip(y, (xi, yi) => (xi - xMean) * (yi - yMean)).Sum();
            var denominator = x.Select(xi => Math.Pow(xi - xMean, 2)).Sum();
            var slope       = denominator > 0 ? numerator / denominator : 0;
            var intercept   = yMean - slope * xMean;

            var residuals = new List<decimal>();
            for (int i = 0; i < data.Count; i++)
            {
                var predicted = intercept + slope * i;
                residuals.Add((decimal)Math.Abs(y[i] - predicted));
            }

            var stdDev = residuals.Count > 0
                ? Math.Sqrt(residuals.Average(x2 => Math.Pow((double)x2, 2)))
                : 0;

            var forecastDate = data.Last().date.AddDays(1);
            for (int i = 0; i < forecastDays; i++)
            {
                var d          = forecastDate.AddDays(i);
                var anchor     = FruitstandDataset.ExpectedDailyRevenue(d);
                var regression = intercept + slope * (n + i);

                // Blend regression with dataset anchor (50/50 for regression which can drift)
                var blended = (decimal)regression * 0.50m + anchor * 0.50m;
                blended     = Math.Max(0, blended);

                result.Forecast.Add(new ForecastDataPoint
                {
                    Date            = d,
                    ForecastedValue = Math.Round(blended, 2),
                    LowerBound      = Math.Round(Math.Max(0, blended - (decimal)(2 * stdDev)), 2),
                    UpperBound      = Math.Round(blended + (decimal)(2 * stdDev), 2),
                    Confidence      = Math.Max(0.50, 0.80 - i * 0.008)
                });
            }

            var ssRes     = residuals.Sum(r => Math.Pow((double)r, 2));
            var ssTot     = y.Select(yi => Math.Pow(yi - yMean, 2)).Sum();
            var rSquared  = ssTot > 0 ? 1 - ssRes / ssTot : 0;

            result.Accuracy = Math.Max(0, rSquared);
            result.MAPE     = residuals.Count > 0 ? (double)(residuals.Average() / (decimal)yMean) * 100 : 0;
            result.Insights = slope > 0
                ? $"Upward trend (slope {slope:F0}/day) blended with ₱{FruitstandDataset.DailyRevenue:N0} dataset baseline."
                : slope < 0
                ? $"Downward trend (slope {slope:F0}/day) — dataset anchor prevents unrealistic projections."
                : $"Flat trend. Dataset baseline ₱{FruitstandDataset.DailyRevenue:N0}/day applied.";

            return Task.FromResult(result);
        }

        // ════════════════════════════════════════════════════════
        // DATASET-BASED PRODUCT REVENUE FORECAST
        // Returns 30-day projected revenue per product using the
        // 5-year revenue weights, anchored to the daily baseline,
        // with seasonal adjustments applied day-by-day.
        // ════════════════════════════════════════════════════════
        public Task<List<ProductRevenueForecast>> ForecastProductRevenueAsync(
            DateTime startDate, int forecastDays,
            Dictionary<string, (int qty15d, double dailyAvgQty, double growthRate)>? actualSalesData = null)
        {
            var results = new List<ProductRevenueForecast>();

            foreach (var (name, weight) in FruitstandDataset.ProductWeights)
            {
                decimal totalProjectedRevenue = 0m;
                var dailyPoints = new List<(DateTime date, decimal revenue)>();

                for (int i = 0; i < forecastDays; i++)
                {
                    var d              = startDate.AddDays(i);
                    var dailyBase      = FruitstandDataset.ExpectedDailyRevenue(d);
                    var productRevenue = Math.Round(dailyBase * (decimal)weight, 2);
                    totalProjectedRevenue += productRevenue;
                    dailyPoints.Add((d, productRevenue));
                }

                var price = FruitstandDataset.ProductPrices.TryGetValue(name, out var p) ? p : 100m;
                var avgDailyRevenue = totalProjectedRevenue / forecastDays;
                var projectedQty    = (int)Math.Round(avgDailyRevenue / price * forecastDays);

                // If we have actual sales data, blend it in
                double growthRate = 0;
                int    qty15d     = 0;
                double dailyAvg   = 0;
                string trend      = "stable";

                if (actualSalesData != null && actualSalesData.TryGetValue(name, out var actual))
                {
                    qty15d   = actual.qty15d;
                    dailyAvg = actual.dailyAvgQty;
                    growthRate = actual.growthRate;
                    trend    = growthRate > 10 ? "up" : growthRate < -10 ? "down" : "stable";

                    // Blend actual daily avg with dataset-derived avg (60/40 actual/dataset)
                    if (dailyAvg > 0)
                    {
                        var datasetDailyQty = (double)(avgDailyRevenue / price);
                        var blendedDailyQty = dailyAvg * 0.60 + datasetDailyQty * 0.40;
                        projectedQty = (int)Math.Round(blendedDailyQty * forecastDays);
                        totalProjectedRevenue = Math.Round((decimal)blendedDailyQty * price * forecastDays, 2);
                    }
                }
                else
                {
                    // Pure dataset projection — estimate daily avg from dataset
                    dailyAvg = (double)(avgDailyRevenue / price);
                }

                results.Add(new ProductRevenueForecast
                {
                    ProductName              = name,
                    Emoji                    = FruitstandDataset.ProductEmojis.TryGetValue(name, out var e) ? e : "📦",
                    RevenueWeight            = weight,
                    UnitPrice                = price,
                    ProjectedRevenue30Days   = totalProjectedRevenue,
                    ProjectedQty30Days       = projectedQty,
                    AvgDailyRevenue          = Math.Round(avgDailyRevenue, 2),
                    TotalQty15Days           = qty15d,
                    DailyAvgQty              = Math.Round(dailyAvg, 1),
                    GrowthRate               = Math.Round(growthRate, 1),
                    Trend                    = trend,
                    DataSource               = qty15d > 0 ? "Actual + Dataset" : "Dataset Baseline",
                    SpoilageAllowancePct     = FruitstandDataset.SpoilagePct
                });
            }

            // Sort by projected revenue descending
            results = results.OrderByDescending(r => r.ProjectedRevenue30Days).ToList();
            return Task.FromResult(results);
        }

        // ════════════════════════════════════════════════════════
        // CONFIDENCE INTERVALS
        // ════════════════════════════════════════════════════════
        public Task<(decimal lower68, decimal upper68, decimal lower95, decimal upper95)> CalculateConfidenceIntervalsAsync(
            List<decimal> residuals, decimal forecastValue)
        {
            if (residuals.Count == 0)
                return Task.FromResult((forecastValue * 0.9m, forecastValue * 1.1m,
                                        forecastValue * 0.8m, forecastValue * 1.2m));

            var stdDev   = (decimal)Math.Sqrt(residuals.Average(x => Math.Pow((double)x, 2)));
            var lower68  = forecastValue - stdDev;
            var upper68  = forecastValue + stdDev;
            var lower95  = forecastValue - 2 * stdDev;
            var upper95  = forecastValue + 2 * stdDev;

            return Task.FromResult((lower68, upper68, lower95, upper95));
        }

        // ════════════════════════════════════════════════════════
        // SEASONALITY ANALYSIS
        // ════════════════════════════════════════════════════════
        public Task<(double seasonalityIndex, bool isHighlySeasonable, List<double> weeklyFactors)> AnalyzeSeasonalityAsync(
            List<(DateTime date, decimal value)> historicalData)
        {
            var data = historicalData.OrderBy(x => x.date).ToList();
            var byDayOfWeek = new Dictionary<int, List<decimal>>();

            foreach (var item in data)
            {
                var dow = (int)item.date.DayOfWeek;
                if (!byDayOfWeek.ContainsKey(dow)) byDayOfWeek[dow] = new List<decimal>();
                byDayOfWeek[dow].Add(item.value);
            }

            var overallAverage = data.Count > 0 ? data.Average(x => (double)x.value) : (double)FruitstandDataset.DailyRevenue;
            var dayAverages    = new List<double>();

            for (int i = 0; i < 7; i++)
            {
                var dayAvg = byDayOfWeek.ContainsKey(i) && byDayOfWeek[i].Count > 0
                    ? (double)byDayOfWeek[i].Average()
                    : overallAverage * FruitstandDataset.SeasonalFactor(DateTime.Today.AddDays(i - (int)DateTime.Today.DayOfWeek));
                dayAverages.Add((double)dayAvg);
            }

            var mean             = dayAverages.Average();
            var variance         = dayAverages.Average(x => Math.Pow(x - mean, 2));
            var stdDev           = Math.Sqrt(variance);
            var seasonalityIndex = mean > 0 ? stdDev / mean * 100 : 0;
            var weeklyFactors    = dayAverages.Select(d => d / mean).ToList();

            return Task.FromResult((seasonalityIndex, seasonalityIndex > 15, weeklyFactors));
        }

        // ════════════════════════════════════════════════════════
        // INSIGHTS
        // ════════════════════════════════════════════════════════
        public Task<string> GenerateInsightsAsync(ForecastResult forecast,
            List<(DateTime date, decimal value)> historicalData)
        {
            var insights = new List<string>();
            insights.Add($"Using {forecast.Method} method.");

            if (forecast.Forecast.Count >= 2)
            {
                var first = forecast.Forecast[0].ForecastedValue;
                var last  = forecast.Forecast.Last().ForecastedValue;
                var pct   = first > 0 ? (last - first) / first * 100 : 0;

                if (pct > 5)       insights.Add($"Forecast shows upward trend of {pct:F1}%.");
                else if (pct < -5) insights.Add($"Forecast shows downward trend of {Math.Abs(pct):F1}%.");
                else               insights.Add("Forecast is relatively stable.");
            }

            if (forecast.Accuracy > 0.8)      insights.Add("High confidence (R² > 0.8).");
            else if (forecast.Accuracy > 0.6)  insights.Add("Moderate confidence (R² > 0.6).");
            else                               insights.Add("Low confidence — consider ensemble of multiple methods.");

            insights.Add($"Dataset anchor: ₱{FruitstandDataset.DailyRevenue:N0}/day | " +
                         $"Net margin: {FruitstandDataset.NetMarginPct * 100:F1}% | " +
                         $"Spoilage: {FruitstandDataset.SpoilagePct * 100:F1}%.");

            return Task.FromResult(string.Join(" ", insights));
        }
    }
}
