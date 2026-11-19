# 🚀 Quick Start Guide - Gemini AI Chatbot

## ⚡ 5-Minute Setup

### 1️⃣ Get Your Gemini API Key

Visit: **https://makersuite.google.com/app/apikey**

### 2️⃣ Configure API Key

Edit `EmployeeManagement/src/EmployeeManagement.Api/appsettings.json`:

```json
{
  "Gemini": {
    "ApiKey": "PASTE_YOUR_KEY_HERE"
  }
}
```

### 3️⃣ Start Everything

```powershell
.\start-gemini-dev.ps1
```

**That's it!** 🎉

---

## 💬 Try These Commands

Once the app is running, click the chat button (💬) and try:

| What You Want | What To Say |
|---------------|-------------|
| **Create** | "Add employee John Doe, email john@test.com, Engineering, salary $85000" |
| **List All** | "Show me all employees" |
| **Search** | "Who works in Engineering?" |
| **Find One** | "Find John Doe" |
| **Update** | "Update John's salary to $90000" |
| **Delete** | "Delete John Doe" → "confirm" |

---

## 🌐 URLs

- **App:** http://localhost:5173
- **API:** http://localhost:5065
- **Swagger:** http://localhost:5065/swagger

---

## 🆘 Common Issues

### "API Key not configured"
➡️ Edit `appsettings.json` and add your Gemini API key

### "PostgreSQL connection failed"
➡️ Make sure PostgreSQL is running on `localhost:5432`

### Build errors
➡️ Run: `dotnet restore` then `dotnet build`

---

## 📚 Full Documentation

See **README_GEMINI_AI.md** for complete details

---

**Enjoy your AI-powered employee management! 🤖**
