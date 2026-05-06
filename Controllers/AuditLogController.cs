using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRLFruitstandESS.Data;
using CRLFruitstandESS.Models;
using CRLFruitstandESS.Models.ViewModels;

namespace CRLFruitstandESS.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AuditLogController : Controller
    {
        private readonly ApplicationDbContext _db;
        public AuditLogController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Index(string module = "all", DateTime? from = null, DateTime? to = null, int page = 1)
        {
            const int pageSize = 20;
            var dateFrom = from ?? DateTime.Today.AddDays(-30);
            var dateTo   = to   ?? DateTime.Today;

            var logs = new List<AuditLog>();

            // Stock movements
            var movements = await _db.StockMovements
                .Include(m => m.Product)
                .Where(m => m.MovementDate >= dateFrom && m.MovementDate <= dateTo.AddDays(1))
                .OrderByDescending(m => m.MovementDate)
                .Take(200).ToListAsync();

            foreach (var m in movements)
                logs.Add(new AuditLog
                {
                    Action    = m.Type == MovementType.StockIn ? "Stock In" : m.Type.ToString(),
                    Entity    = m.Product?.Name ?? "Product",
                    EntityId  = m.ProductId,
                    User      = m.PerformedBy ?? "System",
                    Timestamp = m.MovementDate,
                    Details   = $"{m.Type}: {m.Quantity} units | {m.PreviousStock} → {m.NewStock}",
                    Module    = "Inventory"
                });

            // Supplier payments
            var payments = await _db.SupplierPayments
                .Include(p => p.Supplier)
                .Where(p => p.PaymentDate >= dateFrom && p.PaymentDate <= dateTo.AddDays(1))
                .OrderByDescending(p => p.PaymentDate)
                .Take(100).ToListAsync();

            foreach (var p in payments)
                logs.Add(new AuditLog
                {
                    Action    = "Payment",
                    Entity    = p.Supplier?.Name ?? "Supplier",
                    EntityId  = p.SupplierId,
                    User      = p.PaidBy,
                    Timestamp = p.PaymentDate,
                    Details   = $"₱{p.Amount:N2} via {p.PaymentMethod}",
                    Module    = "Finance"
                });

            // Sales
            var sales = await _db.Sales
                .Include(s => s.Cashier)
                .Where(s => s.SaleDate >= dateFrom && s.SaleDate <= dateTo.AddDays(1))
                .OrderByDescending(s => s.SaleDate)
                .Take(200).ToListAsync();

            foreach (var s in sales)
                logs.Add(new AuditLog
                {
                    Action    = s.Status == "Voided" ? "Voided Sale" : "Sale",
                    Entity    = "POS Transaction",
                    EntityId  = s.Id,
                    User      = s.Cashier?.FullName ?? s.CashierId,
                    Timestamp = s.SaleDate,
                    Details   = $"₱{s.TotalAmount:N2} — {s.Status}",
                    Module    = "POS"
                });

            // Revenues
            var revenues = await _db.Revenues
                .Where(r => !r.IsDeleted && r.TransactionDate >= dateFrom && r.TransactionDate <= dateTo.AddDays(1))
                .OrderByDescending(r => r.TransactionDate)
                .Take(100).ToListAsync();

            foreach (var r in revenues)
                logs.Add(new AuditLog
                {
                    Action    = "Revenue",
                    Entity    = r.Source,
                    EntityId  = r.Id,
                    User      = r.RecordedBy,
                    Timestamp = r.TransactionDate,
                    Details   = $"₱{r.Amount:N2} — {r.Category}",
                    Module    = "Finance"
                });

            // Expenses
            var expenses = await _db.Expenses
                .Where(e => !e.IsDeleted && e.ExpenseDate >= dateFrom && e.ExpenseDate <= dateTo.AddDays(1))
                .OrderByDescending(e => e.ExpenseDate)
                .Take(100).ToListAsync();

            foreach (var e in expenses)
                logs.Add(new AuditLog
                {
                    Action    = "Expense",
                    Entity    = e.Description,
                    EntityId  = e.Id,
                    User      = e.RecordedBy,
                    Timestamp = e.ExpenseDate,
                    Details   = $"₱{e.Amount:N2} — {e.Category}",
                    Module    = "Finance"
                });

            var filtered = module == "all"
                ? logs
                : logs.Where(l => l.Module == module).ToList();

            var sorted = filtered.OrderByDescending(l => l.Timestamp).ToList();

            // Pagination
            int totalItems  = sorted.Count;
            int totalPages  = (int)Math.Ceiling(totalItems / (double)pageSize);
            page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));
            var paged = sorted.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.Module      = module;
            ViewBag.DateFrom    = dateFrom;
            ViewBag.DateTo      = dateTo;
            ViewBag.Total       = totalItems;
            ViewBag.Modules     = new[] { "all", "POS", "Finance", "Inventory" };
            ViewBag.Page        = page;
            ViewBag.TotalPages  = totalPages;
            ViewBag.PageSize    = pageSize;

            return View(paged);
        }
    }
}
