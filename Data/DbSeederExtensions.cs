using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CRLFruitstandESS.Models;
using CRLFruitstandESS.Models.Executive;

namespace CRLFruitstandESS.Data
{
    /// <summary>
    /// Seeds all supporting tables so every page has demo data for the professor.
    /// </summary>
    public static class DbSeederExtensions
    {
        public static async Task SeedSupportingDataAsync(ApplicationDbContext db, IServiceProvider services)
        {
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var cfo      = await userManager.FindByNameAsync("cfo");
            var manager  = await userManager.FindByNameAsync("manager");
            var cashier  = await userManager.FindByNameAsync("cashier");
            var admin    = await userManager.FindByNameAsync("admin");

            var today    = DateTime.Today;
            var month    = today.Month;
            var year     = today.Year;

            await SeedSuppliersAsync(db);
            await SeedBudgetsAsync(db, cfo, month, year);
            await SeedExecutiveAlertsAsync(db);
            await SeedKPITargetsAsync(db, cfo, month, year);
            await SeedRiskRegistersAsync(db);
            await SeedComplianceFlagsAsync(db);
            await SeedTrendReportsAsync(db, cfo, today);
            await SeedPettyCashClaimsAsync(db, cashier, cfo);
            await SeedSpoilageRecordsAsync(db, manager);
            await SeedStockMovementsAsync(db, manager);
        }

