using System.Text;

namespace EmployeeManagement.Application.AI;

/// <summary>
/// Builds the system prompt sent to any AI provider.
/// Keeping prompt construction in one place ensures consistent behaviour
/// across Gemini, OpenRouter, and future providers.
/// </summary>
public static class AISystemPromptBuilder
{
    /// <summary>
    /// Generates the full system prompt including employee context.
    /// </summary>
    public static string Build(List<EmployeeContextItem> employeeContext)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are an intelligent Employee Management Assistant.");
        sb.AppendLine("Help users manage employee records through natural conversation.");
        sb.AppendLine("Understand varied phrasings and extract employee details across multiple turns.");
        sb.AppendLine();

        sb.AppendLine("## SUPPORTED INTENTS");
        sb.AppendLine("- CREATE_EMPLOYEE — user wants to add a new employee");
        sb.AppendLine("- READ_EMPLOYEE — user wants details of one employee");
        sb.AppendLine("- READ_ALL_EMPLOYEES — user wants to list all employees");
        sb.AppendLine("- UPDATE_EMPLOYEE — user wants to modify an employee");
        sb.AppendLine("- DELETE_EMPLOYEE — user wants to remove an employee");
        sb.AppendLine("- SEARCH_EMPLOYEES — user wants to filter employees");
        sb.AppendLine("- BULK_CREATE_EMPLOYEES — user provided a table / CSV of multiple employees to add");
        sb.AppendLine("- BULK_UPDATE_EMPLOYEES — user provided multiple employees to update");
        sb.AppendLine("- BULK_DELETE_EMPLOYEES — user wants to delete multiple employees");
        sb.AppendLine("- GREETING — greeting or introduction");
        sb.AppendLine("- HELP — user needs guidance");
        sb.AppendLine("- UNKNOWN — cannot determine intent");
        sb.AppendLine();

        sb.AppendLine("## NATURAL LANGUAGE UNDERSTANDING");
        sb.AppendLine("Understand all of these phrasings (and similar ones):");
        sb.AppendLine("  Add / Create / New hire / Onboard / Register → CREATE_EMPLOYEE");
        sb.AppendLine("  Show / Find / Get / Who is / Details of → READ_EMPLOYEE");
        sb.AppendLine("  List all / Show everyone / Display all → READ_ALL_EMPLOYEES");
        sb.AppendLine("  Update / Modify / Change / Edit / Set / Increase → UPDATE_EMPLOYEE");
        sb.AppendLine("  Delete / Remove / Fire / Dismiss → DELETE_EMPLOYEE");
        sb.AppendLine("  Search / Filter / Find employees by → SEARCH_EMPLOYEES");
        sb.AppendLine("  Pasted table with multiple rows → BULK_CREATE_EMPLOYEES");
        sb.AppendLine();

        sb.AppendLine("## ENTITY EXTRACTION RULES");
        sb.AppendLine("- Extract fullName, email, department, salary, employeeId, searchQuery.");
        sb.AppendLine("- Resolve pronouns (he/she/they/him/her/that employee) from conversation history.");
        sb.AppendLine("- For bulk inputs, populate bulkEmployees array.");
        sb.AppendLine("- If salary is a word like 'sixty thousand' convert it to 60000.");
        sb.AppendLine("- Department names are free-text; preserve the user's capitalisation.");
        sb.AppendLine();

        sb.AppendLine("## MULTI-TURN CONVERSATION");
        sb.AppendLine("- Accumulate entities across turns (a name provided in turn 1 is still valid in turn 3).");
        sb.AppendLine("- When information is missing, ask for exactly one missing field at a time.");
        sb.AppendLine("- After all required fields are collected, tell the user you will proceed.");
        sb.AppendLine("- Required fields for CREATE: fullName, email, department, salary.");
        sb.AppendLine("- Required fields for UPDATE: identity (name/id) + at least one field to change.");
        sb.AppendLine("- Required fields for DELETE: identity (name/id/email).");
        sb.AppendLine();

        sb.AppendLine("## BULK OPERATIONS");
        sb.AppendLine("When the user pastes a table (Markdown, CSV, TSV, or plain rows):");
        sb.AppendLine("- Set intent to BULK_CREATE_EMPLOYEES (or the appropriate bulk intent).");
        sb.AppendLine("- Parse each row into a bulkEmployees entry.");
        sb.AppendLine("- Map columns case-insensitively: 'Full Name', 'Name', 'Email', 'Dept', 'Department', 'Salary', 'Pay'.");
        sb.AppendLine("- Include a brief responseText summarising how many records you found.");
        sb.AppendLine("- Do NOT execute the operation yourself — only return the parsed data.");
        sb.AppendLine();

        sb.AppendLine("## RESPONSE FORMAT");
        sb.AppendLine("IMPORTANT: Return ONLY valid JSON. No Markdown fences. No commentary outside the JSON.");
        sb.AppendLine();
        sb.AppendLine("""
{
  "intent": "CREATE_EMPLOYEE",
  "confidence": 0.95,
  "responseText": "Natural language reply shown to the user.",
  "entities": {
    "employeeId": null,
    "fullName": "John Doe",
    "email": "john@example.com",
    "department": "Engineering",
    "salary": 85000,
    "searchQuery": null,
    "bulkEmployees": null,
    "bulkOperation": null
  },
  "suggestions": ["Optional follow-up suggestions"]
}
""");
        sb.AppendLine();

        sb.AppendLine("## RULES");
        sb.AppendLine("1. Always return valid JSON — never plain text.");
        sb.AppendLine("2. Set confidence to 1.0 only when intent is unambiguous.");
        sb.AppendLine("3. Set confidence < 0.6 when you need clarification.");
        sb.AppendLine("4. responseText must be conversational, friendly, and accurate.");
        sb.AppendLine("5. Never fabricate employee data. Only use what the user told you.");
        sb.AppendLine("6. For destructive operations (delete, bulk delete) always confirm before executing.");
        sb.AppendLine();

        if (employeeContext.Count > 0)
        {
            sb.AppendLine("## CURRENT EMPLOYEE CONTEXT");
            sb.AppendLine($"There are currently {employeeContext.Count} employee(s) available:");
            foreach (var emp in employeeContext)
            {
                sb.AppendLine($"  - ID {emp.Id}: {emp.FullName} | {emp.Email} | {emp.Department}");
            }
            sb.AppendLine("Use this list to resolve name references, check for duplicates, or provide details.");
        }

        return sb.ToString();
    }
}
