# Agent Skill Guide

The A2A-Ask CLI includes a SKILL.md file that teaches AI coding assistants (GitHub Copilot CLI, Claude Code, VS Code Copilot, Cursor, etc.) how to use the tool effectively.

## What is a Skill?

A skill is a structured document that gives AI assistants domain knowledge about a tool — its commands, workflows, decision trees, and error handling. When installed, the AI assistant can:

- Discover A2A agents and understand their capabilities
- Send messages and interpret responses
- Handle multi-turn conversations automatically
- Manage authentication requirements
- Stream responses and poll for status
- Handle errors and edge cases

## Installing the Skill

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

### From the SpecWorks Plugins Marketplace

The skill is also available via the [SpecWorks plugins repository](https://github.com/spec-works/plugins):

```bash
# Using GitHub Copilot CLI plugin system
copilot plugins add spec-works
```

After installing, restart your agent session to pick up the new skill.

## What the Skill Teaches

The SKILL.md covers:

1. **Agent Discovery** — How to fetch and interpret agent cards
2. **Message Sending** — Text, file, and structured data messages
3. **Streaming** — Real-time status updates and partial results
4. **Multi-Turn Conversations** — Following up with `--task-id`
5. **Task Management** — Polling, listing, and canceling tasks
6. **Authentication** — Decision tree for API keys, bearer tokens, OAuth2
7. **Error Handling** — Common errors and recovery strategies
8. **A2A Protocol Reference** — Task states, protocol versions, key concepts

## View the Full Skill

The complete SKILL.md is available in the [skill/ folder](https://github.com/spec-works/A2A-Ask/blob/master/skill/SKILL.md) of the repository.