        // ── SUPPLIERS ──────────────────────────────────────────────────────────
        static async Task SeedSuppliersAsync(ApplicationDbContext db)
        {
            if (await db.Suppliers.AnyAsync()) return;

            var suppliers = new[]
            {
                new Supplier { Name = "Davao Fresh Farms",      ContactPerson = "Ramon Dela Cruz",  Phone = "09171234567", Email = "ramon@davaofresh.ph",    Address = "Km 12 Diversion Road, Davao City",   City = "Davao City",    TaxId = "123-456-789-000", IsActive = true, Balance = 45000m  },
                new Supplier { Name = "Mindanao Fruit Traders",  ContactPerson = "Maria Santos",     Phone = "09281234567", Email = "maria@mindanaofruit.ph",  Address = "Bankerohan Market, Davao City",       City = "Davao City",    TaxId = "234-567-890-000", IsActive = true, Balance = 28500m  },
                new Supplier { Name = "Cebu Tropical Produce",   ContactPerson = "Jose Reyes",       Phone = "09391234567", Email = "jose@cebutropical.ph",    Address = "Carbon Market, Cebu City",            City = "Cebu City",     TaxId = "345-678-901-000", IsActive = true, Balance = 12000m  },
                new Supplier { Name = "Benguet Highlands Co-op", ContactPerson = "Ana Bautista",     Phone = "09451234567", Email = "ana@benguethighlands.ph", Address = "La Trinidad, Benguet",                City = "La Trinidad",   TaxId = "456-789-012-000", IsActive = true, Balance = 0m      },
                new Supplier { Name = "Laguna Citrus Growers",   ContactPerson = "Pedro Villanueva", Phone = "09561234567", Email = "pedro@lagunacitrus.ph",   Address = "Calamba, Laguna",                     City = "Calamba",       TaxId = "567-890-123-000", IsActive = false, Balance = 0m     },
            };
            db.Suppliers.AddRange(suppliers);
            await db.SaveChangesAsync();

            // Link products to suppliers
            var products = await db.Products.ToListAsync();
            var s1 = suppliers[0]; var s2 = suppliers[1]; var s3 = suppliers[2];

            var links = new List<SupplierProduct>();
            foreach (var p in products.Where(p => p.Name is "Durian" or "Mangosteen" or "Lanzones" or "Rambutan"))
                links.Add(new SupplierProduct { SupplierId = s1.Id, ProductId = p.Id, SupplierPrice = p.CostPrice, IsPreferredSupplier = true });
            foreach (var p in products.Where(p => p.Name is "Mango" or "Papaya" or "Banana" or "Jackfruit"))
                links.Add(new SupplierProduct { SupplierId = s2.Id, ProductId = p.Id, SupplierPrice = p.CostPrice, IsPreferredSupplier = true });
            foreach (var p in products.Where(p => p.Name is "Pomelo" or "Watermelon"))
                links.Add(new SupplierProduct { SupplierId = s3.Id, ProductId = p.Id, SupplierPrice = p.CostPrice, IsPreferredSupplier = true });

            db.SupplierProducts.AddRange(links);

            // Seed deliveries and payments
            var rng = new Random(7);
            var productMap = products.ToDictionary(p => p.Name);
            var deliveryData = new[]
            {
                (s1, "Durian",     150, 180m, DateTime.Today.AddDays(-14), "DEL-2026-001"),
                (s1, "Mangosteen", 200, 85m,  DateTime.Today.AddDays(-10), "DEL-2026-002"),
                (s2, "Mango",      300, 35m,  DateTime.Today.AddDays(-7),  "DEL-2026-003"),
                (s2, "Banana",     400, 18m,  DateTime.Today.AddDays(-5),  "DEL-2026-004"),
                (s3, "Pomelo",     180, 55m,  DateTime.Today.AddDays(-3),  "DEL-2026-005"),
                (s1, "Lanzones",   220, 40m,  DateTime.Today.AddDays(-2),  "DEL-2026-006"),
                (s2, "Papaya",     160, 22m,  DateTime.Today.AddDays(-1),  "DEL-2026-007"),
            };

            foreach (var (sup, prodName, qty, cost, date, refNo) in deliveryData)
            {
                if (!productMap.TryGetValue(prodName, out var prod)) continue;
                db.SupplierDeliveries.Add(new SupplierDelivery
                {
                    SupplierId = sup.Id, ProductId = prod.Id,
                    Quantity = qty, UnitCost = cost, TotalCost = qty * cost,
                    DeliveryDate = date, ReceivedBy = "manager",
                    ReferenceNumber = refNo, Notes = $"Regular delivery of {prodName}"
                });
            }

            // Payments
            db.SupplierPayments.AddRange(new[]
            {
                new SupplierPayment { SupplierId = s1.Id, Amount = 27000m, PaymentDate = DateTime.Today.AddDays(-12), PaymentMethod = "Bank Transfer", ReferenceNumber = "PAY-2026-001", Notes = "Payment for DEL-2026-001", PaidBy = "cfo", IsPending = false },
                new SupplierPayment { SupplierId = s2.Id, Amount = 10500m, PaymentDate = DateTime.Today.AddDays(-6),  PaymentMethod = "Check",         ReferenceNumber = "PAY-2026-002", Notes = "Payment for DEL-2026-003", PaidBy = "cfo", IsPending = false },
                new SupplierPayment { SupplierId = s1.Id, Amount = 17000m, PaymentDate = DateTime.Today.AddDays(-8),  PaymentMethod = "Bank Transfer", ReferenceNumber = "PAY-2026-003", Notes = "Payment for DEL-2026-002", PaidBy = "cfo", IsPending = false },
                new SupplierPayment { SupplierId = s3.Id, Amount = 9900m,  PaymentDate = DateTime.Today,              PaymentMethod = "Cash",          ReferenceNumber = "PAY-2026-004", Notes = "Partial payment for DEL-2026-005", PaidBy = "cfo", IsPending = true  },
            });

            await db.SaveChangesAsync();
        }

