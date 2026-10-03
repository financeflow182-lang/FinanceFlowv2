using Microsoft.EntityFrameworkCore;
using FinancasApi.Models;

namespace FinancasApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<MonthlyBudget> MonthlyBudgets => Set<MonthlyBudget>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Investment> Investments => Set<Investment>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<Alert> Alerts => Set<Alert>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        //usuarios 
        mb.Entity<User>(e => {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Salary).HasColumnType("numeric(18,2)");
        });

        // Mes orçamento 
        mb.Entity<MonthlyBudget>(e => {
            e.HasIndex(b => new { b.UserId, b.Year, b.Month }).IsUnique();
            e.Property(b => b.Salary).HasColumnType("numeric(18,2)");
        });

        // Despesa 
        mb.Entity<Expense>(e => {
            e.Property(x => x.Amount).HasColumnType("numeric(18,2)");
            e.HasOne(x => x.Category).WithMany(c => c.Expenses).OnDelete(DeleteBehavior.Restrict);
        });

        // Investimento 
        mb.Entity<Investment>(e =>
            e.Property(x => x.Amount).HasColumnType("numeric(18,2)"));

        //  Meta 
        mb.Entity<Goal>(e => {
            e.Property(x => x.TargetAmount).HasColumnType("numeric(18,2)");
            e.Property(x => x.CurrentAmount).HasColumnType("numeric(18,2)");
        });

        // Categorias - dados iniciais
        mb.Entity<Category>().HasData(
            new Category { Id = 1,  Name = "Moradia",         Icon = "🏠", Color = "#60a5fa", IsSystem = true },
            new Category { Id = 2,  Name = "Alimentação",     Icon = "🛒", Color = "#34d399", IsSystem = true },
            new Category { Id = 3,  Name = "Delivery",        Icon = "🍕", Color = "#f87171", IsSystem = true },
            new Category { Id = 4,  Name = "Final de Semana", Icon = "🎉", Color = "#fbbf24", IsSystem = true },
            new Category { Id = 5,  Name = "Transporte",      Icon = "🚗", Color = "#a78bfa", IsSystem = true },
            new Category { Id = 6,  Name = "Saúde",           Icon = "💊", Color = "#f472b6", IsSystem = true },
            new Category { Id = 7,  Name = "Assinaturas",     Icon = "📱", Color = "#818cf8", IsSystem = true },
            new Category { Id = 8,  Name = "Roupas",          Icon = "👕", Color = "#fb923c", IsSystem = true },
            new Category { Id = 9,  Name = "Lazer",           Icon = "🎮", Color = "#2dd4bf", IsSystem = true },
            new Category { Id = 10, Name = "Educação",        Icon = "📚", Color = "#e879f9", IsSystem = true },
            new Category { Id = 11, Name = "Contas Fixas",    Icon = "💡", Color = "#facc15", IsSystem = true },
            new Category { Id = 12, Name = "Outros",          Icon = "🛠️", Color = "#94a3b8", IsSystem = true }
        );
    }
}
