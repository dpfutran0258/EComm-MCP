# Archived documentation

> ARCHIVED: historical notes, may not match current code. See [README.md](../../README.md).

These files were written during earlier iterations of the project (Gemini provider, MCP server, early setup notes).
They are kept for history only. Several describe components that no longer exist, and none of them is a reliable
setup guide. The root [README.md](../../README.md) is the single source of truth.

| File | What it was |
|---|---|
| [README_CHATBOT.md](README_CHATBOT.md) | Original chatbot write-up (MCP intent detection, before the OpenRouter-only design) |
| [README_GEMINI_AI.md](README_GEMINI_AI.md) | Google Gemini provider integration notes (Gemini support has been removed) |
| [IMPLEMENTATION_COMPLETE.md](IMPLEMENTATION_COMPLETE.md) | Gemini-era "implementation complete" summary |
| [QUICK_START_GUIDE.md](QUICK_START_GUIDE.md) | Gemini-era quick start (used the removed `start-gemini-dev.ps1`) |
| [DATABASE_SETUP_GUIDE.md](DATABASE_SETUP_GUIDE.md) | Early PostgreSQL setup notes |

The design specs under `.kiro/specs/` are also historical. `ai-chatbot-crud` is marked SUPERSEDED because its
ChatMcpServer and Gemini parts were removed.

Secrets that appeared in earlier versions of these files have been removed. Configure secrets with
`dotnet user-secrets` as described in the root README.
