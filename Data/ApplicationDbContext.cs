using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CRLFruitstandESS.Models;
using CRLFruitstandESS.Models.Executive;

namespace CRLFruitstandESS.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Product> Products { get; set; }
        public DbSet<Inventory> Inventory { get; set; }
        public DbSet<Sale> Sales { get; set; }
        public DbSet<SaleItem> SaleItems { get; set; }
        
        // NEW: Inventory Module
        public DbSet<StockMovement> StockMovements { get; set; }
        public DbSet<Supplier> Suppliers { get; set; }
        public DbSet<SupplierProduct> SupplierProducts { get; set; }
        public DbSet<SupplierDelivery> SupplierDeliveries { get; set; }
        public DbSet<SupplierPayment> SupplierPayments { get; set; }

        // NEW: Financial Module
        public DbSet<Revenue> Revenues { get; set; }
        public DbSet<Expense> Expenses { get; set; }
        public DbSet<Budget> Budgets { get; set; }
        public DbSet<FinancialReport> FinancialReports { get; set; }

        // NEW: Executive Decision Support Module
        public DbSet<ExecutiveAlert> ExecutiveAlerts { get; set; }
        public DbSet<KPITarget> KPITargets { get; set; }
        public DbSet<SavedScenario> SavedScenarios { get; set; }
        public DbSet<RiskRegister> RiskRegisters { get; set; }

        // NEW: Compliance, Trend Reports, Petty Cash
        public DbSet<ComplianceFlag> ComplianceFlags { get; set; }
        public DbSet<TrendReport> TrendReports { get; set; }
        public DbSet<PettyCashClaim> PettyCashClaims { get; set; }

        // NEW: PayMongo payment transactions
        public DbSet<PaymentTransaction> PaymentTransactions { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Rename default ASP.NET Identity tables
            builder.Entity<ApplicationUser>().ToTable("Users");
            builder.Entity<ApplicationRole>().ToTable("Roles");
            builder.Entity<IdentityUserRole<string>>().ToTable("UserRoles");
            builder.Entity<IdentityUserClaim<string>>().ToTable("UserClaims");
            builder.Entity<IdentityUserLogin<string>>().ToTable("UserLogins");
            builder.Entity<IdentityRoleClaim<string>>().ToTable("RoleClaims");
            builder.Entity<IdentityUserToken<string>>().ToTable("UserTokens");

            // Inventory unique constraint
            builder.Entity<Inventory>()
                .HasIndex(i => i.ProductId)
                .IsUnique();

            // SupplierProduct composite index
            builder.Entity<SupplierProduct>()
                .HasIndex(sp => new { sp.SupplierId, sp.ProductId })
                .IsUnique();

            // StockMovement indexes for performance
            builder.Entity<StockMovement>()
                .HasIndex(sm => sm.ProductId);

            builder.Entity<StockMovement>()
                .HasIndex(sm => sm.MovementDate);

            // Executive Module indexes
            builder.Entity<ExecutiveAlert>()
                .HasIndex(ea => ea.CreatedAt);

            builder.Entity<ExecutiveAlert>()
                .HasIndex(ea => ea.Severity);

            builder.Entity<KPITarget>()
                .HasIndex(kt => new { kt.Month, kt.Year });

            builder.Entity<SavedScenario>()
                .HasIndex(ss => ss.CreatedAt);

            builder.Entity<RiskRegister>()
                .HasIndex(rr => rr.Category);

            builder.Entity<RiskRegister>()
                .HasIndex(rr => rr.Status);

            // Financial module indexes for performance (if not already added)
            builder.Entity<Revenue>()
                .HasIndex(r => r.TransactionDate);

            builder.Entity<Expense>()
                .HasIndex(e => e.ExpenseDate);
        }
    }
}