        // ── BUDGETS ────────────────────────────────────────────────────────────
        static async Task SeedBudgetsAsync(ApplicationDbContext db, ApplicationUser? cfo, int month, int year)
        {
            if (await db.Budgets.AnyAsync(b => b.Month == month && b.Year == year)) return;

            var budgets = new[]
            {
                new Budget { Title = "Fruit Purchasing Budget",  Category = "Purchasing",  AllocatedAmount = 1_200_000m, SpentAmount = 890_000m,  Month = month, Year = year, CreatedBy = cfo?.Id ?? "cfo", Notes = "Monthly stock procurement from suppliers" },
                new Budget { Title = "Staff Labor Budget",       Category = "Labor",       AllocatedAmount = 180_000m,   SpentAmount = 145_000m,  Month = month, Year = year, CreatedBy = cfo?.Id ?? "cfo", Notes = "Salaries and wages for all staff" },
                new Budget { Title = "Utilities Budget",         Category = "Utilities",   AllocatedAmount = 45_000m,    SpentAmount = 38_500m,   Month = month, Year = year, CreatedBy = cfo?.Id ?? "cfo", Notes = "Electricity, water, internet" },
                new Budget { Title = "Transport & Logistics",    Category = "Transport",   AllocatedAmount = 60_000m,    SpentAmount = 42_000m,   Month = month, Year = year, CreatedBy = cfo?.Id ?? "cfo", Notes = "Delivery vehicles and fuel" },
                new Budget { Title = "Marketing & Promotions",   Category = "Marketing",   AllocatedAmount = 25_000m,    SpentAmount = 8_500m,    Month = month, Year = year, CreatedBy = cfo?.Id ?? "cfo", Notes = "Social media ads and flyers" },
                new Budget { Title = "Spoilage Allowance",       Category = "Spoilage",    AllocatedAmount = 50_000m,    SpentAmount = 31_200m,   Month = month, Year = year, CreatedBy = cfo?.Id ?? "cfo", Notes = "30.5% spoilage rate from 5-year dataset" },
                new Budget { Title = "Miscellaneous Expenses",   Category = "Other",       AllocatedAmount = 30_000m,    SpentAmount = 19_800m,   Month = month, Year = year, CreatedBy = cfo?.Id ?? "cfo", Notes = "Office supplies, repairs, etc." },
            };
            db.Budgets.AddRange(budgets);
            await db.SaveChangesAsync();
        }

        // ── EXECUTIVE ALERTS ───────────────────────────────────────────────────
        static async Task SeedExecutiveAlertsAsync(ApplicationDbContext db)
        {
            if (await db.ExecutiveAlerts.AnyAsync()) return;

            db.ExecutiveAlerts.AddRange(new[]
            {
                new ExecutiveAlert { Title = "Revenue Below Monthly Target",    Message = "Current month revenue is 12% below the ₱3.3M target. Durian and Mangosteen sales are underperforming.",                                    Severity = "warning",  Category = "revenue",       Icon = "trending-down",  CreatedAt = DateTime.UtcNow.AddDays(-2),  IsResolved = false },
                new ExecutiveAlert { Title = "Spoilage Rate Exceeds Benchmark", Message = "This week's spoilage rate is 34.2%, above the 30.5% dataset benchmark. Review cold storage and stock rotation procedures.",                  Severity = "critical", Category = "inventory",     Icon = "alert-triangle", CreatedAt = DateTime.UtcNow.AddDays(-1),  IsResolved = false },
                new ExecutiveAlert { Title = "Supplier Payment Overdue",        Message = "Payment to Davao Fresh Farms (₱45,000) is 5 days overdue. This may affect next delivery schedule.",                                          Severity = "warning",  Category = "expenses",      Icon = "credit-card",    CreatedAt = DateTime.UtcNow.AddHours(-8), IsResolved = false },
                new ExecutiveAlert { Title = "Strong Weekend Sales Performance", Message = "Saturday and Sunday sales averaged ₱142,000/day — 30% above weekday average. Consider extending weekend operating hours.",                   Severity = "info",     Category = "revenue",       Icon = "trending-up",    CreatedAt = DateTime.UtcNow.AddDays(-3),  IsResolved = false },
                new ExecutiveAlert { Title = "Banana Stock Critically Low",     Message = "Banana inventory is at 45 units — below the 50-unit reorder point. A delivery from Mindanao Fruit Traders is recommended immediately.",       Severity = "critical", Category = "inventory",     Icon = "package",        CreatedAt = DateTime.UtcNow.AddHours(-3), IsResolved = false },
                new ExecutiveAlert { Title = "Gross Margin Improved",           Message = "Gross margin reached 71.2% this month, up from 68.4% last month. Improved purchasing negotiations with Davao Fresh Farms contributed.",       Severity = "info",     Category = "profitability", Icon = "bar-chart",      CreatedAt = DateTime.UtcNow.AddDays(-5),  IsResolved = true,  ResolvedAt = DateTime.UtcNow.AddDays(-4), AcknowledgedBy = "Chief Executive Officer" },
            });
            await db.SaveChangesAsync();
        }

