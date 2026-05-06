using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRLFruitstandESS.Data;
using CRLFruitstandESS.Models;

namespace CRLFruitstandESS.Controllers.Api
{
    /// <summary>
    /// REST API for Sales data — daily summaries, transaction history, and per-cashier performance.
    /// Accessible by Admin, Manager, CFO, and CEO roles.
    /// </summary>
    [ApiController]
    [Route("api/sales")]
    [Authorize(Roles = "Admin,Manager,CFO,CEO")]
    public class SalesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public SalesApiController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // ────────────────────────────────────────────────────────
        // GET /api/sales/summary?from=2026-01-01&to=2026-01-31
        // Returns aggregated totals for the requested date range.
        // ────────────────────────────────────────────────────────
        /// <summary>
        /// Returns total revenue, transaction count, average order value,
        /// and a daily breakdown for the given date range.
        /// </summary>
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var start = from?.Date ?? DateTime.Today.AddDays(-29);
            var end   = (to?.Date ?? DateTime.Today).AddDays(1).AddTicks(-1); // inclusive end

            var sales = await _context.Sales
                .Where(s => s.Status == "Completed"
                         && s.SaleDate >= start
                         && s.SaleDate <= end)
                .ToListAsync();

            var daily = sales
                .GroupBy(s => s.SaleDate.Date)
                .Select(g => new
                {
                    date             = g.Key.ToString("yyyy-MM-dd"),
                    transactionCount = g.Count(),
                    totalRevenue     = g.Sum(s => s.TotalAmount)
                })
                .OrderBy(x => x.date)
                .ToList();

