# Gap Analysis: AI Chat Intents and Frontend/Backend Integration

**Scope:** `EmployeeManagement/` (.NET 10 API) and `EmployeeManagement-FE/` (React + Vite), compared with the intended functionality in `.kiro/specs/ai-chatbot-crud/` and `README.md`.
**Method:** Static code review only. I did not run the app, the tests or the LLM. Findings marked **(verify)** rest on known framework behaviour and should be confirmed with a repro test before fixing.
**Status:** This report describes the implementation *before* the MCP rewrite. The MCP server, the tool-calling chat host, server-confirmed deletes, the context-in-request contract and the frontend refresh/confirm UI now exist (see README, "How the chat works"). Gaps G-01 to G-14, G-16 to G-18, G-20, F-01 to F-08 and F-10 are addressed by that change; G-15 (prompt injection) is reduced but not eliminated; G-19 is mitigated; F-03 (versioning) and F-04 (rate limiting) remain open, as does authentication.

---

## 1. Summary

| Area | Verdict |
|---|---|
| Plain CRUD (REST API plus the list and form pages) | Works. It has a few gaps (section 6). |
| Single-turn chat intents (create, read, list, search, update, greeting, help) | Mostly working. They depend on the LLM returning clean JSON. |
| Multi-turn chat flows (delete confirm, bulk create, bulk update, bulk delete) | **Broken.** They cannot complete as written (G-02, G-03, G-04). |
| "MCP server" | **Does not exist.** The spec and the test project assume one (G-01). |
| Chat to UI integration | **Partial.** The employee list never refreshes after a chat change (G-05). |
| Automated tests | Backend: none in the solution. Frontend: some component and API-client tests, none for the chat flow. |

The core finding is that the multi-turn chat design keeps its state in the client-echoed `context` dictionary. The backend cannot read that state back after the JSON round trip. Every confirmation flow depends on it.

---

## 2. How it works today

```
React ChatBot ──POST /api/chat/message {message, sessionId, context}──► ChatController
                                                                          │
                                         ChatService.ProcessMessageAsync  ▼
   1. history      ← ConversationService (in-memory, per sessionId)
   2. employee ctx ← first 20 employees (most recent), sent in the system prompt
   3. OpenRouterAIProvider → LLM returns JSON {intent, confidence, responseText, entities}
   4. confidence < 0.6 → CLARIFICATION, otherwise RouteIntentAsync (switch on intent)
   5. handler → EmployeeService → FluentValidation → EF Core → PostgreSQL
   6. response {responseText, type, data, updatedContext}
```

"Intent detection" is a prompt-engineered LLM call through OpenRouter. It is not an MCP server and does not use tool calling or structured output. The README states this itself: "neither an e-commerce application nor an MCP server. An earlier MCP-based design was removed."

---

## 3. Intent coverage matrix

Intents come from `ChatIntent.cs`, the system prompt in `AISystemPromptBuilder.cs`, and the router in `ChatService.RouteIntentAsync`.

| Intent | In prompt | Handler | Works end to end? | Notes |
|---|---|---|---|---|
| `GREETING` | Yes | Returns the AI text as INFO | Yes | |
| `HELP` | Yes | Static help text | Yes | The help text omits the confirm flows. |
| `CREATE_EMPLOYEE` | Yes | `HandleCreateAsync` | Yes, with caveats | G-08 (salary as a string), G-12 (missing-field text comes from the LLM). |
| `READ_EMPLOYEE` | Yes | `HandleReadAsync` | Yes | The context fallback is dead (G-02). |
| `READ_ALL_EMPLOYEES` | Yes | `HandleReadAllAsync` | Yes, capped at 100 | The AI-written text and the real data can disagree (G-13). |
| `SEARCH_EMPLOYEES` | Yes | `HandleSearchAsync` | Partly | The department filter is exact and case-sensitive. `totalCount` is wrong (G-11). |
| `UPDATE_EMPLOYEE` | Yes | `HandleUpdateAsync` | Partly | No rename, no no-op check, no before/after (G-10). |
| `DELETE_EMPLOYEE` | Yes | `HandleDeleteAsync` | **No** | Confirmation never completes (G-02). The substring check is dangerous (G-03). |
| `BULK_CREATE_EMPLOYEES` | Yes | `HandleBulkCreateAsync` | **No** | Preview works. Confirm fails (G-04). |
| `BULK_UPDATE_EMPLOYEES` | Yes | `HandleBulkUpdateAsync` | **No** | Confirm fails. "Update all in dept X" never executes (G-04). |
| `BULK_DELETE_EMPLOYEES` | Yes | `HandleBulkDeleteAsync` | **No** | The pending list is lost (G-02). |
| `CONFIRMATION` | **No** | `HandleConfirmationAsync` | **No** | The LLM is never told this intent exists (G-06). |
| `CALCULATE_STATS` | **No** | **None** | **No** | Enum value only. The spec requires it (G-07). |
| `CLARIFICATION_NEEDED` | **No** | **None** | **No** | Enum value only. It falls through to the default INFO branch. |
| `UNKNOWN` | Yes | Default branch (INFO) | Yes | |

