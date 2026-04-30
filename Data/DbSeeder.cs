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
            string[] roles = { "Admin", "CFO", "Manager", "Cashier" };

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
            var adminEmail = "admin@crlfruitstand.com";
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

            // Seed default CFO user
            var cfoEmail = "cfo@crlfruitstand.com";
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
            var managerEmail = "manager@crlfruitstand.com";
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
            var cashierEmail = "cashier@crlfruitstand.com";
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
        public static async Task SeedProductsAsync(ApplicationDbContext context)
        {
            if (!context.Products.Any())
            {
                var products = new List<Product>
                {
                    new Product { Name = "Apple",        Category = "Fruits",     Price = 25.00m,  CostPrice = 15.00m, IsActive = true, Emoji = "🍎" },
                    new Product { Name = "Banana",       Category = "Fruits",     Price = 15.00m,  CostPrice = 8.00m,  IsActive = true, Emoji = "🍌" },
                    new Product { Name = "Orange",       Category = "Fruits",     Price = 20.00m,  CostPrice = 12.00m, IsActive = true, Emoji = "🍊" },
                    new Product { Name = "Mango",        Category = "Fruits",     Price = 45.00m,  CostPrice = 28.00m, IsActive = true, Emoji = "🥭" },
                    new Product { Name = "Grapes",       Category = "Fruits",     Price = 120.00m, CostPrice = 75.00m, IsActive = true, Emoji = "🍇" },
                    new Product { Name = "Watermelon",   Category = "Fruits",     Price = 80.00m,  CostPrice = 50.00m, IsActive = true, Emoji = "🍉" },
                    new Product { Name = "Carrot",       Category = "Vegetables", Price = 18.00m,  CostPrice = 10.00m, IsActive = true, Emoji = "🥕" },
                    new Product { Name = "Broccoli",     Category = "Vegetables", Price = 35.00m,  CostPrice = 20.00m, IsActive = true, Emoji = "🥦" },
                    new Product { Name = "Tomato",       Category = "Vegetables", Price = 22.00m,  CostPrice = 13.00m, IsActive = true, Emoji = "🍅" },
                    new Product { Name = "Potato",       Category = "Vegetables", Price = 28.00m,  CostPrice = 16.00m, IsActive = true, Emoji = "🥔" },
                    new Product { Name = "Onion",        Category = "Vegetables", Price = 15.00m,  CostPrice = 8.00m,  IsActive = true, Emoji = "🧅" },
                    new Product { Name = "Rice 1kg",     Category = "Grains",     Price = 55.00m,  CostPrice = 38.00m, IsActive = true, Emoji = "🍚" },
                    new Product { Name = "Bread",        Category = "Bakery",     Price = 40.00m,  CostPrice = 25.00m, IsActive = true, Emoji = "🍞" },
                    new Product { Name = "Croissant",    Category = "Bakery",     Price = 65.00m,  CostPrice = 40.00m, IsActive = true, Emoji = "🥐" },
                    new Product { Name = "Milk 1L",      Category = "Dairy",      Price = 95.00m,  CostPrice = 65.00m, IsActive = true, Emoji = "🥛" },
                    new Product { Name = "Cheese",       Category = "Dairy",      Price = 150.00m, CostPrice = 100.00m,IsActive = true, Emoji = "🧀" },
                    new Product { Name = "Eggs (dozen)", Category = "Dairy",      Price = 110.00m, CostPrice = 75.00m, IsActive = true, Emoji = "🥚" },
                    new Product { Name = "Chicken",      Category = "Meat",       Price = 180.00m, CostPrice = 120.00m,IsActive = true, Emoji = "🍗" },
                    new Product { Name = "Beef",         Category = "Meat",       Price = 320.00m, CostPrice = 220.00m,IsActive = true, Emoji = "🥩" },
                    new Product { Name = "Fish",         Category = "Meat",       Price = 200.00m, CostPrice = 130.00m,IsActive = true, Emoji = "🐟" }
                };

                var rng = new Random(42);
                foreach (var product in products)
                {
                    context.Products.Add(product);
                    context.Inventory.Add(new Inventory
                    {
                        Product = product,
                        Quantity = rng.Next(50, 300),
                        MinStockLevel = 10,
                        ReorderPoint  = 20,
                        LastUpdated   = DateTime.Now
                    });
                }
                await context.SaveChangesAsync();
            }
        }

        // ── Seed 90 days of historical data for forecasting / analytics
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

            var rng     = new Random(99);
            var today   = DateTime.Today;
            var start   = today.AddDays(-90);

            var expenseCategories = new[]
            {
                ("Purchasing",  800m,  200m),
                ("Labor",       1200m, 150m),
                ("Utilities",   400m,  80m),
                ("Transport",   300m,  100m),
                ("Spoilage",    150m,  60m),
                ("Other",       200m,  80m)
            };

            for (var day = start; day < today; day = day.AddDays(1))
            {
                // Weekends slightly higher sales
                bool isWeekend = day.DayOfWeek == DayOfWeek.Saturday || day.DayOfWeek == DayOfWeek.Sunday;
                // Slight upward trend over 90 days
                double trendFactor = 1.0 + ((day - start).TotalDays / 90.0) * 0.25;
                int txCount = isWeekend
                    ? rng.Next(8, 18)
                    : rng.Next(4, 12);

                for (int t = 0; t < txCount; t++)
                {
                    // Pick 1-4 random products per sale
                    int itemCount = rng.Next(1, 5);
                    var saleItems = new List<(Product p, int qty)>();
                    decimal total = 0;

                    for (int i = 0; i < itemCount; i++)
                    {
                        var p   = products[rng.Next(products.Count)];
                        int qty = rng.Next(1, 6);
                        saleItems.Add((p, qty));
                        total += p.Price * qty;
                    }

                    total = Math.Round(total * (decimal)trendFactor, 2);

                    var saleTime = day.AddHours(rng.Next(8, 20)).AddMinutes(rng.Next(0, 60));

                    var sale = new Sale
                    {
                        CashierId   = cashier.Id,
                        SaleDate    = saleTime,
                        TotalAmount = total,
                        AmountPaid  = total,
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

                    // Revenue record
                    context.Revenues.Add(new Revenue
                    {
                        Source          = "POS Sale",
                        Category        = "Direct Sales",
                        Amount          = total,
                        TransactionDate = saleTime,
                        Notes           = $"Historical sale #{sale.Id}",
                        RecordedBy      = cashier.Id,
                        CreatedAt       = saleTime,
                        IsDeleted       = false
                    });
                }

                // Daily expenses (1-3 per day)
                int expCount = rng.Next(1, 4);
                for (int e = 0; e < expCount; e++)
                {
                    var (cat, baseAmt, variance) = expenseCategories[rng.Next(expenseCategories.Length)];
                    decimal amt = Math.Round(baseAmt + (decimal)(rng.NextDouble() * (double)variance - (double)variance / 2), 2);
                    context.Expenses.Add(new Expense
                    {
                        Description     = $"{cat} expense",
                        Category        = cat,
                        Amount          = Math.Max(50m, amt),
                        ExpenseDate     = day.AddHours(rng.Next(8, 18)),
                        RecordedBy      = "cfo",
                        CreatedAt       = day,
                        IsDeleted       = false
                    });
                }

                await context.SaveChangesAsync();
            }
        }

    }
}
