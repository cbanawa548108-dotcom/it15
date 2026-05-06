using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRLFruitstandESS.Data;
using CRLFruitstandESS.Models;

namespace CRLFruitstandESS.Controllers.Api
{
    /// <summary>
    /// REST API for Supplier management — supplier list, balances,
    /// delivery history, and payment records.
    /// Accessible by Admin, Manager, CFO, and CEO roles.
    /// </summary>
    [ApiController]
    [Route("api/suppliers")]
    [Authorize(Roles = "Admin,Manager,CFO,CEO")]
    public class SupplierApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SupplierApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ────────────────────────────────────────────────────────
        // GET /api/suppliers
        // All active suppliers with product count and balance.
        // ────────────────────────────────────────────────────────
        /// <summary>
        /// Returns all active suppliers with their outstanding balance
        /// and the number of products they supply.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false)
        {
            var query = _context.Suppliers
                .Include(s => s.SupplierProducts)
                .AsQueryable();

            if (!includeInactive)
                query = query.Where(s => s.IsActive);

            var suppliers = await query
                .OrderBy(s => s.Name)
                .ToListAsync();

            var data = suppliers.Select(s => new
            {
                id            = s.Id,
                name          = s.Name,
                contactPerson = s.ContactPerson,
                phone         = s.Phone,
                email         = s.Email,
                city          = s.City,
                address       = s.Address,
                taxId         = s.TaxId,
                isActive      = s.IsActive,
                balance       = s.Balance,
                productCount  = s.SupplierProducts.Count,
                createdAt     = s.CreatedAt.ToString("yyyy-MM-dd")
            });

            return Ok(new
            {
                success   = true,
                timestamp = DateTime.UtcNow,
                count     = suppliers.Count,
                data
            });
        }

        // ────────────────────────────────────────────────────────
        // GET /api/suppliers/{id}
        // Single supplier with products, recent deliveries, and payments.
        // ────────────────────────────────────────────────────────
        /// <summary>
        /// Returns full detail for a single supplier including their
        /// product catalogue, last 10 deliveries, and last 10 payments.
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var supplier = await _context.Suppliers
                .Include(s => s.SupplierProducts)
                    .ThenInclude(sp => sp.Product)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (supplier == null)
                return NotFound(new { success = false, message = $"Supplier #{id} not found." });

            var recentDeliveries = await _context.SupplierDeliveries
                .Include(d => d.Product)
                .Where(d => d.SupplierId == id)
                .OrderByDescending(d => d.DeliveryDate)
                .Take(10)
                .ToListAsync();

