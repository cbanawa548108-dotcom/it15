using System.ComponentModel.DataAnnotations;

namespace CRLFruitstandESS.Models.Executive
{
    public class KPITarget
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string KPIName { get; set; } = string.Empty; // Revenue, Profit, Margin, Efficiency, etc

        public decimal TargetValue { get; set; }

        [StringLength(20)]
        public string Unit { get; set; } = string.Empty; // %, amount, etc

        public int Month { get; set; } // 1-12

        public int Year { get; set; }

        [StringLength(50)]
        public string Status { get; set; } = "on-track"; // on-track, at-risk, critical

        public decimal? CurrentValue { get; set; }

        public decimal? VariancePercent { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Notes { get; set; }
    }
}
