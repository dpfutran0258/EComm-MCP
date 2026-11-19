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
}
