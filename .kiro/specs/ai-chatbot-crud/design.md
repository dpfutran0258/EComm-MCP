# AI Chatbot CRUD Feature - Technical Design Document

**Feature Name:** AI-Powered Chatbot for Employee Management  
**Version:** 1.0  
**Date Created:** November 4, 2026

---

## 1. Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    FRONTEND (React)                          │
│  ┌──────────────────────────────────────────────────────┐   │
│  │         Chatbot Component                             │   │
│  │  - Chat UI (message history + input)                 │   │
│  │  - Message formatting & timestamps                   │   │
│  │  - Auto-scroll & responsiveness                      │   │
│  └──────────────────────────────────────────────────────┘   │
└──────────────────────┬──────────────────────────────────────┘
                       │ HTTP/WS
                       ▼
┌─────────────────────────────────────────────────────────────┐
│            BACKEND (ASP.NET Core + MCP Server)              │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  ChatController (REST Endpoint)                      │   │
│  │  - POST /api/chat/message                            │   │
│  │  - WebSocket /ws/chat                               │   │
│  └──────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  MCP Server (Intent & Entity Extraction)            │   │
│  │  - Natural Language Processing                       │   │
│  │  - Intent Detection (CRUD operations)                │   │
│  │  - Entity Extraction (employee fields)               │   │
│  │  - Confidence Scoring                                │   │
│  └──────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  Chat Service (Business Logic)                       │   │
│  │  - Command Processing                                │   │
│  │  - Context Management                                │   │
│  │  - Error Handling & Responses                        │   │
│  └──────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  Employee Service (API Calls)                        │   │
│  │  - Delegates to existing endpoints                   │   │
│  │  - POST /api/employees (Create)                      │   │
│  │  - GET /api/employees (Read)                         │   │
│  │  - PUT /api/employees/{id} (Update)                  │   │
│  │  - DELETE /api/employees/{id} (Delete)               │   │
│  └──────────────────────────────────────────────────────┘   │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ▼
┌─────────────────────────────────────────────────────────────┐
│              PostgreSQL Database                             │
│  - employees table (existing)                               │
│  - chat_sessions table (new, optional)                      │
└─────────────────────────────────────────────────────────────┘
```

---

## 2. Component Design

### 2.1 Frontend: Chatbot UI Component

**Location:** `EmployeeManagement-FE/src/components/ChatBot.tsx`

```tsx
// ChatBot.tsx Component Structure
- ChatContainer
  ├── ChatHeader
  │   ├── Title: "Employee Assistant"
  │   └── Close Button
  ├── ChatMessages (scrollable)
  │   ├── UserMessage
  │   │   └── Message text + timestamp
  │   └── BotMessage
  │       ├── Text content
  │       ├── Formatted tables (if applicable)
  │       └── Action buttons (if needed)
  └── ChatInput
      ├── Input field
      ├── Placeholder: "Ask me to list employees, add new ones, update info..."
      ├── Send button
      └── Suggestions/examples
```

**Props:**
```typescript
interface ChatBotProps {
  isOpen: boolean
  onClose: () => void
}
```

**State Management:**
```typescript
- messages: Message[] (user + bot messages)
- isLoading: boolean (awaiting server response)
- sessionId: string (conversation session)
- context: ChatContext (previous results for follow-ups)
- inputValue: string (current message being typed)
```

**Styling:** Premium glassmorphism design consistent with app theme

---

### 2.2 Backend: Chat Controller

**Location:** `EmployeeManagement/src/EmployeeManagement.Api/Controllers/ChatController.cs`

```csharp
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    // POST /api/chat/message
    [HttpPost("message")]
    public async Task<ActionResult<ChatResponse>> SendMessage(
        [FromBody] ChatRequest request)
    {
        // 1. Receive user message
        // 2. Pass to MCP Server for intent/entity extraction
        // 3. Execute appropriate action
        // 4. Return formatted response
    }

    // WebSocket support for real-time chat
    [HttpGet("ws")]
    public async Task GetWebSocket()
    {
        // Handle WebSocket upgrade for real-time messaging
    }
}
```

**DTO Models:**
```csharp
public class ChatRequest
{
    public string Message { get; set; }
    public string SessionId { get; set; }
    public Dictionary<string, object> Context { get; set; }
}

