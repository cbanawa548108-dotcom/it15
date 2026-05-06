using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRLFruitstandESS.Data;
using CRLFruitstandESS.Models;

namespace CRLFruitstandESS.Controllers.Api
{
    /// <summary>
    /// REST API for Inventory management — stock levels, movements, low-stock alerts,
    /// and inventory valuation.
    /// Accessible by Admin and Manager roles.
    /// </summary>
    [ApiController]
    [Route("api/inventory")]
    [Authorize(Roles = "Admin,Manager,CFO,CEO")]
    public class InventoryApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public InventoryApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ────────────────────────────────────────────────────────
        // GET /api/inventory/stock-levels
        // Current stock for every product.
        // ────────────────────────────────────────────────────────
        /// <summary>
        /// Returns current stock quantity, reorder point, and status for all products.
        /// </summary>
        [HttpGet("stock-levels")]
        public async Task<IActionResult> GetStockLevels([FromQuery] string? category)
        {
            var query = _context.Inventory
                .Include(i => i.Product)
                .Where(i => i.Product.IsActive);

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(i => i.Product.Category == category);

            var inventory = await query
                .OrderBy(i => i.Product.Category)
                .ThenBy(i => i.Product.Name)
                .ToListAsync();

            var data = inventory.Select(i => new
            {
                productId    = i.ProductId,
                productName  = i.Product.Name,
                category     = i.Product.Category,
                emoji        = i.Product.Emoji,
                quantity     = i.Quantity,
                minStock     = i.MinStockLevel,
                maxStock     = i.MaxStockLevel,
                reorderPoint = i.ReorderPoint,
                location     = i.Location,
                lastUpdated  = i.LastUpdated.ToString("yyyy-MM-dd HH:mm:ss"),
                stockStatus  = i.Quantity == 0 ? "Out of Stock"
                             : i.Quantity <= i.MinStockLevel ? "Critical"
                             : i.Quantity <= i.ReorderPoint  ? "Low"
                             : "OK",
                stockValue   = i.Quantity * i.Product.CostPrice
            });

            return Ok(new
            {
                success   = true,
                timestamp = DateTime.UtcNow,
                count     = inventory.Count,
                data
            });
        }

        // ────────────────────────────────────────────────────────
        // GET /api/inventory/low-stock
        // Products at or below their reorder point.
        // ────────────────────────────────────────────────────────
        /// <summary>
        /// Returns products that are at or below their reorder point,
        /// sorted by urgency (out-of-stock first).
        /// </summary>
        [HttpGet("low-stock")]
        public async Task<IActionResult> GetLowStock()
        {
            var inventory = await _context.Inventory
                .Include(i => i.Product)
                .Where(i => i.Product.IsActive && i.Quantity <= i.ReorderPoint)
                .OrderBy(i => i.Quantity)
                .ToListAsync();

            var data = inventory.Select(i => new
            {
                productId    = i.ProductId,
                productName  = i.Product.Name,
                category     = i.Product.Category,
                emoji        = i.Product.Emoji,
                quantity     = i.Quantity,
                reorderPoint = i.ReorderPoint,
                minStock     = i.MinStockLevel,
                shortage     = Math.Max(0, i.ReorderPoint - i.Quantity),
                stockStatus  = i.Quantity == 0 ? "Out of Stock"
                             : i.Quantity <= i.MinStockLevel ? "Critical"
                             : "Low",
                lastUpdated  = i.LastUpdated.ToString("yyyy-MM-dd HH:mm:ss")
            });

            return Ok(new
            {
                success          = true,
                timestamp        = DateTime.UtcNow,
                outOfStockCount  = inventory.Count(i => i.Quantity == 0),
                criticalCount    = inventory.Count(i => i.Quantity > 0 && i.Quantity <= i.MinStockLevel),
                lowCount         = inventory.Count(i => i.Quantity > i.MinStockLevel && i.Quantity <= i.ReorderPoint),
                data
            });
        }

        // ────────────────────────────────────────────────────────
        // GET /api/inventory/valuation
        // Total inventory value broken down by category.
        // ────────────────────────────────────────────────────────
        /// <summary>
        /// Returns total inventory value at cost price, grouped by category.
        /// </summary>
        [HttpGet("valuation")]
        public async Task<IActionResult> GetValuation()
        {
            var inventory = await _context.Inventory
                .Include(i => i.Product)
                .Where(i => i.Product.IsActive)
                .ToListAsync();

            var byCategory = inventory
                .GroupBy(i => i.Product.Category)
                .Select(g => new
                {
                    category      = g.Key,
                    productCount  = g.Count(),
                    totalQuantity = g.Sum(i => i.Quantity),
                    totalValue    = g.Sum(i => i.Quantity * i.Product.CostPrice)
                })
                .OrderByDescending(x => x.totalValue)
                .ToList();

            var grandTotal = byCategory.Sum(x => x.totalValue);

            return Ok(new
            {
                success      = true,
                timestamp    = DateTime.UtcNow,
                grandTotal,
                productCount = inventory.Count,
                byCategory   = byCategory.Select(x => new
                {
                    x.category,
                    x.productCount,
                    x.totalQuantity,
                    x.totalValue,
                    percentShare = grandTotal > 0
                        ? Math.Round(x.totalValue / grandTotal * 100, 2)
                        : 0m
                })
            });
        }

