using System.ComponentModel.DataAnnotations;

namespace FinancasApi.DTOs;

// Senha: mínimo de 10 caracteres, com maiúscula, minúscula e número
public class StrongPasswordAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext ctx)
    {
        var p = value as string ?? "";
        if (p.Length == 0) return ValidationResult.Success; // [Required] cuida do vazio

        if (p.Length < 10)
            return new ValidationResult("A senha deve ter no mínimo 10 caracteres.");
        if (!p.Any(char.IsUpper))
            return new ValidationResult("A senha deve conter pelo menos uma letra maiúscula.");
        if (!p.Any(char.IsLower))
            return new ValidationResult("A senha deve conter pelo menos uma letra minúscula.");
        if (!p.Any(char.IsDigit))
            return new ValidationResult("A senha deve conter pelo menos um número.");
        return ValidationResult.Success;
    }
}

public static class Limits { public const double MaxMoney = 999_999_999.99; }

public static class IncomeCategories
{
    public static readonly string[] All =
        ["Freela", "Venda", "13º Salário", "Férias", "Reembolso", "Rendimentos", "Presente", "Outros"];
}

public class IncomeCategoryAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext ctx) =>
        value is string s && IncomeCategories.All.Contains(s)
            ? ValidationResult.Success
            : new ValidationResult($"Categoria de receita inválida. Use: {string.Join(", ", IncomeCategories.All)}.");
}

public record RegisterRequest(
    [Required(ErrorMessage = "Informe seu nome."),
     StringLength(100, MinimumLength = 2, ErrorMessage = "O nome deve ter entre 2 e 100 caracteres.")] string Name,
    [Required(ErrorMessage = "Informe seu e-mail."),
     EmailAddress(ErrorMessage = "Informe um e-mail válido."),
     StringLength(254, ErrorMessage = "O e-mail deve ter no máximo 254 caracteres.")] string Email,
    [Required(ErrorMessage = "Informe uma senha."), StrongPassword] string Password);
public record LoginRequest(
    [Required(ErrorMessage = "Informe seu e-mail."),
     StringLength(254, ErrorMessage = "O e-mail deve ter no máximo 254 caracteres.")] string Email,
    [Required(ErrorMessage = "Informe sua senha."),
     StringLength(72, ErrorMessage = "A senha deve ter no máximo 72 caracteres.")] string Password);
public record RefreshRequest([Required(ErrorMessage = "Refresh token não informado."), StringLength(200, ErrorMessage = "Refresh token inválido.")] string RefreshToken);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User);

public record UserDto(int Id, string Name, string Email);


public record UpsertBudgetRequest(
    [Range(2000, 2100)] int Year,
    [Range(1, 12)] int Month,
    [Range(0, Limits.MaxMoney)] decimal Salary);

// TotalExpenses = todos os gastos do mês (pagos e pendentes); PaidExpenses = já pago; PendingExpenses = falta pagar.
// Balance desconta só o que já saiu (pagos + compras no cartão); ProjectedBalance desconta também o pendente.
// Salary = salário base; OtherIncome = receitas lançadas; TotalIncome = Salary + OtherIncome
public record BudgetDto(
    int Id, int Year, int Month, decimal Salary,
    decimal TotalExpenses, decimal TotalInvestments, decimal Balance,
    decimal SpendingPercent, decimal OtherIncome, decimal TotalIncome, decimal GoalDeposits,
    decimal PaidExpenses, decimal PendingExpenses, decimal ProjectedBalance);


public record CategoryDto(int Id, string Name, string Icon, string Color, bool IsSystem);
public record CreateCategoryRequest(
    [Required, StringLength(50, MinimumLength = 1)] string Name,
    [Required, StringLength(16)] string Icon,
    [Required, RegularExpression("^#[0-9a-fA-F]{6}$")] string Color);


public record CreateExpenseRequest(
    [Required, StringLength(200)] string Description,
    [Range(0.01, Limits.MaxMoney)] decimal Amount,
    DateOnly Date,
    int CategoryId,
    bool IsRecurring = false,
    bool IsPaid = false);

public record UpdateExpenseRequest(
    [Required, StringLength(200)] string Description,
    [Range(0.01, Limits.MaxMoney)] decimal Amount,
    DateOnly Date,
    int CategoryId,
    bool IsRecurring);

public record ExpenseDto(
    int Id,
    string Description,
    decimal Amount,
    DateOnly Date,
    CategoryDto Category,
    bool IsRecurring,
    DateTime CreatedAt,
    int? CreditCardId = null,
    int? InstallmentNumber = null,
    int? InstallmentTotal = null,
    Guid? InstallmentGroupId = null,
    bool IsInvoiceBalance = false,
    bool ExcludeFromBudget = false,
    bool IsPaid = false,
    DateTime? PaidAt = null);


