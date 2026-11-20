# Employee Management with AI chat

An employee management demo: an ASP.NET Core Web API with PostgreSQL, and a React frontend with a chat assistant
that turns natural-language requests (and pasted tables) into employee create, read, update and delete operations.
The assistant uses a large language model through [OpenRouter](https://openrouter.ai).

This README is the single source of truth for the project. Older notes live in [docs/archive](docs/archive/README.md)
and may not match the code.

> **About the name:** the repository folder is called `EComm-MCP`, but the project is neither an e-commerce
> application nor an MCP (Model Context Protocol) server. An earlier MCP-based design was removed.

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
- The AI never touches the database directly. It only proposes an intent and entities, and the backend validates
  them with FluentValidation before saving.
- CORS allows only `http://localhost:5173` and `http://localhost:3000` by default.

This is a working localhost demo, not a production-ready system. See [Known issues](#known-issues).

---

## Tech stack

| Layer | Technology |
|---|---|
| Backend | .NET 10, ASP.NET Core Web API (controllers), clean architecture: Api / Application / Domain / Infrastructure |
| Database | PostgreSQL via EF Core 10 (Npgsql) |
| AI | OpenRouter chat completions API (OpenAI-compatible). Default model `nvidia/nemotron-3-ultra-550b-a55b:free` |
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
| `MaxOutputTokens`, `Temperature`, `TopP` | `2048`, `0.7`, `0.95` | Sampling settings |
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
| POST | `/api/chat/message` | Send one user message and get the assistant's reply |

Request:

```json
{
  "message": "Add Jane Doe, jane@example.com, Engineering, 85000",
  "sessionId": "any-stable-id-per-conversation",
  "context": {},
  "preferredModel": null
}
```

- `message` and `sessionId` are required. A missing value returns `400` with `{ "error": "..." }`.
- `context` is the `updatedContext` from the previous reply. Send it back unchanged.
- `preferredModel` is optional. A value that is not in `AllowedModels` returns `400`.

Response:

```json
{
  "responseText": "Created Jane Doe (ID 12).",
  "type": "SUCCESS",
  "data": { },
  "updatedContext": { },
  "providerUsed": "OpenRouter",
  "modelUsed": "nvidia/nemotron-3-ultra-550b-a55b:free"
}
```

`type` is one of `SUCCESS`, `ERROR`, `CLARIFICATION`, `INFO`, `CONFIRMATION`. `data` depends on the operation
(an employee, a list, a bulk preview or bulk results).

### Chat examples

| You type | What happens |
|---|---|
| "Add Yusuf, yusuf@example.com, IT, 50000" | The AI extracts the fields and the backend creates the employee |
| "Add Yusuf" | The assistant asks for the missing fields |
| "Show all employees" / "Who works in IT?" | List or filtered search |
| "Delete Yusuf" | The assistant asks for confirmation before deleting |
| Paste a Markdown or CSV table | The rows are parsed and previewed, then created after confirmation |

---

## Project structure

```
EComm-MCP/
├── EmployeeManagement/                    # .NET backend (EmployeeManagement.slnx)
│   └── src/
│       ├── EmployeeManagement.Api/            # Controllers, middleware, Program.cs, appsettings
│       ├── EmployeeManagement.Application/    # Services, DTOs, validators, AI provider (OpenRouter)
│       ├── EmployeeManagement.Domain/         # Employee entity
│       └── EmployeeManagement.Infrastructure/ # EF Core DbContext, migrations, repository
├── EmployeeManagement-FE/                 # React + TypeScript + Vite frontend
│   └── src/  (api/, components/, hooks/, pages/, types/)
├── docs/archive/                          # Historical notes (not maintained)
├── start-dev.ps1                          # Starts API + frontend for local development
└── test-chat-api.ps1                      # Sends one request to POST /api/chat/message
```

---

## Known issues

These are tracked and being fixed in stages:

- **No authentication** (see [Security status](#security-status)).
- **Fresh database migrations:** the current migration chain may not apply cleanly to a brand-new, empty database.
- **Multi-turn chat flows** (delete confirmation, bulk create, update and delete) are unreliable, because the
  chat context round-trips through the client.
- **Tests:** there are no working automated tests yet. The old `EmployeeManagement.Infrastructure.Tests` project
  targets a removed component and is not part of the solution.
- No rate limiting or input size limits on the chat endpoint yet.
