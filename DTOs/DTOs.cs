namespace FinancasApi.DTOs;
public record RegisterRequest(string Name, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User);

public record UserDto(int Id, string Name, string Email);


public record UpsertBudgetRequest(int Year, int Month, decimal Salary);

public record BudgetDto(
    int Id, int Year, int Month, decimal Salary,
    decimal TotalExpenses, decimal TotalInvestments, decimal Balance,
    decimal SpendingPercent);


public record CategoryDto(int Id, string Name, string Icon, string Color, bool IsSystem);
public record CreateCategoryRequest(string Name, string Icon, string Color);


public record CreateExpenseRequest(
    string Description,
    decimal Amount,
    DateOnly Date,
    int CategoryId,
    bool IsRecurring = false);

public record UpdateExpenseRequest(
    string Description,
    decimal Amount,
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


public record CreateInvestmentRequest(string Name, string Type, decimal Amount, DateOnly Date);
public record UpdateInvestmentRequest(string Name, string Type, decimal Amount, DateOnly Date);

public record InvestmentDto(int Id, string Name, string Type, decimal Amount, DateOnly Date, DateTime CreatedAt);


public record CreateGoalRequest(string Name, string Icon, decimal TargetAmount, DateOnly? Deadline);
public record UpdateGoalRequest(string Name, string Icon, decimal TargetAmount, DateOnly? Deadline);
public record AddToGoalRequest(decimal Amount);

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