public class ChatResponse
{
    public string ResponseText { get; set; }
    public ChatResponseType Type { get; set; } // SUCCESS, ERROR, CLARIFICATION, INFO
    public object Data { get; set; } // Employee data or lists
    public Dictionary<string, object> UpdatedContext { get; set; }
}

public enum ChatResponseType
{
    SUCCESS,      // Action completed successfully
    ERROR,        // Error occurred
    CLARIFICATION, // Need more info from user
    INFO,         // Informational response
    CONFIRMATION  // Waiting for confirmation
}
```

---

### 2.3 MCP Server: Intent & Entity Extraction

**Location:** `EmployeeManagement/src/EmployeeManagement.Infrastructure/MCP/ChatMcpServer.cs`

**Responsibilities:**
1. Natural language processing
2. Intent detection
3. Entity extraction
4. Confidence scoring
5. Validation

**Intent Types:**
```csharp
public enum ChatIntent
{
    CREATE_EMPLOYEE,
    READ_EMPLOYEE,
    READ_ALL_EMPLOYEES,
    UPDATE_EMPLOYEE,
    DELETE_EMPLOYEE,
    SEARCH_EMPLOYEES,
    CALCULATE_STATS,
    CLARIFICATION_NEEDED,
    UNKNOWN
}
```

**Extracted Entities:**
```csharp
public class ChatEntity
{
    public string FullName { get; set; }
    public string Email { get; set; }
    public string Department { get; set; }
    public decimal? Salary { get; set; }
    public int? EmployeeId { get; set; }
    public string SearchQuery { get; set; }
}
```

**Intent Analysis Logic:**
```csharp
public class ChatMcpServer
{
    // Keywords-based intent detection (rule-based initially)
    private readonly Dictionary<ChatIntent, string[]> _intentKeywords = new()
    {
        {
            ChatIntent.CREATE_EMPLOYEE,
            new[] { "add", "create", "new employee", "hire", "onboard" }
        },
        {
            ChatIntent.UPDATE_EMPLOYEE,
            new[] { "update", "modify", "change", "edit", "set" }
        },
        {
            ChatIntent.DELETE_EMPLOYEE,
            new[] { "delete", "remove", "fire", "dismiss" }
        },
        {
            ChatIntent.READ_EMPLOYEE,
            new[] { "show", "get", "find", "info", "details", "who is" }
        },
        {
            ChatIntent.READ_ALL_EMPLOYEES,
            new[] { "list", "show all", "display", "all employees" }
        },
        {
            ChatIntent.SEARCH_EMPLOYEES,
            new[] { "search", "find employees", "filter", "department" }
        },
        {
            ChatIntent.CALCULATE_STATS,
            new[] { "average", "salary", "total", "count", "how many" }
        }
    };

    public ChatIntentResult AnalyzeMessage(string message)
    {
        // 1. Convert to lowercase
        // 2. Detect intent from keywords
        // 3. Extract entities using regex/patterns
        // 4. Calculate confidence
        // 5. Return results
    }
}
```

---

### 2.4 Chat Service: Business Logic

**Location:** `EmployeeManagement/src/EmployeeManagement.Application/Services/ChatService.cs`

```csharp
public class ChatService
{
    public async Task<ChatResponse> ProcessMessage(
        string userMessage,
        string sessionId,
        Dictionary<string, object> context)
    {
        // Step 1: Analyze with MCP Server
        var analysis = _mcpServer.AnalyzeMessage(userMessage);

        // Step 2: Validate confidence
        if (analysis.Confidence < 0.6)
        {
            return ClarificationResponse("I didn't quite understand. Can you rephrase?");
        }

        // Step 3: Execute based on intent
        return analysis.Intent switch
        {
            ChatIntent.CREATE_EMPLOYEE => await HandleCreateEmployee(analysis),
            ChatIntent.READ_EMPLOYEE => await HandleReadEmployee(analysis),
            ChatIntent.READ_ALL_EMPLOYEES => await HandleReadAllEmployees(analysis),
            ChatIntent.UPDATE_EMPLOYEE => await HandleUpdateEmployee(analysis),
            ChatIntent.DELETE_EMPLOYEE => await HandleDeleteEmployee(analysis),
            ChatIntent.SEARCH_EMPLOYEES => await HandleSearchEmployees(analysis),
            ChatIntent.CALCULATE_STATS => await HandleCalculateStats(analysis, context),
            _ => ErrorResponse("I'm not sure how to help with that.")
        };
    }

