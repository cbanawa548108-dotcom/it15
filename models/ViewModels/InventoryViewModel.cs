// Models/ViewModels/InventoryViewModel.cs
namespace CRLFruitstandESS.Models.ViewModels
{
    public class InventoryViewModel
    {
        public int ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? Category { get; set; }
        public string? Emoji { get; set; }
        public int CurrentStock { get; set; }
        public int MinStockLevel { get; set; }
        public int MaxStockLevel { get; set; }
        public int ReorderPoint { get; set; }
        public string? Location { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal CostPrice { get; set; }
        public decimal InventoryValue { get; set; }
        public string? StockStatus { get; set; }
        public int DaysUntilStockout { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class InventoryDashboardViewModel
    {
        public int TotalProducts { get; set; }
        public int TotalStockItems { get; set; }
        public decimal TotalInventoryValue { get; set; }
        public int LowStockCount { get; set; }
        public int OutOfStockCount { get; set; }
        public int CriticalStockCount { get; set; }
        public List<InventoryViewModel>? InventoryItems { get; set; }
        public List<InventoryViewModel>? LowStockAlerts { get; set; }
    }

    public class StockMovementViewModel
    {
        public int Id { get; set; }
        public string? ProductName { get; set; }
        public string? Type { get; set; }
        public int Quantity { get; set; }
        public int PreviousStock { get; set; }
        public int NewStock { get; set; }
        public string? Notes { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? PerformedBy { get; set; }
        public DateTime MovementDate { get; set; }
    }
}