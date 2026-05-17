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

### `a2a-ask discover <target>`

Fetch and display an A2A agent card.

```bash
a2a-ask discover <target> [options]
```

**Arguments:**

| Argument | Description |
|----------|-------------|
| `<target>` | Agent URL or `@agent@catalog` reference |

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

### `a2a-ask catalog list <target>`

List A2A agents available in a catalog.

```bash
a2a-ask catalog list <target> [options]
```

**Arguments:**

| Argument | Description |
|----------|-------------|
| `<target>` | Catalog URL, host/origin shorthand, or `@@catalog` reference |

**Options:**

| Option | Description | Default |
|--------|-------------|---------|
| `--filter <text>` | Filter by entry id, display name, description, or tags | — |

**Examples:**

```bash
a2a-ask catalog list https://example.com/.well-known/ai-catalog.json
a2a-ask catalog list @@catalog.example.com --filter weather
```

---

### `a2a-ask catalog show <target>`

Show one resolved A2A agent from a catalog.

```bash
a2a-ask catalog show <target>
```

**Arguments:**

| Argument | Description |
|----------|-------------|
| `<target>` | Catalog URL, host/origin shorthand, `@@catalog`, or `@agent@catalog` target |

**Examples:**

```bash
a2a-ask catalog show @weather@catalog.example.com
a2a-ask catalog show https://example.com/.well-known/ai-catalog.json
```

If the catalog contains multiple A2A agents, use `@agent@catalog` to choose one explicitly. Bare `@agent` targets are parsed, but Phase 1 still requires an explicit catalog host or URL.

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

Plain URLs are sent directly with no agent card fetch. Use `discover` when you need card metadata first, and use `--a2a-version 0.3` for older direct endpoints.

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
| `--a2a-version` | Protocol version for direct URL calls |
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
| `--a2a-version` | Protocol version for direct URL calls |
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
| `--a2a-version` | Protocol version for direct URL calls |
| Auth options | Same as `send` |

---

### `a2a-ask auth login <url>`

Interactively authenticate with an A2A agent using OAuth2 device code flow.

```bash
a2a-ask auth login <url> [--client-id <id>] [--tenant <id>]
```

Reads the agent card's security schemes and runs the appropriate interactive authentication flow. The obtained token is stored for reuse. When a persisted client registration matches the agent's OAuth2 issuer, the CLI automatically uses that client ID and optional RFC 8707 resource.

---

### `a2a-ask auth register-client`

Register an OAuth2 client for automatic issuer matching.

```bash
a2a-ask auth register-client --client-id <id> --issuer <url> [--resource <url>]
```

---

### `a2a-ask auth list-clients`

List registered OAuth2 clients.

```bash
a2a-ask auth list-clients
```

---

### `a2a-ask auth remove-client`

Remove a registered OAuth2 client.

```bash
a2a-ask auth remove-client --issuer <url> [--resource <url>]
```

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

For plain direct URLs, the CLI defaults to v1.0. When you know an older agent speaks v0.3, pass `--a2a-version 0.3` on `send`, `stream`, or `task` commands.