            var recentPayments = await _context.SupplierPayments
                .Where(p => p.SupplierId == id)
                .OrderByDescending(p => p.PaymentDate)
                .Take(10)
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = new
                {
                    id            = supplier.Id,
                    name          = supplier.Name,
                    contactPerson = supplier.ContactPerson,
                    phone         = supplier.Phone,
                    email         = supplier.Email,
                    city          = supplier.City,
                    address       = supplier.Address,
                    taxId         = supplier.TaxId,
                    isActive      = supplier.IsActive,
                    balance       = supplier.Balance,
                    products      = supplier.SupplierProducts.Select(sp => new
                    {
                        productId          = sp.ProductId,
                        productName        = sp.Product?.Name,
                        category           = sp.Product?.Category,
                        supplierPrice      = sp.SupplierPrice,
                        supplierSku        = sp.SupplierSku,
                        leadTimeDays       = sp.LeadTimeDays,
                        minOrderQuantity   = sp.MinOrderQuantity,
                        isPreferredSupplier = sp.IsPreferredSupplier
                    }),
                    recentDeliveries = recentDeliveries.Select(d => new
                    {
                        id             = d.Id,
                        productName    = d.Product?.Name,
                        quantity       = d.Quantity,
                        unitCost       = d.UnitCost,
                        totalCost      = d.TotalCost,
                        deliveryDate   = d.DeliveryDate.ToString("yyyy-MM-dd"),
                        referenceNumber = d.ReferenceNumber,
                        receivedBy     = d.ReceivedBy
                    }),
                    recentPayments = recentPayments.Select(p => new
                    {
                        id              = p.Id,
                        amount          = p.Amount,
                        paymentDate     = p.PaymentDate.ToString("yyyy-MM-dd"),
                        paymentMethod   = p.PaymentMethod,
                        referenceNumber = p.ReferenceNumber,
                        isPending       = p.IsPending,
                        paidBy          = p.PaidBy
                    })
                }
            });
        }

        // ────────────────────────────────────────────────────────
        // GET /api/suppliers/{id}/balance
        // Outstanding balance and payment summary for one supplier.
        // ────────────────────────────────────────────────────────
        /// <summary>
        /// Returns the outstanding balance, total deliveries value,
        /// and total payments made for a supplier.
        /// </summary>
        [HttpGet("{id:int}/balance")]
        public async Task<IActionResult> GetBalance(int id)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null)
                return NotFound(new { success = false, message = $"Supplier #{id} not found." });

            var totalDeliveries = await _context.SupplierDeliveries
                .Where(d => d.SupplierId == id)
                .SumAsync(d => (decimal?)d.TotalCost) ?? 0m;

            var totalPayments = await _context.SupplierPayments
                .Where(p => p.SupplierId == id && !p.IsPending)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            var pendingPayments = await _context.SupplierPayments
                .Where(p => p.SupplierId == id && p.IsPending)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            return Ok(new
            {
                success = true,
                data = new
                {
                    supplierId      = supplier.Id,
                    supplierName    = supplier.Name,
                    currentBalance  = supplier.Balance,
                    totalDeliveries,
                    totalPaid       = totalPayments,
                    pendingPayments,
                    lastUpdated     = supplier.UpdatedAt?.ToString("yyyy-MM-dd") ?? supplier.CreatedAt.ToString("yyyy-MM-dd")
                }
            });
        }

        // ────────────────────────────────────────────────────────
        // GET /api/suppliers/{id}/deliveries?from=&to=&page=1&pageSize=20
        // Paginated delivery history for one supplier.
        // ────────────────────────────────────────────────────────
        /// <summary>
        /// Returns paginated delivery history for a supplier with optional date range filter.
        /// </summary>
        [HttpGet("{id:int}/deliveries")]
        public async Task<IActionResult> GetDeliveries(
            int id,
            [FromQuery] DateTime? from     = null,
            [FromQuery] DateTime? to       = null,
            [FromQuery] int page           = 1,
            [FromQuery] int pageSize       = 20)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null)
                return NotFound(new { success = false, message = $"Supplier #{id} not found." });

            pageSize = Math.Clamp(pageSize, 1, 100);
            page     = Math.Max(1, page);

            var start = from?.Date ?? DateTime.Today.AddMonths(-3);
            var end   = (to?.Date ?? DateTime.Today).AddDays(1).AddTicks(-1);

            var query = _context.SupplierDeliveries
                .Include(d => d.Product)
                .Where(d => d.SupplierId == id
                         && d.DeliveryDate >= start
                         && d.DeliveryDate <= end);

            var total = await query.CountAsync();

            var deliveries = await query
                .OrderByDescending(d => d.DeliveryDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var data = deliveries.Select(d => new
            {
                id              = d.Id,
                productId       = d.ProductId,
                productName     = d.Product?.Name ?? "Unknown",
                category        = d.Product?.Category ?? "Unknown",
                quantity        = d.Quantity,
                unitCost        = d.UnitCost,
                totalCost       = d.TotalCost,
                deliveryDate    = d.DeliveryDate.ToString("yyyy-MM-dd"),
                referenceNumber = d.ReferenceNumber,
                receivedBy      = d.ReceivedBy,
                notes           = d.Notes
            });

            return Ok(new
            {
                success      = true,
                timestamp    = DateTime.UtcNow,
                supplierName = supplier.Name,
                pagination   = new { page, pageSize, total, totalPages = (int)Math.Ceiling(total / (double)pageSize) },
                data
            });
        }

        // ────────────────────────────────────────────────────────
        // GET /api/suppliers/{id}/payments?from=&to=&page=1&pageSize=20
        // Paginated payment history for one supplier.
        // ────────────────────────────────────────────────────────
        /// <summary>
        /// Returns paginated payment history for a supplier with optional date range filter.
        /// </summary>
        [HttpGet("{id:int}/payments")]
        public async Task<IActionResult> GetPayments(
            int id,
            [FromQuery] DateTime? from     = null,
            [FromQuery] DateTime? to       = null,
            [FromQuery] int page           = 1,
            [FromQuery] int pageSize       = 20)
        {
            var supplier = await _context.Suppliers.FindAsync(id);
            if (supplier == null)
                return NotFound(new { success = false, message = $"Supplier #{id} not found." });

            pageSize = Math.Clamp(pageSize, 1, 100);
            page     = Math.Max(1, page);

            var start = from?.Date ?? DateTime.Today.AddMonths(-3);
            var end   = (to?.Date ?? DateTime.Today).AddDays(1).AddTicks(-1);

            var query = _context.SupplierPayments
                .Where(p => p.SupplierId == id
                         && p.PaymentDate >= start
                         && p.PaymentDate <= end);

            var total = await query.CountAsync();

            var payments = await query
                .OrderByDescending(p => p.PaymentDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var data = payments.Select(p => new
            {
                id              = p.Id,
                amount          = p.Amount,
                paymentDate     = p.PaymentDate.ToString("yyyy-MM-dd"),
                paymentMethod   = p.PaymentMethod,
                referenceNumber = p.ReferenceNumber,
                isPending       = p.IsPending,
                paidBy          = p.PaidBy,
                notes           = p.Notes
            });

            return Ok(new
            {
                success      = true,
                timestamp    = DateTime.UtcNow,
                supplierName = supplier.Name,
                pagination   = new { page, pageSize, total, totalPages = (int)Math.Ceiling(total / (double)pageSize) },
                data
            });
        }

        // ────────────────────────────────────────────────────────
        // GET /api/suppliers/summary
        // Aggregate overview of all suppliers.
        // ────────────────────────────────────────────────────────
        /// <summary>
        /// Returns an aggregate summary: total suppliers, total outstanding balance,
        /// and top 5 suppliers by balance.
        /// </summary>
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var suppliers = await _context.Suppliers
                .Where(s => s.IsActive)
                .ToListAsync();

            var top5 = suppliers
                .OrderByDescending(s => s.Balance)
                .Take(5)
                .Select(s => new { s.Id, s.Name, s.Balance });

            return Ok(new
            {
                success          = true,
                timestamp        = DateTime.UtcNow,
                totalSuppliers   = suppliers.Count,
                totalBalance     = suppliers.Sum(s => s.Balance),
                suppliersWithDebt = suppliers.Count(s => s.Balance > 0),
                top5ByBalance    = top5
            });
        }
    }
}
