using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CRLFruitstandESS.Data;
using CRLFruitstandESS.Models;
using CRLFruitstandESS.Models.ViewModels;

namespace CRLFruitstandESS.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser>  _userManager;
        private readonly RoleManager<ApplicationRole>  _roleManager;
        private readonly ILogger<AdminController>      _logger;

        public AdminController(ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            ILogger<AdminController> logger)
        {
            _db          = db;
            _userManager = userManager;
            _roleManager = roleManager;
            _logger      = logger;
        }

        // ════════════════════════════════════════════
        // USER MANAGEMENT
        // ════════════════════════════════════════════
        public async Task<IActionResult> Users(string search = "")
        {
            var users = await _userManager.Users
                .OrderBy(u => u.FullName)
                .ToListAsync();

            if (!string.IsNullOrEmpty(search))
                users = users.Where(u =>
                    u.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    u.Email!.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    u.UserName!.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

            var vms = new List<UserListViewModel>();
            foreach (var u in users)
            {
                var roles = await _userManager.GetRolesAsync(u);
                vms.Add(new UserListViewModel
                {
                    Id          = u.Id,
                    UserName    = u.UserName ?? "",
                    FullName    = u.FullName,
                    Email       = u.Email ?? "",
                    Department  = u.Department,
                    IsActive    = u.IsActive,
                    CreatedAt   = u.CreatedAt,
                    LastLoginAt = u.LastLoginAt,
                    Roles       = roles.ToList()
                });
            }

            ViewBag.Search = search;
            ViewBag.AllRoles = await _roleManager.Roles.Select(r => r.Name).ToListAsync();
            return View(vms);
        }

        [HttpGet]
        public async Task<IActionResult> CreateUser()
        {
            ViewBag.Roles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
            return View(new CreateUserViewModel());
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(CreateUserViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Roles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
                return View(vm);
            }

            var user = new ApplicationUser
            {
                UserName    = vm.UserName,
                Email       = vm.Email,
                FullName    = vm.FullName,
                Department  = vm.Department,
                IsActive    = vm.IsActive,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, vm.Password);
            if (!result.Succeeded)
            {
                foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
                ViewBag.Roles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
                return View(vm);
            }

            if (!string.IsNullOrEmpty(vm.Role))
                await _userManager.AddToRoleAsync(user, vm.Role);

            TempData["Success"] = $"User '{vm.FullName}' created successfully.";
            return RedirectToAction(nameof(Users));
        }

        [HttpGet]
        public async Task<IActionResult> EditUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            ViewBag.Roles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();

            return View(new EditUserViewModel
            {
                Id         = user.Id,
                FullName   = user.FullName,
                Email      = user.Email ?? "",
                Department = user.Department,
                IsActive   = user.IsActive,
                Role       = roles.FirstOrDefault() ?? ""
            });
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(EditUserViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Roles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
                return View(vm);
            }

            var user = await _userManager.FindByIdAsync(vm.Id);
            if (user == null) return NotFound();

            user.FullName   = vm.FullName;
            user.Email      = vm.Email;
            user.UserName   = vm.Email;
            user.Department = vm.Department;
            user.IsActive   = vm.IsActive;

            await _userManager.UpdateAsync(user);

            // Update role
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            if (!string.IsNullOrEmpty(vm.Role))
                await _userManager.AddToRoleAsync(user, vm.Role);

            // Update password if provided
            if (!string.IsNullOrEmpty(vm.NewPassword))
            {
                var token  = await _userManager.GeneratePasswordResetTokenAsync(user);
                await _userManager.ResetPasswordAsync(user, token, vm.NewPassword);
            }

            TempData["Success"] = $"User '{vm.FullName}' updated.";
            return RedirectToAction(nameof(Users));
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUser(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);
            TempData["Success"] = $"User '{user.FullName}' {(user.IsActive ? "activated" : "deactivated")}.";
            return RedirectToAction(nameof(Users));
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var current = await _userManager.GetUserAsync(User);
            if (current?.Id == id) { TempData["Error"] = "You cannot delete your own account."; return RedirectToAction(nameof(Users)); }
            var user = await _userManager.FindByIdAsync(id);
            if (user != null) await _userManager.DeleteAsync(user);
            TempData["Success"] = "User deleted.";
            return RedirectToAction(nameof(Users));
        }

        // ── Admin direct password reset (no email required)
        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminResetPassword(string id, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
            {
                TempData["Error"] = "Password must be at least 8 characters.";
                return RedirectToAction(nameof(Users));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var token  = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

            if (result.Succeeded)
            {
                _logger.LogInformation("Admin reset password for user {UserName}", user.UserName);
                TempData["Success"] = $"Password for '{user.FullName}' has been reset.";
            }
            else
            {
                TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction(nameof(Users));
        }

        // ════════════════════════════════════════════
        // ROLES MANAGEMENT
        // ════════════════════════════════════════════
        public async Task<IActionResult> Roles()
        {
            var roles = await _roleManager.Roles.ToListAsync();
            var vm = new List<(ApplicationRole Role, int UserCount)>();
            foreach (var r in roles)
            {
                var users = await _userManager.GetUsersInRoleAsync(r.Name!);
                vm.Add((r, users.Count));
            }
            return View(vm);
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateRole(string roleName, string description)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                TempData["Error"] = "Role name is required.";
                return RedirectToAction(nameof(Roles));
            }
            if (roleName.Length > 50)
            {
                TempData["Error"] = "Role name cannot exceed 50 characters.";
                return RedirectToAction(nameof(Roles));
            }
            if (!System.Text.RegularExpressions.Regex.IsMatch(roleName, @"^[a-zA-Z0-9 _-]+$"))
            {
                TempData["Error"] = "Role name can only contain letters, numbers, spaces, hyphens, and underscores.";
                return RedirectToAction(nameof(Roles));
            }
            if (!string.IsNullOrEmpty(description) && description.Length > 200)
            {
                TempData["Error"] = "Description cannot exceed 200 characters.";
                return RedirectToAction(nameof(Roles));
            }
            if (await _roleManager.RoleExistsAsync(roleName))
            {
                TempData["Error"] = "Role already exists.";
                return RedirectToAction(nameof(Roles));
            }
            await _roleManager.CreateAsync(new ApplicationRole { Name = roleName.Trim(), Description = description?.Trim() ?? "" });
            TempData["Success"] = $"Role '{roleName}' created.";
            return RedirectToAction(nameof(Roles));
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteRole(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role != null)
            {
                var users = await _userManager.GetUsersInRoleAsync(role.Name!);
                if (users.Any()) { TempData["Error"] = "Cannot delete a role that has users assigned."; return RedirectToAction(nameof(Roles)); }
                await _roleManager.DeleteAsync(role);
                TempData["Success"] = $"Role '{role.Name}' deleted.";
            }
            return RedirectToAction(nameof(Roles));
        }

        // ════════════════════════════════════════════
        // PRODUCT MANAGEMENT
        // ════════════════════════════════════════════
        public async Task<IActionResult> Products(string search = "", string category = "")
        {
            var query = _db.Products.Include(p => p.Inventory).AsQueryable();
            if (!string.IsNullOrEmpty(search))   query = query.Where(p => p.Name.Contains(search));
            if (!string.IsNullOrEmpty(category)) query = query.Where(p => p.Category == category);

            var products  = await query.OrderBy(p => p.Category).ThenBy(p => p.Name).ToListAsync();
            var categories = await _db.Products.Select(p => p.Category).Distinct().OrderBy(c => c).ToListAsync();

            ViewBag.Search     = search;
            ViewBag.Category   = category;
            ViewBag.Categories = categories;
            return View(products);
        }

        [HttpGet]
        public async Task<IActionResult> CreateProduct()
        {
            ViewBag.Categories = await _db.Products.Select(p => p.Category).Distinct().OrderBy(c => c).ToListAsync();
            return View(new ProductFormViewModel());
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateProduct(ProductFormViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Categories = await _db.Products.Select(p => p.Category).Distinct().OrderBy(c => c).ToListAsync();
                return View(vm);
            }

            var product = new Product
            {
                Name = vm.Name, Description = vm.Description, Category = vm.Category,
                Price = vm.Price, CostPrice = vm.CostPrice, Emoji = vm.Emoji ?? "📦",
                IsActive = vm.IsActive, CreatedAt = DateTime.Now
            };
            _db.Products.Add(product);
            await _db.SaveChangesAsync();

            _db.Inventory.Add(new Inventory
            {
                ProductId = product.Id, Quantity = vm.InitialStock,
                MinStockLevel = vm.MinStockLevel, ReorderPoint = vm.ReorderPoint,
                LastUpdated = DateTime.Now
            });

            if (vm.InitialStock > 0)
                _db.StockMovements.Add(new StockMovement
                {
                    ProductId = product.Id, Type = MovementType.StockIn,
                    Quantity = vm.InitialStock, PreviousStock = 0, NewStock = vm.InitialStock,
                    Notes = "Initial stock", PerformedBy = User.Identity?.Name,
                    MovementDate = DateTime.Now
                });

            await _db.SaveChangesAsync();
            TempData["Success"] = $"Product '{vm.Name}' created.";
            return RedirectToAction(nameof(Products));
        }

        [HttpGet]
        public async Task<IActionResult> EditProduct(int id)
        {
            var p = await _db.Products.Include(x => x.Inventory).FirstOrDefaultAsync(x => x.Id == id);
            if (p == null) return NotFound();
            ViewBag.Categories = await _db.Products.Select(x => x.Category).Distinct().OrderBy(c => c).ToListAsync();
            return View(new ProductFormViewModel
            {
                Id = p.Id, Name = p.Name, Description = p.Description, Category = p.Category,
                Price = p.Price, CostPrice = p.CostPrice, Emoji = p.Emoji, IsActive = p.IsActive,
                MinStockLevel = p.Inventory?.MinStockLevel ?? 10,
                ReorderPoint  = p.Inventory?.ReorderPoint  ?? 20
            });
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> EditProduct(ProductFormViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Categories = await _db.Products.Select(p => p.Category).Distinct().OrderBy(c => c).ToListAsync();
                return View(vm);
            }

            var p = await _db.Products.Include(x => x.Inventory).FirstOrDefaultAsync(x => x.Id == vm.Id);
            if (p == null) return NotFound();

            p.Name = vm.Name; p.Description = vm.Description; p.Category = vm.Category;
            p.Price = vm.Price; p.CostPrice = vm.CostPrice; p.Emoji = vm.Emoji;
            p.IsActive = vm.IsActive; p.UpdatedAt = DateTime.Now;

            if (p.Inventory != null)
            {
                p.Inventory.MinStockLevel = vm.MinStockLevel;
                p.Inventory.ReorderPoint  = vm.ReorderPoint;
            }

            await _db.SaveChangesAsync();
            TempData["Success"] = $"Product '{vm.Name}' updated.";
            return RedirectToAction(nameof(Products));
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleProduct(int id)
        {
            var p = await _db.Products.FindAsync(id);
            if (p != null) { p.IsActive = !p.IsActive; await _db.SaveChangesAsync(); }
            TempData["Success"] = "Product status updated.";
            return RedirectToAction(nameof(Products));
        }

        // ════════════════════════════════════════════
        // SUPPLIER MANAGEMENT
        // ════════════════════════════════════════════
        public async Task<IActionResult> Suppliers(string search = "")
        {
            var query = _db.Suppliers.AsQueryable();
            if (!string.IsNullOrEmpty(search))
                query = query.Where(s => s.Name.Contains(search) || (s.City != null && s.City.Contains(search)));
            var suppliers = await query.OrderBy(s => s.Name).ToListAsync();
            ViewBag.Search = search;
            return View(suppliers);
        }

        [HttpGet]
        public IActionResult CreateSupplier() => View(new SupplierFormViewModel());

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSupplier(SupplierFormViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);
            _db.Suppliers.Add(new Supplier
            {
                Name = vm.Name, ContactPerson = vm.ContactPerson, Phone = vm.Phone,
                Email = vm.Email, Address = vm.Address, City = vm.City,
                TaxId = vm.TaxId, IsActive = vm.IsActive, CreatedAt = DateTime.Now
            });
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Supplier '{vm.Name}' created.";
            return RedirectToAction(nameof(Suppliers));
        }

        [HttpGet]
        public async Task<IActionResult> EditSupplier(int id)
        {
            var s = await _db.Suppliers.FindAsync(id);
            if (s == null) return NotFound();
            return View(new SupplierFormViewModel
            {
                Id = s.Id, Name = s.Name, ContactPerson = s.ContactPerson, Phone = s.Phone,
                Email = s.Email, Address = s.Address, City = s.City, TaxId = s.TaxId, IsActive = s.IsActive
            });
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> EditSupplier(SupplierFormViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);
            var s = await _db.Suppliers.FindAsync(vm.Id);
            if (s == null) return NotFound();
            s.Name = vm.Name; s.ContactPerson = vm.ContactPerson; s.Phone = vm.Phone;
            s.Email = vm.Email; s.Address = vm.Address; s.City = vm.City;
            s.TaxId = vm.TaxId; s.IsActive = vm.IsActive; s.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Supplier '{vm.Name}' updated.";
            return RedirectToAction(nameof(Suppliers));
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleSupplier(int id)
        {
            var s = await _db.Suppliers.FindAsync(id);
            if (s != null) { s.IsActive = !s.IsActive; await _db.SaveChangesAsync(); }
            TempData["Success"] = "Supplier status updated.";
            return RedirectToAction(nameof(Suppliers));
        }

        // ════════════════════════════════════════════
        // INVENTORY MANAGEMENT
        // ════════════════════════════════════════════
        public async Task<IActionResult> InventoryManagement(string filter = "all")
        {
            var query = _db.Inventory.Include(i => i.Product).AsQueryable();
            query = filter switch
            {
                "low"      => query.Where(i => i.Quantity > 0 && i.Quantity <= i.ReorderPoint),
                "critical" => query.Where(i => i.Quantity == 0),
                _          => query
            };
            var items = await query.OrderBy(i => i.Product.Name).ToListAsync();
            ViewBag.Filter = filter;
            return View(items);
        }

        [HttpGet]
        public async Task<IActionResult> AdjustStock(int id)
        {
            var inv = await _db.Inventory.Include(i => i.Product).FirstOrDefaultAsync(i => i.ProductId == id);
            if (inv == null) return NotFound();
            return View(new AdminStockAdjustViewModel
            {
                ProductId   = id,
                ProductName = inv.Product?.Name ?? "",
                CurrentQty  = inv.Quantity
            });
        }

        [HttpPost][ValidateAntiForgeryToken]
        public async Task<IActionResult> AdjustStock(AdminStockAdjustViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var inv = await _db.Inventory.FirstOrDefaultAsync(i => i.ProductId == vm.ProductId);
            if (inv == null) return NotFound();

            var prev = inv.Quantity;
            var type = vm.Type switch
            {
                "StockOut"   => MovementType.StockOut,
                "Adjustment" => MovementType.Adjustment,
                _            => MovementType.StockIn
            };

            inv.Quantity = vm.Type == "StockIn"
                ? inv.Quantity + vm.Quantity
                : vm.Type == "StockOut"
                    ? Math.Max(0, inv.Quantity - vm.Quantity)
                    : vm.Quantity; // Adjustment = set directly

            inv.LastUpdated = DateTime.Now;

            _db.StockMovements.Add(new StockMovement
            {
                ProductId     = vm.ProductId,
                Type          = type,
                Quantity      = vm.Quantity,
                PreviousStock = prev,
                NewStock      = inv.Quantity,
                Notes         = vm.Notes,
                ReferenceNumber = vm.Reference,
                PerformedBy   = User.Identity?.Name,
                MovementDate  = DateTime.Now
            });

            await _db.SaveChangesAsync();
            TempData["Success"] = $"Stock adjusted for product #{vm.ProductId}.";
            return RedirectToAction(nameof(InventoryManagement));
        }
    }
}
