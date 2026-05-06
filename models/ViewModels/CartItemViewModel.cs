// ViewModels/CartItemViewModel.cs
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CRLFruitstandESS.Models.ViewModels
{
    public class CartItemViewModel
    {
        [JsonPropertyName("productId")]
        [Range(1, int.MaxValue, ErrorMessage = "Invalid product.")]
        public int ProductId { get; set; }

        [JsonPropertyName("productName")]
        public string ProductName { get; set; } = string.Empty;

        [JsonPropertyName("unitPrice")]
        [Range(0.01, 9999999, ErrorMessage = "Unit price must be greater than zero.")]
        public decimal UnitPrice { get; set; }

        [JsonPropertyName("quantity")]
        [Range(1, 10000, ErrorMessage = "Quantity must be between 1 and 10,000.")]
        public int Quantity { get; set; }

        public decimal Subtotal => UnitPrice * Quantity;
    }

    public class ProcessSaleViewModel
    {
        [JsonPropertyName("items")]
        [Required(ErrorMessage = "Cart cannot be empty.")]
        public List<CartItemViewModel>? Items { get; set; }

        [JsonPropertyName("totalAmount")]
        [Range(0.01, 9999999, ErrorMessage = "Total amount must be greater than zero.")]
        public decimal TotalAmount  { get; set; }

        [JsonPropertyName("amountPaid")]
        [Range(0, 9999999, ErrorMessage = "Amount paid cannot be negative.")]
        public decimal AmountPaid   { get; set; }

        [JsonPropertyName("paymentMethod")]
        [Required]
        [StringLength(20)]
        public string  PaymentMethod { get; set; } = "cash";
    }

    public class DigitalPaymentRequest
    {
        [Range(0.01, 9999999, ErrorMessage = "Amount must be greater than zero.")]
        public decimal Amount    { get; set; }

        [Required(ErrorMessage = "Payment method is required.")]
        [RegularExpression("^(gcash|paymaya|maya)$", ErrorMessage = "Method must be gcash or paymaya.")]
        public string  Method    { get; set; } = "gcash";

        [Required(ErrorMessage = "Sale data is required.")]
        public string  SaleDataJson { get; set; } = string.Empty;
    }

    public class ReceiptViewModel
    {
        public int SaleId { get; set; }
        public string CashierName { get; set; } = string.Empty;
        public DateTime SaleDate { get; set; }
        public List<SaleItem> Items { get; set; } = new();
        public decimal TotalAmount { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal Change { get; set; }
        public string? PaymentMethod { get; set; }
    }

    public class DailySummaryViewModel
    {
        public DateTime Date { get; set; }
        public int TotalTransactions { get; set; }
        public decimal TotalSales { get; set; }
        public List<Sale> Sales { get; set; } = new();
    }

    public class VoidSaleRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "Invalid sale ID.")]
        public int SaleId { get; set; }

        [Required(ErrorMessage = "A reason is required to void a sale.")]
        [StringLength(500, MinimumLength = 3, ErrorMessage = "Reason must be between 3 and 500 characters.")]
        public string Reason { get; set; } = "Customer Request";
    }
}