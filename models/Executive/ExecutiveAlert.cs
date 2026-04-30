using System.ComponentModel.DataAnnotations;

namespace CRLFruitstandESS.Models.Executive
{
    public class ExecutiveAlert
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(500)]
        public string Message { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Severity { get; set; } = "info"; // critical, warning, info

        [StringLength(50)]
        public string Category { get; set; } = string.Empty; // revenue, expenses, profitability, inventory

        [StringLength(50)]
        public string Icon { get; set; } = "alert-circle";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? ResolvedAt { get; set; }

        [StringLength(100)]
        public string? AcknowledgedBy { get; set; }

        public bool IsResolved { get; set; } = false;

        [StringLength(500)]
        public string? Notes { get; set; }
    }
}
