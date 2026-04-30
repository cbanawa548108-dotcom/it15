using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRLFruitstandESS.Data;
using CRLFruitstandESS.Models;
using CRLFruitstandESS.Models.ViewModels;
using ClosedXML.Excel;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using iText.Kernel.Colors;

namespace CRLFruitstandESS.Controllers
{
    [Authorize(Roles = "CFO,CEO,Admin")]
    public class ReportingController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ReportingController> _logger;

        public ReportingController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, ILogger<ReportingController> logger)
        {
            _db = db;
            _userManager = userManager;
            _logger = logger;
        }

        // ════════════════════════════════════════════
        // INDEX — Report Hub
        // ════════════════════════════════════════════
        public async Task<IActionResult> Index(
            DateTime? from = null,
            DateTime? to   = null,
            string reportType = "financial")
        {
            try
            {
                var dateFrom = from ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var dateTo   = to   ?? DateTime.Today;
                var vm = new ReportingViewModel { DateFrom = dateFrom, DateTo = dateTo, ReportType = reportType };
                await PopulateViewModel(vm);
                return View(vm);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading Reporting Index");
                TempData["Error"] = "Failed to load report data. Please try again.";
                return View(new ReportingViewModel());
            }
        }

        // ════════════════════════════════════════════
        // EXPORT TO EXCEL
        // ════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> ExportExcel(
            DateTime? from = null,
            DateTime? to   = null,
            string reportType = "financial")
        {
            try
            {
            var dateFrom = from ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var dateTo   = to   ?? DateTime.Today;

            var vm = new ReportingViewModel { DateFrom = dateFrom, DateTo = dateTo, ReportType = reportType };
            await PopulateViewModel(vm);

            using var wb = new XLWorkbook();

            // ── Financial Summary sheet
            var summary = wb.Worksheets.Add("Summary");
            StyleHeader(summary, $"Financial Report: {dateFrom:MMM dd, yyyy} – {dateTo:MMM dd, yyyy}", 1, 4);
            summary.Cell(2, 1).Value = "Metric";       summary.Cell(2, 2).Value = "Amount";
            summary.Cell(3, 1).Value = "Total Revenue"; summary.Cell(3, 2).Value = (double)vm.TotalRevenue;
            summary.Cell(4, 1).Value = "Total Expenses";summary.Cell(4, 2).Value = (double)vm.TotalExpenses;
            summary.Cell(5, 1).Value = "Net Profit";    summary.Cell(5, 2).Value = (double)vm.NetProfit;
            summary.Cell(6, 1).Value = "Gross Margin %";summary.Cell(6, 2).Value = (double)vm.GrossMargin;
            summary.Columns().AdjustToContents();

            // ── Revenue sheet
            if (vm.Revenues.Any())
            {
                var revSheet = wb.Worksheets.Add("Revenue");
                StyleHeader(revSheet, "Revenue Transactions", 1, 5);
                var rh = new[] { "Date", "Source", "Category", "Amount", "Recorded By" };
                for (int i = 0; i < rh.Length; i++) revSheet.Cell(2, i + 1).Value = rh[i];
                int row = 3;
                foreach (var r in vm.Revenues)
                {
                    revSheet.Cell(row, 1).Value = r.TransactionDate.ToString("yyyy-MM-dd");
                    revSheet.Cell(row, 2).Value = r.Source;
                    revSheet.Cell(row, 3).Value = r.Category;
                    revSheet.Cell(row, 4).Value = (double)r.Amount;
                    revSheet.Cell(row, 5).Value = r.RecordedBy;
                    row++;
                }
                revSheet.Columns().AdjustToContents();
            }

            // ── Expenses sheet
            if (vm.Expenses.Any())
            {
                var expSheet = wb.Worksheets.Add("Expenses");
                StyleHeader(expSheet, "Expense Transactions", 1, 5);
                var eh = new[] { "Date", "Description", "Category", "Amount", "Recorded By" };
                for (int i = 0; i < eh.Length; i++) expSheet.Cell(2, i + 1).Value = eh[i];
                int row = 3;
                foreach (var e in vm.Expenses)
                {
                    expSheet.Cell(row, 1).Value = e.ExpenseDate.ToString("yyyy-MM-dd");
                    expSheet.Cell(row, 2).Value = e.Description;
                    expSheet.Cell(row, 3).Value = e.Category;
                    expSheet.Cell(row, 4).Value = (double)e.Amount;
                    expSheet.Cell(row, 5).Value = e.RecordedBy;
                    row++;
                }
                expSheet.Columns().AdjustToContents();
            }

            // ── Sales sheet
            if (vm.Sales.Any())
            {
                var salesSheet = wb.Worksheets.Add("Sales");
                StyleHeader(salesSheet, "Sales Transactions", 1, 4);
                var sh = new[] { "Date", "Cashier", "Total Amount", "Status" };
                for (int i = 0; i < sh.Length; i++) salesSheet.Cell(2, i + 1).Value = sh[i];
                int row = 3;
                foreach (var s in vm.Sales)
                {
                    salesSheet.Cell(row, 1).Value = s.SaleDate.ToString("yyyy-MM-dd HH:mm");
                    salesSheet.Cell(row, 2).Value = s.Cashier?.FullName ?? s.CashierId;
                    salesSheet.Cell(row, 3).Value = (double)s.TotalAmount;
                    salesSheet.Cell(row, 4).Value = s.Status;
                    row++;
                }
                salesSheet.Columns().AdjustToContents();
            }

            // ── Stock Movements sheet
            if (vm.StockMovements.Any())
            {
                var smSheet = wb.Worksheets.Add("Stock Movements");
                StyleHeader(smSheet, "Stock Movement Log", 1, 6);
                var smh = new[] { "Date", "Product", "Type", "Qty", "Previous Stock", "New Stock" };
                for (int i = 0; i < smh.Length; i++) smSheet.Cell(2, i + 1).Value = smh[i];
                int row = 3;
                foreach (var m in vm.StockMovements)
                {
                    smSheet.Cell(row, 1).Value = m.MovementDate.ToString("yyyy-MM-dd HH:mm");
                    smSheet.Cell(row, 2).Value = m.Product?.Name ?? m.ProductId.ToString();
                    smSheet.Cell(row, 3).Value = m.Type.ToString();
                    smSheet.Cell(row, 4).Value = m.Quantity;
                    smSheet.Cell(row, 5).Value = m.PreviousStock;
                    smSheet.Cell(row, 6).Value = m.NewStock;
                    row++;
                }
                smSheet.Columns().AdjustToContents();
            }

            // ── Audit sheet
            if (vm.AuditLog.Any())
            {
                var auditSheet = wb.Worksheets.Add("Audit Log");
                StyleHeader(auditSheet, "Audit Trail", 1, 5);
                var ah = new[] { "Timestamp", "User", "Module", "Action", "Description" };
                for (int i = 0; i < ah.Length; i++) auditSheet.Cell(2, i + 1).Value = ah[i];
                int row = 3;
                foreach (var a in vm.AuditLog)
                {
                    auditSheet.Cell(row, 1).Value = a.Timestamp.ToString("yyyy-MM-dd HH:mm");
                    auditSheet.Cell(row, 2).Value = a.User;
                    auditSheet.Cell(row, 3).Value = a.Module;
                    auditSheet.Cell(row, 4).Value = a.Action;
                    auditSheet.Cell(row, 5).Value = a.Description;
                    row++;
                }
                auditSheet.Columns().AdjustToContents();
            }

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            stream.Position = 0;
            var fileName = $"Report_{dateFrom:yyyyMMdd}_{dateTo:yyyyMMdd}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating Excel export");
                TempData["Error"] = "Failed to generate Excel export. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        // ════════════════════════════════════════════
        // EXPORT TO PDF
        // ════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> ExportPdf(
            DateTime? from = null,
            DateTime? to   = null,
            string reportType = "financial")
        {
            try
            {
            var dateFrom = from ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var dateTo   = to   ?? DateTime.Today;

            var vm = new ReportingViewModel { DateFrom = dateFrom, DateTo = dateTo, ReportType = reportType };
            await PopulateViewModel(vm);

            using var stream = new MemoryStream();
            var writer = new PdfWriter(stream);
            var pdf    = new PdfDocument(writer);
            var doc    = new Document(pdf);

            var darkBg  = new DeviceRgb(15,  23,  42);
            var cardBg  = new DeviceRgb(30,  41,  59);
            var blue    = new DeviceRgb(59,  130, 246);
            var green   = new DeviceRgb(16,  185, 129);
            var amber   = new DeviceRgb(245, 158, 11);
            var red     = new DeviceRgb(239, 68,  68);
            var white   = new DeviceRgb(241, 245, 249);
            var muted   = new DeviceRgb(100, 116, 139);

            // Title
            doc.Add(new Paragraph("CRL Fruitstand ESS")
                .SetFontSize(10).SetFontColor(blue)
                .SetMarginBottom(2));
            doc.Add(new Paragraph("Business Report")
                .SetFontSize(22).SetFontColor(white)
                .SetMarginBottom(4));
            doc.Add(new Paragraph($"Period: {dateFrom:MMMM dd, yyyy} – {dateTo:MMMM dd, yyyy}   |   Generated: {DateTime.Now:MMM dd, yyyy h:mm tt}")
                .SetFontSize(9).SetFontColor(muted).SetMarginBottom(16));

            // Summary table
            doc.Add(new Paragraph("Financial Summary")
                .SetFontSize(13).SetFontColor(white).SetMarginBottom(6));

            var summaryTable = new Table(UnitValue.CreatePercentArray(new float[] { 3, 2 })).UseAllAvailableWidth();
            AddPdfHeaderCell(summaryTable, "Metric",   cardBg, white);
            AddPdfHeaderCell(summaryTable, "Amount",   cardBg, white);
            AddPdfDataRow(summaryTable, "Total Revenue",   $"₱{vm.TotalRevenue:N0}",  green, white, muted);
            AddPdfDataRow(summaryTable, "Total Expenses",  $"₱{vm.TotalExpenses:N0}", red,   white, muted);
            AddPdfDataRow(summaryTable, "Net Profit",      $"₱{vm.NetProfit:N0}",     vm.NetProfit >= 0 ? green : red, white, muted);
            AddPdfDataRow(summaryTable, "Gross Margin",    $"{vm.GrossMargin:F1}%",   amber, white, muted);
            doc.Add(summaryTable);
            doc.Add(new Paragraph("\n"));

            // Revenue table
            if (vm.Revenues.Any())
            {
                doc.Add(new Paragraph("Revenue Transactions")
                    .SetFontSize(13).SetFontColor(white).SetMarginBottom(6));
                var t = new Table(UnitValue.CreatePercentArray(new float[] { 2, 3, 2, 2, 2 })).UseAllAvailableWidth();
                foreach (var h in new[] { "Date", "Source", "Category", "Amount", "By" })
                    AddPdfHeaderCell(t, h, cardBg, white);
                foreach (var r in vm.Revenues.Take(50))
                {
                    t.AddCell(PdfCell(r.TransactionDate.ToString("MM/dd/yy"), white, muted));
                    t.AddCell(PdfCell(r.Source, white, muted));
                    t.AddCell(PdfCell(r.Category, white, muted));
                    t.AddCell(PdfCell($"₱{r.Amount:N0}", green, muted));
                    t.AddCell(PdfCell(r.RecordedBy, white, muted));
                }
                doc.Add(t);
                doc.Add(new Paragraph("\n"));
            }

            // Expenses table
            if (vm.Expenses.Any())
            {
                doc.Add(new Paragraph("Expense Transactions")
                    .SetFontSize(13).SetFontColor(white).SetMarginBottom(6));
                var t = new Table(UnitValue.CreatePercentArray(new float[] { 2, 3, 2, 2, 2 })).UseAllAvailableWidth();
                foreach (var h in new[] { "Date", "Description", "Category", "Amount", "By" })
                    AddPdfHeaderCell(t, h, cardBg, white);
                foreach (var e in vm.Expenses.Take(50))
                {
                    t.AddCell(PdfCell(e.ExpenseDate.ToString("MM/dd/yy"), white, muted));
                    t.AddCell(PdfCell(e.Description, white, muted));
                    t.AddCell(PdfCell(e.Category, white, muted));
                    t.AddCell(PdfCell($"₱{e.Amount:N0}", red, muted));
                    t.AddCell(PdfCell(e.RecordedBy, white, muted));
                }
                doc.Add(t);
                doc.Add(new Paragraph("\n"));
            }

            // Audit log
            if (vm.AuditLog.Any())
            {
                doc.Add(new Paragraph("Audit Trail")
                    .SetFontSize(13).SetFontColor(white).SetMarginBottom(6));
                var t = new Table(UnitValue.CreatePercentArray(new float[] { 2, 2, 2, 2, 4 })).UseAllAvailableWidth();
                foreach (var h in new[] { "Timestamp", "User", "Module", "Action", "Description" })
                    AddPdfHeaderCell(t, h, cardBg, white);
                foreach (var a in vm.AuditLog.Take(100))
                {
                    t.AddCell(PdfCell(a.Timestamp.ToString("MM/dd HH:mm"), white, muted));
                    t.AddCell(PdfCell(a.User, white, muted));
                    t.AddCell(PdfCell(a.Module, white, muted));
                    t.AddCell(PdfCell(a.Action, white, muted));
                    t.AddCell(PdfCell(a.Description, white, muted));
                }
                doc.Add(t);
            }

            doc.Close();
            var fileName = $"Report_{dateFrom:yyyyMMdd}_{dateTo:yyyyMMdd}.pdf";
            return File(stream.ToArray(), "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating PDF export");
                TempData["Error"] = "Failed to generate PDF export. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        // ════════════════════════════════════════════
        // PRIVATE HELPERS
        // ════════════════════════════════════════════
        private async Task PopulateViewModel(ReportingViewModel vm)
        {
            var from = vm.DateFrom.Date;
            var to   = vm.DateTo.Date.AddDays(1).AddTicks(-1);

            vm.Revenues = await _db.Revenues
                .Where(r => !r.IsDeleted && r.TransactionDate >= from && r.TransactionDate <= to)
                .OrderByDescending(r => r.TransactionDate).ToListAsync();

            vm.Expenses = await _db.Expenses
                .Where(e => !e.IsDeleted && e.ExpenseDate >= from && e.ExpenseDate <= to)
                .OrderByDescending(e => e.ExpenseDate).ToListAsync();

            vm.Sales = await _db.Sales
                .Include(s => s.Cashier)
                .Where(s => s.SaleDate >= from && s.SaleDate <= to)
                .OrderByDescending(s => s.SaleDate).ToListAsync();

            vm.StockMovements = await _db.StockMovements
                .Include(m => m.Product)
                .Where(m => m.MovementDate >= from && m.MovementDate <= to)
                .OrderByDescending(m => m.MovementDate).ToListAsync();

            vm.SupplierPayments = await _db.SupplierPayments
                .Include(p => p.Supplier)
                .Where(p => p.PaymentDate >= from && p.PaymentDate <= to)
                .OrderByDescending(p => p.PaymentDate).ToListAsync();

            vm.SupplierDeliveries = await _db.SupplierDeliveries
                .Include(d => d.Supplier)
                .Include(d => d.Product)
                .Where(d => d.DeliveryDate >= from && d.DeliveryDate <= to)
                .OrderByDescending(d => d.DeliveryDate).ToListAsync();

            vm.TotalRevenue  = vm.Revenues.Sum(r => r.Amount);
            vm.TotalExpenses = vm.Expenses.Sum(e => e.Amount);
            vm.NetProfit     = vm.TotalRevenue - vm.TotalExpenses;
            vm.GrossMargin   = vm.TotalRevenue > 0 ? (vm.NetProfit / vm.TotalRevenue) * 100 : 0;

            // Daily trend (last 14 days within range)
            var trendDays = Math.Min((int)(vm.DateTo - vm.DateFrom).TotalDays + 1, 14);
            for (int i = trendDays - 1; i >= 0; i--)
            {
                var day = vm.DateTo.AddDays(-i).Date;
                var rev = vm.Revenues.Where(r => r.TransactionDate.Date == day).Sum(r => r.Amount);
                var exp = vm.Expenses.Where(e => e.ExpenseDate.Date == day).Sum(e => e.Amount);
                vm.DailyTrend.Add((day.ToString("MMM dd"), rev, exp));
            }

            // Breakdowns
            vm.ExpenseBreakdown = vm.Expenses
                .GroupBy(e => e.Category)
                .Select(g => (g.Key, g.Sum(e => e.Amount)))
                .OrderByDescending(x => x.Item2).ToList();

            vm.RevenueBreakdown = vm.Revenues
                .GroupBy(r => r.Category)
                .Select(g => (g.Key, g.Sum(r => r.Amount)))
                .OrderByDescending(x => x.Item2).ToList();

            // Audit log — synthesised from real transaction data
            var audit = new List<AuditEntry>();
            foreach (var r in vm.Revenues.Take(30))
                audit.Add(new AuditEntry { Timestamp = r.CreatedAt, User = r.RecordedBy, Module = "Finance", Action = "Revenue Added", Description = $"₱{r.Amount:N0} — {r.Source}", Severity = "info" });
            foreach (var e in vm.Expenses.Take(30))
                audit.Add(new AuditEntry { Timestamp = e.CreatedAt, User = e.RecordedBy, Module = "Finance", Action = "Expense Added", Description = $"₱{e.Amount:N0} — {e.Description}", Severity = "info" });
            foreach (var s in vm.Sales.Take(20))
                audit.Add(new AuditEntry { Timestamp = s.SaleDate, User = s.Cashier?.FullName ?? s.CashierId, Module = "POS", Action = "Sale Completed", Description = $"₱{s.TotalAmount:N0} — {s.Status}", Severity = s.Status == "Voided" ? "warning" : "info" });
            foreach (var m in vm.StockMovements.Take(20))
                audit.Add(new AuditEntry { Timestamp = m.MovementDate, User = m.PerformedBy ?? "System", Module = "Inventory", Action = m.Type.ToString(), Description = $"{m.Product?.Name} | Qty: {m.Quantity} | {m.PreviousStock}→{m.NewStock}", Severity = m.Type == MovementType.Damaged ? "warning" : "info" });
            foreach (var p in vm.SupplierPayments.Take(10))
                audit.Add(new AuditEntry { Timestamp = p.PaymentDate, User = p.PaidBy, Module = "Suppliers", Action = "Payment Made", Description = $"₱{p.Amount:N0} to {p.Supplier?.Name}", Severity = "info" });

            vm.AuditLog = audit.OrderByDescending(a => a.Timestamp).ToList();
        }

        // ── Excel helpers
        private static void StyleHeader(IXLWorksheet ws, string title, int row, int cols)
        {
            ws.Cell(row, 1).Value = title;
            ws.Range(row, 1, row, cols).Merge().Style
                .Font.SetBold(true).Font.SetFontSize(13)
                .Fill.SetBackgroundColor(XLColor.FromHtml("#1e293b"))
                .Font.SetFontColor(XLColor.White);
        }

        // ── PDF helpers
        private static void AddPdfHeaderCell(Table t, string text, DeviceRgb bg, DeviceRgb fg)
        {
            t.AddHeaderCell(new Cell().Add(new Paragraph(text).SetFontSize(8).SetFontColor(fg))
                .SetBackgroundColor(bg).SetPadding(6));
        }

        private static void AddPdfDataRow(Table t, string label, string value, DeviceRgb valueColor, DeviceRgb fg, DeviceRgb bg)
        {
            t.AddCell(new Cell().Add(new Paragraph(label).SetFontSize(9).SetFontColor(fg)).SetBackgroundColor(bg).SetPadding(5));
            t.AddCell(new Cell().Add(new Paragraph(value).SetFontSize(9).SetFontColor(valueColor)).SetBackgroundColor(bg).SetPadding(5));
        }

        private static Cell PdfCell(string text, DeviceRgb fg, DeviceRgb bg) =>
            new Cell().Add(new Paragraph(text).SetFontSize(8).SetFontColor(fg))
                .SetBackgroundColor(bg).SetPadding(4);
    }
}
