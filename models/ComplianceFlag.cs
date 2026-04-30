using System.ComponentModel.DataAnnotations;

namespace CRLFruitstandESS.Models
{
    public class ComplianceFlag
    {
        public int Id { get; set; }

        [Required][StringLength(100)]
        public string Module { get; set; } = string.Empty; // POS, Finance, Inventory

        [Required][StringLength(200)]
        public string Description { get; set; } = string.Empty;

        [StringLength(50)]
        public string Severity { get; set; } = "warning"; // info, warning, critical

        [StringLength(100)]
        public string? ReferenceId { get; set; } // e.g. SaleId, ExpenseId

        [StringLength(100)]
        public string? PolicyReference { get; set; } // e.g. "RA 10173 §12"

        public bool IsResolved { get; set; } = false;
        public string? ResolvedBy { get; set; }
        public DateTime? ResolvedAt { get; set; }

        public string FlaggedBy { get; set; } = "System";
        public DateTime FlaggedAt { get; set; } = DateTime.UtcNow;
    }
}
