using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRLFruitstandESS.Models
{
    public class Budget
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Category { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal AllocatedAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal SpentAmount { get; set; } = 0;

        [Required]
        public int Month { get; set; }

        [Required]
        public int Year { get; set; }

        [StringLength(255)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string CreatedBy { get; set; } = string.Empty;

        [NotMapped]
        public decimal RemainingAmount => AllocatedAmount - SpentAmount;

        [NotMapped]
        public decimal UtilizationPercent =>
            AllocatedAmount > 0 ? (SpentAmount / AllocatedAmount) * 100 : 0;
    }
}