        // ── KPI TARGETS ────────────────────────────────────────────────────────
        static async Task SeedKPITargetsAsync(ApplicationDbContext db, ApplicationUser? cfo, int month, int year)
        {
            if (await db.KPITargets.AnyAsync(k => k.Month == month && k.Year == year)) return;

            db.KPITargets.AddRange(new[]
            {
                new KPITarget { KPIName = "Monthly Revenue",          TargetValue = 3_300_000m, CurrentValue = 2_904_000m, Unit = "amount", Month = month, Year = year, Status = "at-risk",   VariancePercent = -12.0m, CreatedBy = cfo?.Id ?? "cfo", Notes = "Based on ₱39.8M annual target ÷ 12" },
                new KPITarget { KPIName = "Gross Margin",             TargetValue = 68.0m,      CurrentValue = 71.2m,      Unit = "%",      Month = month, Year = year, Status = "on-track",  VariancePercent = 4.7m,   CreatedBy = cfo?.Id ?? "cfo", Notes = "Target based on 5-year average" },
                new KPITarget { KPIName = "Net Profit Margin",        TargetValue = 30.0m,      CurrentValue = 31.9m,      Unit = "%",      Month = month, Year = year, Status = "on-track",  VariancePercent = 6.3m,   CreatedBy = cfo?.Id ?? "cfo", Notes = "31.9% from dataset benchmark" },
                new KPITarget { KPIName = "Spoilage Rate",            TargetValue = 30.5m,      CurrentValue = 34.2m,      Unit = "%",      Month = month, Year = year, Status = "critical",  VariancePercent = 12.1m,  CreatedBy = cfo?.Id ?? "cfo", Notes = "Must stay at or below 30.5% benchmark" },
                new KPITarget { KPIName = "Operational Efficiency",   TargetValue = 70.0m,      CurrentValue = 68.1m,      Unit = "%",      Month = month, Year = year, Status = "at-risk",   VariancePercent = -2.7m,  CreatedBy = cfo?.Id ?? "cfo", Notes = "Revenue retained after all expenses" },
                new KPITarget { KPIName = "Supplier Payment Cycle",   TargetValue = 7.0m,       CurrentValue = 9.5m,       Unit = "days",   Month = month, Year = year, Status = "at-risk",   VariancePercent = 35.7m,  CreatedBy = cfo?.Id ?? "cfo", Notes = "Average days to pay suppliers" },
                new KPITarget { KPIName = "Daily Transaction Count",  TargetValue = 18.0m,      CurrentValue = 16.4m,      Unit = "count",  Month = month, Year = year, Status = "on-track",  VariancePercent = -8.9m,  CreatedBy = cfo?.Id ?? "cfo", Notes = "Average POS transactions per day" },
            });
            await db.SaveChangesAsync();
        }

