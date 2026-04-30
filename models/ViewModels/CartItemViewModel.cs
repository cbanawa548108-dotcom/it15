// ViewModels/CartItemViewModel.cs
namespace CRLFruitstandESS.Models.ViewModels
{
    public class CartItemViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal Subtotal => UnitPrice * Quantity;
    }

    public class ProcessSaleViewModel
    {
        public List<CartItemViewModel>? Items { get; set; }
        public decimal TotalAmount  { get; set; }
        public decimal AmountPaid   { get; set; }
        public string  PaymentMethod { get; set; } = "cash"; // cash | gcash | paymaya | card
    }

    public class DigitalPaymentRequest
    {
        public decimal Amount    { get; set; }
        public string  Method    { get; set; } = "gcash"; // gcash | paymaya
        public string  SaleDataJson { get; set; } = string.Empty;
    }

    public class ReceiptViewModel
    {
        public int SaleId { get; set; }
        public string CashierName { get; set; }
        public DateTime SaleDate { get; set; }
        public List<SaleItem> Items { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal Change { get; set; }
    }

    public class DailySummaryViewModel
    {
        public DateTime Date { get; set; }
        public int TotalTransactions { get; set; }
        public decimal TotalSales { get; set; }
        public List<Sale> Sales { get; set; }
    }
}