using System.ComponentModel;
using System.Text.Json;
using FluentValidation;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using EmployeeManagement.Application.DTOs;
using EmployeeManagement.Application.Exceptions;
using EmployeeManagement.Application.Interfaces;

namespace EmployeeManagement.Api.Mcp;

/// <summary>
/// MCP tools that expose employee management to an AI client. Each tool is a thin wrapper over
/// <see cref="IEmployeeService"/>, so validation and business rules are the same as for the REST API.
///
/// Tools return JSON text. Failures (validation, not found) are thrown as <see cref="McpException"/>, which the
/// MCP SDK turns into a tool result with <c>isError = true</c> and the message intact, so the model can explain it.
/// Tools that remove data are annotated <c>Destructive = true</c>; the chat host asks the user before running them.
/// </summary>
[McpServerToolType]
public sealed class EmployeeTools
{
    private const int MaxListPageSize = 50;
    private const int MaxBulkRows = 100;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly IEmployeeService _employees;

    public EmployeeTools(IEmployeeService employees)
    {
        _employees = employees;
    }

    // ── Read ─────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "list_employees", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("Lists employees, newest first, one page at a time. Optional filters: 'search' matches name or email (partial, case-insensitive); " +
                 "'department' matches a department name exactly (case-insensitive). Returns the true totalCount.")]
    public async Task<string> ListEmployees(
        [Description("1-based page number. Default 1.")] int page = 1,
        [Description("Rows per page, 1 to 50. Default 20.")] int pageSize = 20,
        [Description("Text to look for in the full name or email.")] string? search = null,
        [Description("Department name to filter by, e.g. 'IT'.")] string? department = null)
    {
        var result = await Run(() => _employees.GetAllAsync(
            Math.Max(1, page), Math.Clamp(pageSize, 1, MaxListPageSize), Clean(search), Clean(department)));

        return Serialize(new
        {
            employees = result.Data.Select(Project),
            totalCount = result.TotalCount,
            pageNumber = result.PageNumber,
            totalPages = result.TotalPages
        });
    }

    [McpServerTool(Name = "find_employees", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("Finds employees whose name or email contains the given text (partial, case-insensitive). " +
                 "Use this to get an employee's id before updating or deleting. Returns up to 20 matches.")]
    public async Task<string> FindEmployees(
        [Description("Name or email fragment, e.g. 'john' or 'john@example.com'.")] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new McpException("'query' must not be empty. Use list_employees to list everyone.");

        var result = await Run(() => _employees.GetAllAsync(1, 20, query.Trim(), null));
        return Serialize(new { employees = result.Data.Select(Project), totalCount = result.TotalCount });
    }

    [McpServerTool(Name = "get_employee", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("Gets one employee by id.")]
    public async Task<string> GetEmployee([Description("Employee id.")] int id)
    {
        var employee = await Run(() => _employees.GetByIdAsync(id));
        return Serialize(new { employee = Project(employee) });
    }

    [McpServerTool(Name = "list_departments", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("Lists the distinct department names currently in use.")]
    public async Task<string> ListDepartments()
    {
        var departments = await Run(() => _employees.GetDepartmentsAsync());
        return Serialize(new { departments });
    }

    [McpServerTool(Name = "get_salary_stats", ReadOnly = true, Destructive = false, Idempotent = true)]
    [Description("Salary statistics: employee count, average, minimum, maximum and total salary, overall and per department. " +
                 "Optionally limited to one department. Use it for questions like 'average salary in IT'.")]
    public async Task<string> GetSalaryStats(
        [Description("Department name to limit the statistics to. Omit for all departments.")] string? department = null)
    {
        var stats = await Run(() => _employees.GetSalaryStatsAsync(Clean(department)));
        return Serialize(new { stats });
    }

    // ── Write ────────────────────────────────────────────────────────────────

    [McpServerTool(Name = "create_employee", ReadOnly = false, Destructive = false, Idempotent = false)]
    [Description("Creates one employee. All four fields are required; ask the user for any that are missing. " +
                 "Fails if the email is already used.")]
    public async Task<string> CreateEmployee(
        [Description("Full name, 2 to 100 characters.")] string fullName,
        [Description("Unique email address.")] string email,
        [Description("Department, 2 to 50 characters.")] string department,
        [Description("Annual salary as a plain number greater than 0, e.g. 85000.")] decimal salary)
    {
        var created = await Run(() => _employees.CreateAsync(new CreateEmployeeDto
        {
            FullName = fullName?.Trim() ?? string.Empty,
            Email = email?.Trim() ?? string.Empty,
            Department = department?.Trim() ?? string.Empty,
            Salary = salary
        }));

        return Serialize(new { employee = Project(created), message = $"✅ {created.FullName} was added (ID {created.Id})." });
    }

    [McpServerTool(Name = "update_employee", ReadOnly = false, Destructive = false, Idempotent = true)]
    [Description("Updates one employee by id. Pass only the fields that change; the others keep their current value. " +
                 "Returns the before/after values. To rename, pass the new name in fullName.")]
    public async Task<string> UpdateEmployee(
        [Description("Id of the employee to update.")] int id,
        [Description("New full name.")] string? fullName = null,
        [Description("New email address.")] string? email = null,
        [Description("New department.")] string? department = null,
        [Description("New annual salary as a plain number.")] decimal? salary = null)
    {
        if (fullName is null && email is null && department is null && salary is null)
            throw new McpException("Nothing to update: provide at least one of fullName, email, department or salary.");

        var current = await Run(() => _employees.GetByIdAsync(id));
        var dto = new UpdateEmployeeDto
        {
            FullName = Clean(fullName) ?? current.FullName,
            Email = Clean(email) ?? current.Email,
            Department = Clean(department) ?? current.Department,
            Salary = salary ?? current.Salary
        };

        var updated = await Run(() => _employees.UpdateAsync(id, dto));
        var changes = Diff(current, updated);

        return Serialize(new
        {
            employee = Project(updated),
            changes,
            message = changes.Count == 0
                ? $"No changes: {updated.FullName} already had those values."
                : $"✅ {updated.FullName} was updated ({string.Join(", ", changes.Select(c => $"{c.Field}: {c.From} → {c.To}"))})."
        });
    }

    [McpServerTool(Name = "bulk_create_employees", ReadOnly = false, Destructive = false, Idempotent = false)]
    [Description("Creates several employees at once (for example from a pasted table). Rows are processed independently; " +
                 "the result lists success or the error for each row. Maximum 100 rows.")]
    public async Task<string> BulkCreateEmployees(
        [Description("The employees to create.")] NewEmployeeInput[] employees)
    {
        EnsureBulkSize(employees?.Length ?? 0);

        var results = new List<object>();
        int ok = 0, failed = 0;
        var row = 0;

        foreach (var e in employees!)
        {
            row++;
            try
            {
                var created = await _employees.CreateAsync(new CreateEmployeeDto
                {
                    FullName = e.FullName?.Trim() ?? string.Empty,
                    Email = e.Email?.Trim() ?? string.Empty,
                    Department = e.Department?.Trim() ?? string.Empty,
                    Salary = e.Salary
                });
                results.Add(new { row, success = true, employee = Project(created) });
                ok++;
            }
            catch (ValidationException vex)
            {
                results.Add(new { row, success = false, name = e.FullName, error = string.Join("; ", vex.Errors.Select(x => x.ErrorMessage)) });
                failed++;
            }
        }

        return BulkResult(results, ok, failed, "created");
    }

    [McpServerTool(Name = "bulk_update_employees", ReadOnly = false, Destructive = false, Idempotent = true)]
    [Description("Updates several employees at once by id (for example 'set everyone in IT to salary 80000' after listing them). " +
                 "Each item needs the id plus the fields to change. Rows are independent. Maximum 100 rows.")]
    public async Task<string> BulkUpdateEmployees(
        [Description("The updates to apply.")] EmployeeUpdateInput[] updates)
    {
        EnsureBulkSize(updates?.Length ?? 0);

        var results = new List<object>();
        int ok = 0, failed = 0;
        var row = 0;

        foreach (var u in updates!)
        {
            row++;
            try
            {
                if (u.FullName is null && u.Email is null && u.Department is null && u.Salary is null)
                    throw new McpException("Nothing to update for this row.");

                var current = await _employees.GetByIdAsync(u.Id);
                var updated = await _employees.UpdateAsync(u.Id, new UpdateEmployeeDto
                {
                    FullName = Clean(u.FullName) ?? current.FullName,
                    Email = Clean(u.Email) ?? current.Email,
                    Department = Clean(u.Department) ?? current.Department,
                    Salary = u.Salary ?? current.Salary
                });
                results.Add(new { row, success = true, employee = Project(updated) });
                ok++;
            }
            catch (Exception ex) when (ex is ValidationException or NotFoundException or McpException)
            {
                results.Add(new { row, success = false, id = u.Id, error = ErrorText(ex) });
                failed++;
            }
        }

        return BulkResult(results, ok, failed, "updated");
    }

    // ── Destructive ──────────────────────────────────────────────────────────

    [McpServerTool(Name = "delete_employee", ReadOnly = false, Destructive = true, Idempotent = true)]
    [Description("Permanently deletes one employee by id. The application asks the user to confirm before this runs. " +
                 "Look up the id first with find_employees if you only have a name.")]
    public async Task<string> DeleteEmployee([Description("Id of the employee to delete.")] int id)
    {
        var employee = await Run(() => _employees.GetByIdAsync(id));
        await Run(() => _employees.DeleteAsync(id));
        return Serialize(new { deletedEmployee = Project(employee), message = $"✅ {employee.FullName} was deleted." });
    }

    [McpServerTool(Name = "bulk_delete_employees", ReadOnly = false, Destructive = true, Idempotent = true)]
    [Description("Permanently deletes several employees by id (for example everyone in a department, after listing them). " +
                 "The application asks the user to confirm before this runs. Maximum 100 ids.")]
    public async Task<string> BulkDeleteEmployees([Description("Ids of the employees to delete.")] int[] ids)
    {
        EnsureBulkSize(ids?.Length ?? 0);

        var results = new List<object>();
        int ok = 0, failed = 0;
        var row = 0;

        foreach (var id in ids!.Distinct())
        {
            row++;
            try
            {
                var employee = await _employees.GetByIdAsync(id);
                await _employees.DeleteAsync(id);
                results.Add(new { row, success = true, employee = Project(employee) });
                ok++;
            }
            catch (NotFoundException ex)
            {
                results.Add(new { row, success = false, id, error = ex.Message });
                failed++;
            }
        }

        return BulkResult(results, ok, failed, "deleted");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<T> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (ValidationException vex)
        {
            throw new McpException(string.Join("; ", vex.Errors.Select(e => e.ErrorMessage).Distinct()));
        }
        catch (NotFoundException nfex)
        {
            throw new McpException(nfex.Message);
        }
    }

    private static async Task Run(Func<Task> action)
    {
        await Run(async () =>
        {
            await action();
            return true;
        });
    }

    private static string ErrorText(Exception ex) => ex switch
    {
        ValidationException vex => string.Join("; ", vex.Errors.Select(e => e.ErrorMessage).Distinct()),
        _ => ex.Message
    };

    private static void EnsureBulkSize(int count)
    {
        if (count == 0) throw new McpException("The list is empty.");
        if (count > MaxBulkRows) throw new McpException($"Too many items ({count}). The maximum is {MaxBulkRows} per call.");
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static object Project(EmployeeDto e) => new
    {
        id = e.Id,
        fullName = e.FullName,
        email = e.Email,
        department = e.Department,
        salary = e.Salary,
        updatedAt = e.UpdatedAt
    };

    private static List<FieldChange> Diff(EmployeeDto before, EmployeeDto after)
    {
        var changes = new List<FieldChange>();
        if (before.FullName != after.FullName) changes.Add(new("fullName", before.FullName, after.FullName));
        if (before.Email != after.Email) changes.Add(new("email", before.Email, after.Email));
        if (before.Department != after.Department) changes.Add(new("department", before.Department, after.Department));
        if (before.Salary != after.Salary) changes.Add(new("salary", before.Salary.ToString("0.##"), after.Salary.ToString("0.##")));
        return changes;
    }

    private static string BulkResult(List<object> results, int ok, int failed, string verb) =>
        Serialize(new
        {
            results,
            successCount = ok,
            failCount = failed,
            totalRows = ok + failed,
            message = failed == 0
                ? $"✅ All {ok} employees were {verb}."
                : $"{ok} {verb}, {failed} failed. See the per-row results."
        });

    private static string Serialize(object value) => JsonSerializer.Serialize(value, Json);
}

/// <summary>One employee to create in a bulk call.</summary>
public sealed class NewEmployeeInput
{
    [Description("Full name.")] public string FullName { get; set; } = string.Empty;
    [Description("Unique email address.")] public string Email { get; set; } = string.Empty;
    [Description("Department.")] public string Department { get; set; } = string.Empty;
    [Description("Annual salary as a plain number greater than 0.")] public decimal Salary { get; set; }
}

/// <summary>One update in a bulk call. Only the provided fields change.</summary>
public sealed class EmployeeUpdateInput
{
    [Description("Id of the employee to update.")] public int Id { get; set; }
    [Description("New full name.")] public string? FullName { get; set; }
    [Description("New email address.")] public string? Email { get; set; }
    [Description("New department.")] public string? Department { get; set; }
    [Description("New annual salary as a plain number.")] public decimal? Salary { get; set; }
}

/// <summary>One changed field in an update result.</summary>
public sealed record FieldChange(string Field, string From, string To);
