# Employee Management System — AI-Powered

A production-ready employee management application with a natural-language AI chat assistant.  
Manage employees through conversation, pasted tables, follow-up messages, and bulk operations.

---

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | .NET 10 / ASP.NET Core Web API |
| Database | PostgreSQL 14+ via EF Core 10 |
| AI — Google | Google Gemini API (REST) |
| AI — OpenRouter | OpenRouter (NVIDIA Nemotron 3 Ultra, GPT-4o-mini, etc.) |
| Frontend | React 18, TypeScript, Vite |
| Routing | React Router v6 |
| HTTP client | Axios |
| Logging | Serilog (structured, with correlation IDs) |
| Validation | FluentValidation |

---

## Quick Start

### 1. Prerequisites

- .NET 10 SDK
- Node.js 18+
- PostgreSQL 14+ running on `localhost:5432`

### 2. Configure AI Keys

Edit `EmployeeManagement/src/EmployeeManagement.Api/appsettings.json`:

```json
{
  "AI": {
    "DefaultProvider": "OpenRouter",
    "OpenRouter": { "ApiKey": "sk-or-v1-..." },
    "Gemini":     { "ApiKey": "AIzaSy..." }
  }
}
```

> **Never commit real API keys.** Use environment variables or a secrets manager in production.

### 3. Start the backend

```powershell
cd EmployeeManagement
dotnet restore EmployeeManagement.sln
dotnet run --project src\EmployeeManagement.Api
# API → http://localhost:5000
# Swagger → http://localhost:5000/swagger
```

### 4. Start the frontend

```powershell
cd EmployeeManagement-FE
npm install
npm run dev
# App → http://localhost:5173
```

---

## AI Provider Selection

The default provider is set in `appsettings.json`:

```json
{ "AI": { "DefaultProvider": "OpenRouter" } }
```

You can override per-request from the frontend by sending `preferredProvider` in the chat request:

```json
{ "message": "...", "sessionId": "...", "preferredProvider": "Gemini" }
```

To add a new provider, implement `IAIProviderService` and register it in DI — no other code changes needed.

---

## Chat Examples

| User says | What happens |
|---|---|
| "Add Yusuf, yusuf@co.com, IT, 50000" | AI extracts entities → backend creates employee |
| "Add Yusuf" | AI asks for missing email, department, salary across turns |
| "Show all employees" | Returns paginated employee list |
| "Who works in IT?" | Filtered search |
| "Update his salary to 60000" | Resolves pronoun from context → updates |
| "Delete Yusuf" | Finds employee → asks for confirmation |
| "confirm" | Executes deletion |
| Paste a Markdown table | Parses rows → shows preview → creates on confirmation |

---

## Bulk Operations (Table Paste)

Paste any of these formats and the assistant handles the rest:

**Markdown table**
```
| Full Name | Email           | Department | Salary |
|-----------|-----------------|------------|--------|
| Yusuf     | y@example.com   | IT         | 50000  |
| Rahul     | r@example.com   | HR         | 45000  |
```

**CSV**
```
Full Name,Email,Department,Salary
Yusuf,y@example.com,IT,50000
```

---

## Project Structure

```
EComm-MCP/
├── EmployeeManagement/           # .NET backend
│   └── src/
│       ├── EmployeeManagement.Api/           # Controllers, Program.cs, Middleware
│       ├── EmployeeManagement.Application/   # Services, DTOs, AI abstractions
│       │   └── AI/                           # IAIProviderService, Factory, Providers
│       ├── EmployeeManagement.Domain/        # Employee entity
│       └── EmployeeManagement.Infrastructure/ # EF Core, Repositories
│
└── EmployeeManagement-FE/        # React frontend
    └── src/
        ├── api/            # Centralized API clients (employees.api.ts, chat.api.ts)
        ├── components/     # Header, ChatBot, chat/ChatMessage, chat/ChatInput, shared/
        ├── hooks/          # useChat, useToast
        ├── pages/          # EmployeeList, EmployeeForm
        └── types/          # employee.types.ts, chat.types.ts
```

---

## API Endpoints

### Employee CRUD

| Method | Path | Description |
|---|---|---|
| GET | `/api/employees` | Paginated list with optional search/department filter |
| GET | `/api/employees/{id}` | Get single employee |
| POST | `/api/employees` | Create employee |
| PUT | `/api/employees/{id}` | Update employee |
| DELETE | `/api/employees/{id}` | Delete employee |

### Chat

| Method | Path | Description |
|---|---|---|
| POST | `/api/chat/message` | Send a message, receive AI-generated response |

**Chat request:**
```json
{
  "message": "Add John Doe, john@example.com, Engineering, 85000",
  "sessionId": "my-session-id",
  "context": {},
  "preferredProvider": "OpenRouter"
}
```

---

## Security Notes

- API keys are read from configuration; never log or expose them.
- All CRUD operations are validated by the backend — the AI never executes database operations directly.
- AI-extracted entities are always validated by FluentValidation before persistence.
- CORS is restricted to `http://localhost:5173` and `http://localhost:3000` by default.

---

## Logging

The application uses Serilog with structured logging and correlation IDs.  
Every request receives an `X-Correlation-ID` header for end-to-end tracing.

Log levels follow these defaults:
- `Information` — all application events
- `Warning` — recoverable errors, retries
- `Error` — unhandled exceptions, provider failures

---

## Adding a New AI Provider

1. Implement `IAIProviderService` in `Application/AI/Providers/`
2. Add a matching options class extending `BaseProviderOptions` in `AIProviderOptions.cs`
3. Register the new provider in `Program.cs`:
   ```csharp
   builder.Services.AddHttpClient<MyNewProvider>(...);
   builder.Services.AddSingleton<IAIProviderService, MyNewProvider>();
   ```
4. Add configuration to `appsettings.json` and set `DefaultProvider` to use it.

---
