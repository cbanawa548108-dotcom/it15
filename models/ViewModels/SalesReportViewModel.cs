using System;
using System.Collections.Generic;

namespace CRLFruitstandESS.Models.ViewModels
{
    public class SalesReportViewModel
    {
        public string Period { get; set; } = "daily"; // daily, weekly, monthly
        public decimal TotalRevenue { get; set; }
        public int TotalTransactions { get; set; }
        public decimal AverageTransactionValue { get; set; }
        public BestDayData? BestDay { get; set; }
        public List<SalesProductData> TopProducts { get; set; } = new();
        public List<SalesProductData> LowProducts { get; set; } = new();
        public List<SalesTrendData> Trends { get; set; } = new();

        // Nested classes
        public class BestDayData
        {
            public DateTime Date { get; set; }
            public decimal Revenue { get; set; }
        }

        public class SalesProductData
        {
            public int ProductId { get; set; }
            public string ProductName { get; set; } = string.Empty;
            public string Emoji { get; set; } = string.Empty;
            public int QuantitySold { get; set; }
            public decimal Revenue { get; set; }
        }

        public class SalesTrendData
        {
            public DateTime Date { get; set; }
            public decimal Revenue { get; set; }
            public int TransactionCount { get; set; }
        }
    }
}
