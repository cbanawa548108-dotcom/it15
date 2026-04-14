using Microsoft.AspNetCore.Identity;
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
                    new Product 
                    { 
                        Name = "Apple", 
                        Category = "Fruits", 
                        Price = 25.00m, 
                        IsActive = true, 
                        Emoji = "🍎" 
                    },
                    new Product 
                    { 
                        Name = "Banana", 
                        Category = "Fruits", 
                        Price = 15.00m, 
                        IsActive = true, 
                        Emoji = "🍌" 
                    },
                    new Product 
                    { 
                        Name = "Orange", 
                        Category = "Fruits", 
                        Price = 20.00m, 
                        IsActive = true, 
                        Emoji = "🍊" 
                    },
                    new Product 
                    { 
                        Name = "Mango", 
                        Category = "Fruits", 
                        Price = 45.00m, 
                        IsActive = true, 
                        Emoji = "🥭" 
                    },
                    new Product 
                    { 
                        Name = "Grapes", 
                        Category = "Fruits", 
                        Price = 120.00m, 
                        IsActive = true, 
                        Emoji = "🍇" 
                    },
                    new Product 
                    { 
                        Name = "Watermelon", 
                        Category = "Fruits", 
                        Price = 80.00m, 
                        IsActive = true, 
                        Emoji = "🍉" 
                    },
                    new Product 
                    { 
                        Name = "Carrot", 
                        Category = "Vegetables", 
                        Price = 18.00m, 
                        IsActive = true, 
                        Emoji = "🥕" 
                    },
                    new Product 
                    { 
                        Name = "Broccoli", 
                        Category = "Vegetables", 
                        Price = 35.00m, 
                        IsActive = true, 
                        Emoji = "🥦" 
                    },
                    new Product 
                    { 
                        Name = "Tomato", 
                        Category = "Vegetables", 
                        Price = 22.00m, 
                        IsActive = true, 
                        Emoji = "🍅" 
                    },
                    new Product 
                    { 
                        Name = "Potato", 
                        Category = "Vegetables", 
                        Price = 28.00m, 
                        IsActive = true, 
                        Emoji = "🥔" 
                    },
                    new Product 
                    { 
                        Name = "Onion", 
                        Category = "Vegetables", 
                        Price = 15.00m, 
                        IsActive = true, 
                        Emoji = "🧅" 
                    },
                    new Product 
                    { 
                        Name = "Rice 1kg", 
                        Category = "Grains", 
                        Price = 55.00m, 
                        IsActive = true, 
                        Emoji = "🍚" 
                    },
                    new Product 
                    { 
                        Name = "Bread", 
                        Category = "Bakery", 
                        Price = 40.00m, 
                        IsActive = true, 
                        Emoji = "🍞" 
                    },
                    new Product 
                    { 
                        Name = "Croissant", 
                        Category = "Bakery", 
                        Price = 65.00m, 
                        IsActive = true, 
                        Emoji = "🥐" 
                    },
                    new Product 
                    { 
                        Name = "Milk 1L", 
                        Category = "Dairy", 
                        Price = 95.00m, 
                        IsActive = true, 
                        Emoji = "🥛" 
                    },
                    new Product 
                    { 
                        Name = "Cheese", 
                        Category = "Dairy", 
                        Price = 150.00m, 
                        IsActive = true, 
                        Emoji = "🧀" 
                    },
                    new Product 
                    { 
                        Name = "Eggs (dozen)", 
                        Category = "Dairy", 
                        Price = 110.00m, 
                        IsActive = true, 
                        Emoji = "🥚" 
                    },
                    new Product 
                    { 
                        Name = "Chicken", 
                        Category = "Meat", 
                        Price = 180.00m, 
                        IsActive = true, 
                        Emoji = "🍗" 
                    },
                    new Product 
                    { 
                        Name = "Beef", 
                        Category = "Meat", 
                        Price = 320.00m, 
                        IsActive = true, 
                        Emoji = "🥩" 
                    },
                    new Product 
                    { 
                        Name = "Fish", 
                        Category = "Meat", 
                        Price = 200.00m, 
                        IsActive = true, 
                        Emoji = "🐟" 
                    }
                };

                foreach (var product in products)
                {
                    context.Products.Add(product);
                    
                    // Create inventory for each product with random stock between 20-100
                    var random = new Random();
                    context.Inventory.Add(new Inventory 
                    { 
                        Product = product, 
                        Quantity = random.Next(20, 101), 
                        LastUpdated = DateTime.Now 
                    });
                }

                await context.SaveChangesAsync();
            }
        }
    }
}