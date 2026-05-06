using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRLFruitstandESS.Models
{
    public class SupplierPayment
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int SupplierId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.Now;

        [StringLength(50)]
        public string PaymentMethod { get; set; } = string.Empty;

        [StringLength(100)]
        public string ReferenceNumber { get; set; } = string.Empty;

        [StringLength(500)]
        public string Notes { get; set; } = string.Empty;

        [StringLength(100)]
        public string PaidBy { get; set; } = string.Empty;

        // NEW: Track if this is a pending payment from delivery
        public bool IsPending { get; set; } = false;

        // NEW: Link to the delivery that created this payment
        public int? SourceDeliveryId { get; set; }

        [ForeignKey("SupplierId")]
        public virtual Supplier Supplier { get; set; } = null!;
    }
}