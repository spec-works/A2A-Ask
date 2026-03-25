# A2A-Ask

**Talk to A2A agents from the command line.**

A2A-Ask is a CLI client for the [A2A (Agent-to-Agent) protocol](https://a2a-protocol.org). It enables you — or AI agents acting on your behalf — to discover remote agents, send messages, stream responses, handle multi-turn conversations, and manage task lifecycle.

Built with the official [a2a-dotnet SDK](https://github.com/a2aproject/a2a-dotnet) and designed for both human users and AI agent skills (GitHub Copilot CLI, Claude Code, etc.).

## Quick Start

```bash
# Install
dotnet tool install --global SpecWorks.A2A-Ask

# Discover an agent
a2a-ask discover https://agent.example.com --output text

# Send a message
a2a-ask send https://agent.example.com --message "Hello, what can you do?" --output text

# Stream a response in real-time
a2a-ask stream https://agent.example.com --message "Generate a report" --output text
```

## Commands

| Command | Description |
|---------|-------------|
| `a2a-ask discover <url>` | Fetch and display an agent card |
| `a2a-ask send <url>` | Send a message to an agent |
| `a2a-ask stream <url>` | Stream a response or subscribe to task events |
| `a2a-ask task get <url>` | Get current task status (for polling) |
| `a2a-ask task cancel <url>` | Cancel a running task |
| `a2a-ask auth login <url>` | Interactive OAuth2 authentication |
| `a2a-ask version` | Display version info |

## Authentication

A2A-Ask supports multiple authentication methods based on the agent's security requirements:

```bash
# Bearer token
a2a-ask send <url> --auth-token "eyJhbGciOi..." --message "Hello"

# API key
a2a-ask send <url> --api-key "sk-123" --api-key-header "X-API-Key" --message "Hello"

# Interactive OAuth2 (device code flow)
a2a-ask auth login <url>

# Custom header
a2a-ask send <url> --auth-header "X-Custom=value" --message "Hello"
```

## Multi-Turn Conversations

When an agent needs more information, it enters the `InputRequired` state. Continue the conversation using `--task-id`:

```bash
# First message
a2a-ask send <url> --message "Plan a trip to Japan" --output text
# → ⏸ InputRequired: "What dates are you considering?"

# Follow up with the same task
a2a-ask send <url> --task-id abc-123 --message "March 15-25" --output text
```

## Streaming

For agents that support streaming, get real-time progress and artifact delivery:

```bash
a2a-ask stream <url> --message "Analyze this data" --output text
# 📤 Task started: abc-123 [Submitted]
# ⏳ [Working] Analyzing data...
# ⏳ [Working] Generating insights...
# ✅ [Completed]
```

## Agent Skill

A2A-Ask includes a comprehensive [SKILL.md](skills/a2a-ask-cli/SKILL.md) for AI agent integration. Install it in your project to give AI assistants the ability to interact with any A2A agent:

```bash
# GitHub Copilot CLI
mkdir -p .github/skills/a2a-ask-cli
cp -r skills/a2a-ask-cli/* .github/skills/a2a-ask-cli/

# Claude Code
mkdir -p .claude/skills/a2a-ask-cli
cp -r skills/a2a-ask-cli/* .claude/skills/a2a-ask-cli/
```

## Requirements

- .NET 10.0 SDK or later
- Works on Windows, macOS, and Linux

## Building from Source

```bash
cd dotnet
dotnet build
dotnet run --project src/A2A-Ask -- --help
```

## License

MIT — see [LICENSE](LICENSE) for details.
