using FluentValidation;
using EmployeeManagement.Application.DTOs;
using EmployeeManagement.Application.Exceptions;
using EmployeeManagement.Application.Interfaces;
using EmployeeManagement.Application.Validators;
using EmployeeManagement.Domain.Entities;

namespace EmployeeManagement.Application.Services;

/// <summary>
/// Core employee business logic. All CRUD operations go through here.
/// Controllers and ChatService depend on IEmployeeService, not this class directly.
/// </summary>
public class EmployeeService : IEmployeeService
{
    private readonly IEmployeeRepository _repository;

    public EmployeeService(IEmployeeRepository repository)
    {
        _repository = repository;
    }

    public async Task<EmployeeDto> CreateAsync(CreateEmployeeDto dto)
    {
        var validator = new CreateEmployeeValidator(_repository);
        var validationResult = await validator.ValidateAsync(dto);
        
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var employee = new Employee
        {
            FullName = dto.FullName,
            Email = dto.Email,
            Department = dto.Department,
            Salary = dto.Salary
        };

        var created = await _repository.CreateAsync(employee);
        return MapToDto(created);
    }

    public async Task<EmployeeDto> UpdateAsync(int id, UpdateEmployeeDto dto)
    {
        var employee = await _repository.GetByIdAsync(id);
        if (employee == null)
        {
            throw new NotFoundException($"Employee with ID {id} not found");
        }

        var validator = new UpdateEmployeeValidator(_repository, id);
        var validationResult = await validator.ValidateAsync(dto);
        
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        employee.FullName = dto.FullName;
        employee.Email = dto.Email;
        employee.Department = dto.Department;
        employee.Salary = dto.Salary;
        employee.UpdatedAt = DateTime.UtcNow;

        var updated = await _repository.UpdateAsync(employee);
        return MapToDto(updated);
    }

    public async Task<EmployeeDto> GetByIdAsync(int id)
    {
        var employee = await _repository.GetByIdAsync(id);
        if (employee == null)
        {
            throw new NotFoundException($"Employee with ID {id} not found");
        }

        return MapToDto(employee);
    }

    public async Task<EmployeeListResponseDto> GetAllAsync(int pageNumber = 1, int pageSize = 10, string? search = null, string? department = null)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var (employees, totalCount) = await _repository.GetAllAsync(pageNumber, pageSize, search, department);
        
        return new EmployeeListResponseDto
        {
            Data = employees.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
        };
    }

    public async Task DeleteAsync(int id)
    {
        var employee = await _repository.GetByIdAsync(id);
        if (employee == null)
        {
            throw new NotFoundException($"Employee with ID {id} not found");
        }

        await _repository.DeleteAsync(id);
    }

    private EmployeeDto MapToDto(Employee employee)
    {
        return new EmployeeDto
        {
            Id = employee.Id,
            FullName = employee.FullName,
            Email = employee.Email,
            Department = employee.Department,
            Salary = employee.Salary,
            CreatedAt = employee.CreatedAt,
            UpdatedAt = employee.UpdatedAt
        };
    }
}
