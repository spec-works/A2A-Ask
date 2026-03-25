# CLI Reference

Complete command reference for the `a2a-ask` CLI tool.

## Installation

```bash
dotnet tool install --global SpecWorks.A2A-Ask
```

## Global Options

These options are available on all commands:

| Option | Description | Default |
|--------|-------------|---------|
| `--output <json\|text>` | Output format | `json` |
| `--pretty` | Pretty-print JSON output | `false` |
| `-v, --verbose` | Verbose/debug output | `false` |
| `--version` | Show version information | — |
| `-?, -h, --help` | Show help | — |

## Commands

### `a2a-ask discover <url>`

Fetch and display an A2A agent card.

```bash
a2a-ask discover <url> [options]
```

**Arguments:**

| Argument | Description |
|----------|-------------|
| `<url>` | Base URL of the agent (or direct agent card URL) |

**Options:**

| Option | Description | Default |
|--------|-------------|---------|
| `--well-known` | Append `/.well-known/agent-card.json` to the URL | `true` |
| `--extended` | Fetch the extended (authenticated) agent card | `false` |
| `--auth-token <token>` | Bearer token for authentication | — |
| `--auth-header <key=value>` | Custom auth header | — |

**Examples:**

```bash
# Discover an agent
a2a-ask discover https://example.com/agents/my-agent --output text

# Fetch extended card with authentication
a2a-ask discover https://example.com/agents/my-agent --extended --auth-token "my-token"
```

---

### `a2a-ask send <url>`

Send a message to an A2A agent and wait for the response.

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
| `--file` | `-f` | File path to include as a message part | — |
| `--data` | `-d` | Structured JSON data to include | — |
| `--task-id` | `-t` | Continue an existing task (multi-turn) | — |
| `--context-id` | `-c` | Context ID for grouping interactions | — |
| `--message-id` | | Custom message ID | auto UUID |
| `--accept` | | Accepted output modes (comma-separated media types) | — |
| `--return-immediately` | | Don't wait for task completion | `false` |
| `--history-length` | | Max history messages in response | — |
| `--save-artifacts` | | Directory to save file artifacts | — |
| `--auth-token` | | Bearer token | — |
| `--auth-header` | | Custom auth header (key=value) | — |
| `--api-key` | | API key value | — |
| `--api-key-header` | | API key header name | from card |
| `--binding` | | Protocol binding: auto, http, jsonrpc | `auto` |
| `--a2a-version` | | A2A protocol version | `1.0` |
| `--tenant` | | Tenant ID | — |

At least one of `--message`, `--file`, or `--data` is required.

**Examples:**

```bash
# Simple message
a2a-ask send https://example.com/agent -m "What is the weather on Mars?"

# Multi-turn follow-up
a2a-ask send https://example.com/agent -m "Tell me more" --task-id "abc-123"

# Send with a file
a2a-ask send https://example.com/agent -m "Summarize this" --file ./report.pdf

# Send JSON data
a2a-ask send https://example.com/agent -d '{"key": "value"}'

# With authentication
a2a-ask send https://example.com/agent -m "Hello" --auth-token "bearer-token"
```

---

### `a2a-ask stream <url>`

Send a message with streaming response, showing real-time progress updates.

```bash
a2a-ask stream <url> [options]
```

Same arguments and options as `send`, plus:

| Option | Description | Default |
|--------|-------------|---------|
| `--subscribe` | Subscribe to an existing task's events (requires `--task-id`) | `false` |

**Examples:**

```bash
# Stream a response
a2a-ask stream https://example.com/agent -m "Analyze this data"

# Subscribe to task events
a2a-ask stream https://example.com/agent --task-id "abc-123" --subscribe
```

---

### `a2a-ask task get <url>`

Get the current state of a task (useful for polling).

```bash
a2a-ask task get <url> --task-id <id> [options]
```

| Option | Description |
|--------|-------------|
| `--task-id` (required) | Task ID to query |
| `--history-length` | Max history messages to include |
| Auth options | Same as `send` |

---

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
| `--page-token` | Pagination cursor token |
| Auth options | Same as `send` |

---

### `a2a-ask task cancel <url>`

Cancel a running task.

```bash
a2a-ask task cancel <url> --task-id <id> [options]
```

| Option | Description |
|--------|-------------|
| `--task-id` (required) | Task ID to cancel |
| Auth options | Same as `send` |

---

### `a2a-ask auth login <url>`

Interactively authenticate with an A2A agent using OAuth2 device code flow.

```bash
a2a-ask auth login <url>
```

Reads the agent card's security schemes and runs the appropriate interactive authentication flow. The obtained token is stored for reuse.

---

### `a2a-ask version`

Display version information.

```bash
a2a-ask version
```

## Authentication Options

All commands that communicate with agents support these auth options:

| Option | Description |
|--------|-------------|
| `--auth-token <token>` | Bearer token for HTTP Bearer auth |
| `--auth-header <key=value>` | Custom authentication header |
| `--api-key <key>` | API key value |
| `--api-key-header <header>` | API key header name (defaults to agent card setting) |
| `--tenant <id>` | Tenant identifier |

## Protocol Options

| Option | Description | Default |
|--------|-------------|---------|
| `--binding <binding>` | Protocol binding: `auto`, `http`, `jsonrpc` | `auto` |
| `--a2a-version <version>` | A2A protocol version | `1.0` |

The CLI automatically detects the agent's protocol version (v0.3 or v1.0) and communicates accordingly. Manual override is rarely needed.