            return Ok(new
            {
                success          = true,
                timestamp        = DateTime.UtcNow,
                period           = new { from = start.ToString("yyyy-MM-dd"), to = end.Date.ToString("yyyy-MM-dd") },
                totalRevenue     = sales.Sum(s => s.TotalAmount),
                transactionCount = sales.Count,
                averageOrderValue = sales.Count > 0
                    ? Math.Round(sales.Sum(s => s.TotalAmount) / sales.Count, 2)
                    : 0m,
                dailyBreakdown   = daily
            });
        }

        // ────────────────────────────────────────────────────────
        // GET /api/sales/today
        // Convenience endpoint — today's sales summary.
        // ────────────────────────────────────────────────────────
        /// <summary>Returns today's sales totals and transaction count.</summary>
        [HttpGet("today")]
        public async Task<IActionResult> GetToday()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var sales = await _context.Sales
                .Where(s => s.Status == "Completed"
                         && s.SaleDate >= today
                         && s.SaleDate < tomorrow)
                .ToListAsync();

            return Ok(new
            {
                success          = true,
                timestamp        = DateTime.UtcNow,
                date             = today.ToString("yyyy-MM-dd"),
                totalRevenue     = sales.Sum(s => s.TotalAmount),
                transactionCount = sales.Count,
                averageOrderValue = sales.Count > 0
                    ? Math.Round(sales.Sum(s => s.TotalAmount) / sales.Count, 2)
                    : 0m
            });
        }

        // ────────────────────────────────────────────────────────
        // GET /api/sales/transactions?from=&to=&page=1&pageSize=20
        // Paginated list of completed sales with their items.
        // ────────────────────────────────────────────────────────
        /// <summary>
        /// Returns a paginated list of sales transactions with line items.
        /// </summary>
        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactions(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to,
            [FromQuery] int page     = 1,
            [FromQuery] int pageSize = 20)
        {
            pageSize = Math.Clamp(pageSize, 1, 100);
            page     = Math.Max(1, page);

            var start = from?.Date ?? DateTime.Today.AddDays(-29);
            var end   = (to?.Date ?? DateTime.Today).AddDays(1).AddTicks(-1);

            var query = _context.Sales
                .Include(s => s.SaleItems)
                    .ThenInclude(si => si.Product)
                .Include(s => s.Cashier)
                .Where(s => s.Status == "Completed"
                         && s.SaleDate >= start
                         && s.SaleDate <= end)
                .OrderByDescending(s => s.SaleDate);

            var total = await query.CountAsync();

            var sales = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var data = sales.Select(s => new
            {
                id           = s.Id,
                saleDate     = s.SaleDate.ToString("yyyy-MM-dd HH:mm:ss"),
                cashier      = s.Cashier?.UserName ?? "Unknown",
                totalAmount  = s.TotalAmount,
                amountPaid   = s.AmountPaid,
                change       = s.Change,
                status       = s.Status,
                itemCount    = s.SaleItems?.Count ?? 0,
                items        = s.SaleItems?.Select(si => new
                {
                    productId   = si.ProductId,
                    productName = si.Product?.Name ?? "Unknown",
                    quantity    = si.Quantity,
                    unitPrice   = si.UnitPrice,
                    subtotal    = si.Subtotal
                })
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
        // GET /api/sales/{id}
        // Single sale with full item detail.
        // ────────────────────────────────────────────────────────
        /// <summary>Returns a single sale record with all line items.</summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var sale = await _context.Sales
                .Include(s => s.SaleItems)
                    .ThenInclude(si => si.Product)
                .Include(s => s.Cashier)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (sale == null)
                return NotFound(new { success = false, message = $"Sale #{id} not found." });

            return Ok(new
            {
                success = true,
                data = new
                {
                    id          = sale.Id,
                    saleDate    = sale.SaleDate.ToString("yyyy-MM-dd HH:mm:ss"),
                    cashier     = sale.Cashier?.UserName ?? "Unknown",
                    totalAmount = sale.TotalAmount,
                    amountPaid  = sale.AmountPaid,
                    change      = sale.Change,
                    status      = sale.Status,
                    items       = sale.SaleItems?.Select(si => new
                    {
                        productId   = si.ProductId,
                        productName = si.Product?.Name ?? "Unknown",
                        category    = si.Product?.Category ?? "Unknown",
                        quantity    = si.Quantity,
                        unitPrice   = si.UnitPrice,
                        subtotal    = si.Subtotal
                    })
                }
            });
        }

        // ────────────────────────────────────────────────────────
        // GET /api/sales/by-product?from=&to=
        // Revenue and quantity sold grouped by product.
        // ────────────────────────────────────────────────────────
        /// <summary>
        /// Returns sales performance broken down by product for the given period.
        /// </summary>
        [HttpGet("by-product")]
        public async Task<IActionResult> GetByProduct(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var start = from?.Date ?? DateTime.Today.AddDays(-29);
            var end   = (to?.Date ?? DateTime.Today).AddDays(1).AddTicks(-1);

            var items = await _context.SaleItems
                .Include(si => si.Sale)
                .Include(si => si.Product)
                .Where(si => si.Sale.Status == "Completed"
                          && si.Sale.SaleDate >= start
                          && si.Sale.SaleDate <= end)
                .ToListAsync();

            var grouped = items
                .GroupBy(si => new { si.ProductId, si.Product?.Name, si.Product?.Category })
                .Select(g => new
                {
                    productId    = g.Key.ProductId,
                    productName  = g.Key.Name ?? "Unknown",
                    category     = g.Key.Category ?? "Unknown",
                    quantitySold = g.Sum(si => si.Quantity),
                    totalRevenue = g.Sum(si => si.Subtotal),
                    transactions = g.Select(si => si.SaleId).Distinct().Count()
                })
                .OrderByDescending(x => x.totalRevenue)
                .ToList();

            return Ok(new
            {
                success   = true,
                timestamp = DateTime.UtcNow,
                period    = new { from = start.ToString("yyyy-MM-dd"), to = end.Date.ToString("yyyy-MM-dd") },
                data      = grouped
            });
        }

        // ────────────────────────────────────────────────────────
        // GET /api/sales/by-cashier?from=&to=
        // Performance metrics per cashier.
        // ────────────────────────────────────────────────────────
        /// <summary>
        /// Returns sales performance grouped by cashier for the given period.
        /// </summary>
        [HttpGet("by-cashier")]
        public async Task<IActionResult> GetByCashier(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var start = from?.Date ?? DateTime.Today.AddDays(-29);
            var end   = (to?.Date ?? DateTime.Today).AddDays(1).AddTicks(-1);

            var sales = await _context.Sales
                .Include(s => s.Cashier)
                .Where(s => s.Status == "Completed"
                         && s.SaleDate >= start
                         && s.SaleDate <= end)
                .ToListAsync();

            var grouped = sales
                .GroupBy(s => new { s.CashierId, Name = s.Cashier?.UserName ?? "Unknown" })
                .Select(g => new
                {
                    cashierId        = g.Key.CashierId,
                    cashierName      = g.Key.Name,
                    transactionCount = g.Count(),
                    totalRevenue     = g.Sum(s => s.TotalAmount),
                    averageOrderValue = Math.Round(g.Sum(s => s.TotalAmount) / g.Count(), 2)
                })
                .OrderByDescending(x => x.totalRevenue)
                .ToList();

            return Ok(new
            {
                success   = true,
                timestamp = DateTime.UtcNow,
                period    = new { from = start.ToString("yyyy-MM-dd"), to = end.Date.ToString("yyyy-MM-dd") },
                data      = grouped
            });
        }

        // ────────────────────────────────────────────────────────
        // GET /api/sales/by-category?from=&to=
        // Revenue grouped by product category.
        // ────────────────────────────────────────────────────────
        /// <summary>
        /// Returns revenue grouped by product category for the given period.
        /// </summary>
        [HttpGet("by-category")]
        public async Task<IActionResult> GetByCategory(
            [FromQuery] DateTime? from,
            [FromQuery] DateTime? to)
        {
            var start = from?.Date ?? DateTime.Today.AddDays(-29);
            var end   = (to?.Date ?? DateTime.Today).AddDays(1).AddTicks(-1);

            var items = await _context.SaleItems
                .Include(si => si.Sale)
                .Include(si => si.Product)
                .Where(si => si.Sale.Status == "Completed"
                          && si.Sale.SaleDate >= start
                          && si.Sale.SaleDate <= end)
                .ToListAsync();

            var grouped = items
                .GroupBy(si => si.Product?.Category ?? "Unknown")
                .Select(g => new
                {
                    category     = g.Key,
                    quantitySold = g.Sum(si => si.Quantity),
                    totalRevenue = g.Sum(si => si.Subtotal)
                })
                .OrderByDescending(x => x.totalRevenue)
                .ToList();

            var grandTotal = grouped.Sum(x => x.totalRevenue);

            return Ok(new
            {
                success    = true,
                timestamp  = DateTime.UtcNow,
                period     = new { from = start.ToString("yyyy-MM-dd"), to = end.Date.ToString("yyyy-MM-dd") },
                grandTotal,
                data       = grouped.Select(x => new
                {
                    x.category,
                    x.quantitySold,
                    x.totalRevenue,
                    percentShare = grandTotal > 0
                        ? Math.Round(x.totalRevenue / grandTotal * 100, 2)
                        : 0m
                })
            });
        }
    }
}
