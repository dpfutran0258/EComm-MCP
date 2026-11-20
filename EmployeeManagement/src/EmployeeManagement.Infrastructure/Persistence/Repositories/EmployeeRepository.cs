using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using EmployeeManagement.Application.DTOs;
using EmployeeManagement.Application.Interfaces;
using EmployeeManagement.Domain.Entities;

namespace EmployeeManagement.Infrastructure.Persistence.Repositories;

public class EmployeeRepository : IEmployeeRepository
{
    private readonly EmployeeManagementDbContext _context;

    public EmployeeRepository(EmployeeManagementDbContext context)
    {
        _context = context;
    }

    public async Task<Employee?> GetByIdAsync(int id)
    {
        return await _context.Employees.FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<(List<Employee> items, int totalCount)> GetAllAsync(int pageNumber, int pageSize, string? search = null, string? department = null)
    {
        var query = _context.Employees.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.Trim().ToLower();
            query = query.Where(e => e.FullName.ToLower().Contains(searchLower) || e.Email.ToLower().Contains(searchLower));
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            var departmentLower = department.Trim().ToLower();
            query = query.Where(e => e.Department.ToLower() == departmentLower);
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Employee> CreateAsync(Employee employee)
    {
        employee.CreatedAt = DateTime.UtcNow;
        employee.UpdatedAt = DateTime.UtcNow;

        _context.Employees.Add(employee);
        await SaveAsync();
        return employee;
    }

    public async Task<Employee> UpdateAsync(Employee employee)
    {
        employee.UpdatedAt = DateTime.UtcNow;
        _context.Employees.Update(employee);
        await SaveAsync();
        return employee;
    }

    public async Task DeleteAsync(int id)
    {
        var employee = await GetByIdAsync(id);
        if (employee != null)
        {
            _context.Employees.Remove(employee);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> EmailExistsAsync(string email, int? excludeId = null)
    {
        var emailLower = email.Trim().ToLower();
        var query = _context.Employees.Where(e => e.Email.ToLower() == emailLower);

        if (excludeId.HasValue)
        {
            query = query.Where(e => e.Id != excludeId.Value);
        }

        return await query.AnyAsync();
    }

    public async Task<List<string>> GetDepartmentsAsync()
    {
        var departments = await _context.Employees
            .Select(e => e.Department)
            .Distinct()
            .ToListAsync();

        return departments
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<List<DepartmentStatDto>> GetDepartmentStatsAsync(string? department = null)
    {
        var query = _context.Employees.AsQueryable();
        if (!string.IsNullOrWhiteSpace(department))
        {
            var departmentLower = department.Trim().ToLower();
            query = query.Where(e => e.Department.ToLower() == departmentLower);
        }

        var groups = await query
            .GroupBy(e => e.Department)
            .Select(g => new
            {
                Department = g.Key,
                Count = g.Count(),
                Min = g.Min(e => e.Salary),
                Max = g.Max(e => e.Salary),
                Total = g.Sum(e => e.Salary)
            })
            .ToListAsync();

        return groups
            .OrderBy(g => g.Department, StringComparer.OrdinalIgnoreCase)
            .Select(g => new DepartmentStatDto
            {
                Department = g.Department,
                Count = g.Count,
                AverageSalary = Math.Round(g.Total / g.Count, 2),
                MinSalary = g.Min,
                MaxSalary = g.Max,
                TotalSalary = g.Total
            })
            .ToList();
    }

    /// <summary>
    /// Saves changes. A unique-index violation (a concurrent duplicate email that slipped past the
    /// validator) becomes a validation error instead of an unhandled 500.
    /// </summary>
    private async Task SaveAsync()
    {
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new ValidationException(new[] { new ValidationFailure("Email", "Email already exists") });
        }
    }
}
