namespace EmployeeManagement.Application.DTOs;

/// <summary>Salary statistics for one department.</summary>
public sealed class DepartmentStatDto
{
    public string Department { get; set; } = string.Empty;
    public int Count { get; set; }
    public decimal AverageSalary { get; set; }
    public decimal MinSalary { get; set; }
    public decimal MaxSalary { get; set; }
    public decimal TotalSalary { get; set; }
}

/// <summary>Overall salary statistics plus a per-department breakdown.</summary>
public sealed class SalaryStatsDto
{
    public int EmployeeCount { get; set; }
    public decimal AverageSalary { get; set; }
    public decimal MinSalary { get; set; }
    public decimal MaxSalary { get; set; }
    public decimal TotalSalary { get; set; }
    public List<DepartmentStatDto> ByDepartment { get; set; } = new();
}
