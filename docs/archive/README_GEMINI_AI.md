> ARCHIVED: historical notes, may not match current code. See README.md.

# 🤖 Employee Management System - Google Gemini AI Integration

## ✨ Overview

This project features a complete **Google Gemini AI** powered chatbot for natural language employee management. The AI assistant allows users to manage employees through conversational interactions.

### 🎯 Key Features

- ✅ **Natural Language Processing** with Google Gemini 2.0 Flash
- ✅ **Intent Recognition** for all CRUD operations
- ✅ **Entity Extraction** for employee data
- ✅ **Conversation History** tracking
- ✅ **Smart Caching** (optional Redis support)
- ✅ **Enhanced UI** with Gemini branding
- ✅ **Real-time Chat** interface

---

## 🏗️ Architecture

```
┌─────────────────┐
│   React UI      │
│   (ChatBot)     │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  ASP.NET Core   │
│   Web API       │
└────────┬────────┘
         │
         ▼
┌─────────────────┐     ┌──────────────┐
│ GeminiService   │────▶│ Gemini API   │
│ (AI Processing) │     │ (Google)     │
└────────┬────────┘     └──────────────┘
         │
         ▼
┌─────────────────┐
│  EmployeeService│
│  (CRUD Logic)   │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│   PostgreSQL    │
│   Database      │
└─────────────────┘
```

---

## 📋 Prerequisites

### Required