---

## 4. Critical gaps (the feature cannot work as designed)

### G-01. No MCP server exists, but the spec, naming and tests assume one
- **Intended:** `requirements.md` F2 says "MCP server processes natural language… intent detection (CREATE, READ, UPDATE, DELETE, SEARCH, CALCULATE)… confidence scoring".
- **Actual:** There is no MCP server or MCP SDK anywhere in the code. Intent detection is a single LLM prompt in `AISystemPromptBuilder` that returns JSON.
- **Stale artifacts:**
  - `EmployeeManagement.Infrastructure.Tests/MCP/ChatMcpServerTests.cs` imports `EmployeeManagement.Infrastructure.MCP` and `ChatMcpServer`, which no longer exist. The project is not in the `.slnx`.
  - `.kiro/specs/*` and `docs/archive/*` still describe MCP and Gemini.
  - The repo folder is named `EComm-MCP`. It is not an e-commerce app.
  - `AISystemPromptBuilder` still mentions Gemini in its comments.
- **Impact:** The question "are all intents in the MCP server captured?" has no direct answer. The real intent layer is the LLM prompt, and section 3 covers it.
- **Decision needed:** Either (a) formally drop MCP and rewrite the spec and tests to match, or (b) build a real MCP tool layer (employee CRUD exposed as tools), which also fixes G-02 to G-04 and G-08.

### G-02. Chat context does not survive the client round trip (breaks delete and all bulk confirmations) **(verify)**
- **Where:** `ChatRequest.Context` is `Dictionary<string, object>`. ASP.NET Core's System.Text.Json deserializes each `object` value as a `JsonElement`. `ChatService` then tests the values with type patterns:
  - `pendingIdObj is int or long` (`ChatService.cs:491`)
  - `lastCreatedIdObj is int or long` (lines 310, 407, 565)
  - `lastViewedIdObj is int or long` (lines 421, 579)
  - `pending is List<EmployeeDto>` (line 789)
  - `pendingObj is List<BulkEmployeeData>` (lines 717, 901)
- **Effect:** On the second turn, every one of these tests is false.
  - **Single delete:** The user says "yes". `isConfirm` is true but the pending id is not recognised. The code falls through, re-resolves the employee and asks for confirmation again. This loops indefinitely.
  - **Bulk delete:** The pending list is never recognised, so deletion never runs.
  - **Bulk create and bulk update:** `ExecuteBulk…` returns "I lost track of the pending records".
  - **Pronoun fallbacks** ("update him") using `lastCreatedEmployeeId` and `lastViewedEmployeeId` are dead. Only the LLM and history can resolve them.
- The README admits this in "Known issues: multi-turn chat flows… are unreliable".
- **Related design flaw:** The client owns the state. A client can forge `pendingDeleteEmployeeId`. Once the type checks are fixed, that allows a delete with no LLM and no prior confirmation turn. Pending operations should be stored server-side, keyed by `sessionId`, with an expiry.

### G-03. Delete confirmation is a substring match, which can delete on a "no"
- **Where:** `HandleDeleteAsync` and `HandleBulkDeleteAsync` use `userMessage.Contains("confirm" | "yes" | "proceed")`.
- **Failure cases (once G-02 is fixed):**
  - "No, don't proceed" is read as a confirmation.
  - "Yesenia" or "eyes" contains "yes".
  - "I don't confirm" is read as a confirmation.
