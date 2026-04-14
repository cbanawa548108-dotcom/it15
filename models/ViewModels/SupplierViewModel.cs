namespace CRLFruitstandESS.Models.ViewModels
{
    public class SupplierViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ContactPerson { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public bool IsActive { get; set; }
        public int ProductCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SupplierDetailViewModel
    {
        public Models.Supplier Supplier { get; set; } = null!;
        public List<SupplierProductDetailViewModel> Products { get; set; } = new List<SupplierProductDetailViewModel>();
    }

    public class SupplierProductDetailViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? Category { get; set; }
        public decimal? SupplierPrice { get; set; }
        public string? SupplierSku { get; set; }
        public int? LeadTimeDays { get; set; }
        public bool IsPreferredSupplier { get; set; }
    }

    public class ValuationReportViewModel
    {
        public decimal TotalInventoryValue { get; set; }
        public decimal TotalCostValue { get; set; }
        public decimal PotentialProfit { get; set; }
        public int TotalProducts { get; set; }
        public int TotalUnits { get; set; }
        public List<CategoryValuationViewModel> CategoryBreakdown { get; set; } = new List<CategoryValuationViewModel>();
        public List<ProductValuationViewModel> TopValuedProducts { get; set; } = new List<ProductValuationViewModel>();
    }

    public class CategoryValuationViewModel
    {
        public string Category { get; set; } = string.Empty;
        public int ProductCount { get; set; }
        public int TotalUnits { get; set; }
        public decimal InventoryValue { get; set; }
        public decimal CostValue { get; set; }
        public decimal Profit { get; set; }
    }

    public class ProductValuationViewModel
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Emoji { get; set; }
        public string Category { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalValue { get; set; }
        public decimal TotalCost { get; set; }
        public decimal Profit { get; set; }
    }
}