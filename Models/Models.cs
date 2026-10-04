namespace FinancasApi.Models;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public decimal Salary { get; set; }

    public ICollection<MonthlyBudget> Budgets { get; set; } = [];
    public ICollection<Expense> Expenses { get; set; } = [];
    public ICollection<Income> Incomes { get; set; } = [];
    public ICollection<Investment> Investments { get; set; } = [];
    public ICollection<Goal> Goals { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}

public class RefreshToken
{
    public int Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
}

public class MonthlyBudget
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Salary { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
}

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public int? UserId { get; set; }

    public ICollection<Expense> Expenses { get; set; } = [];
}

public class Expense
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public bool IsRecurring { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Compras no cartão: fatura a que pertencem (mês de fechamento) e dados de parcelamento
    public int? CreditCardId { get; set; }
    public CreditCard? CreditCard { get; set; }
    public int? InvoiceYear { get; set; }
    public int? InvoiceMonth { get; set; }
    public Guid? InstallmentGroupId { get; set; }
    public int? InstallmentNumber { get; set; }
    public int? InstallmentTotal { get; set; }

    // Saldo de fatura já existente (lançado em bloco); pode ficar fora do orçamento do mês para não duplicar gastos
    public bool IsInvoiceBalance { get; set; }
    public bool ExcludeFromBudget { get; set; }
}

public class Bank
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public bool IsSystem { get; set; }
    public int? UserId { get; set; }
}

// Guarda só dados de identificação: nunca número completo, CVV ou validade
public class CreditCard
{
    public int Id { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public string? Last4 { get; set; }
    public decimal Limit { get; set; }
    public int ClosingDay { get; set; }
    public int DueDay { get; set; }
    public bool IsArchived { get; set; }
    public int BankId { get; set; }
    public Bank Bank { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class InvoicePayment
{
    public int Id { get; set; }
    public int CreditCardId { get; set; }
    public CreditCard CreditCard { get; set; } = null!;
    public int UserId { get; set; }
    public int Year { get; set; }   // mês de fechamento da fatura
    public int Month { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
}

public class Income
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Investment
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateOnly Date { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Goal
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public decimal TargetAmount { get; set; }
    public decimal CurrentAmount { get; set; }
    public DateOnly? Deadline { get; set; }
    public bool IsCompleted { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Alert
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "warning" | "danger" | "info"
    public bool IsRead { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}