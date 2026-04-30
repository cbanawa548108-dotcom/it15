namespace CRLFruitstandESS.Models.ViewModels
{
    // ── Seed data passed to the view for initial render
    public class AnalyticsViewModel
    {
        // Period filter
        public DateTime DateFrom { get; set; } = DateTime.Today.AddDays(-29);
        public DateTime DateTo   { get; set; } = DateTime.Today;
        public string   Period   { get; set; } = "30d";

        // KPI tiles
        public decimal TotalRevenue    { get; set; }
        public decimal TotalExpenses   { get; set; }
        public decimal NetProfit       { get; set; }
        public decimal GrossMargin     { get; set; }
        public int     TotalSales      { get; set; }
        public decimal AvgOrderValue   { get; set; }
        public int     TotalProducts   { get; set; }
        public int     LowStockCount   { get; set; }

        // Revenue vs Expense trend (daily)
        public List<string>  TrendLabels   { get; set; } = new();
        public List<decimal> TrendRevenue  { get; set; } = new();
        public List<decimal> TrendExpenses { get; set; } = new();

        // Expense breakdown by category (pie)
        public List<string>  ExpenseCatLabels { get; set; } = new();
        public List<decimal> ExpenseCatValues { get; set; } = new();

        // Revenue breakdown by category (pie)
        public List<string>  RevenueCatLabels { get; set; } = new();
        public List<decimal> RevenueCatValues { get; set; } = new();

        // Top products by sales qty (bar)
        public List<string>  TopProductLabels { get; set; } = new();
        public List<int>     TopProductQty    { get; set; } = new();
        public List<decimal> TopProductRev    { get; set; } = new();

        // Monthly profit trend (line, last 6 months)
        public List<string>  MonthlyLabels  { get; set; } = new();
        public List<decimal> MonthlyRevenue { get; set; } = new();
        public List<decimal> MonthlyExpense { get; set; } = new();
        public List<decimal> MonthlyProfit  { get; set; } = new();

        // Stock levels (horizontal bar)
        public List<string> StockLabels  { get; set; } = new();
        public List<int>    StockQty     { get; set; } = new();
        public List<int>    StockReorder { get; set; } = new();
    }

    // ── Drill-down response shapes (returned as JSON from API)
    public class DrillDownResult
    {
        public string Title  { get; set; } = string.Empty;
        public string Type   { get; set; } = "bar"; // bar | line | pie | table
        public List<string>        Labels   { get; set; } = new();
        public List<DrillDataset>  Datasets { get; set; } = new();
        public List<DrillRow>      Rows     { get; set; } = new();
    }

    public class DrillDataset
    {
        public string         Label           { get; set; } = string.Empty;
        public List<decimal>  Data            { get; set; } = new();
        public string         BackgroundColor { get; set; } = "#3b82f6";
        public string         BorderColor     { get; set; } = "#3b82f6";
    }

    public class DrillRow
    {
        public string Date     { get; set; } = string.Empty;
        public string Label    { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Amount  { get; set; }
        public string By       { get; set; } = string.Empty;
    }
}
