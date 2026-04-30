using System.ComponentModel.DataAnnotations;

namespace CRLFruitstandESS.Models.ViewModels
{
    public class StockInOutViewModel
    {
        [Required]
        public int ProductId { get; set; }

        public string? ProductName { get; set; }

        public int? SupplierId { get; set; }

        public int CurrentStock { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }

        [StringLength(100)]
        public string? ReferenceNumber { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public bool IsStockIn { get; set; }
    }
}