using EmployeeManagement.Application.DTOs;

namespace EmployeeManagement.Application.Interfaces;

/// <summary>
/// Application-level employee operations. Controllers and the MCP tools
/// depend on this interface, not the concrete EmployeeService class.
/// </summary>
public interface IEmployeeService
{
    Task<EmployeeDto> CreateAsync(CreateEmployeeDto dto);
    Task<EmployeeDto> UpdateAsync(int id, UpdateEmployeeDto dto);
    Task<EmployeeDto> GetByIdAsync(int id);
    Task<EmployeeListResponseDto> GetAllAsync(int pageNumber = 1, int pageSize = 10, string? search = null, string? department = null);
    Task DeleteAsync(int id);

    /// <summary>Distinct department names.</summary>
    Task<List<string>> GetDepartmentsAsync();

    /// <summary>Salary statistics overall and per department; optionally limited to one department.</summary>
    Task<SalaryStatsDto> GetSalaryStatsAsync(string? department = null);
}
