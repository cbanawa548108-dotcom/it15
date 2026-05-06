// Models/Inventory.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRLFruitstandESS.Models
{
    public class Inventory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }

        [Required]
        public int Quantity { get; set; }

        public int MinStockLevel { get; set; } = 10;

        public int MaxStockLevel { get; set; } = 1000;

        public int ReorderPoint { get; set; } = 20;

        [StringLength(50)]
        public string Location { get; set; } = "Main Warehouse";

        public DateTime LastUpdated { get; set; } = DateTime.Now;

        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; } = null!;
    }
}