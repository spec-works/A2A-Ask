# A2A Protocol Quick Reference

Quick reference for the [A2A (Agent-to-Agent) protocol](https://a2a-protocol.org/latest/specification/) — the key concepts an LLM agent needs when using `a2a-ask`.

## What is A2A?

A2A is an open protocol that lets AI agents communicate with each other over HTTP. One agent (the **client**) sends messages to another agent (the **server**) and receives structured responses. The protocol handles task lifecycle, streaming, multi-turn conversations, and authentication.

## Agent Card

Every A2A agent publishes an **Agent Card** at `/.well-known/agent-card.json` describing its capabilities:

```json
{
  "name": "My Agent",
  "description": "Does useful things",
  "version": "1.0.0",
  "protocolVersion": "1.0",
  "capabilities": {
    "streaming": true,
    "pushNotifications": false,
    "stateTransitionHistory": true
  },
  "skills": [
    {
      "id": "data-analysis",
      "name": "Data Analysis",
      "description": "Analyzes datasets and produces reports",
      "tags": ["analysis", "data"],
      "examples": ["Analyze my sales data"]
    }
  ],
  "securitySchemes": { ... },
  "security": [ ... ],
  "defaultInputModes": ["text/plain", "application/json"],
  "defaultOutputModes": ["text/plain", "application/json"]
}
```

## Task Lifecycle

```
  ┌──────────┐
  │ Submitted │ ← Message received
  └────┬─────┘
       │
  ┌────▼─────┐
  │  Working  │ ← Agent is processing
  └────┬─────┘
       │
  ┌────▼──────────────┐
  │ Terminal State:    │
  │  ✅ Completed      │
  │  ❌ Failed         │
  │  🚫 Canceled       │
  │  ⛔ Rejected       │
  └───────────────────┘
       │
  OR   │
       │
  ┌────▼──────────────┐
  │ Interrupted State: │
  │  ⏸ InputRequired   │ → Send more input with --task-id
  │  🔐 AuthRequired   │ → Authenticate, then resume
  └────────────────────┘
```

## Message Structure

Messages contain **Parts** — the content payload:

| Part Type | Description | Example |
|-----------|-------------|---------|
| `TextPart` | Plain text or markdown | `--message "Hello"` |
| `FilePart` | Binary file with MIME type | `--file report.pdf` |
| `DataPart` | Structured JSON data | `--data '{"key": "value"}'` |

Messages have a **role**: `user` (from client) or `agent` (from server).

## Streaming Events

When using `a2a-ask stream`, events arrive as Server-Sent Events (SSE):

| Event Type | Description |
|-----------|-------------|
| `TaskStatusUpdateEvent` | Task state changed (working, completed, etc.) with optional status message |
| `TaskArtifactUpdateEvent` | New artifact chunk (text, file, or data) — may arrive in multiple chunks |
| `AgentTask` | Full task snapshot |
| `AgentMessage` | Direct message response (no task wrapper) |

## Security Schemes

Agent cards declare required authentication via `securitySchemes`:

| Scheme | Description | a2a-ask Usage |
|--------|-------------|---------------|
| `HttpAuthSecurityScheme` | HTTP Bearer token | `--auth-token <jwt>` |
| `ApiKeySecurityScheme` | API key in header | `--api-key <key> --api-key-header <name>` |
| `OAuth2SecurityScheme` | OAuth 2.0 flows | `a2a-ask auth login <url>` |
| `OpenIdConnectSecurityScheme` | OIDC with discovery URL | `--auth-token <token>` |
| `MutualTlsSecurityScheme` | Client certificate | Not yet supported in CLI |

## Key Protocol Details

- **Task IDs**: Server-assigned unique identifiers. Use `--task-id` to continue a conversation.
- **Context IDs**: Group related tasks. Use `--context-id` to maintain context across separate tasks.
- **Message IDs**: Client-assigned per message. Auto-generated if not specified.
- **Artifacts**: Named output attachments (files, text, data) produced by the agent during task execution.
- **Blocking mode**: By default, `send` waits for the task to reach a terminal or interrupted state. Use `--return-immediately` for async.

## Common Patterns

### Fire-and-forget
```bash
a2a-ask send <url> -m "Do the thing" --return-immediately
```

### Stream with real-time display
```bash
a2a-ask stream <url> -m "Generate report" --output text
```

### Poll a running task
```bash
a2a-ask task get <url> --task-id <id> --output json
```

### Continue a multi-turn conversation
```bash
a2a-ask send <url> --task-id <id> -m "Here's the info you asked for"
```

### Cancel a running task
```bash
a2a-ask task cancel <url> --task-id <id>
```

## Links

- [A2A Specification](https://a2a-protocol.org/latest/specification/)
- [A2A .NET SDK](https://github.com/a2aproject/a2a-dotnet)
- [A2A-Ask Repository](https://github.com/spec-works/A2A-Ask)