        // ── RISK REGISTERS ─────────────────────────────────────────────────────
        static async Task SeedRiskRegistersAsync(ApplicationDbContext db)
        {
            if (await db.RiskRegisters.AnyAsync()) return;

            db.RiskRegisters.AddRange(new[]
            {
                new RiskRegister { RiskName = "Revenue Volatility",          Description = "Daily revenue fluctuates significantly due to seasonal demand and weather conditions affecting fruit supply.",                                    Category = "Revenue",       Probability = 0.65, Impact = 0.40, RiskScore = 0.42, Volatility = 18.5, MitigationStrategy = "Diversify product mix; introduce pre-orders for premium fruits like Durian.",                    Owner = "Store Manager",  Status = "in-progress", Priority = "high",     TargetResolutionDate = DateTime.Today.AddMonths(2) },
                new RiskRegister { RiskName = "High Spoilage Rate",          Description = "Spoilage consistently runs at 30-35%, eroding margins. Cold storage capacity is insufficient during peak season.",                              Category = "Operational",   Probability = 0.80, Impact = 0.35, RiskScore = 0.45, Volatility = 12.3, MitigationStrategy = "Invest in additional cold storage; implement FIFO stock rotation; reduce order quantities.",       Owner = "Warehouse Staff", Status = "identified",  Priority = "critical", TargetResolutionDate = DateTime.Today.AddMonths(1) },
                new RiskRegister { RiskName = "Supplier Concentration Risk", Description = "Over 60% of fruit supply comes from a single supplier (Davao Fresh Farms). Any disruption would severely impact operations.",                   Category = "External",      Probability = 0.30, Impact = 0.70, RiskScore = 0.33, Volatility = 8.1,  MitigationStrategy = "Onboard 2 additional suppliers; negotiate backup supply agreements with Cebu Tropical Produce.",  Owner = "CFO",            Status = "in-progress", Priority = "high",     TargetResolutionDate = DateTime.Today.AddMonths(3) },
                new RiskRegister { RiskName = "Cash Flow Timing Gap",        Description = "Supplier payments are due before customer revenue is fully collected, creating a 3-5 day cash flow gap each week.",                             Category = "Liquidity",     Probability = 0.55, Impact = 0.45, RiskScore = 0.38, Volatility = 15.2, MitigationStrategy = "Negotiate 14-day payment terms with suppliers; maintain a ₱200,000 operating reserve.",            Owner = "CFO",            Status = "in-progress", Priority = "high",     TargetResolutionDate = DateTime.Today.AddMonths(1) },
                new RiskRegister { RiskName = "Seasonal Demand Fluctuation", Description = "Revenue drops 15-20% during dry season (Oct-May) as tropical fruit availability decreases and consumer demand shifts.",                         Category = "External",      Probability = 0.90, Impact = 0.25, RiskScore = 0.30, Volatility = 22.0, MitigationStrategy = "Introduce complementary products during off-season; run promotions to maintain foot traffic.",      Owner = "Store Manager",  Status = "mitigated",   Priority = "medium",   TargetResolutionDate = DateTime.Today.AddMonths(6) },
                new RiskRegister { RiskName = "Staff Turnover",              Description = "High turnover among cashiers and warehouse staff increases training costs and reduces operational efficiency.",                                   Category = "Operational",   Probability = 0.40, Impact = 0.20, RiskScore = 0.18, Volatility = 5.5,  MitigationStrategy = "Implement performance bonuses; improve working conditions; cross-train staff for multiple roles.", Owner = "Admin",          Status = "identified",  Priority = "low",      TargetResolutionDate = DateTime.Today.AddMonths(4) },
            });
            await db.SaveChangesAsync();
        }

        // ── COMPLIANCE FLAGS ───────────────────────────────────────────────────
        static async Task SeedComplianceFlagsAsync(ApplicationDbContext db)
        {
            if (await db.ComplianceFlags.AnyAsync()) return;

            db.ComplianceFlags.AddRange(new[]
            {
                new ComplianceFlag { Module = "Finance",    Description = "Expense entry of ₱15,000 under 'Other' category lacks supporting receipt or documentation.",                                  Severity = "warning",  PolicyReference = "Internal Policy §4.2",  FlaggedBy = "System", FlaggedAt = DateTime.UtcNow.AddDays(-3),  IsResolved = false },
                new ComplianceFlag { Module = "POS",        Description = "3 sales transactions were voided within 24 hours by the same cashier without manager approval on record.",                    Severity = "critical", PolicyReference = "POS Policy §7.1",        FlaggedBy = "System", FlaggedAt = DateTime.UtcNow.AddDays(-1),  IsResolved = false },
                new ComplianceFlag { Module = "Inventory",  Description = "Spoilage rate of 34.2% this week exceeds the 30.5% benchmark. Excess spoilage must be documented and reported to CFO.",      Severity = "warning",  PolicyReference = "Inventory SOP §3.4",     FlaggedBy = "System", FlaggedAt = DateTime.UtcNow.AddDays(-2),  IsResolved = false },
                new ComplianceFlag { Module = "Finance",    Description = "Supplier payment to Davao Fresh Farms is 5 days overdue. Late payments may incur penalties per supplier contract.",           Severity = "critical", PolicyReference = "Procurement Policy §5.1", FlaggedBy = "System", FlaggedAt = DateTime.UtcNow.AddHours(-6), IsResolved = false },
                new ComplianceFlag { Module = "POS",        Description = "Daily cash reconciliation was not completed for May 1, 2026. Missing sign-off from shift supervisor.",                        Severity = "warning",  PolicyReference = "Cash Handling SOP §2.1", FlaggedBy = "System", FlaggedAt = DateTime.UtcNow.AddDays(-4),  IsResolved = true,  ResolvedBy = "manager", ResolvedAt = DateTime.UtcNow.AddDays(-3) },
                new ComplianceFlag { Module = "Inventory",  Description = "Stock-in for Banana (400 units) was recorded without a corresponding supplier delivery reference number.",                     Severity = "info",     PolicyReference = "Inventory SOP §2.3",     FlaggedBy = "System", FlaggedAt = DateTime.UtcNow.AddDays(-5),  IsResolved = true,  ResolvedBy = "manager", ResolvedAt = DateTime.UtcNow.AddDays(-4) },
            });
            await db.SaveChangesAsync();
        }

