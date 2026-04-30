using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRLFruitstandESS.Models
{
    public class PettyCashClaim
    {
        public int Id { get; set; }

        [Required][StringLength(100)]
        public string Description { get; set; } = string.Empty;

        [Required][StringLength(50)]
        public string Category { get; set; } = string.Empty;

        [Required][Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public string SubmittedBy { get; set; } = string.Empty;
        public string SubmittedByName { get; set; } = string.Empty;
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        // Approval workflow
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
        public string? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewNotes { get; set; }
    }
}
