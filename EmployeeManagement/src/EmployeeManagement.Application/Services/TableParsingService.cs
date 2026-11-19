using System.Text.RegularExpressions;
using EmployeeManagement.Application.AI;
using Microsoft.Extensions.Logging;

namespace EmployeeManagement.Application.Services;

/// <summary>
/// Parses pasted employee tables (Markdown, CSV, TSV, plain rows) into
/// <see cref="BulkEmployeeData"/> records ready for validation and insertion.
/// </summary>
public sealed class TableParsingService
{
    private readonly ILogger<TableParsingService> _logger;

    // Column name aliases mapped to canonical property names
    private static readonly Dictionary<string, string> ColumnAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["full name"] = "fullname",
        ["fullname"] = "fullname",
        ["name"] = "fullname",
        ["employee name"] = "fullname",
        ["email"] = "email",
        ["email address"] = "email",
        ["dept"] = "department",
        ["department"] = "department",
        ["division"] = "department",
        ["team"] = "department",
        ["salary"] = "salary",
        ["pay"] = "salary",
        ["compensation"] = "salary",
        ["annual salary"] = "salary",
        ["ctc"] = "salary"
    };

    public TableParsingService(ILogger<TableParsingService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Attempts to parse <paramref name="input"/> as a table of employee records.
    /// Returns an empty list when the input does not look like tabular data.
    /// </summary>
    public List<BulkEmployeeData> TryParse(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return new();

        var lines = input.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Need at least a header + one data row
        if (lines.Length < 2) return new();

        // Detect delimiter
        var delimiter = DetectDelimiter(lines[0]);

        var headerRow = SplitRow(lines[0], delimiter);
        var columnMap = BuildColumnMap(headerRow);

        if (columnMap.Count == 0)
        {
            _logger.LogDebug("No recognisable employee columns found in table header.");
            return new();
        }

        var results = new List<BulkEmployeeData>();

        foreach (var line in lines.Skip(1))
        {
            // Skip Markdown separator lines like |---|---|
            if (Regex.IsMatch(line, @"^[\s|\-:]+$")) continue;
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cells = SplitRow(line, delimiter);
            var row = MapRow(cells, columnMap);
            if (row != null) results.Add(row);
        }

        _logger.LogInformation("Table parsed: {Count} employee records extracted.", results.Count);
        return results;
    }

    private static char DetectDelimiter(string headerLine)
    {
        if (headerLine.Contains('|')) return '|';
        if (headerLine.Contains('\t')) return '\t';
        return ',';
    }

    private static List<string> SplitRow(string line, char delimiter)
    {
        var parts = line.Split(delimiter)
            .Select(c => c.Trim().Trim('"'))
            .ToList();

        // Remove empty leading/trailing cells from Markdown pipes (| col | col |)
        while (parts.Count > 0 && string.IsNullOrWhiteSpace(parts[0])) parts.RemoveAt(0);
        while (parts.Count > 0 && string.IsNullOrWhiteSpace(parts[^1])) parts.RemoveAt(parts.Count - 1);

        return parts;
    }

    private static Dictionary<int, string> BuildColumnMap(List<string> headers)
    {
        var map = new Dictionary<int, string>();
        for (int i = 0; i < headers.Count; i++)
        {
            var headerNorm = headers[i].Trim().ToLowerInvariant();
            if (ColumnAliases.TryGetValue(headerNorm, out var canonical))
                map[i] = canonical;
        }
        return map;
    }

    private BulkEmployeeData? MapRow(List<string> cells, Dictionary<int, string> columnMap)
    {
        var row = new BulkEmployeeData();
        bool hasAnyData = false;

        foreach (var (index, field) in columnMap)
        {
            if (index >= cells.Count) continue;
            var value = cells[index].Trim();
            if (string.IsNullOrWhiteSpace(value)) continue;

            hasAnyData = true;
            switch (field)
            {
                case "fullname":
                    row.FullName = value;
                    break;
                case "email":
                    row.Email = value;
                    break;
                case "department":
                    row.Department = value;
                    break;
                case "salary":
                    var salaryStr = Regex.Replace(value, @"[^\d.]", "");
                    if (decimal.TryParse(salaryStr, out var salary))
                        row.Salary = salary;
                    else
                        _logger.LogWarning("Could not parse salary value '{Value}'", value);
                    break;
            }
        }

        return hasAnyData ? row : null;
    }
}
