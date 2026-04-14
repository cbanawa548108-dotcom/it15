using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRLFruitstandESS.Models
{
    public enum MovementType
    {
        StockIn,
        StockOut,
        Sale,
        Return,
        Adjustment,
        Damaged
    }

    public class StockMovement
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        public MovementType Type { get; set; }

        [Required]
        public int Quantity { get; set; }

        public int PreviousStock { get; set; }

        public int NewStock { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }  // Made nullable

        [StringLength(100)]
        public string? ReferenceNumber { get; set; }  // Made nullable

        [StringLength(100)]
        public string? PerformedBy { get; set; }  // Made nullable

        public DateTime MovementDate { get; set; } = DateTime.Now;

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; } = null!;
    }
}