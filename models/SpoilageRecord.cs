namespace CRLFruitstandESS.Models
{
    /// <summary>
    /// Records actual spoilage events — when fruit is written off as unsellable.
    /// Used to track the real spoilage rate vs the 30.5% dataset benchmark.
    /// </summary>
    public class SpoilageRecord
    {
        public int      Id          { get; set; }
        public int      ProductId   { get; set; }
        public Product? Product     { get; set; }
        public int      Quantity    { get; set; }
        public decimal  EstimatedLoss { get; set; }  // Quantity × CostPrice
        public string   Reason      { get; set; } = "Overripe";  // Overripe | Damaged | Expired | Other
        public string   RecordedBy  { get; set; } = string.Empty;
        public DateTime RecordedAt  { get; set; } = DateTime.Now;
        public string?  Notes       { get; set; }
    }
}
