using System.ComponentModel.DataAnnotations;
using CRLFruitstandESS.Models;

namespace CRLFruitstandESS.Models.ViewModels
{
    // ── Main Dashboard ──────────────────────────
    public class CfoDashboardViewModel
    {
        public string FullName { get; set; } = string.Empty;
        public DateTime LastLoginAt { get; set; }

        // Today's figures
        public decimal TodayRevenue { get; set; }
        public decimal TodayExpenses { get; set; }
        public decimal TodayNetProfit { get; set; }

        // This month's figures
        public decimal MonthRevenue { get; set; }
        public decimal MonthExpenses { get; set; }
        public decimal MonthNetProfit { get; set; }
        public decimal MonthGrossMargin { get; set; }

        // Budget
        public decimal TotalBudgetAllocated { get; set; }
        public decimal TotalBudgetSpent { get; set; }
        public decimal BudgetUtilizationPercent { get; set; }

        // Charts (JSON-ready)
        public List<ChartDataPoint> WeeklyRevenue { get; set; } = new();
        public List<ChartDataPoint> WeeklyExpenses { get; set; } = new();
        public List<ChartDataPoint> MonthlyTrend { get; set; } = new();
        public List<PieDataPoint> ExpenseBreakdown { get; set; } = new();

        // Recent records
        public List<Revenue> RecentRevenues { get; set; } = new();
        public List<Expense> RecentExpenses { get; set; } = new();
        public List<Budget> ActiveBudgets { get; set; } = new();
    }

    public class ChartDataPoint
    {
        public string Label { get; set; } = string.Empty;
        public decimal Value { get; set; }
    }

    public class PieDataPoint
    {
        public string Label { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public string Color { get; set; } = string.Empty;
    }

    // ── Revenue ─────────────────────────────────
    public class RevenueViewModel
    {
        public List<Revenue> Revenues { get; set; } = new();
        public decimal TotalRevenue { get; set; }
        public string FilterPeriod { get; set; } = "month";
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
    }

    public class RevenueFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Source is required.")]
        [StringLength(100)]
        public string Source { get; set; } = string.Empty;

        [Required(ErrorMessage = "Category is required.")]
        public string Category { get; set; } = "Sales";

        [Required(ErrorMessage = "Amount is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Date is required.")]
        public DateTime TransactionDate { get; set; } = DateTime.Today;

        public string? Notes { get; set; }
    }

    // ── Expense ──────────────────────────────────
    public class ExpenseViewModel
    {
        public List<Expense> Expenses { get; set; } = new();
        public decimal TotalExpenses { get; set; }
        public string FilterPeriod { get; set; } = "month";
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
    }

    public class ExpenseFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Description is required.")]
        [StringLength(100)]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "Category is required.")]
        public string Category { get; set; } = string.Empty;

        [Required(ErrorMessage = "Amount is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Date is required.")]
        public DateTime ExpenseDate { get; set; } = DateTime.Today;

        public string? Supplier { get; set; }
        public string? Notes { get; set; }
    }

    // ── Budget ───────────────────────────────────
    public class BudgetViewModel
    {
        public List<Budget> Budgets { get; set; } = new();
        public decimal TotalAllocated { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal TotalRemaining { get; set; }
        public int FilterMonth { get; set; } = DateTime.Today.Month;
        public int FilterYear { get; set; } = DateTime.Today.Year;
    }

    public class BudgetFormViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Title is required.")]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Category is required.")]
        public string Category { get; set; } = string.Empty;

        [Required(ErrorMessage = "Allocated amount is required.")]
        [Range(1, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
        public decimal AllocatedAmount { get; set; }

        [Required]
        [Range(1, 12)]
        public int Month { get; set; } = DateTime.Today.Month;

        [Required]
        public int Year { get; set; } = DateTime.Today.Year;

        public string? Notes { get; set; }
    }

    // ── Reports ──────────────────────────────────
    public class FinancialReportViewModel
    {
        public string ReportType { get; set; } = "Monthly";
        public DateTime? ReportDate { get; set; }
        public int ReportMonth { get; set; } = DateTime.Today.Month;
        public int ReportYear { get; set; } = DateTime.Today.Year;

        public decimal TotalRevenue { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetProfit { get; set; }
        public decimal GrossMarginPercent { get; set; }

        public List<Revenue> Revenues { get; set; } = new();
        public List<Expense> Expenses { get; set; } = new();
        public List<ExpenseCategorySummary> ExpenseByCategory { get; set; } = new();
        public List<DailyFinancialSummary> DailyBreakdown { get; set; } = new();
    }

    public class ExpenseCategorySummary
    {
        public string Category { get; set; } = string.Empty;
        public decimal Total { get; set; }
        public decimal Percent { get; set; }
    }

    public class DailyFinancialSummary
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
        public decimal Expenses { get; set; }
        public decimal NetProfit { get; set; }
    }
}