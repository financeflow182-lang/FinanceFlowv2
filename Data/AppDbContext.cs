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
    public DbSet<Bank> Banks => Set<Bank>();
    public DbSet<CreditCard> CreditCards => Set<CreditCard>();
    public DbSet<InvoicePayment> InvoicePayments => Set<InvoicePayment>();
    public DbSet<Income> Incomes => Set<Income>();
    public DbSet<Investment> Investments => Set<Investment>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<GoalDeposit> GoalDeposits => Set<GoalDeposit>();
    public DbSet<Alert> Alerts => Set<Alert>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        // Refresh tokens: guardados como hash SHA-256 (64 hex), com índice único para busca
        mb.Entity<RefreshToken>(e => {
            e.Property(t => t.Token).HasMaxLength(64);
            e.HasIndex(t => t.Token).IsUnique();
        });

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
            e.HasOne(x => x.CreditCard).WithMany().HasForeignKey(x => x.CreditCardId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.CreditCardId, x.InvoiceYear, x.InvoiceMonth });
        });

        // Cartão de crédito
        mb.Entity<CreditCard>(e => {
            e.Property(x => x.Limit).HasColumnType("numeric(18,2)");
            e.Property(x => x.Nickname).HasMaxLength(50);
            e.Property(x => x.Brand).HasMaxLength(20);
            e.Property(x => x.Last4).HasMaxLength(4);
            e.HasOne(x => x.Bank).WithMany().HasForeignKey(x => x.BankId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.UserId);
        });

        mb.Entity<InvoicePayment>(e => {
            e.Property(x => x.Amount).HasColumnType("numeric(18,2)");
            e.HasIndex(x => new { x.CreditCardId, x.Year, x.Month }).IsUnique();
        });

        // Bancos - dados iniciais
        mb.Entity<Bank>().HasData(
            new Bank { Id = 1,  Name = "Nubank",          Icon = "🏦", Color = "#8A05BE", IsSystem = true },
            new Bank { Id = 2,  Name = "Santander",       Icon = "🏦", Color = "#EC0000", IsSystem = true },
            new Bank { Id = 3,  Name = "Bradesco",        Icon = "🏦", Color = "#CC092F", IsSystem = true },
            new Bank { Id = 4,  Name = "Itaú",            Icon = "🏦", Color = "#EC7000", IsSystem = true },
            new Bank { Id = 5,  Name = "Banco do Brasil", Icon = "🏦", Color = "#FCDD00", IsSystem = true },
            new Bank { Id = 6,  Name = "Caixa",           Icon = "🏦", Color = "#005CA9", IsSystem = true },
            new Bank { Id = 7,  Name = "Safra",           Icon = "🏦", Color = "#0A2F5C", IsSystem = true },
            new Bank { Id = 8,  Name = "Inter",           Icon = "🏦", Color = "#FF7A00", IsSystem = true },
            new Bank { Id = 9,  Name = "C6 Bank",         Icon = "🏦", Color = "#242424", IsSystem = true },
            new Bank { Id = 10, Name = "BTG Pactual",     Icon = "🏦", Color = "#001E62", IsSystem = true },
            new Bank { Id = 11, Name = "PicPay",          Icon = "🏦", Color = "#21C25E", IsSystem = true },
            new Bank { Id = 12, Name = "Mercado Pago",    Icon = "🏦", Color = "#009EE3", IsSystem = true },
            new Bank { Id = 13, Name = "Neon",            Icon = "🏦", Color = "#00C2CB", IsSystem = true },
            new Bank { Id = 14, Name = "Next",            Icon = "🏦", Color = "#00D95F", IsSystem = true },
            new Bank { Id = 15, Name = "Sicredi",         Icon = "🏦", Color = "#3FA110", IsSystem = true },
            new Bank { Id = 16, Name = "Sicoob",          Icon = "🏦", Color = "#003641", IsSystem = true },
            new Bank { Id = 17, Name = "Banrisul",        Icon = "🏦", Color = "#004A9F", IsSystem = true },
            new Bank { Id = 18, Name = "Banco Original",  Icon = "🏦", Color = "#1CA53B", IsSystem = true },
            new Bank { Id = 19, Name = "Outro",           Icon = "🏦", Color = "#94A3B8", IsSystem = true }
        );

        // Receita
        mb.Entity<Income>(e => {
            e.Property(x => x.Amount).HasColumnType("numeric(18,2)");
            e.Property(x => x.Category).HasMaxLength(50);
            e.HasIndex(x => new { x.UserId, x.Date });
        });

        // Investimento 
        mb.Entity<Investment>(e =>
            e.Property(x => x.Amount).HasColumnType("numeric(18,2)"));

        //  Meta 
        mb.Entity<Goal>(e => {
            e.Property(x => x.TargetAmount).HasColumnType("numeric(18,2)");
            e.Property(x => x.CurrentAmount).HasColumnType("numeric(18,2)");
            e.Property(x => x.PlannedMonthly).HasColumnType("numeric(18,2)");
        });

        // Depósitos em meta: mantidos mesmo se a meta for excluída (histórico do saldo)
        mb.Entity<GoalDeposit>(e => {
            e.Property(x => x.Amount).HasColumnType("numeric(18,2)");
            e.HasOne(x => x.Goal).WithMany().HasForeignKey(x => x.GoalId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.UserId, x.Date });
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
