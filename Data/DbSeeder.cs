using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using CRLFruitstandESS.Models;

namespace CRLFruitstandESS.Data
{
    public static class DbSeeder
    {
        public static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            // Define roles
            string[] roles = { "Admin", "CFO", "CEO", "Manager", "Cashier", "Warehouse", "Compliance" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new ApplicationRole
                    {
                        Name = role,
                        Description = $"{role} role for CRL Fruitstand ESS"
                    });
                }
            }

            // Seed default Admin user
            var adminEmail = "crisleebanawa07@gmail.com";
            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var admin = new ApplicationUser
                {
                    UserName = "admin",
                    Email = adminEmail,
                    FullName = "System Administrator",
                    Department = "IT",
                    IsActive = true,
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(admin, "Admin@12345");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(admin, "Admin");
            }

            // Seed default CEO user
            var ceoEmail = "crisleebanawa07@gmail.com";
            if (await userManager.FindByEmailAsync(ceoEmail) == null)
            {
                var ceo = new ApplicationUser
                {
                    UserName = "ceo",
                    Email = ceoEmail,
                    FullName = "Chief Executive Officer",
                    Department = "Executive",
                    IsActive = true,
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(ceo, "Ceo@12345!");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(ceo, "CEO");
            }

            // Seed default CFO user
            var cfoEmail = "crisleebanawa07@gmail.com";
            if (await userManager.FindByEmailAsync(cfoEmail) == null)
            {
                var cfo = new ApplicationUser
                {
                    UserName = "cfo",
                    Email = cfoEmail,
                    FullName = "Chief Financial Officer",
                    Department = "Finance",
                    IsActive = true,
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(cfo, "Cfo@12345");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(cfo, "CFO");
            }

            // Seed default Manager user
            var managerEmail = "crisleebanawa07@gmail.com";
            if (await userManager.FindByEmailAsync(managerEmail) == null)
            {
                var manager = new ApplicationUser
                {
                    UserName = "manager",
                    Email = managerEmail,
                    FullName = "Store Manager",
                    Department = "Operations",
                    IsActive = true,
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(manager, "Manager@12345");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(manager, "Manager");
            }

            // Seed default Cashier user
            var cashierEmail = "crisleebanawa07@gmail.com";
            if (await userManager.FindByEmailAsync(cashierEmail) == null)
            {
                var cashier = new ApplicationUser
                {
                    UserName = "cashier",
                    Email = cashierEmail,
                    FullName = "Front Cashier",
                    Department = "Sales",
                    IsActive = true,
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(cashier, "Cashier@12345");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(cashier, "Cashier");
            }
        }

        // Seed sample products with inventory
        // Product prices and costs sourced from 5-year actual sales data (2020-2024)
        public static async Task SeedProductsAsync(ApplicationDbContext context)
        {
            if (!context.Products.Any())
            {
                var products = new List<Product>
                {
                    // Prices & costs from actual 5-year dataset (2020-2024)
                    new Product { Name = "Durian",      Category = "Fruits", Price = 350.00m, CostPrice = 180.00m, IsActive = true, Emoji = "🌵" },
                    new Product { Name = "Mangosteen",  Category = "Fruits", Price = 180.00m, CostPrice = 85.00m,  IsActive = true, Emoji = "🍇" },
                    new Product { Name = "Pomelo",      Category = "Fruits", Price = 120.00m, CostPrice = 55.00m,  IsActive = true, Emoji = "🍊" },
                    new Product { Name = "Mango",       Category = "Fruits", Price = 80.00m,  CostPrice = 35.00m,  IsActive = true, Emoji = "🥭" },
                    new Product { Name = "Lanzones",    Category = "Fruits", Price = 90.00m,  CostPrice = 40.00m,  IsActive = true, Emoji = "🍈" },
                    new Product { Name = "Jackfruit",   Category = "Fruits", Price = 150.00m, CostPrice = 70.00m,  IsActive = true, Emoji = "🍐" },
                    new Product { Name = "Rambutan",    Category = "Fruits", Price = 70.00m,  CostPrice = 28.00m,  IsActive = true, Emoji = "🍒" },
                    new Product { Name = "Banana",      Category = "Fruits", Price = 45.00m,  CostPrice = 18.00m,  IsActive = true, Emoji = "🍌" },
                    new Product { Name = "Watermelon",  Category = "Fruits", Price = 95.00m,  CostPrice = 38.00m,  IsActive = true, Emoji = "🍉" },
                    new Product { Name = "Papaya",      Category = "Fruits", Price = 60.00m,  CostPrice = 22.00m,  IsActive = true, Emoji = "🍑" },
                };

                // Inventory levels based on typical daily stock-in from dataset (~100-300 units/day)
                var stockLevels = new Dictionary<string, int>
                {
                    { "Durian",     120 }, { "Mangosteen", 180 }, { "Pomelo",    200 },
                    { "Mango",      250 }, { "Lanzones",   220 }, { "Jackfruit", 110 },
                    { "Rambutan",   210 }, { "Banana",     320 }, { "Watermelon",140 },
                    { "Papaya",     190 },
                };

                foreach (var product in products)
                {
                    context.Products.Add(product);
                    context.Inventory.Add(new Inventory
                    {
                        Product       = product,
                        Quantity      = stockLevels.TryGetValue(product.Name, out var qty) ? qty : 150,
                        MinStockLevel = 20,
                        ReorderPoint  = 40,
                        LastUpdated   = DateTime.Now
                    });
                }
                await context.SaveChangesAsync();
            }
        }

        // ── Seed 90 days of historical data for forecasting / analytics
        // Data patterns sourced from actual 5-year CRL Fruitstand dataset (2020-2024):
        //   Annual revenue: ~₱39.4M–₱39.97M  →  ~₱109K/day average
        //   Spoilage rate: ~30.5% of stock-in
        //   Net margin: ~31.9%
        //   Product revenue weights: Durian 20.9%, Mangosteen 12.7%, Pomelo 10.2%,
        //     Mango 10.1%, Lanzones 10.0%, Jackfruit 8.5%, Rambutan 7.9%,
        //     Banana 7.1%, Watermelon 6.6%, Papaya 6.1%
        public static async Task SeedHistoricalDataAsync(ApplicationDbContext context, IServiceProvider services)
        {
            // Only seed if fewer than 30 revenue records exist
            if (await context.Revenues.CountAsync() >= 30)
                return;

            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var cashier = await userManager.FindByNameAsync("cashier");
            if (cashier == null) return;

            var products = context.Products.ToList();
            if (!products.Any()) return;

            var rng   = new Random(42);
            var today = DateTime.Today;
            var start = today.AddDays(-90);

            // ── Product revenue weights from 5-year dataset (must sum to 1.0)
            // Key = product name, Value = share of daily revenue
            var revenueWeights = new Dictionary<string, double>
            {
                { "Durian",     0.209 },
                { "Mangosteen", 0.127 },
                { "Pomelo",     0.102 },
                { "Mango",      0.101 },
                { "Lanzones",   0.100 },
                { "Jackfruit",  0.085 },
                { "Rambutan",   0.079 },
                { "Banana",     0.071 },
                { "Watermelon", 0.066 },
                { "Papaya",     0.061 },
            };

            // Build a lookup: product name → Product entity
            var productMap = products.ToDictionary(p => p.Name, p => p);

            // ── Expense categories with realistic daily ranges for a fruit stand
            // (Purchasing is the dominant cost — ~₱800-1000/day for stock replenishment)
            var expenseCategories = new[]
            {
                ("Purchasing", 900m,  100m),   // stock buying — largest daily cost
                ("Labor",      1200m, 100m),   // staff wages
                ("Utilities",  400m,   50m),   // electricity, water
                ("Transport",  300m,   50m),   // delivery / logistics
                ("Spoilage",   150m,   50m),   // written-off spoiled stock
                ("Other",      200m,   50m),   // misc
            };

            // ── Target daily revenue: ₱109,000 (₱39.8M / 365)
            const decimal baseDailyRevenue = 109_000m;

            for (var day = start; day < today; day = day.AddDays(1))
            {
                // Weekend boost: +7% on Sat/Sun (fruit stands busier on market days)
                bool isWeekend = day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday;
                double weekendFactor = isWeekend ? 1.07 : 1.0;

                // Rainy-season boost: June–September +5% (higher fruit consumption)
                double seasonFactor = (day.Month >= 6 && day.Month <= 9) ? 1.05 : 1.0;

                // Random daily variance ±15%
                double variance = 1.0 + (rng.NextDouble() * 0.30 - 0.15);

                decimal targetDayRevenue = Math.Round(
                    baseDailyRevenue * (decimal)(weekendFactor * seasonFactor * variance), 2);

                // ── Generate individual sales transactions for the day
                // Typical fruit stand: 10–20 transactions on weekdays, 15–28 on weekends
                int txCount = isWeekend ? rng.Next(15, 29) : rng.Next(10, 21);

                // Distribute target revenue across transactions
                decimal revenueRemaining = targetDayRevenue;

                for (int t = 0; t < txCount; t++)
                {
                    bool isLastTx = (t == txCount - 1);

                    // Each transaction gets a proportional slice of the day's revenue
                    // with some per-transaction variance
                    decimal txShare = isLastTx
                        ? revenueRemaining
                        : Math.Round(revenueRemaining / (txCount - t) * (decimal)(0.7 + rng.NextDouble() * 0.6), 2);

                    txShare = Math.Max(50m, txShare); // minimum ₱50 per transaction

                    // Build sale items weighted by product revenue share
                    var saleItems = new List<(Product p, int qty)>();
                    decimal saleTotal = 0m;

                    // Pick 1–4 products per transaction, weighted by revenue share
                    int itemCount = rng.Next(1, 5);
                    var shuffledProducts = revenueWeights
                        .OrderBy(_ => rng.NextDouble())
                        .Take(itemCount)
                        .ToList();

                    foreach (var (name, weight) in shuffledProducts)
                    {
                        if (!productMap.TryGetValue(name, out var prod)) continue;

                        // Qty derived from the product's share of this transaction's value
                        decimal itemBudget = txShare * (decimal)(weight / shuffledProducts.Sum(x => x.Value));
                        int qty = Math.Max(1, (int)Math.Round(itemBudget / prod.Price));
                        qty = Math.Min(qty, 20); // cap at 20 units per line item

                        saleItems.Add((prod, qty));
                        saleTotal += prod.Price * qty;
                    }

                    if (!saleItems.Any()) continue;

                    saleTotal = Math.Round(saleTotal, 2);
                    revenueRemaining -= saleTotal;

                    var saleTime = day.AddHours(rng.Next(7, 20)).AddMinutes(rng.Next(0, 60));

                    var sale = new Sale
                    {
                        CashierId   = cashier.Id,
                        SaleDate    = saleTime,
                        TotalAmount = saleTotal,
                        AmountPaid  = saleTotal,
                        Change      = 0,
                        Status      = "Completed"
                    };
                    context.Sales.Add(sale);
                    await context.SaveChangesAsync();

                    foreach (var (p, qty) in saleItems)
                    {
                        context.SaleItems.Add(new SaleItem
                        {
                            SaleId    = sale.Id,
                            ProductId = p.Id,
                            Quantity  = qty,
                            UnitPrice = p.Price,
                            Subtotal  = p.Price * qty
                        });
                    }

                    context.Revenues.Add(new Revenue
                    {
                        Source          = "POS Sale",
                        Category        = "Direct Sales",
                        Amount          = saleTotal,
                        TransactionDate = saleTime,
                        Notes           = $"Historical sale #{sale.Id}",
                        RecordedBy      = cashier.Id,
                        CreatedAt       = saleTime,
                        IsDeleted       = false
                    });
                }

                // ── Daily expenses — all 6 categories every day (realistic for a fruit stand)
                // Spoilage ~30.5% of stock-in is captured in the Spoilage expense line
                foreach (var (cat, baseAmt, halfRange) in expenseCategories)
                {
                    decimal amt = Math.Round(
                        baseAmt + (decimal)(rng.NextDouble() * (double)(halfRange * 2) - (double)halfRange), 2);

                    context.Expenses.Add(new Expense
                    {
                        Description = $"{cat} expense",
                        Category    = cat,
                        Amount      = Math.Max(50m, amt),
                        ExpenseDate = day.AddHours(rng.Next(7, 18)),
                        RecordedBy  = "cfo",
                        CreatedAt   = day,
                        IsDeleted   = false
                    });
                }

                await context.SaveChangesAsync();
            }
        }

    }
}

