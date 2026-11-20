using EmployeeManagement.Application.DTOs;
using EmployeeManagement.Domain.Entities;

namespace EmployeeManagement.Application.Interfaces;

public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(int id);
    Task<(List<Employee> items, int totalCount)> GetAllAsync(int pageNumber, int pageSize, string? search = null, string? department = null);
    Task<Employee> CreateAsync(Employee employee);
    Task<Employee> UpdateAsync(Employee employee);
    Task DeleteAsync(int id);
    Task<bool> EmailExistsAsync(string email, int? excludeId = null);

    /// <summary>Distinct department names, sorted alphabetically (case-insensitive).</summary>
    Task<List<string>> GetDepartmentsAsync();

    /// <summary>Salary statistics grouped by department, optionally limited to one department (case-insensitive).</summary>
    Task<List<DepartmentStatDto>> GetDepartmentStatsAsync(string? department = null);
}
