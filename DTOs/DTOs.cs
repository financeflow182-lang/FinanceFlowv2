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

public record BudgetDto(
    int Id, int Year, int Month, decimal Salary,
    decimal TotalExpenses, decimal TotalInvestments, decimal Balance,
    decimal SpendingPercent);


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
    bool IsRecurring = false);

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
    DateTime CreatedAt);


public record CreateInvestmentRequest([Required, StringLength(100)] string Name, [Required, StringLength(50)] string Type, [Range(0.01, Limits.MaxMoney)] decimal Amount, DateOnly Date);
public record UpdateInvestmentRequest([Required, StringLength(100)] string Name, [Required, StringLength(50)] string Type, [Range(0.01, Limits.MaxMoney)] decimal Amount, DateOnly Date);

public record InvestmentDto(int Id, string Name, string Type, decimal Amount, DateOnly Date, DateTime CreatedAt);


public record CreateGoalRequest([Required, StringLength(100)] string Name, [Required, StringLength(16)] string Icon, [Range(0.01, Limits.MaxMoney)] decimal TargetAmount, DateOnly? Deadline);
public record UpdateGoalRequest([Required, StringLength(100)] string Name, [Required, StringLength(16)] string Icon, [Range(0.01, Limits.MaxMoney)] decimal TargetAmount, DateOnly? Deadline);
public record AddToGoalRequest([Range(0.01, Limits.MaxMoney)] decimal Amount);

public record GoalDto(
    int Id, string Name, string Icon,
    decimal TargetAmount, decimal CurrentAmount,
    DateOnly? Deadline, bool IsCompleted,
    decimal ProgressPercent, DateTime CreatedAt);


public record AlertDto(int Id, string Title, string Message, string Type, bool IsRead, DateTime CreatedAt);


public record DashboardDto(
    BudgetDto Budget,
    IEnumerable<CategorySummaryDto> CategorySummaries,
    IEnumerable<MonthlyTrendDto> MonthlyTrend,
    IEnumerable<AlertDto> UnreadAlerts,
    IEnumerable<GoalDto> ActiveGoals);

public record CategorySummaryDto(CategoryDto Category, decimal Total, decimal Percent);

public record MonthlyTrendDto(int Year, int Month, string Label, decimal Salary, decimal Expenses, decimal Investments, decimal Balance);