public static class CardBrands
{
    public static readonly string[] All = ["Visa", "Mastercard", "Elo", "Amex", "Hipercard", "Outra"];
}

public class CardBrandAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext ctx) =>
        value is string s && CardBrands.All.Contains(s)
            ? ValidationResult.Success
            : new ValidationResult($"Bandeira inválida. Use: {string.Join(", ", CardBrands.All)}.");
}

public record BankDto(int Id, string Name, string Icon, string Color, bool IsSystem);
public record CreateBankRequest(
    [Required(ErrorMessage = "Informe o nome do banco."), StringLength(50, MinimumLength = 2, ErrorMessage = "O nome do banco deve ter entre 2 e 50 caracteres.")] string Name,
    [Required(ErrorMessage = "Informe a cor."), RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Cor inválida. Use o formato #RRGGBB.")] string Color);

public record CreateCardRequest(
    int BankId,
    [Required(ErrorMessage = "Informe um apelido para o cartão."), StringLength(50, ErrorMessage = "O apelido deve ter no máximo 50 caracteres.")] string Nickname,
    [Required(ErrorMessage = "Informe a bandeira."), CardBrand] string Brand,
    [RegularExpression(@"^\d{4}$", ErrorMessage = "Informe somente os 4 últimos dígitos.")] string? Last4,
    [Range(0.01, Limits.MaxMoney, ErrorMessage = "Informe um limite maior que zero.")] decimal Limit,
    [Range(1, 31, ErrorMessage = "O dia de fechamento deve estar entre 1 e 31.")] int ClosingDay,
    [Range(1, 31, ErrorMessage = "O dia de vencimento deve estar entre 1 e 31.")] int DueDay);

public record UpdateCardRequest(
    int BankId,
    [Required(ErrorMessage = "Informe um apelido para o cartão."), StringLength(50, ErrorMessage = "O apelido deve ter no máximo 50 caracteres.")] string Nickname,
    [Required(ErrorMessage = "Informe a bandeira."), CardBrand] string Brand,
    [RegularExpression(@"^\d{4}$", ErrorMessage = "Informe somente os 4 últimos dígitos.")] string? Last4,
    [Range(0.01, Limits.MaxMoney, ErrorMessage = "Informe um limite maior que zero.")] decimal Limit,
    [Range(1, 31, ErrorMessage = "O dia de fechamento deve estar entre 1 e 31.")] int ClosingDay,
    [Range(1, 31, ErrorMessage = "O dia de vencimento deve estar entre 1 e 31.")] int DueDay,
    bool IsArchived);

public record CardDto(
    int Id, int BankId, string BankName, string BankColor, string BankIcon,
    string Nickname, string Brand, string? Last4,
    decimal Limit, int ClosingDay, int DueDay, bool IsArchived,
    decimal UsedLimit, decimal AvailableLimit,
    int CurrentInvoiceYear, int CurrentInvoiceMonth, decimal CurrentInvoiceTotal);

public record CreateCardPurchaseRequest(
    [Required(ErrorMessage = "Informe a descrição."), StringLength(200, ErrorMessage = "A descrição deve ter no máximo 200 caracteres.")] string Description,
    [Range(0.01, Limits.MaxMoney, ErrorMessage = "Informe um valor maior que zero.")] decimal Amount,
    DateOnly Date,
    int CategoryId,
    [Range(1, 48, ErrorMessage = "O número de parcelas deve estar entre 1 e 48.")] int Installments = 1,
    bool IsRecurring = false);

public record InvoiceBalanceItem(
    [Range(2000, 2100, ErrorMessage = "Ano da fatura inválido.")] int Year,
    [Range(1, 12, ErrorMessage = "Mês da fatura inválido.")] int Month,
    [Range(0, Limits.MaxMoney, ErrorMessage = "Valor da fatura inválido.")] decimal Amount,
    bool CountInBudget = false);

// Valor 0 remove o saldo daquele mês
public record SetInvoiceBalancesRequest(
    [Required(ErrorMessage = "Informe as faturas."), MinLength(1, ErrorMessage = "Informe ao menos uma fatura."), MaxLength(36, ErrorMessage = "Informe no máximo 36 faturas.")] List<InvoiceBalanceItem> Items);

public record InvoiceBalanceDto(int ExpenseId, int Year, int Month, decimal Amount, bool CountInBudget);

// Status: "open" (ainda não fechou), "closed" (fechada, a pagar) ou "paid"
public record InvoiceDto(
    int CardId, int Year, int Month,
    DateOnly ClosingDate, DateOnly DueDate,
    decimal Total, string Status, DateTime? PaidAt,
    IEnumerable<ExpenseDto> Items);

public record CreateIncomeRequest(
    [Required(ErrorMessage = "Informe a descrição."), StringLength(200, ErrorMessage = "A descrição deve ter no máximo 200 caracteres.")] string Description,
    [Required(ErrorMessage = "Informe a categoria."), IncomeCategory] string Category,
    [Range(0.01, Limits.MaxMoney, ErrorMessage = "Informe um valor maior que zero.")] decimal Amount,
    DateOnly Date);
public record UpdateIncomeRequest(
    [Required(ErrorMessage = "Informe a descrição."), StringLength(200, ErrorMessage = "A descrição deve ter no máximo 200 caracteres.")] string Description,
    [Required(ErrorMessage = "Informe a categoria."), IncomeCategory] string Category,
    [Range(0.01, Limits.MaxMoney, ErrorMessage = "Informe um valor maior que zero.")] decimal Amount,
    DateOnly Date);
public record IncomeDto(int Id, string Description, string Category, decimal Amount, DateOnly Date, DateTime CreatedAt);

public record CreateInvestmentRequest([Required, StringLength(100)] string Name, [Required, StringLength(50)] string Type, [Range(0.01, Limits.MaxMoney)] decimal Amount, DateOnly Date);
public record UpdateInvestmentRequest([Required, StringLength(100)] string Name, [Required, StringLength(50)] string Type, [Range(0.01, Limits.MaxMoney)] decimal Amount, DateOnly Date);

public record InvestmentDto(int Id, string Name, string Type, decimal Amount, DateOnly Date, DateTime CreatedAt);


public record CreateGoalRequest(
    [Required(ErrorMessage = "Informe o nome da meta."), StringLength(100, ErrorMessage = "O nome deve ter no máximo 100 caracteres.")] string Name,
    [Required(ErrorMessage = "Informe um ícone."), StringLength(16, ErrorMessage = "Ícone inválido.")] string Icon,
    [Range(0.01, Limits.MaxMoney, ErrorMessage = "Informe um valor alvo maior que zero.")] decimal TargetAmount,
    DateOnly? Deadline,
    [Range(0.01, Limits.MaxMoney, ErrorMessage = "Informe um valor mensal maior que zero.")] decimal? PlannedMonthly = null);
// Atualizar também serve para renovar o prazo
public record UpdateGoalRequest(
    [Required(ErrorMessage = "Informe o nome da meta."), StringLength(100, ErrorMessage = "O nome deve ter no máximo 100 caracteres.")] string Name,
    [Required(ErrorMessage = "Informe um ícone."), StringLength(16, ErrorMessage = "Ícone inválido.")] string Icon,
    [Range(0.01, Limits.MaxMoney, ErrorMessage = "Informe um valor alvo maior que zero.")] decimal TargetAmount,
    DateOnly? Deadline,
    [Range(0.01, Limits.MaxMoney, ErrorMessage = "Informe um valor mensal maior que zero.")] decimal? PlannedMonthly = null);
public record AddToGoalRequest([Range(0.01, Limits.MaxMoney)] decimal Amount);

// Cenário: guardando Monthly por mês, a meta é batida em Months meses (mês de Date)
public record GoalScenarioDto(int Months, decimal Monthly, DateOnly Date);

// Status: "completed" | "on_track" | "behind" | "overdue" (prazo vencido) | "no_deadline"
public record GoalDto(
    int Id, string Name, string Icon,
    decimal TargetAmount, decimal CurrentAmount,
    DateOnly? Deadline, bool IsCompleted,
    decimal ProgressPercent, DateTime CreatedAt,
    decimal? PlannedMonthly,
    decimal Remaining,
    int? MonthsLeft,                  // só com prazo
    decimal? RequiredMonthly,         // só com prazo: quanto guardar por mês para bater a meta
    string Status,
    DateOnly? ProjectedDate,          // mês previsto de conclusão guardando PlannedMonthly
    bool? PlannedMeetsDeadline,       // PlannedMonthly é suficiente para o prazo (só com prazo e aporte planejado)
    IEnumerable<GoalScenarioDto> Scenarios);


public record AlertDto(int Id, string Title, string Message, string Type, bool IsRead, DateTime CreatedAt);


public record DashboardDto(
    BudgetDto Budget,
    IEnumerable<CategorySummaryDto> CategorySummaries,
    IEnumerable<MonthlyTrendDto> MonthlyTrend,
    IEnumerable<AlertDto> UnreadAlerts,
    IEnumerable<GoalDto> ActiveGoals);

public record CategorySummaryDto(CategoryDto Category, decimal Total, decimal Percent);

public record MonthlyTrendDto(int Year, int Month, string Label, decimal Salary, decimal Expenses, decimal Investments, decimal Balance, decimal OtherIncome, decimal GoalDeposits);