- ✅ **.NET 10.0** SDK
- ✅ **Node.js 18+** and npm
- ✅ **PostgreSQL 14+**
- ✅ **Google Gemini API Key** (from [Google AI Studio](https://makersuite.google.com/app/apikey))

### Optional

- **Redis** (for response caching)

---

## 🚀 Quick Start

### 1️⃣ Configure Gemini API Key

**Backend Configuration:**

Edit `EmployeeManagement/src/EmployeeManagement.Api/appsettings.json`:

```json
{
  "Gemini": {
    "ApiKey": "YOUR_GEMINI_API_KEY_HERE",
    "Model": "gemini-2.0-flash-exp",
    "EnableCaching": false,
    "EnableConversationHistory": true,
    "MaxHistoryLength": 10
  }
}
```

**Get your API key:** [https://makersuite.google.com/app/apikey](https://makersuite.google.com/app/apikey)

### 2️⃣ Start PostgreSQL Database

```powershell
# Make sure PostgreSQL is running on localhost:5432
# Default credentials (configured in appsettings.json):
# - Database: employee_management_dev
# - Username: postgres
# - Password: root@123
```

### 3️⃣ Start Backend API

```powershell
cd EmployeeManagement
dotnet restore
dotnet build
dotnet run --project src\EmployeeManagement.Api
```

API will be available at: **http://localhost:5065**

### 4️⃣ Start Frontend

```powershell
cd EmployeeManagement-FE
npm install
npm run dev
```

Frontend will be available at: **http://localhost:5173**

### 5️⃣ Open ChatBot

1. Navigate to http://localhost:5173
2. Click the **chat button** (💬) in the bottom-right corner
3. Start chatting with the AI assistant!

---

## 💬 Example Conversations

### Creating an Employee

```
👤 You: Add a new employee named John Doe, email john@example.com, 
       department Engineering, salary $85,000

🤖 AI: ✅ Employee created successfully!

      📋 Details:
      • Name: John Doe
      • Email: john@example.com
      • Department: Engineering
      • Salary: $85,000.00
```

### Finding an Employee

```
👤 You: Find John Doe

🤖 AI: 👤 Employee Found:

      • Name: John Doe
      • Email: john@example.com
      • Department: Engineering
      • Salary: $85,000.00
```

### Listing All Employees

```
👤 You: Show me all employees

🤖 AI: 👥 Employee List (3 total):

      1. John Doe - Engineering ($85,000.00)
      2. Jane Smith - Marketing ($75,000.00)
      3. Bob Johnson - Sales ($70,000.00)
```

### Searching by Department

```
👤 You: Who works in Engineering?

🤖 AI: 🔍 Search Results (1 found):

      • John Doe - Engineering ($85,000.00)
```

### Updating an Employee

```
👤 You: Update John Doe's salary to $90,000

🤖 AI: ✅ Employee updated successfully!

      📋 Updated Details:
      • Name: John Doe
      • Email: john@example.com
      • Department: Engineering
      • Salary: $90,000.00
```

### Deleting an Employee

```
👤 You: Delete John Doe

🤖 AI: ⚠️ Are you sure you want to delete John Doe?

      Type 'confirm' to proceed.

👤 You: confirm

🤖 AI: ✅ Successfully deleted employee: John Doe
```

---

## ⚙️ Configuration Options

### Gemini Service Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `ApiKey` | *required* | Your Google Gemini API key |
| `Model` | `gemini-2.0-flash-exp` | Gemini model to use |
| `MaxOutputTokens` | `2048` | Maximum tokens in response |
| `Temperature` | `0.7` | Response randomness (0.0-1.0) |
| `TopP` | `0.95` | Top-p sampling parameter |
| `TopK` | `40` | Top-k sampling parameter |
| `EnableCaching` | `false` | Enable Redis response caching |
| `CacheExpirationMinutes` | `60` | Cache expiration time |
| `RedisConnectionString` | `null` | Redis connection (if caching enabled) |
| `EnableConversationHistory` | `true` | Track conversation context |
| `MaxHistoryLength` | `10` | Max messages to remember |
| `TimeoutSeconds` | `30` | API request timeout |
| `EnableDetailedLogging` | `true` | Detailed logging for debugging |

### Redis Caching (Optional)

To enable caching:

1. Install and run Redis
2. Update `appsettings.json`:

```json
{
  "Gemini": {
    "EnableCaching": true,
    "RedisConnectionString": "localhost:6379"
  }
}
```

---

## 📁 Project Structure

### Backend

```
EmployeeManagement/
├── src/
│   ├── EmployeeManagement.Api/
│   │   ├── Controllers/
│   │   │   ├── ChatController.cs        # Chat API endpoint
│   │   │   └── EmployeesController.cs   # Employee CRUD API
│   │   ├── Program.cs                   # Startup configuration
│   │   └── appsettings.json            # Gemini API settings
│   │
│   ├── EmployeeManagement.Application/
│   │   ├── Services/
│   │   │   ├── AI/
│   │   │   │   ├── IGeminiService.cs           # Gemini interface
│   │   │   │   ├── GeminiService.cs            # Gemini implementation
│   │   │   │   └── GeminiConfiguration.cs      # Configuration model
│   │   │   ├── ChatService.cs                  # Chat orchestration
│   │   │   └── EmployeeService.cs              # Employee business logic
│   │   └── DTOs/
│   │       ├── ChatIntent.cs                   # Intent enumeration
│   │       ├── ChatResponse.cs                 # Chat response model
│   │       └── ...
│   │
│   ├── EmployeeManagement.Domain/              # Domain entities
│   └── EmployeeManagement.Infrastructure/      # Data access
```

### Frontend

```
EmployeeManagement-FE/
├── src/
│   ├── components/
│   │   ├── ChatBot.tsx              # Main chatbot component
│   │   ├── ChatBot.css              # Chatbot styles (w/ Gemini branding)
│   │   └── ...
│   ├── hooks/
│   │   └── useChat.ts               # Chat state management
│   └── services/
│       └── chatService.ts           # API communication
```

---

## 🧪 Testing the API

### Using PowerShell

```powershell
# Test chat endpoint
Invoke-RestMethod -Uri "http://localhost:5065/api/chat" `
  -Method POST `
  -ContentType "application/json" `
  -Body (@{
    message = "Add employee named John Doe"
    sessionId = "test-session"
    context = @{}
  } | ConvertTo-Json)
```

### Using curl

```bash
curl -X POST http://localhost:5065/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "Show all employees",
    "sessionId": "test-session",
    "context": {}
  }'
```

### Using Swagger

Navigate to: **http://localhost:5065/swagger**

---

## 🔧 Troubleshooting

### Issue: "Gemini API Key not configured"

**Solution:** Make sure you've added your API key to `appsettings.json`:

```json
{
  "Gemini": {
    "ApiKey": "YOUR_ACTUAL_API_KEY_HERE"
  }
}
```

### Issue: Build Errors

**Solution:** Restore NuGet packages:

```powershell
cd EmployeeManagement
dotnet restore
dotnet build
```

### Issue: PostgreSQL Connection Failed

**Solution:** Verify PostgreSQL is running and credentials match:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=employee_management_dev;Username=postgres;Password=<your-password>"
  }
}
```

### Issue: CORS Errors

**Solution:** The API is configured with CORS enabled for all origins. Check that:
- Backend is running on port 5065
- Frontend is configured to call `http://localhost:5065`

### Issue: AI Responses are Slow

**Solutions:**
1. Check your internet connection
2. Enable Redis caching
3. Reduce `MaxOutputTokens` in config
4. Use a faster Gemini model

---

## 🎨 UI Customization

The ChatBot UI features **Gemini AI branding** with:

- ✨ Animated AI badge with Gemini logo
- 💎 Glassmorphism design
- 🎨 Purple gradient accents
- ⚡ Smooth animations
- 📱 Responsive layout

Customize colors in `ChatBot.css`:

```css
/* Gemini brand colors */
.chatbot-ai-badge {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
}
```

---

## 📊 API Endpoints

### Chat Endpoint

**POST** `/api/chat`

Request:
```json
{
  "message": "Show all employees",
  "sessionId": "unique-session-id",
  "context": {}
}
```

Response:
```json
{
  "responseText": "👥 Employee List (3 total):\n\n1. John Doe - Engineering",
  "type": "SUCCESS",
  "data": {
    "employees": [...],
    "totalCount": 3
  }
}
```

### Employee Endpoints

- **GET** `/api/employees` - List all employees
- **GET** `/api/employees/{id}` - Get employee by ID
- **POST** `/api/employees` - Create employee
- **PUT** `/api/employees/{id}` - Update employee
- **DELETE** `/api/employees/{id}` - Delete employee

---

## 🔐 Security Notes

⚠️ **Important:**

- Never commit your Gemini API key to version control
- Use environment variables for production deployments
- Implement rate limiting for the chat endpoint
- Add authentication/authorization before deploying

---

## 📚 Additional Resources

- **Google Gemini API Docs:** https://ai.google.dev/docs
- **Get API Key:** https://makersuite.google.com/app/apikey
- **Gemini Models:** https://ai.google.dev/models/gemini

---

## 📝 License

This project is for demonstration purposes. Ensure compliance with Google's Gemini API terms of service.

---

## 🤝 Support

For issues or questions:

1. Check the troubleshooting section above
2. Review the Google Gemini API documentation
3. Verify your API key is valid and has quota remaining

---

**🎉 Enjoy your AI-powered Employee Management System!**
