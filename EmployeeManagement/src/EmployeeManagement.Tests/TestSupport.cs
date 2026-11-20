using System.Text.Json;
using EmployeeManagement.Application.AI;
using EmployeeManagement.Application.DTOs;
using EmployeeManagement.Application.Interfaces;
using EmployeeManagement.Domain.Entities;

namespace EmployeeManagement.Tests;

/// <summary>In-memory repository so tests run the real EmployeeService and validators without a database.</summary>
public sealed class InMemoryEmployeeRepository : IEmployeeRepository
{
    private readonly List<Employee> _items = new();
    private int _nextId = 1;
    private readonly object _gate = new();

    public Employee Seed(string name, string email, string department, decimal salary)
    {
        lock (_gate)
        {
            var e = new Employee
            {
                Id = _nextId++,
                FullName = name,
                Email = email,
                Department = department,
                Salary = salary,
                CreatedAt = DateTime.UtcNow.AddSeconds(_nextId),
                UpdatedAt = DateTime.UtcNow
            };
            _items.Add(e);
            return e;
        }
    }

    public int Count { get { lock (_gate) return _items.Count; } }

    public Employee? Find(int id) { lock (_gate) return _items.FirstOrDefault(e => e.Id == id); }

    public Task<Employee?> GetByIdAsync(int id) => Task.FromResult(Find(id));

    public Task<(List<Employee> items, int totalCount)> GetAllAsync(int pageNumber, int pageSize, string? search = null, string? department = null)
    {
        lock (_gate)
        {
            IEnumerable<Employee> q = _items;
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                q = q.Where(e => e.FullName.ToLowerInvariant().Contains(s) || e.Email.ToLowerInvariant().Contains(s));
            }
            if (!string.IsNullOrWhiteSpace(department))
            {
                var d = department.Trim().ToLowerInvariant();
                q = q.Where(e => e.Department.ToLowerInvariant() == d);
            }

            var all = q.OrderByDescending(e => e.CreatedAt).ToList();
            return Task.FromResult((all.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList(), all.Count));
        }
    }

    public Task<Employee> CreateAsync(Employee employee)
    {
        lock (_gate)
        {
            employee.Id = _nextId++;
            employee.CreatedAt = DateTime.UtcNow;
            employee.UpdatedAt = DateTime.UtcNow;
            _items.Add(employee);
            return Task.FromResult(employee);
        }
    }

    public Task<Employee> UpdateAsync(Employee employee) => Task.FromResult(employee);

    public Task DeleteAsync(int id)
    {
        lock (_gate) _items.RemoveAll(e => e.Id == id);
        return Task.CompletedTask;
    }

    public Task<bool> EmailExistsAsync(string email, int? excludeId = null)
    {
        lock (_gate)
        {
            var e = email.Trim().ToLowerInvariant();
            return Task.FromResult(_items.Any(x => x.Email.ToLowerInvariant() == e && x.Id != excludeId));
        }
    }

    public Task<List<string>> GetDepartmentsAsync()
    {
        lock (_gate)
            return Task.FromResult(_items.Select(e => e.Department).Distinct().OrderBy(d => d, StringComparer.OrdinalIgnoreCase).ToList());
    }

    public Task<List<DepartmentStatDto>> GetDepartmentStatsAsync(string? department = null)
    {
        lock (_gate)
        {
            IEnumerable<Employee> q = _items;
            if (!string.IsNullOrWhiteSpace(department))
                q = q.Where(e => string.Equals(e.Department, department.Trim(), StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(q.GroupBy(e => e.Department).Select(g => new DepartmentStatDto
            {
                Department = g.Key,
                Count = g.Count(),
                AverageSalary = Math.Round(g.Average(e => e.Salary), 2),
                MinSalary = g.Min(e => e.Salary),
                MaxSalary = g.Max(e => e.Salary),
                TotalSalary = g.Sum(e => e.Salary)
            }).OrderBy(s => s.Department).ToList());
        }
    }
}

/// <summary>AI provider that replays scripted turns and records what it was sent.</summary>
public sealed class ScriptedAIProvider : IAIProviderService
{
    private readonly Queue<AIChatTurn> _turns = new();

    public string ProviderName => "Scripted";
    public string ModelName => "scripted-model";

    /// <summary>Snapshot of the messages passed on each call.</summary>
    public List<List<ChatMessageDto>> Calls { get; } = new();

    public ScriptedAIProvider Say(string text)
    {
        _turns.Enqueue(new AIChatTurn(text, Array.Empty<ChatToolCallDto>(), ProviderName, ModelName));
        return this;
    }

    public ScriptedAIProvider CallTool(string name, object arguments, string id = "call-1")
    {
        _turns.Enqueue(new AIChatTurn(null,
            new[] { new ChatToolCallDto { Id = id, Name = name, Arguments = JsonSerializer.Serialize(arguments) } },
            ProviderName, ModelName));
        return this;
    }

    public Task<AIChatTurn> CompleteAsync(
        IReadOnlyList<ChatMessageDto> messages, IReadOnlyList<ToolDefinition> tools, string? preferredModel, CancellationToken cancellationToken = default)
    {
        Calls.Add(messages.Select(m => new ChatMessageDto
        {
            Role = m.Role, Content = m.Content, ToolCallId = m.ToolCallId, ToolCalls = m.ToolCalls
        }).ToList());

        if (_turns.Count == 0)
            throw new InvalidOperationException("The scripted AI provider ran out of turns.");
        return Task.FromResult(_turns.Dequeue());
    }
}

/// <summary>Tool invoker with canned results; records every call.</summary>
public sealed class FakeToolInvoker : IToolInvoker
{
    private static readonly JsonElement EmptySchema = JsonDocument.Parse("""{"type":"object","properties":{}}""").RootElement.Clone();

    private readonly Dictionary<string, Func<string, ToolResult>> _handlers = new();
    private readonly List<ToolDefinition> _definitions = new();

    public List<(string Name, string Args)> Calls { get; } = new();

    public FakeToolInvoker Tool(string name, bool readOnly, bool destructive, Func<string, ToolResult> handler)
    {
        _definitions.Add(new ToolDefinition(name, name, EmptySchema, readOnly, destructive));
        _handlers[name] = handler;
        return this;
    }

    public Task<IReadOnlyList<ToolDefinition>> ListToolsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ToolDefinition>>(_definitions);

    public Task<ToolResult> CallToolAsync(string name, string argumentsJson, CancellationToken cancellationToken = default)
    {
        Calls.Add((name, argumentsJson));
        return Task.FromResult(_handlers[name](argumentsJson));
    }
}
