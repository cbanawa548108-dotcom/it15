using Microsoft.EntityFrameworkCore;
using CRLFruitstandESS.Data;

namespace CRLFruitstandESS.Services
{
    public class KpiCalculationService : IKPICalculationService
    {
        private readonly ApplicationDbContext _context;

        public KpiCalculationService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Dictionary<string, decimal>> CalculateStrategicKPIsAsync(DateTime startDate, DateTime endDate)
        {
            var kpis = new Dictionary<string, decimal>();

            var totalRevenue = await _context.Revenues
                .Where(r => !r.IsDeleted && r.TransactionDate >= startDate && r.TransactionDate <= endDate)
                .SumAsync(r => r.Amount);
            kpis["TotalRevenue"] = totalRevenue;

            var totalExpenses = await _context.Expenses
                .Where(e => !e.IsDeleted && e.ExpenseDate >= startDate && e.ExpenseDate <= endDate)
                .SumAsync(e => e.Amount);
            kpis["TotalExpenses"] = totalExpenses;

            var netProfit = totalRevenue - totalExpenses;
            kpis["NetProfit"] = netProfit;

            var cogs = await _context.SaleItems
                .Where(si => si.Sale.SaleDate >= startDate && si.Sale.SaleDate <= endDate)
                .SumAsync(si => (decimal?)si.Quantity * si.Product.CostPrice) ?? 0m;
            var grossProfit = totalRevenue - cogs;
            kpis["GrossProfit"] = grossProfit;
            kpis["GrossMarginPercent"] = totalRevenue > 0m
                ? (grossProfit / totalRevenue) * 100m
                : 0m;

            // FIX CS0019: all operands are now decimal — no double mixing
            kpis["OperationalEfficiency"] = totalRevenue > 0m
                ? 100m - ((totalExpenses / totalRevenue) * 100m)
                : 0m;

            kpis["ROI"] = totalExpenses > 0m
                ? (netProfit / totalExpenses) * 100m
                : 0m;

            kpis["CashFlowRatio"] = totalExpenses > 0m
                ? totalRevenue / totalExpenses
                : 0m;

            return kpis;
        }

        public async Task<Dictionary<string, (decimal current, decimal previous, double changePercent)>> ComparePeriodsAsync(
            DateTime currentStart, DateTime currentEnd,
            DateTime previousStart, DateTime previousEnd)
        {
            var current  = await CalculateStrategicKPIsAsync(currentStart, currentEnd);
            var previous = await CalculateStrategicKPIsAsync(previousStart, previousEnd);

            var comparison = new Dictionary<string, (decimal, decimal, double)>();

            foreach (var key in current.Keys)
            {
                if (previous.ContainsKey(key))
                {
                    var currValue = current[key];
                    var prevValue = previous[key];

                    // FIX CS0019: keep arithmetic in decimal, convert only the final result
                    double changePercent = prevValue != 0m
                        ? Convert.ToDouble((currValue - prevValue) / prevValue * 100m)
                        : 0.0;

                    comparison[key] = (currValue, prevValue, changePercent);
                }
            }

            return comparison;
        }

        public async Task<(double score, string status, List<string> insights)> CalculateHealthScoreAsync(DateTime fromDate)
        {
            var today = DateTime.Today;
            var kpis  = await CalculateStrategicKPIsAsync(fromDate, today);

            double score    = 50;
            var    insights = new List<string>();

            if (kpis.ContainsKey("NetProfit") && kpis["NetProfit"] > 0m)
            {
                score += 15;
                insights.Add("Profitable operations");
            }
            else
            {
                score -= 15;
                insights.Add("Negative profitability requires attention");
            }

            if (kpis.ContainsKey("GrossMarginPercent") && kpis["GrossMarginPercent"] >= 30m)
            {
                score += 10;
                insights.Add("Healthy gross margin");
            }
            else if (kpis.ContainsKey("GrossMarginPercent") && kpis["GrossMarginPercent"] < 20m)
            {
                score -= 10;
                insights.Add("Low gross margin - review pricing and COGS");
            }

            if (kpis.ContainsKey("OperationalEfficiency") && kpis["OperationalEfficiency"] >= 30m)
            {
                score += 10;
                insights.Add("Good operational efficiency");
            }
            else if (kpis.ContainsKey("OperationalEfficiency") && kpis["OperationalEfficiency"] < 15m)
            {
                score -= 10;
                insights.Add("Expenses are too high relative to revenue");
            }

            if (kpis.ContainsKey("ROI") && kpis["ROI"] >= 50m)
            {
                score += 10;
                insights.Add("Strong return on investment");
            }

            score = Math.Max(0, Math.Min(100, score));

            var status = score >= 70 ? "Excellent"
                       : score >= 50 ? "Good"
                       : score >= 30 ? "Fair"
                       : "Poor";

            return (score, status, insights);
        }

        public async Task<List<(string metric, decimal value, string anomalyType)>> DetectAnomaliesAsync(
            DateTime fromDate, double deviationThreshold = 2.0)
        {
            var anomalies = new List<(string, decimal, string)>();

            var dailyRevenues = await _context.Revenues
                .Where(r => !r.IsDeleted && r.TransactionDate >= fromDate)
                .GroupBy(r => r.TransactionDate.Date)
                .Select(g => new { Date = g.Key, Total = g.Sum(r => r.Amount) })
                .OrderBy(x => x.Date)
                .ToListAsync();

            if (dailyRevenues.Count < 3)
                return anomalies;

            var values = dailyRevenues.Select(x => (double)x.Total).ToList();
            var mean   = values.Average();
            var stdDev = Math.Sqrt(values.Average(x => Math.Pow(x - mean, 2)));

            foreach (var day in dailyRevenues)
            {
                var deviation = Math.Abs((double)day.Total - mean) / (stdDev > 0 ? stdDev : 1);

                if (deviation > deviationThreshold)
                {
                    var anomalyType = day.Total > (decimal)mean ? "spike" : "dip";
                    anomalies.Add(($"Revenue_{day.Date:yyyy-MM-dd}", day.Total, anomalyType));
                }
            }

            return anomalies;
        }
    }
}