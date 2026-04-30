using System.ComponentModel.DataAnnotations;

namespace CRLFruitstandESS.Models.Executive
{
    public class SavedScenario
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string ScenarioName { get; set; } = string.Empty;

        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        // Scenario parameters
        public double RevenueGrowthPercent { get; set; }

        public double ExpenseIncreasePercent { get; set; }

        public double CostOfGoodsSoldPercent { get; set; }

        public int SimulationMonths { get; set; } = 12;

        // Metadata
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        public bool IsPublished { get; set; } = false;

        // Results stored as JSON for quick retrieval
        [StringLength(5000)]
        public string? ResultsJson { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }
    }
}
