using CRLFruitstandESS.Models;

namespace CRLFruitstandESS.Models.ViewModels
{
    public class ReportingViewModel
    {
        // Filters
        public DateTime DateFrom { get; set; } = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        public DateTime DateTo   { get; set; } = DateTime.Today;
        public string   ReportType { get; set; } = "financial";

        // Financial Summary
        public decimal TotalRevenue  { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetProfit     { get; set; }
        public decimal GrossMargin   { get; set; }

        // Transaction History
        public List<Revenue>         Revenues         { get; set; } = new();
        public List<Expense>         Expenses         { get; set; } = new();
        public List<Sale>            Sales            { get; set; } = new();
        public List<StockMovement>   StockMovements   { get; set; } = new();
        public List<SupplierPayment> SupplierPayments { get; set; } = new();
        public List<SupplierDelivery> SupplierDeliveries { get; set; } = new();

        // Audit
        public List<AuditEntry> AuditLog { get; set; } = new();

        // Chart data
        public List<(string Label, decimal Revenue, decimal Expense)> DailyTrend { get; set; } = new();
        public List<(string Category, decimal Amount)> ExpenseBreakdown { get; set; } = new();
        public List<(string Category, decimal Amount)> RevenueBreakdown { get; set; } = new();
    }

    public class AuditEntry
    {
        public DateTime Timestamp   { get; set; }
        public string   User        { get; set; } = string.Empty;
        public string   Action      { get; set; } = string.Empty;
        public string   Module      { get; set; } = string.Empty;
        public string   Description { get; set; } = string.Empty;
        public string   Severity    { get; set; } = "info"; // info, warning, critical
    }
}