        // ── TREND REPORTS ──────────────────────────────────────────────────────
        static async Task SeedTrendReportsAsync(ApplicationDbContext db, ApplicationUser? cfo, DateTime today)
        {
            if (await db.TrendReports.AnyAsync()) return;

            var q1Start = new DateTime(today.Year, 1, 1);
            var q1End   = new DateTime(today.Year, 3, 31);
            var m1Start = today.AddDays(-30);

            db.TrendReports.AddRange(new[]
            {
                new TrendReport
                {
                    Title      = "Q1 2026 Revenue Performance",
                    ReportType = "Revenue",
                    Summary    = "Q1 2026 total revenue reached ₱9.87M, up 8.3% from Q1 2025. Durian contributed 20.9% of total revenue. Weekend sales averaged 7% higher than weekdays. Rainy season months (Jan-Mar) showed stable demand with minimal seasonal dip.",
                    PeriodFrom = q1Start, PeriodTo = q1End,
                    CreatedBy  = cfo?.UserName ?? "cfo",
                    CreatedAt  = today.AddDays(-15),
                    DataJson   = "{\"labels\":[\"Jan\",\"Feb\",\"Mar\"],\"revenue\":[3280000,3190000,3400000],\"expenses\":[2230000,2170000,2310000]}"
                },
                new TrendReport
                {
                    Title      = "30-Day Expense Analysis — April 2026",
                    ReportType = "Expense",
                    Summary    = "Total expenses for April 2026 were ₱2.24M. Purchasing (fruit procurement) accounted for 53.6% of all expenses. Labor costs remained stable at ₱145,000. Spoilage write-offs totaled ₱31,200 — within the ₱50,000 monthly allowance.",
                    PeriodFrom = today.AddDays(-60), PeriodTo = today.AddDays(-30),
                    CreatedBy  = cfo?.UserName ?? "cfo",
                    CreatedAt  = today.AddDays(-7),
                    DataJson   = "{\"categories\":[\"Purchasing\",\"Labor\",\"Utilities\",\"Transport\",\"Spoilage\",\"Other\"],\"amounts\":[1200000,145000,38500,42000,31200,19800]}"
                },
                new TrendReport
                {
                    Title      = "Top Products by Revenue — Last 30 Days",
                    ReportType = "Sales",
                    Summary    = "Durian remains the top revenue driver at 20.9% share. Mangosteen and Pomelo follow at 12.7% and 10.2% respectively. Banana shows the highest unit volume but lowest revenue per unit. Rambutan demand increased 15% week-over-week.",
                    PeriodFrom = m1Start, PeriodTo = today,
                    CreatedBy  = cfo?.UserName ?? "cfo",
                    CreatedAt  = today.AddDays(-2),
                    DataJson   = "{\"products\":[\"Durian\",\"Mangosteen\",\"Pomelo\",\"Mango\",\"Lanzones\"],\"shares\":[20.9,12.7,10.2,10.1,10.0]}"
                },
            });
            await db.SaveChangesAsync();
        }

