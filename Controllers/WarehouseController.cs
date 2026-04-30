using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRLFruitstandESS.Data;
using CRLFruitstandESS.Models;

namespace CRLFruitstandESS.Controllers
{
    [Authorize(Roles = "Manager,Admin")]
    public class WarehouseController : Controller
    {
        private readonly ApplicationDbContext _db;
        public WarehouseController(ApplicationDbContext db) => _db = db;

        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;

            // Stock-in movements today (deliveries received)
            var stockInToday = await _db.StockMovements
                .Include(m => m.Product)
                .Where(m => m.Type == MovementType.StockIn && m.MovementDate.Date == today)
                .OrderByDescending(m => m.MovementDate)
                .ToListAsync();

            // Stock-out movements today (dispatched)
            var stockOutToday = await _db.StockMovements
                .Include(m => m.Product)
                .Where(m => (m.Type == MovementType.StockOut || m.Type == MovementType.Sale)
                         && m.MovementDate.Date == today)
                .OrderByDescending(m => m.MovementDate)
                .ToListAsync();

            // Pending deliveries (supplier deliveries not yet stocked in)
            var pendingDeliveries = await _db.SupplierDeliveries
                .Include(d => d.Supplier)
                .Include(d => d.Product)
                .Where(d => d.DeliveryDate >= today.AddDays(-7))
                .OrderByDescending(d => d.DeliveryDate)
                .Take(20)
                .ToListAsync();

            // Products needing restock
            var lowStock = await _db.Inventory
                .Include(i => i.Product)
                .Where(i => i.Quantity <= i.ReorderPoint)
                .OrderBy(i => i.Quantity)
                .ToListAsync();

            // Weekly movement summary (last 7 days)
            var weekAgo = today.AddDays(-6);
            var weeklyMovements = await _db.StockMovements
                .Where(m => m.MovementDate >= weekAgo)
                .GroupBy(m => m.MovementDate.Date)
                .Select(g => new
                {
                    Date    = g.Key,
                    StockIn = g.Where(m => m.Type == MovementType.StockIn).Sum(m => m.Quantity),
                    StockOut= g.Where(m => m.Type == MovementType.StockOut || m.Type == MovementType.Sale).Sum(m => m.Quantity)
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            ViewBag.StockInToday      = stockInToday;
            ViewBag.StockOutToday     = stockOutToday;
            ViewBag.PendingDeliveries = pendingDeliveries;
            ViewBag.LowStock          = lowStock;
            ViewBag.WeeklyMovements   = weeklyMovements;
            ViewBag.TotalStockInToday = stockInToday.Sum(m => m.Quantity);
            ViewBag.TotalStockOutToday= stockOutToday.Sum(m => m.Quantity);
            ViewBag.PendingCount      = pendingDeliveries.Count;
            ViewBag.LowStockCount     = lowStock.Count;

            return View();
        }
    }
}
