using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRLFruitstandESS.Models
{
    public class FinancialReport
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string ReportType { get; set; } = string.Empty; // Daily, Monthly

        [Required]
        public DateTime ReportDate { get; set; }

        public int? ReportMonth { get; set; }
        public int? ReportYear { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalRevenue { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalExpenses { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetProfit { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal GrossMargin { get; set; }

        public string GeneratedBy { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        [StringLength(255)]
        public string? Notes { get; set; }
    }
}