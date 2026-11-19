namespace EmployeeManagement.Application.DTOs;

public class CreateEmployeeDto
{
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Department { get; set; } = null!;
    public decimal Salary { get; set; }
}
