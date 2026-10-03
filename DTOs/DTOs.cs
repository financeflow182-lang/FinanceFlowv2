using System.ComponentModel.DataAnnotations;

namespace FinancasApi.DTOs;

// Senha: 10-72 bytes (limite do BCrypt), com maiúscula, minúscula e número
public class StrongPasswordAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext ctx)
    {
        var p = value as string ?? "";
        if (p.Length < 10 || System.Text.Encoding.UTF8.GetByteCount(p) > 72
            || !p.Any(char.IsUpper) || !p.Any(char.IsLower) || !p.Any(char.IsDigit))
            return new ValidationResult("A senha deve ter de 10 a 72 caracteres, com maiúscula, minúscula e número.");
        return ValidationResult.Success;
    }
}

public static class Limits { public const double MaxMoney = 999_999_999.99; }

public record RegisterRequest(
    [property: Required, StringLength(100, MinimumLength = 2)] string Name,
    [property: Required, EmailAddress, StringLength(254)] string Email,
    [property: Required, StrongPassword] string Password);
public record LoginRequest(
    [property: Required, StringLength(254)] string Email,
    [property: Required, StringLength(72)] string Password);
public record RefreshRequest([property: Required, StringLength(200)] string RefreshToken);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User);

public record UserDto(int Id, string Name, string Email);


public record UpsertBudgetRequest(
    [property: Range(2000, 2100)] int Year,
    [property: Range(1, 12)] int Month,
    [property: Range(0, Limits.MaxMoney)] decimal Salary);

public record BudgetDto(
    int Id, int Year, int Month, decimal Salary,
    decimal TotalExpenses, decimal TotalInvestments, decimal Balance,
    decimal SpendingPercent);


public record CategoryDto(int Id, string Name, string Icon, string Color, bool IsSystem);
public record CreateCategoryRequest(
    [property: Required, StringLength(50, MinimumLength = 1)] string Name,
    [property: Required, StringLength(16)] string Icon,
    [property: Required, RegularExpression("^#[0-9a-fA-F]{6}$")] string Color);


public record CreateExpenseRequest(
    [property: Required, StringLength(200)] string Description,
    [property: Range(0.01, Limits.MaxMoney)] decimal Amount,
    DateOnly Date,
    int CategoryId,
    bool IsRecurring = false);

public record UpdateExpenseRequest(
    [property: Required, StringLength(200)] string Description,
    [property: Range(0.01, Limits.MaxMoney)] decimal Amount,
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


public record CreateInvestmentRequest([property: Required, StringLength(100)] string Name, [property: Required, StringLength(50)] string Type, [property: Range(0.01, Limits.MaxMoney)] decimal Amount, DateOnly Date);
public record UpdateInvestmentRequest([property: Required, StringLength(100)] string Name, [property: Required, StringLength(50)] string Type, [property: Range(0.01, Limits.MaxMoney)] decimal Amount, DateOnly Date);

public record InvestmentDto(int Id, string Name, string Type, decimal Amount, DateOnly Date, DateTime CreatedAt);


public record CreateGoalRequest([property: Required, StringLength(100)] string Name, [property: Required, StringLength(16)] string Icon, [property: Range(0.01, Limits.MaxMoney)] decimal TargetAmount, DateOnly? Deadline);
public record UpdateGoalRequest([property: Required, StringLength(100)] string Name, [property: Required, StringLength(16)] string Icon, [property: Range(0.01, Limits.MaxMoney)] decimal TargetAmount, DateOnly? Deadline);
public record AddToGoalRequest([property: Range(0.01, Limits.MaxMoney)] decimal Amount);

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