- The spec asks for an explicit pattern, "confirm delete John Smith".
- **Fix direction:** Use an exact match on a confirm phrase or an id token. A UI Confirm button that sends a structured action is better still.

### G-04. Bulk create and bulk update confirmation is logically broken, even apart from G-02
- **Bulk create:** `HandleConfirmationAsync` sets `bulkCreateConfirmed` and re-enters `HandleBulkCreateAsync`. That method re-derives the rows with `ai.Entities.BulkEmployees ?? _tableParser.TryParse(userMessage)`.
  - On the "confirm" turn, the LLM returns no `bulkEmployees`.
  - `TryParse("confirm")` returns an empty list (never null), so `??` does not help.
  - The code reports "couldn't parse the data" before it reaches `ExecuteBulkCreateAsync`.
- **Bulk update:** It has the same re-entry problem. It also supports only rows that carry a name or email.
  - "Update all employees in IT to salary 80000" (the example in the code's own message) only produces a "please confirm" prompt. It never executes.
  - A row cannot change `FullName`, because the name is used as the lookup key.
- **Bulk delete:** It is the only flow that does not re-parse, but it fails on G-02.

### G-05. The employee list never refreshes after a chat change
- **Intended:** Per the comment in `EmployeeList.tsx` ("Refetch when the chat assistant… changes employee data"), the list reloads after a chat mutation.
- **Actual:** `notifyEmployeesChanged()` (`constants/events.ts`) is defined and **never called**. The backend sends `needsRefresh: true`, and nothing on the frontend reads it. The `EmployeeList` listener exists, but nothing dispatches the event.
- **Impact:** After "Add John…" in chat, the table behind it is stale until the user reloads the page or changes a filter.
- **Fix direction:** In `useChat`, after a response where `data.needsRefresh` is true (or `successCount > 0`), call `notifyEmployeesChanged()`.

### G-06. The `CONFIRMATION` intent is never offered to the LLM
- The system prompt lists 12 intents and omits `CONFIRMATION`.
- When a user replies "yes" to "Are you sure?", the LLM must pick another intent. It may choose `DELETE_EMPLOYEE`, `UNKNOWN` or `GREETING`, and the result depends on the model.
- If its confidence is below 0.6, the request becomes a CLARIFICATION and the pending flow is lost.
- Confirmation should be handled deterministically by the backend before the LLM call whenever a pending action exists. It should not rely on intent classification.

### G-07. `CALCULATE_STATS` and aggregates are missing (spec Story 5)
- The spec requires "What's their average salary?" and "Give Alice a 10% raise".
- There is no prompt entry, no handler and no aggregation in `IEmployeeService`.
- Salary is also **excluded** from the employee context sent to the LLM (`GetRelevantContextAsync` copies Id, FullName, Email and Department only). The LLM therefore cannot compute a percentage raise, and any computed answer would be a hallucination.
- Relative updates ("10% raise", "add 5000") are not modelled in `ExtractedEntities`.

---

## 5. High and medium severity gaps

| ID | Sev | Gap | Evidence | Impact |
|---|---|---|---|---|
| G-08 | High | **Salary parsing only accepts a JSON number.** `TryGetDecimal` returns null for `"85000"`, `"$85,000"` or `"85k"`. `TryGetInt` throws on `5.0` or non-integers, and the whole response then falls back. | `OpenRouterAIProvider.cs:333-340` | Create loops on "salary is missing" when the LLM returns a string. A single bad field turns into a generic "trouble processing" fallback. |
| G-09 | High | **Client context merge never deletes keys.** The backend does `ctx.Remove("pendingDeleteEmployeeId")` and similar, but `useChat` does `{...contextRef.current, ...updatedContext}`. Removed keys stay on the client forever. | `useChat.ts` (context merge) | After G-02 is fixed, a stale `pendingDelete…` key lets a later "yes" delete an old target. It should replace the context rather than merge it. |
| G-10 | Medium | **Update gaps vs spec Story 3.** There is no "before and after" display. There is no check that any field actually changed, so an update with no changes still reports success. A new name cannot be separated from the lookup name, because `fullName` is used for both. Failures leak `ex.Message` to the user. | `HandleUpdateAsync` | Misleading success messages. Rename is impossible. |
| G-11 | Medium | **Search issues.** The department filter is `==` (exact, case-sensitive), so "it" does not match "IT". `totalCount = results.Data.Count`, so it is capped at 100 and is not the true total. A name search by `Contains` returns "John" and "Johnson". | `EmployeeRepository.cs`, `HandleSearchAsync` | Wrong or empty results. The UI claims counts that are not true. |
| G-12 | Medium | **Multi-turn create depends on the LLM.** The "missing field" message is the LLM's `responseText`. The backend only checks that fields are present. | `HandleCreateAsync` | The LLM may say "I've added John" while the backend asks for more data. The prompt requires one missing field at a time, but nothing enforces it. |
| G-13 | Medium | **LLM text and real data can disagree.** `READ_ALL`, `SEARCH` and `BULK` responses use the LLM's `responseText`, which was written before the data was fetched. | `HandleReadAllAsync`, `HandleSearchAsync` | "I found 5 employees" can be shown above 3 results. |
| G-14 | Medium | **Employee context is not "relevant".** It is only the 20 newest rows. Employees beyond that are invisible to name and pronoun resolution, and duplicate checks are only advisory. | `GetRelevantContextAsync` (the `userMessage` parameter is unused) | Wrong or missed matches once there are more than 20 employees. |
| G-15 | Medium | **Prompt injection through stored data.** Employee names and emails from the database go straight into the system prompt. A name such as "ignore previous instructions…" influences the LLM. The intent layer can reach delete. | `AISystemPromptBuilder` | Low likelihood in a demo, but bad with real data. Backend confirmation (G-02/G-03) is the only guard. |
| G-16 | Medium | **LLM parameters are wrong for structured extraction.** Temperature is 0.7. JSON-only output is requested in the prompt, not enforced with `response_format` or tool calling. `MaxOutputTokens` is 2048, so large bulk tables are likely to be truncated, which breaks the JSON and triggers the generic fallback. | `appsettings.json`, `OpenRouterAIProvider` | Flaky intent detection and failed bulk parsing. |
| G-17 | Medium | **LLM failures surface as CLARIFICATION.** A provider error returns `Confidence = 0`, which becomes CLARIFICATION with "I'm having trouble…", and `Metadata.Error` is dropped. | `ChatService` step 4, `AIResponse.Fallback` | The user cannot tell a service outage from an unclear request. |
| G-18 | Medium | **Chat history and sessions.** `ConversationService` is in-memory and never evicts sessions (the spec requires a 30-minute timeout). It is lost on restart. The "Clear conversation" button creates a new session id but never clears the old one on the server. Only plain-text assistant turns are stored, so history shows the LLM text replies while the prompt demands JSON. | `ConversationService.cs` | Memory growth and inconsistent behaviour. |
| G-19 | Medium | **Race on duplicate email.** The email-exists check is read-then-write and case-sensitive. A concurrent create hits the unique index and throws `DbUpdateException`, which is not handled and becomes a 500. In bulk create this is reported as a generic error. | `CreateEmployeeValidator`, `EmployeeRepository.CreateAsync` | 500 errors, and the same email in different case is accepted. |
| G-20 | Medium | **CORS headers are missing on error responses.** `ExceptionHandlingMiddleware` runs *before* `UseCors`. Error responses (400/404/500) written by it may lack `Access-Control-Allow-Origin`. **(verify)** | `Program.cs` middleware order | The browser may report a network or CORS failure instead of a validation message. |

---

## 6. Frontend and backend integration gaps

| ID | Sev | Gap | Detail |
|---|---|---|---|
| F-01 | High | **List does not refresh after chat changes** | See G-05. |
| F-02 | Medium | **`GET /api/employees/departments` does not exist** | `employees.api.getDepartments` calls it and silently returns `[]` on any error. The department filter suggestions never populate. |
| F-03 | Medium | **Optimistic concurrency is only half built** | The frontend reads and sends `version` on update. The backend has no `version` on `EmployeeDto` or `UpdateEmployeeDto`, so the field is ignored. Concurrent edits silently overwrite. |
| F-04 | Medium | **Rate limiting expected, none present** | `client.ts` handles 429 and `Retry-After`. The backend has no rate limiter. The README says so ("No rate limiting"). Chat calls LLM credits with no throttle. |
| F-05 | Medium | **Confirmation has no UI** | A comment says "Confirmation actions", but `ChatMessage` shows no Confirm or Cancel buttons. Users must type free text, which depends on G-03 and G-06. |
| F-06 | Low | **Wrong labels on bulk results** | `BulkResultTable` always says "created" and "Created successfully", even for bulk update and delete results. |
| F-07 | Low | **Dead or mismatched fields** | The frontend sends `preferredProvider`. The backend ignores it (the model field is `preferredModel`). `useChat.sendMessage` still has a `preferredProvider` argument. |
| F-08 | Low | **Console logging of chat content and context** | `useChat` calls `console.log` with message text and context in all builds. |
| F-09 | Low | **Error body shapes differ** | `ChatController` returns `{ error: "..." }`. All other endpoints return `ApiErrorResponse`. `client.ts` copes with both. The backend should be consistent. |
| F-10 | Low | **Timeout mismatch** | The provider retries up to 3 times with a 120 s timeout each, plus backoff (up to about 6 minutes). The frontend gives up after 120 s while the backend keeps working, and a "timed out" request can still create or delete data. |

---

## 7. Security, configuration and quality

- **No authentication or authorization.** This is documented. Chat can delete everything, and all of it is unauthenticated.
- **PII in logs.** `ChatService` logs the raw user message at Information level. Employee emails and salaries can end up in logs.
- **`ex.Message` returned to the chat user** in update, delete and bulk handlers.
- **`EnableSensitiveDataLogging`** is on in Development, together with `LogTo` at Debug.
- **Migration layout.** There are two migration folders: `Infrastructure/Migrations/` (`FixTimestamps`) and `Infrastructure/Persistence/Migrations/`. `Database.Migrate()` runs at startup. The README warns that a fresh empty database may not migrate cleanly.
- **Cancellation not propagated.** `CancellationToken` stops at the controller and `ChatService`. The `IEmployeeService` calls do not take it.
- **Unused or leftover code.** `AIResponseMetadata.ProviderSpecific`, `ExtractedEntities.AdditionalData`, `BulkOperation`, the `HealthCheckAsync` method (never called, and the `/health` endpoint does not use it), and `AIRequest.Metadata`.

---

## 8. Test coverage

- **Backend:** There are no tests in the solution. The only test project (`Infrastructure.Tests`) targets the removed `ChatMcpServer`, would not compile, and is not in `EmployeeManagement.slnx`.
- **Frontend:** Tests exist for `client.ts`, `ConfirmDialog`, `Input`, `EmployeeForm` and `EmployeeList` (with msw). There are no tests for `useChat`, `ChatBot` or `ChatMessage`.
- **Missing:** Nothing covers the multi-turn flows. A test that posts "delete X", echoes `updatedContext`, then posts "yes" through the real JSON pipeline would have caught G-02.

---

## 9. Recommended order of work

1. **Decide on MCP (G-01).** Either drop it and update the spec and tests, or build the tool layer.
2. **Move pending actions server-side (G-02, G-04, G-09).** Store pending delete and bulk operations per `sessionId` with an expiry. Handle confirmation before the LLM call. Stop echoing state in `context`.
3. **Make confirmation explicit (G-03, G-06, F-05).** Use a confirm token or UI buttons, and exact phrase matching.
4. **Wire the UI refresh (G-05).** Call `notifyEmployeesChanged()` from `useChat`.
5. **Harden LLM parsing (G-08, G-16, G-17).** Use low temperature, structured output or tool calling, tolerant number parsing, a larger token budget, and separate outage errors from clarifications.
6. **Fill missing functionality (G-07, G-10, G-11, F-02).** Add stats and relative updates, rename support, case-insensitive department search, true totals, and the departments endpoint.
7. **Fix infrastructure issues (G-19, G-20, F-03, F-04, F-10).** Handle `DbUpdateException`, reorder CORS, add concurrency and rate limiting, and align timeouts.
8. **Add tests (section 8).** Start with an integration test for the delete flow through the real JSON pipeline.

---

## 10. What I did not verify

- I did not run the API, the frontend, the test suites or a real LLM call. Section 5 findings G-16 and G-17 describe likely behaviour, not observed behaviour.
- G-02 and G-20 rely on documented System.Text.Json and ASP.NET Core middleware behaviour. A 20-line repro test should confirm them before a fix.
- I did not check the archived documents in `docs/archive/` beyond noting they are stale.
