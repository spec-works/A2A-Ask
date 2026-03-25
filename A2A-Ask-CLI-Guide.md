# A2A-Ask — CLI Reference and Skill Guide

## Overview

**A2A-Ask** is a command-line tool for interacting with remote [A2A (Agent-to-Agent) protocol](https://a2a-protocol.org) agents. It enables any AI agent — such as GitHub Copilot CLI, Claude Code, or Cursor — to discover, communicate with, and manage tasks on remote A2A agents directly from the terminal.

The tool has two complementary parts:

1. **The CLI** (`a2a-ask`) — a .NET global tool that handles the HTTP/JSON-RPC communication, streaming, authentication, and output formatting.
2. **The Skill** (`SKILL.md`) — an instruction document that teaches AI agents *how* to use the CLI, including workflows, decision trees, and error handling patterns.

Together, they turn any agentic coding assistant into a bridge to the broader A2A ecosystem.

## Installation

```bash
dotnet tool install --global SpecWorks.A2A-Ask
```

Verify:

```bash
a2a-ask version
```

## CLI Commands

### Global Options

Every command supports these options:

| Option | Description | Default |
|--------|-------------|---------|
| `--output <json\|text>` | Output format | `json` |
| `--pretty` | Pretty-print JSON output | `false` |
| `-v, --verbose` | Enable verbose/debug output | `false` |
| `--version` | Show version information | — |

### discover — Fetch an Agent Card

Retrieves and displays an agent's metadata, capabilities, skills, and security requirements.

```bash
a2a-ask discover <url> [options]
```

**Arguments:**

| Argument | Description |
|----------|-------------|
| `<url>` | Base URL of the agent, or a direct agent card URL |

**Options:**

| Option | Description | Default |
|--------|-------------|---------|
| `--well-known` | Append `/.well-known/agent-card.json` to the URL | `true` |
| `--extended` | Fetch the extended (authenticated) agent card | `false` |
| `--auth-token` | Bearer token for authentication | — |
| `--auth-header` | Custom auth header (`key=value` format) | — |

**Example:**

```bash
a2a-ask discover https://example.com/agent --output text
```

Output includes the agent name, description, version, protocol version, capabilities (streaming, push notifications), supported input/output modes, available skills with descriptions, and security schemes.

### send — Send a Message

Sends a message to an A2A agent and waits for the response. Supports text, file, and structured data payloads.

```bash
a2a-ask send <url> [options]
```

**Arguments:**

| Argument | Description |
|----------|-------------|
| `<url>` | Agent endpoint URL |

**Options:**

| Option | Alias | Description | Default |
|--------|-------|-------------|---------|
| `--message` | `-m` | Message text to send | — |
| `--file` | `-f` | File to include as a message part | — |
| `--data` | `-d` | Structured JSON data to include | — |
| `--task-id` | `-t` | Task ID for continuing an existing task | — |
| `--context-id` | `-c` | Context ID for grouping related interactions | — |
| `--message-id` | — | Custom message ID (auto-generated if omitted) | UUID |
| `--accept` | — | Accepted output modes (comma-separated) | — |
| `--return-immediately` | — | Return without waiting for completion | `false` |
| `--history-length` | — | Max history messages in response | — |
| `--auth-token` | — | Bearer token for authentication | — |
| `--auth-header` | — | Custom auth header (`key=value`) | — |
| `--api-key` | — | API key value | — |
| `--api-key-header` | — | API key header name | — |
| `--binding` | — | Protocol binding: `auto`, `http`, `jsonrpc` | `auto` |
| `--a2a-version` | — | A2A protocol version | `1.0` |
| `--tenant` | — | Tenant ID | — |
| `--save-artifacts` | — | Directory to save file artifacts | — |

**Examples:**

```bash
# Simple text message
a2a-ask send https://example.com/agent -m "Hello, what can you do?" --output text

# With file attachment
a2a-ask send https://example.com/agent -m "Summarize this" -f report.pdf --output text

# Continue a multi-turn conversation
a2a-ask send https://example.com/agent -m "done" --task-id abc-123 --output text

# Non-blocking (returns task ID immediately)
a2a-ask send https://example.com/agent -m "Long analysis" --return-immediately --output json
```

### stream — Streaming Responses

Sends a message and receives real-time Server-Sent Events (SSE) as the agent processes the request. Shows status updates, artifact chunks, and final results as they arrive.

```bash
a2a-ask stream <url> [options]
```

**Options** are the same as `send`, plus:

| Option | Description | Default |
|--------|-------------|---------|
| `--subscribe` | Subscribe to an existing task's events (requires `--task-id`) | `false` |

**Examples:**

```bash
# Stream a new request
a2a-ask stream https://example.com/agent -m "Generate a report" --output text

# Subscribe to an existing task
a2a-ask stream https://example.com/agent --subscribe --task-id abc-123 --output text
```

**Text mode output shows real-time progress:**

```
📤 Task started: abc-123 [submitted]
⏳ [working] Analyzing data...
⏳ [working] Generating report sections...
Chunk 1: Introduction...
Chunk 2: Analysis...
✅ [completed] Report generation complete.
```

### task — Task Management

Subcommands for managing A2A tasks:

#### task get

Retrieves the current state of a task, including status, artifacts, and history.

```bash
a2a-ask task get <url> --task-id <id> [options]
```

| Option | Description |
|--------|-------------|
| `--task-id, -t` | Task ID (required) |
| `--history-length` | Max history messages to include |

#### task cancel

Cancels a running task.

```bash
a2a-ask task cancel <url> --task-id <id> [options]
```

#### task list

Lists tasks with optional filtering.

```bash
a2a-ask task list <url> [options]
```

| Option | Description |
|--------|-------------|
| `--context-id` | Filter by context ID |
| `--status` | Filter by task state |
| `--page-size` | Results per page |
| `--page-token` | Pagination cursor |

### auth — Authentication Management

#### auth login

Performs an interactive OAuth 2.0 device code flow for agents that require it.

```bash
a2a-ask auth login <url>
```

The command prints a URL and code for the user to enter in their browser. Once authenticated, the token is stored in `~/.a2a-ask/tokens.json` and reused for subsequent requests.

## Task States

A2A tasks follow a state machine. The CLI renders each state with a visual icon:

| State | Icon | Meaning |
|-------|------|---------|
| Submitted | 📤 | Task received, not yet processing |
| Working | ⏳ | Agent is actively processing |
| Completed | ✅ | Task finished successfully |
| Failed | ❌ | Task encountered an error |
| Input Required | ⏸ | Agent needs additional user input |
| Auth Required | 🔐 | Agent needs authentication credentials |
| Canceled | 🚫 | Task was canceled by the user |
| Rejected | ⛔ | Agent rejected the request |

**Terminal states** (task is done): Completed, Failed, Canceled, Rejected.

**Interrupted states** (user action needed): Input Required, Auth Required.

## Output Formats

The CLI supports two output modes:

**JSON mode** (`--output json`) returns structured data suitable for machine parsing. Use `--pretty` for indented output. This is the default.

**Text mode** (`--output text`) provides human-readable output with status icons, formatted agent cards, and inline artifact content. This is the recommended mode for interactive use and for AI agents parsing the output.

## What the Skill Enables

The `SKILL.md` file is the key innovation of A2A-Ask. It transforms any AI coding agent into an A2A client by providing structured instructions the agent follows automatically.

### Agent Discovery and Understanding

When a user provides an A2A agent URL, the skill instructs the AI agent to:

1. **Fetch the agent card** using `a2a-ask discover` to learn what the remote agent can do.
2. **Read the capabilities** to determine whether to use streaming or polling.
3. **Check security requirements** and guide the user to provide credentials using the correct authentication method.
4. **Present the agent's skills** to the user so they can choose what to ask.

### Intelligent Message Routing

The skill teaches the AI agent to:

- Use `stream` when the remote agent supports streaming for real-time progress.
- Use `send` for simple request-response interactions.
- Use `--return-immediately` with `task get` polling for long-running operations.
- Include file attachments and structured data when the user's request involves documents or JSON payloads.

### Multi-Turn Conversation Handling

When a task returns `input-required`, the skill instructs the AI agent to:

1. Show the agent's message to the user.
2. Ask the user for their response.
3. Send the follow-up using `--task-id` to continue the same conversation.
4. Repeat until the task reaches a terminal state.

### Error Recovery

The skill includes an error reference table so the AI agent can:

- Diagnose common failures (authentication errors, network issues, unsupported operations).
- Suggest corrective actions (re-authenticate, check the URL, try a different command).
- Retry with appropriate adjustments rather than giving up.

### Authentication Workflow

The skill provides a decision tree for authentication:

- **No security** → proceed directly.
- **Bearer token** → ask the user for their token.
- **API key** → ask for the key and use the header name from the agent card.
- **OAuth 2.0** → run `a2a-ask auth login` for an interactive device code flow.
- **OpenID Connect** → ask the user to obtain a token from their provider.

### Protocol Compatibility

A2A-Ask includes built-in fallback handling for agents running different protocol versions. When the SDK's strict deserialization fails (e.g., for agents using older A2A v0.3 schemas), the CLI automatically retries with lenient raw JSON parsing. This means the skill works with agents across the A2A ecosystem regardless of their exact protocol version.

## Architecture

```
┌─────────────────────────────────────────────┐
│          AI Agent (Copilot, Claude, etc.)    │
│                                             │
│  Reads SKILL.md → Knows how to use CLI      │
└─────────────────┬───────────────────────────┘
                  │ Executes CLI commands
                  ▼
┌─────────────────────────────────────────────┐
│              a2a-ask CLI                    │
│                                             │
│  discover │ send │ stream │ task │ auth     │
│                                             │
│  Auth Module ─── Output Module              │
│  (Bearer, API key, OAuth2)  (JSON, Text)    │
│                                             │
│  A2A SDK (v0.3.3-preview)                   │
│  + Raw JSON/SSE Fallback                    │
└─────────────────┬───────────────────────────┘
                  │ HTTP / JSON-RPC / SSE
                  ▼
┌─────────────────────────────────────────────┐
│           Remote A2A Agent                  │
│                                             │
│  Agent Card → Skills → Tasks → Artifacts    │
└─────────────────────────────────────────────┘
```

## Live Example

Discovering and querying the SAP Solar System Explorer agent:

```bash
# Step 1: Discover what the agent can do
a2a-ask discover https://a2a-agents-server.cfapps.sap.hana.ondemand.com/agents/solar --output text

# Agent: Solar System Explorer
# Skills: solar-weather, space-facts, astronomy-events
# Authentication: None required

# Step 2: Ask a question
a2a-ask send https://a2a-agents-server.cfapps.sap.hana.ondemand.com/agents/solar \
  -m "What's the weather like on Mars today?" --output text

# ✅ Task: 559b8167-8672-48df-9699-b78a4abe2d49
#   State: Completed
#   🌍 Mars Weather Report
#   Temperature: -63°C (-81°F), light dust haze across Jezero Crater.
#   Winds from the northwest at 25 km/h.
```

## Summary

A2A-Ask bridges the gap between AI coding agents and the A2A protocol ecosystem. The CLI handles the protocol mechanics — HTTP transport, JSON-RPC framing, SSE streaming, authentication — while the skill provides the intelligence layer that lets any AI agent use these capabilities effectively. Together, they make it possible to orchestrate multi-agent workflows from a single terminal session.
