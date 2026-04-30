using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRLFruitstandESS.Models
{
    public class PaymentTransaction
    {
        public int Id { get; set; }

        public int? SaleId { get; set; }

        [Required][StringLength(50)]
        public string Method { get; set; } = string.Empty; // cash | gcash | paymaya | card

        [Required][StringLength(50)]
        public string Status { get; set; } = "pending"; // pending | paid | failed | expired

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [StringLength(200)]
        public string? PayMongoIntentId { get; set; }

        [StringLength(200)]
        public string? PayMongoSourceId { get; set; }

        [StringLength(500)]
        public string? CheckoutUrl { get; set; }

        [StringLength(100)]
        public string? ReferenceNumber { get; set; }

        public string ProcessedBy { get; set; } = string.Empty;
        public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAt    { get; set; }

        [ForeignKey("SaleId")]
        public virtual Sale? Sale { get; set; }
    }
}