        // ── PETTY CASH CLAIMS ──────────────────────────────────────────────────
        static async Task SeedPettyCashClaimsAsync(ApplicationDbContext db, ApplicationUser? cashier, ApplicationUser? cfo)
        {
            if (await db.PettyCashClaims.AnyAsync()) return;

            db.PettyCashClaims.AddRange(new[]
            {
                new PettyCashClaim { Description = "Plastic bags and packaging materials",  Category = "Supplies",   Amount = 850m,   Notes = "Bought from SM Supermarket — receipt attached",          SubmittedBy = cashier?.Id ?? "", SubmittedByName = cashier?.FullName ?? "Front Cashier", SubmittedAt = DateTime.UtcNow.AddDays(-5), Status = "Approved",  ReviewedBy = cfo?.UserName, ReviewedAt = DateTime.UtcNow.AddDays(-4), ReviewNotes = "Approved. Valid receipt provided." },
                new PettyCashClaim { Description = "Tricycle fare for market run",          Category = "Transport",  Amount = 120m,   Notes = "Emergency stock pickup from Bankerohan Market",          SubmittedBy = cashier?.Id ?? "", SubmittedByName = cashier?.FullName ?? "Front Cashier", SubmittedAt = DateTime.UtcNow.AddDays(-3), Status = "Approved",  ReviewedBy = cfo?.UserName, ReviewedAt = DateTime.UtcNow.AddDays(-2), ReviewNotes = "Approved." },
                new PettyCashClaim { Description = "Staff lunch during inventory count",    Category = "Meals",      Amount = 650m,   Notes = "Team of 5 staff — overtime inventory count on Sunday",   SubmittedBy = cashier?.Id ?? "", SubmittedByName = cashier?.FullName ?? "Front Cashier", SubmittedAt = DateTime.UtcNow.AddDays(-1), Status = "Pending",   ReviewedBy = null,          ReviewedAt = null,                        ReviewNotes = null },
                new PettyCashClaim { Description = "Printer ink cartridge replacement",    Category = "Supplies",   Amount = 480m,   Notes = "Receipt printer ran out of ink during peak hours",       SubmittedBy = cashier?.Id ?? "", SubmittedByName = cashier?.FullName ?? "Front Cashier", SubmittedAt = DateTime.UtcNow.AddHours(-6), Status = "Pending",  ReviewedBy = null,          ReviewedAt = null,                        ReviewNotes = null },
                new PettyCashClaim { Description = "Cleaning supplies for storage area",   Category = "Supplies",   Amount = 320m,   Notes = "Bleach, mops, and disinfectant for weekly cleaning",     SubmittedBy = cashier?.Id ?? "", SubmittedByName = cashier?.FullName ?? "Front Cashier", SubmittedAt = DateTime.UtcNow.AddDays(-7), Status = "Rejected",  ReviewedBy = cfo?.UserName, ReviewedAt = DateTime.UtcNow.AddDays(-6), ReviewNotes = "Rejected. Cleaning supplies are covered under the monthly utilities budget. Please coordinate with the manager." },
                new PettyCashClaim { Description = "Extension cord for POS terminal",      Category = "Supplies",   Amount = 275m,   Notes = "Old cord was damaged — safety hazard",                   SubmittedBy = cashier?.Id ?? "", SubmittedByName = cashier?.FullName ?? "Front Cashier", SubmittedAt = DateTime.UtcNow.AddDays(-2), Status = "Pending",   ReviewedBy = null,          ReviewedAt = null,                        ReviewNotes = null },
            });
            await db.SaveChangesAsync();
        }

