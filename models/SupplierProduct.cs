using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRLFruitstandESS.Models
{
    public class SupplierProduct
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SupplierId { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? SupplierPrice { get; set; }

        [StringLength(50)]
        public string? SupplierSku { get; set; }

        public int? LeadTimeDays { get; set; }

        public int? MinOrderQuantity { get; set; }

        public bool IsPreferredSupplier { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey("SupplierId")]
        public virtual Supplier Supplier { get; set; } = null!;

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; } = null!;
    }
}