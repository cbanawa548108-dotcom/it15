using System.ComponentModel.DataAnnotations;

namespace CRLFruitstandESS.Models.Executive
{
    public class RiskRegister
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string RiskName { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [StringLength(50)]
        public string Category { get; set; } = string.Empty; // Revenue, Operational, Profitability, Liquidity, External

        // Risk assessment
        public double Probability { get; set; } // 0-1 scale

        public double Impact { get; set; } // 0-1 scale

        public double RiskScore { get; set; } // Calculated: (Prob × Impact × 0.6) + (Volatility × 0.3) + (Trend × 0.1)

        public double? Volatility { get; set; } // Standard deviation or coefficient of variation

        public double? Trend { get; set; } // Velocity of change

        // Mitigation
        [StringLength(500)]
        public string MitigationStrategy { get; set; } = string.Empty;

        [StringLength(100)]
        public string Owner { get; set; } = string.Empty;

        public DateTime? TargetResolutionDate { get; set; }

        // Status tracking
        [StringLength(50)]
        public string Status { get; set; } = "identified"; // identified, in-progress, mitigated, closed

        [StringLength(20)]
        public string Priority { get; set; } = "medium"; // critical, high, medium, low

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime LastAssessedDate { get; set; } = DateTime.UtcNow;

        [StringLength(500)]
        public string? Notes { get; set; }
    }
}
