// Models/Sale.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CRLFruitstandESS.Models
{
    public class Sale
    {
        public int Id { get; set; }

        [Required]
        public string CashierId { get; set; } = string.Empty;

        [ForeignKey("CashierId")]
        public ApplicationUser Cashier { get; set; } = null!;

        public DateTime SaleDate { get; set; } = DateTime.Now;

        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalAmount { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal AmountPaid { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Change { get; set; }

        public string Status { get; set; } = "Completed"; // Completed, Voided

        public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();
    }
}