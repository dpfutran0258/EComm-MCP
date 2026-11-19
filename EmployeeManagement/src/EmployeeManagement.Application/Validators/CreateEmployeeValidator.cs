using FluentValidation;
using EmployeeManagement.Application.DTOs;
using EmployeeManagement.Application.Interfaces;

namespace EmployeeManagement.Application.Validators;

public class CreateEmployeeValidator : AbstractValidator<CreateEmployeeDto>
{
    private readonly IEmployeeRepository _repository;

    public CreateEmployeeValidator(IEmployeeRepository repository)
    {
        _repository = repository;

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full Name is required")
            .MinimumLength(2).WithMessage("Full Name must be at least 2 characters")
            .MaximumLength(100).WithMessage("Full Name cannot exceed 100 characters");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required")
            .EmailAddress().WithMessage("Invalid email format")
            .MustAsync(async (email, _) => !await _repository.EmailExistsAsync(email))
            .WithMessage("Email already exists");

        RuleFor(x => x.Department)
            .NotEmpty().WithMessage("Department is required")
            .MinimumLength(2).WithMessage("Department must be at least 2 characters")
            .MaximumLength(50).WithMessage("Department cannot exceed 50 characters");

        RuleFor(x => x.Salary)
            .GreaterThan(0).WithMessage("Salary must be greater than 0");
    }
}
