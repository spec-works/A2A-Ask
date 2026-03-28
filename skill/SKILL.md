---
name: a2a-ask-cli
description: >
  Interact with remote A2A (Agent-to-Agent) protocol agents via the a2a-ask CLI tool.
  Use this skill when the user wants to discover, communicate with, or manage tasks on
  remote AI agents that implement the A2A protocol. Activate when the user mentions A2A agents,
  agent cards, agent-to-agent communication, or wants to send messages to remote agents.
  Also useful for multi-turn agent conversations, streaming agent responses, and task management.
license: MIT
compatibility: Requires .NET 10.0 or later SDK. Works on Windows, macOS, and Linux.
metadata:
  author: spec-works
  version: "1.0"
  repository: https://github.com/spec-works/A2A-Ask
---

# A2A-Ask CLI — Talk to A2A Agents from the Command Line

A2A-Ask (`a2a-ask`) is a CLI tool that lets you interact with any remote agent implementing the [A2A (Agent-to-Agent) protocol](https://a2a-protocol.org/latest/specification/). It handles agent discovery, message sending, streaming responses, multi-turn conversations, task management, and authentication — all from the terminal.

**A2A is an open protocol** that enables AI agents to communicate with each other. Any agent that publishes an agent card and implements the A2A JSON-RPC API can be called with this tool. The CLI supports both A2A v1.0 and v0.3 agents transparently.

## Prerequisites — Installing .NET

The a2a-ask CLI requires .NET 10.0 or later.

### Windows

```powershell
winget install Microsoft.DotNet.SDK.10
```

### macOS

```bash
brew install dotnet-sdk
```

### Linux (Ubuntu/Debian)

```bash
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb && rm packages-microsoft-prod.deb
sudo apt-get update && sudo apt-get install -y dotnet-sdk-10.0
```

### Verify Installation

```bash
dotnet --version
# Should show 10.x.x or later
```

## Installing the CLI Tool

```bash
dotnet tool install --global SpecWorks.A2A-Ask
```

Verify it works:

```bash
a2a-ask version
```

## Workflow — How to Interact with an A2A Agent

**Always follow this sequence when interacting with an A2A agent:**

### Step 1: Discover the Agent

Before sending any messages, discover the agent to understand its capabilities, skills, and security requirements.

```bash
a2a-ask discover <agent-url> --output text
```

The `<agent-url>` is the base URL of the agent endpoint. The CLI will automatically locate the agent card by:
1. Trying a GET on the URL itself (some agents return their card at their endpoint URL)
2. Falling back to `<agent-url>/.well-known/agent-card.json`

**Example:**

```bash
a2a-ask discover https://example.com/agents/my-agent --output text
```

**What to look for in the response:**
- **Name and description** — what the agent does
- **Skills** — specific capabilities the agent offers (each has an id, name, and description)
- **Capabilities** — whether streaming is supported
- **Security schemes** — what authentication is required (see Authentication section)
- **Supported interfaces** — protocol versions and bindings

If the agent card shows **no security requirements**, you can proceed directly to sending messages. If it does require auth, see the Authentication section below before proceeding.

### Step 2: Send a Message

For a simple one-shot interaction:

```bash
a2a-ask send <agent-url> -m "Your question or request here"
```

**For agents that support streaming** (check capabilities in the agent card):

```bash
a2a-ask stream <agent-url> -m "Your question or request here"
```

Streaming shows real-time progress updates and partial results as the agent works. Prefer streaming when available — it gives better visibility into what the agent is doing.

### Step 3: Handle the Response

The response will be one of these task states:

| State | Meaning | Action |
|-------|---------|--------|
| `completed` | Agent finished successfully | Read the result from the response artifacts/messages |
| `failed` | Agent encountered an error | Read the error message, adjust your request |
| `input-required` | Agent needs more information | Read the agent's question, then send a follow-up (see Multi-Turn below) |
| `auth-required` | Agent needs authentication | Obtain credentials and retry with auth options (see Authentication below) |
| `working` | Agent is still processing | Poll for status (see Polling below) |
| `canceled` | Task was canceled | No further action needed |

### Step 4: Multi-Turn Conversations

When the agent returns `input-required`, it is asking a follow-up question. The response will contain the agent's question in its message content.

**Send a follow-up using the task ID from the previous response:**

```bash
a2a-ask send <agent-url> -m "Your answer to the agent's question" --task-id <task-id-from-response>
```

You can continue this conversation loop as many times as needed. Always pass the `--task-id` to maintain conversation context.

**To group related but separate tasks into a logical session, use `--context-id`:**

```bash
a2a-ask send <agent-url> -m "First request" --context-id my-session-123
a2a-ask send <agent-url> -m "Related request" --context-id my-session-123
```

## CLI Commands Reference

### `a2a-ask discover <url>`

Fetch and display an A2A agent card.

```bash
a2a-ask discover <url> [options]
```

| Option | Description | Default |
|--------|-------------|---------|
| `--well-known` | Append `/.well-known/agent-card.json` to the URL | `true` |
| `--extended` | Fetch the extended (authenticated) agent card | `false` |
| `--auth-token <token>` | Bearer token for authenticated card fetch | — |
| `--auth-header <key=value>` | Custom auth header | — |
| `--output <json\|text>` | Output format | `json` |
| `--pretty` | Pretty-print JSON | `false` |
| `-v, --verbose` | Verbose output | `false` |

### `a2a-ask send <url>`

Send a message to an A2A agent and wait for the response.

```bash
a2a-ask send <url> -m "message" [options]
```

| Option | Alias | Description | Default |
|--------|-------|-------------|---------|
| `--message` | `-m` | Message text to send | — |
| `--file` | `-f` | File path to include as a message part | — |
| `--data` | `-d` | Structured JSON data to include | — |
| `--task-id` | `-t` | Continue an existing task (multi-turn) | — |
| `--context-id` | `-c` | Context ID for grouping interactions | — |
| `--message-id` | | Custom message ID | auto UUID |
| `--accept` | | Accepted output modes (comma-separated) | — |
| `--return-immediately` | | Don't wait for completion | `false` |
| `--history-length` | | Max history messages in response | — |
| `--save-artifacts` | | Directory to save file artifacts | — |
| `--auth-token` | | Bearer token | — |
| `--auth-header` | | Custom auth header (key=value) | — |
| `--auth-user` | | Username for HTTP Basic auth | — |
| `--auth-password` | | Password for HTTP Basic auth | — |
| `--api-key` | | API key value | — |
| `--api-key-header` | | API key header name | from card |
| `--api-key-location` | | API key location: header, query, or cookie | `header` |
| `--client-id` | | OAuth2 client ID (for client_credentials) | — |
| `--client-secret` | | OAuth2 client secret (for client_credentials) | — |
| `--tenant` | | Tenant ID (separates token storage per tenant) | — |
| `--output` | | Output format (json/text) | `json` |
| `--pretty` | | Pretty-print JSON | `false` |
| `-v, --verbose` | | Verbose output | `false` |

At least one of `--message`, `--file`, or `--data` is required.

### `a2a-ask stream <url>`

Send a message with streaming response, showing real-time progress.

```bash
a2a-ask stream <url> -m "message" [options]
```

Same options as `send`, plus:

| Option | Description | Default |
|--------|-------------|---------|
| `--subscribe` | Subscribe to an existing task's events (requires `--task-id`) | `false` |

### `a2a-ask task get <url>`

Get the current state of a task (for polling).

```bash
a2a-ask task get <url> --task-id <id> [options]
```

| Option | Description |
|--------|-------------|
| `--task-id` (required) | Task ID to query |
| `--history-length` | Max history messages |
| Auth options | Same as `send` |

### `a2a-ask task list <url>`

List tasks with optional filtering.

```bash
a2a-ask task list <url> [options]
```

| Option | Description |
|--------|-------------|
| `--context-id` | Filter by context ID |
| `--status` | Filter by task state |
| `--page-size` | Results per page (default: 50) |
| `--page-token` | Pagination cursor |
| Auth options | Same as `send` |

### `a2a-ask task cancel <url>`

Cancel a running task.

```bash
a2a-ask task cancel <url> --task-id <id> [options]
```

### `a2a-ask auth login <url>`

Interactively authenticate with an A2A agent using OAuth2 device code flow or client credentials.

```bash
a2a-ask auth login <url> [--client-id <id> --client-secret <secret>] [--tenant <id>]
```

| Option | Description |
|--------|-------------|
| `--client-id` | OAuth2 client ID (triggers client_credentials flow) |
| `--client-secret` | OAuth2 client secret |
| `--tenant` | Tenant ID (separates token storage per tenant) |

**Without** `--client-id`: Runs interactive device code flow (displays a code and URL for the user to visit).
**With** `--client-id` and `--client-secret`: Runs non-interactive client_credentials flow (for service-to-service auth).

The obtained token is stored for reuse. Stored tokens are automatically used by subsequent commands and auto-refreshed when expired (if a refresh token is available). On Windows, tokens are encrypted using DPAPI.

### `a2a-ask auth logout <url>`

Remove the stored authentication token for an agent.

```bash
a2a-ask auth logout <url> [--tenant <id>]
```

### `a2a-ask auth status <url>`

Show authentication status for an agent — token validity, expiry, and whether a refresh token is available.

```bash
a2a-ask auth status <url> [--tenant <id>]
```

### `a2a-ask version`

Display version information.

## Authentication

### Checking Security Requirements

When you discover an agent, look for security schemes in the output. If the agent has no security requirements, you can interact freely. Otherwise:

**No auth required:**
```
Security: None required
```
→ Proceed directly with `send` or `stream`.

**API Key required:**
```
Security Schemes:
  "api_key": API Key (header: X-API-Key)
```
→ Ask the user for the API key, then:
```bash
a2a-ask send <url> -m "message" --api-key "user-provided-key" --api-key-header "X-API-Key"
```

**Bearer token required:**
```
Security Schemes:
  "bearer_auth": HTTP Bearer
```
→ Ask the user for their bearer token, then:
```bash
a2a-ask send <url> -m "message" --auth-token "user-provided-token"
```

**OAuth2 required:**
```
Security Schemes:
  "oauth2": OAuth2 (device code flow available)
```
→ For interactive users, run the device code login:
```bash
a2a-ask auth login <url>
```
This will display a device code and verification URL. Ask the user to visit the URL and enter the code. Once authenticated, the token is stored and subsequent commands will use it automatically.

→ For service-to-service auth (client credentials), ask the user for their client ID and secret:
```bash
a2a-ask auth login <url> --client-id "my-app" --client-secret "secret"
```

**HTTP Basic auth required:**
```
Security Schemes:
  "basic_auth": HTTP Basic
```
→ Ask the user for their username and password:
```bash
a2a-ask send <url> -m "message" --auth-user "username" --auth-password "password"
```

**Custom headers:**
For non-standard auth mechanisms, use:
```bash
a2a-ask send <url> -m "message" --auth-header "X-Custom-Auth=secret-value"
```

### Auth Decision Tree

Follow this logic when an agent requires authentication:

1. Check the agent card's security schemes (from `discover` output)
2. If **API Key** → ask the user for the key → pass via `--api-key` (with `--api-key-location` if not header)
3. If **HTTP Bearer** → ask the user for a token → pass via `--auth-token`
4. If **HTTP Basic** → ask the user for credentials → pass via `--auth-user` + `--auth-password`
5. If **OAuth2 with device code** → run `a2a-ask auth login <url>` → interactive flow
6. If **OAuth2 with client credentials** → ask for client ID/secret → `a2a-ask auth login <url> --client-id <id> --client-secret <secret>`
7. If **OpenID Connect** → extract the issuer URL, guide user to obtain a token, pass via `--auth-token`
8. If **auth-required** state returned mid-task → obtain credentials and retry the same task with `--task-id` and auth options

## Streaming vs Polling

### When to Use Streaming

If the agent card's capabilities indicate streaming is supported, prefer `a2a-ask stream` over `a2a-ask send`. Streaming provides:
- Real-time status updates as the agent works
- Partial results as they become available
- Better user experience for long-running tasks

```bash
a2a-ask stream <url> -m "Analyze this data" --output text
```

In text mode, streaming renders status updates like:
```
[working] Analyzing data...
[working] Processing results...
[completed] Done!
```

### When to Use Polling

If the agent doesn't support streaming, or you used `send` with `--return-immediately`:

1. Send the initial message:
```bash
a2a-ask send <url> -m "Long running task" --return-immediately
```

2. Note the `task-id` from the response.

3. Poll for status (start at 2-second intervals, back off to 5 seconds):
```bash
a2a-ask task get <url> --task-id <id>
```

4. Continue polling until the state is terminal: `completed`, `failed`, `canceled`, or `input-required`.

### Subscribing to Task Events

If you already have a task ID and want to watch for updates via streaming:

```bash
a2a-ask stream <url> --task-id <task-id> --subscribe
```

## Working with Files and Data

### Sending Files

Include a file with your message:

```bash
a2a-ask send <url> -m "Summarize this document" --file ./report.pdf
```

### Sending Structured Data

Send JSON data as a message part:

```bash
a2a-ask send <url> -d '{"key": "value", "items": [1, 2, 3]}'
```

### Saving Artifact Files

When the agent returns file artifacts (images, documents, etc.), save them to disk:

```bash
a2a-ask send <url> -m "Generate a chart" --save-artifacts ./output/
```

## Common Patterns

### Simple Question → Answer

```bash
a2a-ask discover https://agent.example.com/weather --output text
a2a-ask send https://agent.example.com/weather -m "What's the weather in Seattle?"
```

### Streaming Conversation with Follow-ups

```bash
# Initial request
a2a-ask stream https://agent.example.com/assistant -m "Help me plan a trip to Japan"

# Agent responds with questions (input-required state) — note the task-id
# Send follow-up
a2a-ask stream https://agent.example.com/assistant -m "2 weeks in April, interested in history and food" --task-id <task-id>
```

### Discovering and Calling an Unknown Agent

When the user provides an agent URL you haven't seen before:

```bash
# Step 1: Always discover first
a2a-ask discover <url> --output text --pretty

# Step 2: Review capabilities, skills, and security requirements
# Step 3: Handle authentication if needed
# Step 4: Send a message appropriate to the agent's skills
a2a-ask send <url> -m "request matching the agent's described skills"
```

### Working with a Secured Agent

```bash
# Discover and see security requirements
a2a-ask discover https://secure-agent.example.com --output text

# If OAuth2 device code:
a2a-ask auth login https://secure-agent.example.com
# → Follow the interactive flow, user visits URL and enters code

# Now send messages (token is stored)
a2a-ask send https://secure-agent.example.com -m "Secured request"
```

## Error Handling

### Common Errors and Solutions

| Error | Likely Cause | Solution |
|-------|-------------|----------|
| Connection refused / timeout | Agent is down or URL is wrong | Verify the URL, try `discover` first |
| 401 Unauthorized | Missing or invalid auth | Check security requirements with `discover`, provide correct credentials |
| 404 Not Found | Wrong endpoint URL | Verify the agent URL, check if well-known path is correct |
| Empty response `{}` | Protocol version mismatch | The CLI auto-negotiates v0.3/v1.0 — if this persists, the agent may have issues |
| `input-required` state | Agent needs more info | Read the agent's question, send a follow-up with `--task-id` |
| `auth-required` state | Auth needed mid-task | Obtain credentials, retry with auth options and `--task-id` |
| `failed` state | Agent processing error | Read the error message in the response, adjust request and retry |

### Verbose Mode

For debugging, add `-v` to any command to see detailed request/response information:

```bash
a2a-ask send <url> -m "test" -v
```

## Global Options

These options are available on all commands:

| Option | Description | Default |
|--------|-------------|---------|
| `--output <json\|text>` | Output format | `json` |
| `--pretty` | Pretty-print JSON output | `false` |
| `-v, --verbose` | Verbose/debug output | `false` |

**Use `--output json`** (default) when parsing output programmatically.
**Use `--output text`** when displaying results to a user for readability.

## A2A Protocol Quick Reference

### What is A2A?

A2A (Agent-to-Agent) is an open protocol that allows AI agents to communicate with each other over HTTP. Key concepts:

- **Agent Card** — JSON metadata describing an agent's capabilities, skills, and security requirements. Published at a well-known URL.
- **Message** — A communication unit containing one or more Parts (text, files, data).
- **Task** — A stateful unit of work created when a message is sent to an agent. Has a lifecycle with states.
- **Artifact** — Output produced by the agent (text, files, structured data).
- **Part** — A content unit within a message or artifact: text, raw bytes (file), URL reference, or structured data.

### Task States

```
           ┌─────────────────────────────┐
           │                             ▼
 submitted → working → completed
                │         │
                │         ├→ failed
                │         │
                │         └→ canceled
                │
                └→ input-required (waiting for user input)
                │
                └→ auth-required (waiting for credentials)
```

### Protocol Versions

- **v1.0** — Current version with `supportedInterfaces`, discriminated unions, structured types
- **v0.3** — Older version still used by many agents — the CLI handles both transparently

The CLI automatically detects the agent's protocol version and communicates accordingly. You don't need to specify the version manually.

## Limitations

1. **OAuth2 authorization code flow** — Only device code flow and client credentials are supported interactively. For auth code flow, obtain the token externally and pass via `--auth-token`.
2. **mTLS** — Mutual TLS authentication is not yet supported.
3. **Push notifications** — The CLI cannot receive push notifications (it's a client, not a server). Use streaming or polling instead.
4. **Binary output in JSON mode** — File artifacts are base64-encoded inline. Use `--save-artifacts` for large files.
5. **Query-string API keys** — The `--api-key-location query` option is recognized but may not work with all A2A SDK versions. Use header-based API keys when possible.
6. **Token encryption** — Token storage is encrypted with DPAPI on Windows only. On macOS/Linux, tokens are stored as plaintext JSON with restricted file permissions (0600).

## Installing This Skill

### GitHub Copilot CLI (personal)

```bash
mkdir -p ~/.copilot/skills/a2a-ask-cli
cp skill/SKILL.md ~/.copilot/skills/a2a-ask-cli/SKILL.md
```

### GitHub Copilot CLI (project)

```bash
mkdir -p .github/skills/a2a-ask-cli
cp skill/SKILL.md .github/skills/a2a-ask-cli/SKILL.md
git add .github/skills/
git commit -m "Add A2A-Ask CLI agent skill"
```

### Claude Code (personal)

```bash
mkdir -p ~/.claude/skills/a2a-ask-cli
cp skill/SKILL.md ~/.claude/skills/a2a-ask-cli/SKILL.md
```

### Claude Code (project)

```bash
mkdir -p .claude/skills/a2a-ask-cli
cp skill/SKILL.md .claude/skills/a2a-ask-cli/SKILL.md
```

### VS Code / Cursor (project)

```bash
mkdir -p .github/skills/a2a-ask-cli
cp skill/SKILL.md .github/skills/a2a-ask-cli/SKILL.md
```

After installing, restart your agent session to pick up the new skill.
