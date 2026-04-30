using System.ComponentModel.DataAnnotations;

namespace CRLFruitstandESS.Models
{
    public class TrendReport
    {
        public int Id { get; set; }

        [Required][StringLength(150)]
        public string Title { get; set; } = string.Empty;

        [Required][StringLength(50)]
        public string ReportType { get; set; } = string.Empty; // Revenue, Expense, Sales, Inventory, Custom

        [StringLength(2000)]
        public string? Summary { get; set; }

        public string DataJson { get; set; } = "{}"; // serialized chart config

        public DateTime PeriodFrom { get; set; }
        public DateTime PeriodTo   { get; set; }

        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        // No delete — enforced at controller level
    }
}