    // Handlers for each intent
    private async Task<ChatResponse> HandleCreateEmployee(ChatIntentResult analysis)
    {
        // Extract all required fields
        // Validate data
        // Call EmployeeService.CreateAsync()
        // Return formatted success response
    }

    private async Task<ChatResponse> HandleUpdateEmployee(ChatIntentResult analysis)
    {
        // Find employee by name
        // Extract fields to update
        // Call EmployeeService.UpdateAsync()
        // Show before/after comparison
    }

    private async Task<ChatResponse> HandleDeleteEmployee(ChatIntentResult analysis)
    {
        // Find employee
        // Return confirmation request
        // Process only after user confirms
    }

    // ... other handlers
}
```

---

## 3. Data Flow Diagrams

### Flow 1: Create Employee via Chat
```
User Input: "Add Jane, jane@email.com, HR, 50000"
     ↓
[ChatController.SendMessage]
     ↓
[ChatMcpServer.AnalyzeMessage]
  - Intent: CREATE_EMPLOYEE (confidence: 0.98)
  - Entities: name=Jane, email=jane@email.com, dept=HR, salary=50000
     ↓
[ChatService.HandleCreateEmployee]
  - Validate entities
  - Call CreateEmployeeValidator
     ↓
[EmployeeService.CreateAsync]
  - POST /api/employees
  - DB INSERT
     ↓
[ChatResponse] → User
  "✅ Employee created: Jane (jane@email.com)"
```

### Flow 2: Search & Update via Chat
```
User Input: "Show Engineering employees"
     ↓
[ChatMcpServer] → Intent: SEARCH_EMPLOYEES, Entity: dept=Engineering
     ↓
[ChatService.HandleSearchEmployees]
  - Call EmployeeService.GetAllAsync(department=Engineering)
  - Save results in context
     ↓
[ChatResponse] → User
  Lists 3 Engineering employees
  [Context saved for next query]
     ↓
User Input: "Give Alice a 5% raise"
     ↓
[ChatMcpServer] → Intent: UPDATE_EMPLOYEE, Entity: name=Alice, raise=5%
     ↓
[ChatService.HandleUpdateEmployee]
  - Find Alice in context
  - Calculate: 72000 * 1.05 = 75600
  - Call UpdateAsync
     ↓
[ChatResponse] → User
  "✅ Updated Alice: $72,000 → $75,600"
```

---

## 4. Message Flow Examples

### Example: Successful Create
```
Frontend → Backend: { message: "Add John Smith, john@acme.com, Sales, 60000" }
         ↓
Backend → MCP Server: Analyze message
         ↓
MCP → Backend: { intent: CREATE_EMPLOYEE, entities: {...} }
         ↓
Backend → API: POST /api/employees { ... }
         ↓
API → DB: Insert employee
         ↓
Backend → Frontend: { type: SUCCESS, data: { id: 5, ... } }
         ↓
Frontend: Display "✅ Employee created: John Smith"
```

### Example: Delete with Confirmation
```
Frontend → Backend: { message: "Delete John Smith" }
         ↓
Backend → MCP: Analyze message
         ↓
MCP → Backend: { intent: DELETE_EMPLOYEE, entity: { name: "John Smith" } }
         ↓
Backend → API: GET /api/employees?search=john
         ↓
Backend → Frontend: { type: CONFIRMATION, data: { id: 5, name: "John Smith", ... } }
         ↓
Frontend: Display "Are you sure? Type 'confirm delete John Smith'"
         ↓
User Input: "confirm delete John Smith"
         ↓
Frontend → Backend: { message: "confirm delete John Smith" }
         ↓
Backend → API: DELETE /api/employees/5
         ↓
Frontend: Display "✅ Deleted: John Smith"
```

---

## 5. Session & Context Management

```csharp
public class ChatSession
{
    public string SessionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastActivity { get; set; }
    public Queue<ChatMessage> MessageHistory { get; set; } = new();
    public Dictionary<string, object> Context { get; set; } = new();
}

