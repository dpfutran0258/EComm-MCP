# Employee Management with AI chat

An employee management demo: an ASP.NET Core Web API with PostgreSQL, and a React frontend with a chat assistant
that turns natural-language requests (and pasted tables) into employee create, read, update and delete operations.
The assistant uses a large language model through [OpenRouter](https://openrouter.ai).

This README is the single source of truth for the project.

> **About the name:** the repository folder is called `EComm-MCP`, but this is not an e-commerce application.
> The "MCP" is real: the API hosts an [MCP](https://modelcontextprotocol.io) server (`/mcp`) whose tools manage
> employees, and the chat assistant is an MCP client that lets the AI choose those tools. See
> [How the chat works](#how-the-chat-works).

## How the chat works

The user types normal text. The AI model decides which **MCP tool** to call, and the application performs it.

```
Browser ── POST /api/chat { messages: [...transcript...] } ──► ChatAgentService (MCP client + agent loop)
                                                                   │  tools/list, tools/call (MCP over HTTP)
                                                                   ▼
                                       /mcp  MCP server: EmployeeTools ──► EmployeeService ──► PostgreSQL
```

- **Context is sent in the request.** The browser keeps the transcript (user messages, the assistant's tool calls,
  tool results, answers) and sends all of it with every message. The server keeps no conversation state. That is how
  "give her a 10% raise" or "delete the second one" works: the model sees the earlier results, including employee ids.
- **Tools** (`EmployeeManagement.Api/Mcp/EmployeeTools.cs`): `list_employees`, `find_employees`, `get_employee`,
  `create_employee`, `update_employee`, `delete_employee`, `bulk_create_employees`, `bulk_update_employees`,
  `bulk_delete_employees`, `list_departments`, `get_salary_stats`. They use the same service and validators as the REST API.
- **Deleting needs a click.** Tools marked destructive (`delete_employee`, `bulk_delete_employees`) are never run on the
  model's say-so. The chat shows **Confirm / Cancel** buttons; Confirm sends a signed, 5-minute token
  (ASP.NET Data Protection) and the server runs the action without involving the model.
- The same `/mcp` endpoint works with any MCP client (for example `npx @modelcontextprotocol/inspector` pointed at
  `http://localhost:5000/mcp`). It only accepts loopback connections because there is no authentication yet.

---

## Security status

> **There is no authentication or authorization yet.** Every API endpoint, including create, update, delete and
> the AI chat endpoint, can be called by anyone who can reach the server. **Run it on localhost only.** Do not
> expose it on a network or deploy it until authentication is added.

What is in place today:

- Secrets (database connection string, OpenRouter API key) are read from .NET user-secrets or environment variables.
  `appsettings*.json` keeps them blank, and they must never be committed.
- Startup fails with a non-zero exit code if the connection string or the AI settings are missing or invalid.
- Clients cannot choose arbitrary LLM models. `preferredModel` must be in `AI:OpenRouter:AllowedModels`
  (or equal the configured `Model`), otherwise the chat endpoint returns `400`.
- The AI never touches the database directly. It can only call the MCP tools, which validate with FluentValidation
  before saving, and destructive tools need an explicit user confirmation.
- CORS allows only `http://localhost:5173` and `http://localhost:3000` by default.

This is a working localhost demo, not a production-ready system. See [Known issues](#known-issues).

---

## Tech stack

| Layer | Technology |
|---|---|
| Backend | .NET 10, ASP.NET Core Web API (controllers), clean architecture: Api / Application / Domain / Infrastructure |
| Database | PostgreSQL via EF Core 10 (Npgsql) |
| AI | OpenRouter chat completions API (OpenAI-compatible) with native tool calling. Default model `nvidia/nemotron-3-ultra-550b-a55b:free` |
| MCP | Official [`ModelContextProtocol.AspNetCore`](https://www.nuget.org/packages/ModelContextProtocol.AspNetCore) SDK: server at `/mcp` (stateless Streamable HTTP) and the chat host's client |
| Validation | FluentValidation |
| Logging | Serilog (console), with an `X-Correlation-ID` header on every request |
| API docs | Swagger (Swashbuckle), Development environment only |
| Frontend | React 18, TypeScript, Vite 5 |
| Routing | React Router 7 (`react-router-dom` ^7) |
| HTTP client | Axios |

---

## Prerequisites

- .NET 10 SDK
- Node.js 18 or later
- PostgreSQL 14 or later on `localhost:5432`, with a database you can use (for example `employee_management_dev`)
- An OpenRouter API key

---

## Setup

### 1. Configure secrets (user-secrets)

The API project already has a `UserSecretsId`. Run these from the repository root and replace the placeholders
with your own values:

```powershell
cd EmployeeManagement/src/EmployeeManagement.Api

dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=<db-name>;Username=<user>;Password=<password>"
dotnet user-secrets set "AI:OpenRouter:ApiKey" "<your-openrouter-api-key>"
```

User-secrets are loaded only in the `Development` environment (the default for `dotnet run` with the `http`
launch profile). In any other environment, use environment variables instead, for example
`ConnectionStrings__DefaultConnection` and `AI__OpenRouter__ApiKey`.

Never put real values in `appsettings.json`, `appsettings.Development.json`, scripts or docs.

### 2. AI settings

Non-secret AI settings are in `EmployeeManagement/src/EmployeeManagement.Api/appsettings.json` under `AI:OpenRouter`:

| Setting | Default | Notes |
|---|---|---|
| `Model` | `nvidia/nemotron-3-ultra-550b-a55b:free` | Model used for every request unless an allowed `preferredModel` is sent |
| `AllowedModels` | `["nvidia/nemotron-3-ultra-550b-a55b:free"]` | Models a client may request with `preferredModel` |
| `BaseUrl` | `https://openrouter.ai/api/v1` | Must be an absolute http(s) URI |
| `TimeoutSeconds` | `120` | Per-request HTTP timeout. Must be between 1 and 300 |
| `MaxOutputTokens`, `Temperature`, `TopP` | `2048`, `0.2`, `0.95` | Sampling settings |
| `EnableDetailedLogging` | `false` (`true` in Development) | Extra debug-level provider logs |

The API validates these settings at startup and exits with code 1 if they are invalid or the API key is missing.

### 3. Database

On startup the API applies EF Core migrations to the configured database (`Database.Migrate()`).
See [Known issues](#known-issues) about migrating a brand-new, empty database.

### 4. Run

The easiest way is the start script from the repository root:

```powershell
./start-dev.ps1          # asks before stopping anything already listening on ports 5000/5173
./start-dev.ps1 -Force   # stops whatever listens on ports 5000/5173 without asking
```

The script only stops processes that are listening on the API and frontend ports. It does not stop other
`dotnet` or `node` processes.

Or run each part yourself:

```powershell
# Backend
dotnet run --project EmployeeManagement/src/EmployeeManagement.Api --launch-profile http

# Frontend (second terminal)
cd EmployeeManagement-FE
npm install
npm run dev
```

| What | URL |
|---|---|
| API | http://localhost:5000 |
| Swagger UI (Development only) | http://localhost:5000/swagger |
| Health check | http://localhost:5000/health |
| Frontend | http://localhost:5173 |

To send one test message to the chat endpoint: `./test-chat-api.ps1 -Message "list all employees"`.

### 5. Build and test

```powershell
dotnet build EmployeeManagement/EmployeeManagement.slnx
dotnet test  EmployeeManagement/EmployeeManagement.slnx

cd EmployeeManagement-FE
npm run build
```

If Visual Studio is running the API and locks `bin/`, add `--artifacts-path artifacts` to the `dotnet` commands.

---

## API

All JSON uses camelCase. Enums are serialized as strings.

### Employees

| Method | Path | Description |
|---|---|---|
| GET | `/api/employees?pageNumber=1&pageSize=10&search=&department=` | Paged list, optional name/email search and department filter |
| GET | `/api/employees/{id}` | One employee |
| POST | `/api/employees` | Create an employee |
| PUT | `/api/employees/{id}` | Update an employee |
| DELETE | `/api/employees/{id}` | Delete an employee |

Create or update body:

```json
{ "fullName": "Jane Doe", "email": "jane@example.com", "department": "Engineering", "salary": 85000 }
```

Employee:

```json
{ "id": 1, "fullName": "Jane Doe", "email": "jane@example.com", "department": "Engineering",
  "salary": 85000, "createdAt": "2026-01-01T00:00:00Z", "updatedAt": "2026-01-01T00:00:00Z" }
```

List response:

```json
{ "data": [ /* employees */ ], "pageNumber": 1, "pageSize": 10, "totalCount": 42,
  "totalPages": 5, "hasNextPage": true, "hasPreviousPage": false }
```

Errors from these endpoints use one shape (validation errors fill `errors`):

```json
{ "message": "One or more validation errors occurred.", "statusCode": 400,
  "correlationId": "...", "errors": { "Email": ["..."] }, "timestamp": "..." }
```

### Chat

| Method | Path | Description |
|---|---|---|
| POST | `/api/chat` | Send the conversation and get the assistant's reply (or run a confirmed action) |
| POST | `/mcp` | MCP server endpoint (Streamable HTTP, stateless, loopback only) |

Request (the transcript is the context; send everything the previous responses returned in `newMessages`):

```json
{
  "messages": [
    { "role": "user", "content": "find jane" },
    { "role": "assistant", "content": null,
      "toolCalls": [{ "id": "c1", "name": "find_employees", "arguments": "{\"query\":\"jane\"}" }] },
    { "role": "tool", "toolCallId": "c1", "content": "{\"employees\":[{\"id\":7,\"fullName\":\"Jane Doe\"}]}" },
    { "role": "assistant", "content": "I found Jane Doe (ID 7)." },
    { "role": "user", "content": "give her a 10% raise" }
  ],
  "confirmAction": null,
  "preferredModel": null
}
```

- `messages` must end with a non-empty user message. Only the roles `user`, `assistant` and `tool` are accepted.
  The server keeps the last 40 messages / 60,000 characters and the request body is limited to 512 KB.
- `confirmAction` is the `pendingAction.token` of a previous reply. When set, the action runs without the model.
- `preferredModel` is optional. A value that is not in `AllowedModels` returns `400`.

Response:

```json
{
  "responseText": "✅ Jane Doe was updated (salary: 80000 → 88000).",
  "type": "SUCCESS",
  "newMessages": [ /* append these to your transcript */ ],
  "toolActivity": [{ "tool": "get_employee", "isError": false }, { "tool": "update_employee", "isError": false }],
  "data": { "employee": { }, "changes": [{ "field": "salary", "from": "80000", "to": "88000" }] },
  "dataChanged": true,
  "pendingAction": null,
  "providerUsed": "OpenRouter",
  "modelUsed": "nvidia/nemotron-3-ultra-550b-a55b:free"
}
```

`type` is `SUCCESS`, `ERROR`, `CLARIFICATION`, `INFO` or `CONFIRMATION`. `dataChanged` is true when a tool changed
employee data (the UI uses it to refresh the list). For `CONFIRMATION`, `pendingAction` holds
`{ token, tool, summary, expiresAtUtc }`.

### Chat examples

| You type | What happens |
|---|---|
| "Add Yusuf, yusuf@example.com, IT, 50000" | The model calls `create_employee` |
| "Add Yusuf" | The model asks for the missing fields (no tool call) |
| "Show all employees" / "Who works in IT?" | `list_employees` (filters are case-insensitive) |
| "Give her a 10% raise" | The model reads the salary from earlier results (or `get_employee`), then calls `update_employee` |
| "What is the average salary in IT?" | `get_salary_stats` |
| "Delete Yusuf" | Confirm / Cancel buttons appear; nothing is deleted until Confirm is pressed |
| Paste a Markdown or CSV table | The model maps the columns and calls `bulk_create_employees`; each row reports success or its error |

---

## Project structure

```
EComm-MCP/
├── EmployeeManagement/                    # .NET backend (EmployeeManagement.slnx)
│   └── src/
│       ├── EmployeeManagement.Api/            # Controllers, middleware, Program.cs, Mcp/ (MCP tools, MCP client, confirmation tokens)
│       ├── EmployeeManagement.Application/    # Services, DTOs, validators, ChatAgentService, AI provider (OpenRouter)
│       ├── EmployeeManagement.Domain/         # Employee entity
│       ├── EmployeeManagement.Infrastructure/ # EF Core DbContext, migrations, repository
│       └── EmployeeManagement.Tests/          # xUnit: agent loop, MCP tools, in-memory API + MCP integration tests
├── EmployeeManagement-FE/                 # React + TypeScript + Vite frontend
│   └── src/  (api/, components/, hooks/, pages/, types/)
├── GAP_ANALYSIS.md                        # Analysis of the pre-MCP implementation (see its status note)
├── start-dev.ps1                          # Starts API + frontend for local development
└── test-chat-api.ps1                      # Sends one chat message to POST /api/chat
```

---

## Tests

```powershell
dotnet test EmployeeManagement/EmployeeManagement.slnx --artifacts-path artifacts   # unit + in-memory integration, no DB, no LLM
cd EmployeeManagement-FE; npm test
```

The backend integration tests host the real API, MCP server and MCP client in memory with a scripted model.
An optional live check calls the real model (with in-memory data, never the dev database):

```powershell
$env:EM_OPENROUTER_KEY = '<key>'; dotnet test EmployeeManagement/EmployeeManagement.slnx --filter LiveLlmSmokeTests
```

---

## Known issues

- **No authentication** (see [Security status](#security-status)). `/mcp` and `/api/chat` can create and delete data.
- **Fresh database migrations:** the current migration chain may not apply cleanly to a brand-new, empty database.
- **Model quality:** tool choice depends on the configured model. The default free model works for the flows above,
  but a stronger model is more reliable for long or ambiguous requests (change `AI:OpenRouter:Model`).
- **Mcp:Endpoint** must match the port the API listens on (default `http://localhost:5000/mcp`).
- No rate limiting on the chat endpoint yet, so each message can spend LLM credits.
- Concurrent edits of the same employee are not detected (no version/ETag yet).