        // ────────────────────────────────────────────────────────
        // GET /api/inventory/movements?productId=&type=&from=&to=&page=1&pageSize=50
        // Paginated stock movement history.
        // ────────────────────────────────────────────────────────
        /// <summary>
        /// Returns paginated stock movement history with optional filters
        /// for product, movement type (StockIn / StockOut / Adjustment), and date range.
        /// </summary>
        [HttpGet("movements")]
        public async Task<IActionResult> GetMovements(
            [FromQuery] int?     productId = null,
            [FromQuery] string?  type      = null,
            [FromQuery] DateTime? from     = null,
            [FromQuery] DateTime? to       = null,
            [FromQuery] int page           = 1,
            [FromQuery] int pageSize       = 50)
        {
            pageSize = Math.Clamp(pageSize, 1, 200);
            page     = Math.Max(1, page);

            var start = from?.Date ?? DateTime.Today.AddDays(-29);
            var end   = (to?.Date ?? DateTime.Today).AddDays(1).AddTicks(-1);

            var query = _context.StockMovements
                .Include(sm => sm.Product)
                .Where(sm => sm.MovementDate >= start && sm.MovementDate <= end);

            if (productId.HasValue)
                query = query.Where(sm => sm.ProductId == productId.Value);

            if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<MovementType>(type, true, out var movementType))
                query = query.Where(sm => sm.Type == movementType);

            var total = await query.CountAsync();

            var movements = await query
                .OrderByDescending(sm => sm.MovementDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var data = movements.Select(sm => new
            {
                id            = sm.Id,
                productId     = sm.ProductId,
                productName   = sm.Product?.Name ?? "Unknown",
                movementType  = sm.Type.ToString(),
                quantity      = sm.Quantity,
                previousStock = sm.PreviousStock,
                newStock      = sm.NewStock,
                movementDate  = sm.MovementDate.ToString("yyyy-MM-dd HH:mm:ss"),
                reference     = sm.ReferenceNumber,
                notes         = sm.Notes,
                performedBy   = sm.PerformedBy
            });

            return Ok(new
            {
                success    = true,
                timestamp  = DateTime.UtcNow,
                pagination = new { page, pageSize, total, totalPages = (int)Math.Ceiling(total / (double)pageSize) },
                data
            });
        }

        // ────────────────────────────────────────────────────────
        // GET /api/inventory/products
        // All active products with current stock.
        // ────────────────────────────────────────────────────────
        /// <summary>
        /// Returns all active products with their current stock level and pricing.
        /// </summary>
        [HttpGet("products")]
        public async Task<IActionResult> GetProducts([FromQuery] string? category)
        {
            var query = _context.Products
                .Include(p => p.Inventory)
                .Where(p => p.IsActive);

            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(p => p.Category == category);

            var products = await query
                .OrderBy(p => p.Category)
                .ThenBy(p => p.Name)
                .ToListAsync();

            var categories = products.Select(p => p.Category).Distinct().OrderBy(c => c).ToList();

            var data = products.Select(p => new
            {
                id           = p.Id,
                name         = p.Name,
                description  = p.Description,
                category     = p.Category,
                emoji        = p.Emoji,
                price        = p.Price,
                costPrice    = p.CostPrice,
                margin       = p.Price > 0
                    ? Math.Round((p.Price - p.CostPrice) / p.Price * 100, 2)
                    : 0m,
                currentStock = p.Inventory?.Quantity ?? 0,
                reorderPoint = p.Inventory?.ReorderPoint ?? 0,
                stockStatus  = p.Inventory == null ? "No Record"
                             : p.Inventory.Quantity == 0 ? "Out of Stock"
                             : p.Inventory.Quantity <= p.Inventory.MinStockLevel ? "Critical"
                             : p.Inventory.Quantity <= p.Inventory.ReorderPoint  ? "Low"
                             : "OK"
            });

            return Ok(new
            {
                success    = true,
                timestamp  = DateTime.UtcNow,
                categories,
                count      = products.Count,
                data
            });
        }

        // ────────────────────────────────────────────────────────
        // GET /api/inventory/products/{id}
        // Single product detail with stock and movement summary.
        // ────────────────────────────────────────────────────────
        /// <summary>
        /// Returns full detail for a single product including stock level
        /// and a 30-day movement summary.
        /// </summary>
        [HttpGet("products/{id:int}")]
        public async Task<IActionResult> GetProduct(int id)
        {
            var product = await _context.Products
                .Include(p => p.Inventory)
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

            if (product == null)
                return NotFound(new { success = false, message = $"Product #{id} not found." });

            // 30-day movement summary
            var since = DateTime.Today.AddDays(-29);
            var movements = await _context.StockMovements
                .Where(sm => sm.ProductId == id && sm.MovementDate >= since)
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = new
                {
                    id           = product.Id,
                    name         = product.Name,
                    description  = product.Description,
                    category     = product.Category,
                    emoji        = product.Emoji,
                    price        = product.Price,
                    costPrice    = product.CostPrice,
                    margin       = product.Price > 0
                        ? Math.Round((product.Price - product.CostPrice) / product.Price * 100, 2)
                        : 0m,
                    inventory = product.Inventory == null ? null : new
                    {
                        quantity     = product.Inventory.Quantity,
                        minStock     = product.Inventory.MinStockLevel,
                        maxStock     = product.Inventory.MaxStockLevel,
                        reorderPoint = product.Inventory.ReorderPoint,
                        location     = product.Inventory.Location,
                        lastUpdated  = product.Inventory.LastUpdated.ToString("yyyy-MM-dd HH:mm:ss"),
                        stockValue   = product.Inventory.Quantity * product.CostPrice
                    },
                    last30Days = new
                    {
                        totalIn  = movements.Where(m => m.Type == MovementType.StockIn).Sum(m => m.Quantity),
                        totalOut = movements.Where(m => m.Type == MovementType.StockOut || m.Type == MovementType.Sale).Sum(m => m.Quantity),
                        netChange = movements.Sum(m =>
                            m.Type == MovementType.StockIn ? m.Quantity : -m.Quantity)
                    }
                }
            });
        }
    }
}