// Context Structure
{
    "lastSearchResults": [{ id: 1, name: "Alice" }, ...],
    "lastSearchDepartment": "Engineering",
    "currentPage": 1,
    "totalResults": 5,
    "selectedEmployee": { id: 3, name: "Carol Davis" }
}
```

---

## 6. Error Handling & Response Patterns

### Error Response
```csharp
return new ChatResponse
{
    ResponseText = "❌ I couldn't find an employee named 'Joh Smith'. Did you mean 'John Smith'?",
    Type = ChatResponseType.CLARIFICATION,
    Data = new { suggestions = new[] { "John Smith", "John Davis" } }
};
```

### Validation Error
```csharp
return new ChatResponse
{
    ResponseText = "I need more information to create the employee:\n" +
                   "- Email is required\n" +
                   "- Department is required",
    Type = ChatResponseType.CLARIFICATION
};
```

### Duplicate Email
```csharp
return new ChatResponse
{
    ResponseText = "❌ Email 'john@acme.com' is already registered to John Smith.",
    Type = ChatResponseType.ERROR,
    Data = new { existingEmployee = employee }
};
```

---

## 7. UI Integration Points

### 7.1 Floating Chat Widget
- Position: Bottom-right corner (fixed)
- Expandable/collapsible
- Badge showing new messages
- Can be minimized while working on main interface

### 7.2 Chat Dialog (Full Screen Option)
- Modal with chat history
- Takes up 40% of screen width on desktop
- Full screen on mobile
- Can be resized/dragged

### 7.3 Integration with Existing Features
- Chat results link to main list view
- "View in list" button on search results
- Sync between chat updates and main interface

---

## 8. Technology Stack

### Frontend
- React 18+
- TypeScript
- Axios (for API calls)
- CSS-in-JS or Tailwind (styling)

### Backend
- .NET 10
- ASP.NET Core
- MCP Server (local process)
- FluentValidation

### Database
- PostgreSQL (existing)
- No schema changes needed initially

### Communication
- REST API (HTTP POST for messages)
- Optional WebSocket for real-time updates

---

## 9. File Structure

```
EmployeeManagement/
├── src/
│   ├── EmployeeManagement.Api/
│   │   └── Controllers/
│   │       └── ChatController.cs (NEW)
│   │
│   ├── EmployeeManagement.Application/
│   │   ├── Services/
│   │   │   └── ChatService.cs (NEW)
│   │   ├── DTOs/
│   │   │   ├── ChatRequest.cs (NEW)
│   │   │   ├── ChatResponse.cs (NEW)
│   │   │   └── ChatEntity.cs (NEW)
│   │   └── Interfaces/
│   │       └── IChatMcpServer.cs (NEW)
│   │
│   └── EmployeeManagement.Infrastructure/
│       └── MCP/
│           └── ChatMcpServer.cs (NEW)

EmployeeManagement-FE/
├── src/
│   ├── components/
│   │   └── ChatBot.tsx (NEW)
│   │       ├── ChatContainer.tsx
│   │       ├── ChatMessages.tsx
│   │       ├── ChatInput.tsx
│   │       └── ChatBot.css (or .module.css)
│   │
│   ├── hooks/
│   │   └── useChat.ts (NEW)
│   │
│   └── services/
│       └── chatService.ts (NEW)
```

---

## 10. Testing Strategy

### Unit Tests
- MCP intent detection accuracy (>95%)
- Entity extraction correctness
- Response formatting

### Integration Tests
- Full chat message flow
- API integration (CRUD operations)
- Error handling paths

### E2E Tests
- User creates employee via chat
- User searches and updates
- User deletes with confirmation
- Context persistence across messages

---

## 11. Implementation Phases

### Phase 1: MVP (Basic Functionality)
- ✅ Chat UI component
- ✅ MCP server (rule-based intent detection)
- ✅ CREATE operation only
- ✅ Simple responses

**Effort:** 1-2 weeks

### Phase 2: Full CRUD
- ✅ READ, UPDATE, DELETE operations
- ✅ Search functionality
- ✅ Better entity extraction
- ✅ Error handling

**Effort:** 1-2 weeks

### Phase 3: Advanced Features
- ✅ Context management
- ✅ Follow-up queries
- ✅ Bulk operations
- ✅ Stats calculation

**Effort:** 1 week

### Phase 4: Optimization
- ✅ WebSocket support
- ✅ Session persistence
- ✅ Performance tuning
- ✅ Advanced NLP

**Effort:** 1 week

---

## 12. Success Criteria

✅ Chat interface responsive and intuitive  
✅ Intent detection accuracy ≥95%  
✅ All CRUD operations work via chat  
✅ Response time <2 seconds  
✅ Error messages clear and actionable  
✅ Context management working  
✅ No data corruption  
✅ Zero security issues  

---

**Design Status:** READY FOR IMPLEMENTATION ✅
