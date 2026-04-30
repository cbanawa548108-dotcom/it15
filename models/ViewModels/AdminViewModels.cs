using System.ComponentModel.DataAnnotations;

namespace CRLFruitstandESS.Models.ViewModels
{
    // ── USER MANAGEMENT ──────────────────────────
    public class UserListViewModel
    {
        public string Id          { get; set; } = string.Empty;
        public string UserName    { get; set; } = string.Empty;
        public string FullName    { get; set; } = string.Empty;
        public string Email       { get; set; } = string.Empty;
        public string Department  { get; set; } = string.Empty;
        public bool   IsActive    { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public List<string> Roles { get; set; } = new();
    }

    public class CreateUserViewModel
    {
        [Required][StringLength(50)]
        public string UserName   { get; set; } = string.Empty;

        [Required][StringLength(100)]
        public string FullName   { get; set; } = string.Empty;

        [Required][EmailAddress]
        public string Email      { get; set; } = string.Empty;

        [StringLength(100)]
        public string Department { get; set; } = string.Empty;

        [Required][StringLength(100, MinimumLength = 8)]
        public string Password   { get; set; } = string.Empty;

        [Required][Compare("Password")]
        public string ConfirmPassword { get; set; } = string.Empty;

        public string Role       { get; set; } = "Cashier";
        public bool   IsActive   { get; set; } = true;
    }

    public class EditUserViewModel
    {
        public string Id         { get; set; } = string.Empty;

        [Required][StringLength(100)]
        public string FullName   { get; set; } = string.Empty;

        [Required][EmailAddress]
        public string Email      { get; set; } = string.Empty;

        [StringLength(100)]
        public string Department { get; set; } = string.Empty;

        public string Role       { get; set; } = string.Empty;
        public bool   IsActive   { get; set; } = true;

        [StringLength(100, MinimumLength = 8)]
        public string? NewPassword { get; set; }
    }

    // ── PRODUCT MANAGEMENT ───────────────────────
    public class ProductFormViewModel
    {
        public int    Id          { get; set; }

        [Required][StringLength(100)]
        public string Name        { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required][StringLength(50)]
        public string Category    { get; set; } = string.Empty;

        [Required][Range(0.01, double.MaxValue)]
        public decimal Price      { get; set; }

        [Range(0, double.MaxValue)]
        public decimal CostPrice  { get; set; }

        public string? Emoji      { get; set; } = "📦";
        public bool    IsActive   { get; set; } = true;

        // Inventory
        public int InitialStock   { get; set; } = 0;
        public int MinStockLevel  { get; set; } = 10;
        public int ReorderPoint   { get; set; } = 20;
    }

    // ── SUPPLIER MANAGEMENT ──────────────────────
    public class SupplierFormViewModel
    {
        public int    Id            { get; set; }

        [Required][StringLength(100)]
        public string Name          { get; set; } = string.Empty;

        [StringLength(100)]
        public string? ContactPerson { get; set; }

        [StringLength(20)]
        public string? Phone        { get; set; }

        [EmailAddress][StringLength(100)]
        public string? Email        { get; set; }

        [StringLength(500)]
        public string? Address      { get; set; }

        [StringLength(100)]
        public string? City         { get; set; }

        [StringLength(50)]
        public string? TaxId        { get; set; }

        public bool IsActive        { get; set; } = true;
    }

    // ── INVENTORY MANAGEMENT ─────────────────────
    public class AdminStockAdjustViewModel
    {
        [Required]
        public int    ProductId   { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int    CurrentQty  { get; set; }

        [Required][Range(1, 10000)]
        public int    Quantity    { get; set; }

        [Required]
        public string Type        { get; set; } = "StockIn"; // StockIn | StockOut | Adjustment

        [StringLength(500)]
        public string? Notes      { get; set; }

        [StringLength(100)]
        public string? Reference  { get; set; }
    }
}
