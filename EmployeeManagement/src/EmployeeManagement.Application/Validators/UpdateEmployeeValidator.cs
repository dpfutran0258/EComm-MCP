using FluentValidation;
using EmployeeManagement.Application.DTOs;
using EmployeeManagement.Application.Interfaces;

namespace EmployeeManagement.Application.Validators;

public class UpdateEmployeeValidator : AbstractValidator<UpdateEmployeeDto>
{
    private readonly IEmployeeRepository _repository;
    private readonly int _employeeId;

    public UpdateEmployeeValidator(IEmployeeRepository repository, int employeeId)
    {
        _repository = repository;
        _employeeId = employeeId;

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full Name is required")
            .MinimumLength(2).WithMessage("Full Name must be at least 2 characters")
            .MaximumLength(100).WithMessage("Full Name cannot exceed 100 characters");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MustAsync(async (email, _) => !await _repository.EmailExistsAsync(email, _employeeId))
            .WithMessage("Email already exists");

        RuleFor(x => x.Department)
            .NotEmpty().WithMessage("Department is required")
            .MinimumLength(2).WithMessage("Department must be at least 2 characters")
            .MaximumLength(50).WithMessage("Department cannot exceed 50 characters");

        RuleFor(x => x.Salary)
            .GreaterThan(0).WithMessage("Salary must be greater than 0");
    }
}
