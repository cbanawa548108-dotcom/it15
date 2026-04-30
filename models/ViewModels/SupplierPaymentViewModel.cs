using System;
using System.ComponentModel.DataAnnotations;

namespace CRLFruitstandESS.Models.ViewModels
{
    public class SupplierPaymentViewModel
    {
        public int Id { get; set; }
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string PaidBy { get; set; } = string.Empty;
        public bool IsPending { get; set; }
    }

    public class PaySupplierViewModel
    {
        [Required(ErrorMessage = "Please select a supplier")]
        [Display(Name = "Supplier")]
        public int SupplierId { get; set; }

        [Required(ErrorMessage = "Please enter payment amount")]
        [Range(0.01, 999999999.99, ErrorMessage = "Amount must be greater than 0")]
        [Display(Name = "Amount")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Please select payment method")]
        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = string.Empty;

        [Display(Name = "Reference Number")]
        [StringLength(100)]
        public string? ReferenceNumber { get; set; }

        [Display(Name = "Notes")]
        [StringLength(500)]
        public string? Notes { get; set; }
    }

    public class SupplierBalanceViewModel
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public decimal TotalDeliveries { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal BalanceDue { get; set; }
        public DateTime? LastDeliveryDate { get; set; }
        public DateTime? LastPaymentDate { get; set; }
    }
}