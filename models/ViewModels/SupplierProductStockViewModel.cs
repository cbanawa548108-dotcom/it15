using System;
using System.ComponentModel.DataAnnotations;

namespace CRLFruitstandESS.Models.ViewModels
{
    // ============================================
    // VIEW MODEL FOR SUPPLIER DELIVERED PRODUCTS
    // ============================================
    public class SupplierProductStockViewModel
    {
        public int DeliveryId { get; set; }
        public int SupplierId { get; set; }
        public int ProductId { get; set; }
        
        public string? SupplierName { get; set; }
        public string? ProductName { get; set; }
        public string? Category { get; set; }
        public string? Emoji { get; set; }
        
        public int Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }
        
        public DateTime DeliveryDate { get; set; }
        public string? ReceivedBy { get; set; }
        public string? ReferenceNumber { get; set; }
        public string? Notes { get; set; }
        
        public int? LeadTimeDays { get; set; }
        public int? MinOrderQuantity { get; set; }
        public bool IsPreferredSupplier { get; set; }
        
        public int? CurrentStock { get; set; }
    }
}
