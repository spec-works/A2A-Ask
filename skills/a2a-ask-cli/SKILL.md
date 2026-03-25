---
name: a2a-ask-cli
description: >
  Interact with remote A2A (Agent-to-Agent) protocol agents from the command line using A2A-Ask.
  Use this skill when the user wants to discover, communicate with, or manage tasks on an A2A agent.
  Activate when the user mentions A2A agents, agent cards, agent-to-agent protocol, remote AI agents,
  or wants to send messages to an external agent endpoint. Handles discovery, authentication,
  messaging, streaming, multi-turn conversations, and task lifecycle management.
license: MIT
compatibility: Requires .NET 10.0 SDK. Works on Windows, macOS, and Linux.
metadata:
  author: spec-works
  version: "1.0"
  repository: https://github.com/spec-works/A2A-Ask
---

# A2A-Ask CLI — Talk to A2A Agents

A2A-Ask is a command-line client for the [A2A (Agent-to-Agent) protocol](https://a2a-protocol.org). It lets you discover remote agents, send them messages, stream their responses, handle multi-turn conversations, and manage task lifecycle — all from the terminal.

**You are an AI agent using this skill.** Follow the workflows below step-by-step when the user asks you to interact with an A2A agent. The CLI is your bridge to remote agents.

## Prerequisites — Installing .NET

A2A-Ask requires .NET 10.0. Install the SDK for your platform:

### Windows

```powershell
# Using winget (recommended)
winget install Microsoft.DotNet.SDK.10

# Or download from https://dotnet.microsoft.com/download/dotnet/10.0
```

### macOS

```bash
# Using Homebrew (recommended)
brew install dotnet-sdk

# Or download from https://dotnet.microsoft.com/download/dotnet/10.0
```

### Linux (Ubuntu/Debian)

```bash
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

sudo apt-get update
sudo apt-get install -y dotnet-sdk-10.0
```

### Linux (Fedora/RHEL)

```bash
sudo dnf install dotnet-sdk-10.0
```

### Verify Installation

```bash
dotnet --version
# Should show 10.x.x
```

## Installing the CLI Tool

```bash
dotnet tool install --global SpecWorks.A2A-Ask
```

Verify it works:

```bash
a2a-ask version
```

## Core Workflow — Step by Step

When a user asks you to interact with an A2A agent, **always follow these steps in order**:

### Step 1: Discover the Agent

Before anything else, fetch the agent card to understand what the agent can do and how to authenticate.

```bash
a2a-ask discover <agent-url> --output text --pretty
```

The agent card tells you:
- **Name and description** — what the agent does
- **Skills** — specific capabilities the agent offers
- **Capabilities** — whether streaming and push notifications are supported
- **Security schemes** — what authentication is required
- **Input/output modes** — what content types are accepted

**Always read the security schemes.** If the agent requires authentication, you must handle it before sending messages (see Step 2).

**Always check capabilities.streaming.** If `true`, prefer the `stream` command over `send` for better UX.

### Step 2: Handle Authentication (if required)

The discover output shows security requirements. Match the scheme type to the right CLI option:

| Security Scheme | CLI Option | How to Get Credentials |
|----------------|-----------|----------------------|
| HTTP Bearer | `--auth-token <token>` | Ask user for their bearer/JWT token |
| API Key | `--api-key <key> --api-key-header <header-name>` | Ask user for their API key; header name shown in agent card |
| OAuth 2.0 | Run `a2a-ask auth login <url>` | Interactive device code flow — follow the prompts |
| OpenID Connect | `--auth-token <token>` | Ask user to obtain token from their OIDC provider |
| No security | (none needed) | Proceed directly |

**If the agent requires OAuth2**, try the interactive login first:

```bash
a2a-ask auth login <agent-url>
```

This runs a device code flow: it prints a URL and code for the user to enter in their browser. Once authenticated, the token is stored in `~/.a2a-ask/tokens.json` and reused automatically.

**If the user already has a token**, pass it directly:

```bash
a2a-ask send <url> --auth-token "eyJhbGciOi..." --message "Hello"
```

### Step 3: Send a Message

Use `send` for a simple request-response, or `stream` for real-time progress.

#### Simple send (blocking):

```bash
a2a-ask send <agent-url> --message "Analyze the Q3 sales data" --output text
```

#### Send with file attachment:

```bash
a2a-ask send <agent-url> --message "Summarize this document" --file report.pdf --output text
```

#### Send with structured data:

```bash
a2a-ask send <agent-url> --message "Process this config" --data '{"key": "value"}' --output text
```

#### Non-blocking send (return immediately):

```bash
a2a-ask send <agent-url> --message "Long running analysis" --return-immediately --output json
```

This returns a task ID immediately. Use polling (Step 5) to check progress.

### Step 4: Stream Responses (Preferred)

If the agent supports streaming (check `capabilities.streaming` from discover), use `stream` for real-time updates:

```bash
a2a-ask stream <agent-url> --message "Generate a detailed report" --output text
```

In text mode, you'll see real-time updates:
```
📤 Task started: abc-123 [Submitted]
⏳ [Working] Analyzing data...
⏳ [Working] Generating report sections...
📎 Artifact: report.md → (text content streamed inline)
✅ [Completed]
```

In JSON mode, each event is a separate JSON line for machine parsing.

#### Subscribe to an existing task:

```bash
a2a-ask stream <agent-url> --subscribe --task-id <task-id> --output text
```

### Step 5: Poll for Task Status

If you used `--return-immediately` or need to check on a running task:

```bash
a2a-ask task get <agent-url> --task-id <task-id> --output text
```

**Polling pattern for agents:** Call `task get` repeatedly with increasing intervals:
1. Wait 1 second, check status
2. Wait 2 seconds, check status
3. Wait 5 seconds, check status
4. Continue at 5-second intervals until a terminal state is reached

**Terminal states** (stop polling): `Completed`, `Failed`, `Canceled`, `Rejected`
**Interrupted states** (need user action): `InputRequired`, `AuthRequired`
**Active states** (keep polling): `Submitted`, `Working`

### Step 6: Handle Multi-Turn Conversations

When the agent needs more information, the task enters `InputRequired` state:

```
⏸ Agent requires additional input.
  Use: a2a-ask send <url> --task-id abc-123 --message "<your response>"
```

**Your workflow as an AI agent:**
1. Read the agent's question from the status message
2. Ask the user for the information the agent needs
3. Send the user's response back to the same task:

```bash
a2a-ask send <agent-url> --task-id <task-id> --message "The budget is $50,000" --output text
```

The `--task-id` flag links your response to the existing conversation.

### Step 7: Handle Auth-Required Mid-Task

Sometimes an agent needs elevated permissions mid-task:

```
🔐 Agent requires authentication to proceed.
  Use: a2a-ask auth login <url>
```

Run the auth flow, then resume by sending another message to the same task:

```bash
a2a-ask auth login <agent-url>
a2a-ask send <agent-url> --task-id <task-id> --message "Authentication complete, please continue" --output text
```

### Step 8: Save Artifacts

If the agent produces file artifacts, save them to disk:

```bash
a2a-ask send <agent-url> --message "Generate the report" --save-artifacts ./output/ --output text
```

Files are saved to the specified directory with appropriate extensions based on MIME type.

## CLI Command Reference

### Global Options (all commands)

| Option | Description | Default |
|--------|-------------|---------|
| `--output <json\|text>` | Output format | `json` |
| `--pretty` | Pretty-print JSON | `false` |
| `-v, --verbose` | Debug output | `false` |

### `a2a-ask discover <url>`

| Option | Description |
|--------|-------------|
| `--well-known` | Append `/.well-known/agent-card.json` (default: true) |
| `--extended` | Request extended agent card |
| `--auth-token` | Bearer token for extended card |
| `--auth-header` | Custom auth header (key=value) |

### `a2a-ask send <url>`

| Option | Alias | Description |
|--------|-------|-------------|
| `--message` | `-m` | Message text (required unless --file or --data) |
| `--file` | `-f` | File to attach |
| `--data` | `-d` | JSON structured data |
| `--task-id` | `-t` | Continue existing task |
| `--context-id` | `-c` | Group related interactions |
| `--message-id` | | Custom message ID |
| `--accept` | | Accepted output modes (comma-separated) |
| `--return-immediately` | | Don't wait for completion |
| `--history-length` | | Max history messages in response |
| `--auth-token` | | Bearer token |
| `--auth-header` | | Custom auth header (key=value) |
| `--api-key` | | API key value |
| `--api-key-header` | | API key header name |
| `--save-artifacts` | | Directory to save file artifacts |

### `a2a-ask stream <url>`

Same options as `send`, plus:

| Option | Description |
|--------|-------------|
| `--subscribe` | Subscribe to existing task events (requires --task-id) |

### `a2a-ask task get <url>`

| Option | Description |
|--------|-------------|
| `--task-id` | Task ID (required) |
| `--history-length` | Max history messages |

### `a2a-ask task cancel <url>`

| Option | Description |
|--------|-------------|
| `--task-id` | Task ID to cancel (required) |

### `a2a-ask auth login <url>`

Interactive OAuth2 authentication. Discovers the agent's security schemes and runs a device code flow if available.

### `a2a-ask version`

Displays CLI version information.

## Task States Reference

| State | Icon | Meaning | Action |
|-------|------|---------|--------|
| `Submitted` | 📤 | Task received, not yet started | Wait/poll |
| `Working` | ⏳ | Agent is processing | Wait/poll |
| `InputRequired` | ⏸ | Agent needs user input | Ask user, then `send --task-id` |
| `AuthRequired` | 🔐 | Agent needs authentication | Run `auth login`, then resume |
| `Completed` | ✅ | Task finished successfully | Read results |
| `Failed` | ❌ | Task failed | Check error message |
| `Canceled` | 🚫 | Task was canceled | No action |
| `Rejected` | ⛔ | Agent rejected the request | Check reason, modify request |

## Error Handling

### Common Errors and Fixes

| Error | Likely Cause | Fix |
|-------|-------------|-----|
| `401 Unauthorized` | Missing or invalid auth | Check `discover` for required auth, add `--auth-token` or `--api-key` |
| `404 Not Found` | Wrong URL or agent not deployed | Verify URL, try `discover` first |
| `Connection refused` | Agent not running | Check agent URL and port |
| `Task not found` | Invalid task ID | Get a fresh task ID from `send` |
| `Unsupported media type` | Wrong content type sent | Check agent's `defaultInputModes` from card |
| `Streaming not supported` | Agent doesn't support SSE | Use `send` instead of `stream` |

### Debugging

Add `--verbose` to any command for detailed error information:

```bash
a2a-ask send <url> --message "test" --verbose --output text
```

## Decision Tree for AI Agents

Use this decision tree when the user asks you to interact with an A2A agent:

```
User wants to talk to an A2A agent
│
├─ Do I know the agent URL?
│  ├─ No → Ask user for the agent URL
│  └─ Yes → Run: a2a-ask discover <url> --output text
│
├─ Does the agent require authentication?
│  ├─ No → Proceed to messaging
│  ├─ API Key → Ask user for key, use --api-key
│  ├─ Bearer → Ask user for token, use --auth-token
│  └─ OAuth2 → Run: a2a-ask auth login <url>
│
├─ Does the agent support streaming?
│  ├─ Yes → Use: a2a-ask stream <url> --message "..." --output text
│  └─ No → Use: a2a-ask send <url> --message "..." --output text
│
├─ What state did the task end in?
│  ├─ Completed → Show results to user
│  ├─ Failed → Show error, suggest fixes
│  ├─ InputRequired → Ask user for input, then:
│  │  a2a-ask send <url> --task-id <id> --message "<user's answer>"
│  ├─ AuthRequired → Run auth login, then resume task
│  └─ Working/Submitted → Poll with:
│     a2a-ask task get <url> --task-id <id>
│
└─ Does user want to cancel?
   └─ a2a-ask task cancel <url> --task-id <id>
```

## Context and Task Continuity

The A2A protocol supports multi-turn conversations through task IDs and context IDs:

- **`--task-id`**: Continue an existing task (e.g., responding to `InputRequired`)
- **`--context-id`**: Group related but separate tasks in the same conversation context

When continuing a task, always use `--task-id` from the previous response. The agent maintains conversation state server-side.

## Example Workflows

### Simple Question-Answer

```bash
# 1. Discover
a2a-ask discover https://agent.example.com --output text

# 2. Ask a question
a2a-ask send https://agent.example.com --message "What is the weather in Seattle?" --output text
```

### Multi-Turn with Streaming

```bash
# 1. Discover
a2a-ask discover https://agent.example.com --output text

# 2. Start streaming conversation
a2a-ask stream https://agent.example.com --message "Plan a trip to Japan" --output text
# → Agent responds with InputRequired: "What dates are you considering?"

# 3. Continue with user's answer
a2a-ask send https://agent.example.com --task-id abc-123 --message "March 15-25, 2025" --output text
# → Agent responds with InputRequired: "What's your budget?"

# 4. Continue again
a2a-ask send https://agent.example.com --task-id abc-123 --message "About $3000" --output text
# → Agent completes with trip plan
```

### Authenticated Agent with File Processing

```bash
# 1. Discover and check auth
a2a-ask discover https://secure-agent.example.com --output text

# 2. Authenticate
a2a-ask auth login https://secure-agent.example.com

# 3. Send file for processing (token auto-loaded from store)
a2a-ask send https://secure-agent.example.com \
  --message "Analyze this spreadsheet and create a summary report" \
  --file data.xlsx \
  --save-artifacts ./reports/ \
  --output text
```

### Long-Running Task with Polling

```bash
# 1. Submit non-blocking
a2a-ask send https://agent.example.com \
  --message "Process the entire dataset" \
  --return-immediately \
  --output json
# → Returns {"id": "task-789", "status": {"state": "submitted"}, ...}

# 2. Poll for status
a2a-ask task get https://agent.example.com --task-id task-789 --output text
# → ⏳ Task: task-789 State: Working

# 3. Poll again later
a2a-ask task get https://agent.example.com --task-id task-789 --output text
# → ✅ Task: task-789 State: Completed
```

## Installing This Skill

### GitHub Copilot CLI (personal)

```bash
git clone https://github.com/spec-works/A2A-Ask.git /tmp/A2A-Ask
mkdir -p ~/.copilot/skills/a2a-ask-cli
cp -r /tmp/A2A-Ask/skills/a2a-ask-cli/* ~/.copilot/skills/a2a-ask-cli/
```

### GitHub Copilot CLI (project)

```bash
mkdir -p .github/skills/a2a-ask-cli
cp -r /path/to/A2A-Ask/skills/a2a-ask-cli/* .github/skills/a2a-ask-cli/
git add .github/skills/
git commit -m "Add A2A-Ask CLI agent skill"
```

### Claude Code (personal)

```bash
mkdir -p ~/.claude/skills/a2a-ask-cli
cp -r /path/to/A2A-Ask/skills/a2a-ask-cli/* ~/.claude/skills/a2a-ask-cli/
```

### Claude Code (project)

```bash
mkdir -p .claude/skills/a2a-ask-cli
cp -r /path/to/A2A-Ask/skills/a2a-ask-cli/* .claude/skills/a2a-ask-cli/
```

### VS Code / Cursor (project)

```bash
mkdir -p .github/skills/a2a-ask-cli
cp -r /path/to/A2A-Ask/skills/a2a-ask-cli/* .github/skills/a2a-ask-cli/
```

After installing, restart your agent session to pick up the new skill.

## Further Reference

See [references/a2a-quick-ref.md](references/a2a-quick-ref.md) for A2A protocol concepts and the official [A2A specification](https://a2a-protocol.org/latest/specification/).
