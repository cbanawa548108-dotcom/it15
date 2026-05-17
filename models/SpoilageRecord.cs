namespace CRLFruitstandESS.Models
{
    /// <summary>
    /// Records spoilage events — manual write-offs and AI-predicted spoilage.
    /// IsSellable = true means the item is degraded but still sellable at a discount.
    /// </summary>
    public class SpoilageRecord
    {
        public int      Id            { get; set; }
        public int      ProductId     { get; set; }
        public Product? Product       { get; set; }
        public int      Quantity      { get; set; }
        public decimal  EstimatedLoss { get; set; }  // Quantity × CostPrice
        public string   Reason        { get; set; } = "Overripe";  // Overripe | Damaged | Expired | Pest | Other
        public string   RecordedBy    { get; set; } = string.Empty;
        public DateTime RecordedAt    { get; set; } = DateTime.Now;
        public string?  Notes         { get; set; }

        // NEW: Manual = staff recorded it; Predicted = system predicted based on stock age
        public string   SpoilageType  { get; set; } = "Manual";  // Manual | Predicted

        // NEW: true = item is degraded but still sellable at a discount price
        public bool     IsSellable    { get; set; } = false;

        // NEW: discounted sell price per unit (set when IsSellable = true)
        public decimal  DiscountedPrice { get; set; } = 0m;

        // NEW: track if sellable spoilage has been sold
        public bool     IsSold        { get; set; } = false;
        public DateTime? SoldAt       { get; set; }
        public int      SoldQuantity  { get; set; } = 0;
        public decimal  SoldRevenue   { get; set; } = 0m;

        /// <summary>Payment method used when sold: cash | gcash | paymaya</summary>
        public string?  PaymentMethod { get; set; }
    }
}