        // ── SPOILAGE RECORDS ───────────────────────────────────────────────────
        static async Task SeedSpoilageRecordsAsync(ApplicationDbContext db, ApplicationUser? manager)
        {
            if (await db.SpoilageRecords.AnyAsync()) return;

            var products = await db.Products.ToListAsync();
            var productMap = products.ToDictionary(p => p.Name);
            var rng = new Random(13);
            var today = DateTime.Today;

            var spoilageData = new[]
            {
                ("Durian",     8,  "Overripe",  today.AddDays(-14), "Batch arrived overripe from supplier"),
                ("Mangosteen", 15, "Overripe",  today.AddDays(-12), "Left in sun too long during display"),
                ("Banana",     25, "Overripe",  today.AddDays(-10), "Overstock — sold slower than expected"),
                ("Papaya",     12, "Damaged",   today.AddDays(-8),  "Damaged during unloading from delivery truck"),
                ("Rambutan",   20, "Expired",   today.AddDays(-7),  "Exceeded 5-day shelf life"),
                ("Mango",      18, "Overripe",  today.AddDays(-5),  "Weekend surplus not sold"),
                ("Lanzones",   22, "Overripe",  today.AddDays(-4),  "High humidity accelerated ripening"),
                ("Watermelon",  3, "Damaged",   today.AddDays(-3),  "Cracked during storage"),
                ("Pomelo",      6, "Overripe",  today.AddDays(-2),  "Slow sales this week"),
                ("Durian",      5, "Other",     today.AddDays(-1),  "Customer returned — quality complaint"),
                ("Banana",     18, "Overripe",  today,              "Morning inspection — overnight spoilage"),
            };

            foreach (var (name, qty, reason, date, notes) in spoilageData)
            {
                if (!productMap.TryGetValue(name, out var prod)) continue;
                db.SpoilageRecords.Add(new SpoilageRecord
                {
                    ProductId     = prod.Id,
                    Quantity      = qty,
                    EstimatedLoss = qty * prod.CostPrice,
                    Reason        = reason,
                    RecordedBy    = manager?.UserName ?? "manager",
                    RecordedAt    = date.AddHours(rng.Next(7, 10)),
                    Notes         = notes
                });
            }
            await db.SaveChangesAsync();
        }

        // ── STOCK MOVEMENTS ────────────────────────────────────────────────────
        static async Task SeedStockMovementsAsync(ApplicationDbContext db, ApplicationUser? manager)
        {
            if (await db.StockMovements.AnyAsync()) return;

            var products = await db.Products.ToListAsync();
            var productMap = products.ToDictionary(p => p.Name);
            var today = DateTime.Today;

            var movements = new[]
            {
                ("Durian",     MovementType.StockIn,    150, 0,   150, "DEL-2026-001", today.AddDays(-14)),
                ("Mangosteen", MovementType.StockIn,    200, 0,   200, "DEL-2026-002", today.AddDays(-10)),
                ("Mango",      MovementType.StockIn,    300, 0,   300, "DEL-2026-003", today.AddDays(-7)),
                ("Banana",     MovementType.StockIn,    400, 0,   400, "DEL-2026-004", today.AddDays(-5)),
                ("Pomelo",     MovementType.StockIn,    180, 0,   180, "DEL-2026-005", today.AddDays(-3)),
                ("Durian",     MovementType.Damaged,      8, 150, 142, "SPL-001",      today.AddDays(-14)),
                ("Banana",     MovementType.Damaged,     25, 400, 375, "SPL-003",      today.AddDays(-10)),
                ("Mango",      MovementType.Adjustment,  50, 300, 250, "ADJ-001",      today.AddDays(-6)),
                ("Lanzones",   MovementType.StockIn,    220, 0,   220, "DEL-2026-006", today.AddDays(-2)),
                ("Papaya",     MovementType.StockIn,    160, 0,   160, "DEL-2026-007", today.AddDays(-1)),
            };

            foreach (var (name, type, qty, prev, next, refNo, date) in movements)
            {
                if (!productMap.TryGetValue(name, out var prod)) continue;
                db.StockMovements.Add(new StockMovement
                {
                    ProductId       = prod.Id,
                    Type            = type,
                    Quantity        = qty,
                    PreviousStock   = prev,
                    NewStock        = next,
                    Notes           = type == MovementType.StockIn ? $"Delivery from supplier — {refNo}" : type == MovementType.Damaged ? "Spoilage write-off" : "Manual stock adjustment",
                    ReferenceNumber = refNo,
                    PerformedBy     = manager?.UserName ?? "manager",
                    MovementDate    = date
                });
            }
            await db.SaveChangesAsync();
        }
    }
}
