using System;
using System.Collections.Generic;

namespace CRLFruitstandESS.Models.ViewModels
{
    // ============================================
    // MAIN DASHBOARD VIEW MODEL
    // ============================================
    public class DashboardViewModel
    {
        // User Info
        public string FullName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public DateTime? LastLoginAt { get; set; }
        public string WelcomeMessage { get; set; } = string.Empty;

        // Today's Summary
        public decimal TodaysRevenue { get; set; }
        public decimal TodaysExpenses { get; set; }
        public decimal NetProfit => TodaysRevenue - TodaysExpenses;
        public int TodaysTransactions { get; set; }

        // Inventory Summary
        public int TotalProducts { get; set; }
        public int TotalStockItems { get; set; }
        public decimal TotalInventoryValue { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }
        public int CriticalStockCount { get; set; }

        // Financial Summary
        public decimal TotalRevenue { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal TotalProfit => TotalRevenue - TotalExpenses;
        public decimal CurrentMonthRevenue { get; set; }
        public decimal CurrentMonthExpenses { get; set; }
        public decimal WeeklyRevenue { get; set; }
        public decimal MonthlyRevenue { get; set; }

        // Supplier Payments (NEW)
        public decimal TotalOutstandingPayables { get; set; }
        public int SuppliersWithBalanceDue { get; set; }
        public List<SupplierPaymentDue> UpcomingPayments { get; set; } = new();
        public List<RecentSupplierPayment> RecentSupplierPayments { get; set; } = new();

        // Sales Analytics
        public List<TopSellingProduct> TopSellingProducts { get; set; } = new();
        public List<TopSellingProduct> LowPerformingProducts { get; set; } = new();
        public List<SalesTrend> SalesTrends { get; set; } = new();
        public List<CategoryPerformance> CategoryPerformance { get; set; } = new();

        // Inventory Forecasting (NEW)
        public List<InventoryForecast> LowStockForecasts { get; set; } = new();
        public List<ReorderSuggestion> ReorderSuggestions { get; set; } = new();

        // Alerts & Notifications
        public List<DashboardAlert> Alerts { get; set; } = new();
        public int UnreadNotifications { get; set; }

        // Stock Movement Views
        public List<StockMovementView> RecentStockMovements { get; set; } = new();

        // Quick Actions
        public List<QuickAction> QuickActions { get; set; } = new();

        // Audit Logs
        public List<AuditLog> AuditLogs { get; set; } = new();

        // KPI Monitoring (NEW)
        public double ProfitMargin { get; set; } // Percentage
        public double InventoryTurnoverRatio { get; set; }
        public double WeeklyGrowthRate { get; set; } // Percentage
        public double MonthlyGrowthRate { get; set; } // Percentage
        public int StockOutFrequency { get; set; } // Number of times items went out of stock
        public decimal AverageOrderValue { get; set; }
        public string BestSellingCategory { get; set; } = string.Empty;
        public string WorstSellingCategory { get; set; } = string.Empty;
        public int TotalStockMovements { get; set; }
        public decimal AverageDailySales { get; set; }
        public List<KPIMetric> KPIMetrics { get; set; } = new();
    }

    // ============================================
    // SUPPLIER PAYMENT DUE
    // ============================================
    public class SupplierPaymentDue
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public decimal BalanceDue { get; set; }
        public DateTime? LastDeliveryDate { get; set; }
        public int DaysSinceLastDelivery => LastDeliveryDate.HasValue 
            ? (DateTime.Now - LastDeliveryDate.Value).Days 
            : 0;
        public string Urgency => BalanceDue > 10000 ? "High" : BalanceDue > 5000 ? "Medium" : "Low";
    }

    // ============================================
    // RECENT SUPPLIER PAYMENT
    // ============================================
    public class RecentSupplierPayment
    {
        public int Id { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public bool IsPending { get; set; }
    }

    // ============================================
    // TOP SELLING PRODUCT
    // ============================================
    public class TopSellingProduct
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Emoji { get; set; } = string.Empty;
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
        public int CurrentStock { get; set; }
        public double PercentageOfTotalSales { get; set; }
    }

    // ============================================
    // SALES TREND (For Charts)
    // ============================================
    public class SalesTrend
    {
        public DateTime Date { get; set; }
        public decimal Revenue { get; set; }
        public decimal Expenses { get; set; }
        public int TransactionCount { get; set; }
    }

    // ============================================
    // CATEGORY PERFORMANCE
    // ============================================
    public class CategoryPerformance
    {
        public string Category { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public int ItemsSold { get; set; }
        public double PercentageOfTotalRevenue { get; set; }
    }

    // ============================================
    // INVENTORY FORECAST (ML/Algorithm)
    // ============================================
    public class InventoryForecast
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string Emoji { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        
        // Prediction Algorithm Results
        public double AverageDailySales { get; set; } // Moving average
        public int DaysUntilStockout { get; set; }
        public DateTime PredictedStockoutDate => DateTime.Now.AddDays(DaysUntilStockout);
        
        // Confidence Score (0-100%)
        public int PredictionConfidence { get; set; }
        
        public string Status => DaysUntilStockout switch
        {
            <= 3 => "Critical",
            <= 7 => "Warning",
            <= 14 => "Caution",
            _ => "Normal"
        };
    }

    // ============================================
    // REORDER SUGGESTION (EOQ Algorithm)
    // ============================================
    public class ReorderSuggestion
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int CurrentStock { get; set; }
        public int SuggestedOrderQuantity { get; set; } // EOQ formula
        public decimal EstimatedCost { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public DateTime SuggestedOrderDate { get; set; }
        
        // EOQ Formula: √(2DS/H)
        // D = Annual demand, S = Ordering cost, H = Holding cost per unit
    }

    // ============================================
    // DASHBOARD ALERT
    // ============================================
    public class DashboardAlert
    {
        public int Id { get; set; }
        public string Type { get; set; } = string.Empty; // "warning", "danger", "info", "success"
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? ActionUrl { get; set; }
        public string? ActionText { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
    }

    // ============================================
    // QUICK ACTION
    // ============================================
    public class QuickAction
    {
        public string Icon { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Color { get; set; } = "blue"; // blue, green, red, purple, orange
    }

    // ============================================
    // AUDIT LOG
    // ============================================
    public class AuditLog
    {
        public int Id { get; set; }
        public string Action    { get; set; } = string.Empty;
        public string Entity    { get; set; } = string.Empty;
        public int?   EntityId  { get; set; }
        public string User      { get; set; } = string.Empty;
        public DateTime? Timestamp { get; set; }
        public string? Details  { get; set; }
        public string  Module   { get; set; } = "System";
    }

    // ============================================
    // STOCK MOVEMENT VIEW
    // ============================================
    public class StockMovementView
    {
        public string ProductName { get; set; } = string.Empty;
        public string Emoji { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // "Stock In", "Stock Out"
        public int Quantity { get; set; }
        public DateTime Date { get; set; }
        public string Reference { get; set; } = string.Empty;
    }

    // ============================================
    // KPI METRIC
    // ============================================
    public class KPIMetric
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public double? NumericValue { get; set; }
        public string Unit { get; set; } = string.Empty;
        public double? ChangePercent { get; set; }
        public string Status { get; set; } = "neutral"; // "good", "warning", "danger", "neutral"
        public string Icon { get; set; } = "bi-graph-up";
        public string Color { get; set; } = "blue";
        public string Target { get; set; } = string.Empty;
        public string Trend { get; set; } = "stable"; // "increasing", "decreasing", "stable"
    }
}