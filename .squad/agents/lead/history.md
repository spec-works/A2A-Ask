# Lead — History

## Sessions

### 2026-05-24 — Architecture & Code Quality Review

Performed a full architecture review of the A2A-Ask CLI project. Covered all major subsystems: CLI surface, auth, catalog, output, streaming, protocol compatibility, and dependencies.

## Learnings

### Key File Paths
- Main source: `dotnet/src/A2A-Ask/` — 4 folders: Commands, Auth, Catalog, Output
- Entry point: `dotnet/src/A2A-Ask/Program.cs`
- Project file: `dotnet/src/A2A-Ask/A2A-Ask.csproj` (targets net10.0, packaged as dotnet tool `a2a-ask`)
- Unit tests: `dotnet/tests/A2A-Ask.Tests/` — xUnit + Moq
- Integration tests: `dotnet/tests/A2A-Ask.IntegrationTests/`
- Test agent server: `dotnet/tests/TestAgentServer/`
- Token store: `~/.a2a-ask/tokens.dat` (DPAPI-encrypted on Windows) / `tokens.json` (Unix, chmod 600)
- Client reg store: `~/.a2a-ask/clients.dat` / `clients.json`
- Catalog aliases: `~/.a2a-ask/catalog-aliases.json`

### Architecture Patterns
- Commands follow static factory pattern: `XCommand.Create()` returns a configured `Command`
- Global options (`--output`, `--pretty`, `--verbose`) are root-level but accessed via brittle LINQ in every handler
- `CommonOptions` is a shared utility class holding target resolution, client factory, V0.3 detection, catalog search — scope is too broad
- Protocol version gated via `CommonOptions.IsV03(string)` — clean, consistent
- Auth priority: explicit CLI flags → stored token (with refresh) → unauthenticated
- Catalog matching: exact EntryId → exact DisplayName → exact Tag → fuzzy substring (three copies of this logic)
- `CatalogRegistry.SaveAliases` uses atomic temp-file-then-move pattern — good
- `TokenStore` migrates from plaintext to DPAPI-encrypted on first use — good migration path

### Confirmed Bugs (as of 2026-05-24)
1. **`--client-id`/`--client-secret` in `send`/`stream` silently ignored** — parsed but never passed to AuthConfigurator
2. **`--binding` option silently ignored** — declared, parsed, never applied in handler
3. **`--status` filter in `task list` silently dropped** — variable read but never set on `ListTasksRequest`
4. **`--api-key-location` not wired into `send`/`stream`** — option exists in CommonOptions but is never added to those commands' option sets
5. **Query-string API key not implemented** — AuthConfigurator prints a warning and does nothing

### Architectural Concerns
- FindMatchingCatalogAgents logic is copy-pasted 3 times (CommonOptions, CatalogCommand, CatalogInputResolver)
- StreamCommand has duplicate message-building code vs SendCommand.BuildParts()
- SystemBrowser hardcoded to port 29080 — no fallback if port is occupied
- `RequireHttps = false` in OIDC discovery — security concern for non-local agents
- `DiscoverCommand` creates two `ConsoleFormatter` instances in the same flow

### Decisions Made
- Identified `CommonOptions` as a candidate for decomposition into `TargetResolver`, `ClientFactory`, `ProtocolVersionHelper`
- Global option access pattern should be refactored (extracted helper or bound context object)
- The three duplicate matching implementations should be consolidated into `CatalogInputResolver.FindMatchingAgents`

### Dependencies (NuGet)
- `A2A` v1.0.0-preview2 — main SDK
- `A2A.V0_3Compat` v1.0.0-preview2 — v0.3 compatibility layer
- `Duende.IdentityModel` 8.1.0 — OAuth2 client requests
- `Duende.IdentityModel.OidcClient` 7.1.0 — PKCE auth code flow
- `System.Security.Cryptography.ProtectedData` 9.0.4 — Windows DPAPI
- `System.CommandLine` 2.0.0-beta4 — CLI framework (still beta)
- `SpecWorks.AiCatalog` 0.1.0 — AI Catalog parsing

## Team Review Update - 2026-05-24T22:41:52Z
Scribe completed decision inbox processing and session documentation.
All team findings consolidated into decisions.md and orchestration logs.
