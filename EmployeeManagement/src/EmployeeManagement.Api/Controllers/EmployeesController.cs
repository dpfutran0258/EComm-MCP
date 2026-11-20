using Microsoft.AspNetCore.Mvc;
using EmployeeManagement.Application.DTOs;
using EmployeeManagement.Application.Interfaces;

namespace EmployeeManagement.Api.Controllers;

/// <summary>
/// REST API for employee CRUD operations.
/// Uses <see cref="IEmployeeService"/> so the controller is testable without a database.
/// Exception handling is delegated to <see cref="Middleware.ExceptionHandlingMiddleware"/>.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _employeeService;
    private readonly ILogger<EmployeesController> _logger;

    public EmployeesController(IEmployeeService employeeService, ILogger<EmployeesController> logger)
    {
        _employeeService = employeeService;
        _logger = logger;
    }

    /// <summary>Creates a new employee.</summary>
    /// <response code="201">Employee created successfully.</response>
    /// <response code="400">Validation error.</response>
    [HttpPost]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EmployeeDto>> CreateEmployee(
        [FromBody] CreateEmployeeDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _employeeService.CreateAsync(dto);
        _logger.LogInformation("Employee created via API. Id={Id}", result.Id);
        return CreatedAtAction(nameof(GetEmployee), new { id = result.Id }, result);
    }

    /// <summary>Returns a single employee by ID.</summary>
    /// <response code="200">Employee found.</response>
    /// <response code="404">Employee not found.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDto>> GetEmployee(int id, CancellationToken cancellationToken)
    {
        var result = await _employeeService.GetByIdAsync(id);
        return Ok(result);
    }

    /// <summary>Returns a paginated, filterable list of employees.</summary>
    /// <param name="pageNumber">1-based page number (default 1).</param>
    /// <param name="pageSize">Records per page, max 100 (default 10).</param>
    /// <param name="search">Filter by name or email.</param>
    /// <param name="department">Filter by exact department name.</param>
    /// <response code="200">List returned successfully.</response>
    [HttpGet]
    [ProducesResponseType(typeof(EmployeeListResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<EmployeeListResponseDto>> GetEmployees(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? department = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _employeeService.GetAllAsync(pageNumber, pageSize, search, department);
        return Ok(result);
    }

    /// <summary>Returns the distinct department names in use (for filter suggestions).</summary>
    /// <response code="200">Department names, sorted alphabetically.</response>
    [HttpGet("departments")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<string>>> GetDepartments(CancellationToken cancellationToken)
    {
        return Ok(await _employeeService.GetDepartmentsAsync());
    }

    /// <summary>Updates an existing employee.</summary>
    /// <response code="200">Updated successfully.</response>
    /// <response code="400">Validation error.</response>
    /// <response code="404">Employee not found.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(EmployeeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EmployeeDto>> UpdateEmployee(
        int id,
        [FromBody] UpdateEmployeeDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _employeeService.UpdateAsync(id, dto);
        _logger.LogInformation("Employee updated via API. Id={Id}", id);
        return Ok(result);
    }

    /// <summary>Deletes an employee.</summary>
    /// <response code="204">Deleted successfully.</response>
    /// <response code="404">Employee not found.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEmployee(int id, CancellationToken cancellationToken)
    {
        await _employeeService.DeleteAsync(id);
        _logger.LogInformation("Employee deleted via API. Id={Id}", id);
        return NoContent();
    }
}
