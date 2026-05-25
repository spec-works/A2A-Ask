# .NET Dev — History

## Sessions

### 2026-05-24 — Implementation Review

Performed a full read of all .cs source files under `dotnet/src/A2A-Ask/`. No code changes made; findings written to `.squad/decisions/inbox/dotnet-dev-code-review.md`.

## Learnings

- Fixed HttpClient lifetime issues by disposing command-scoped clients with `using` and making `DeviceCodeFlow` own/dispose internally-created clients.
- Wired `--client-id` and `--client-secret` through `AuthConfigurator.CreateHttpClientWithStoredTokenAsync` so `send` and `stream` can fetch client_credentials tokens inline from the resolved agent card.
- Removed the unsupported `--binding` option, centralized global option access in `Commands/GlobalOptions.cs`, and consolidated cross-catalog agent matching through `CatalogInputResolver.FindMatchingAgents`.
- Reused `SendCommand.BuildParts()` from `StreamCommand`, fixed `--data` to preserve any JSON shape, added real query-string API key support via an HTTP handler, and added callback port fallback for auth code login.
- Verified the CLI with `dotnet build` in `dotnet/src/A2A-Ask/` and `dotnet test ..\A2A-Ask.sln` from `dotnet/tests/`.

### Project Layout
- Source root: `dotnet/src/A2A-Ask/`
- Commands: `Commands/` — one file per command (static class, `Create()` factory)
- Auth logic: `Auth/` — `AuthConfigurator`, `TokenStore`, `DeviceCodeFlow`, `AuthCodeFlow`, `ClientRegistrationStore`
- Output: `Output/` — `ConsoleFormatter`, `ProgressRenderer`, `ArtifactSaver`
- Catalog: `Catalog/` — `CatalogRegistry`, `CatalogInputResolver`, `TargetParser`, `ResolvedCatalogAgent`
- Entry point: `Program.cs` — top-level statements, global options registered first

### Key Architecture Decisions
- `System.CommandLine` (beta4) with `SetHandler(InvocationContext)` pattern; global options (--output, --pretty, --verbose) retrieved via LINQ inside each handler — brittle, should be extracted
- `AuthConfigurator.CreateHttpClientWithStoredTokenAsync` is the central HttpClient factory; it does not return `IDisposable`-friendly wrapper — callers rarely `using` it
- Token store uses DPAPI on Windows, plaintext on Unix; DPAPI file = `tokens.dat`, plaintext = `tokens.json`; migration from legacy plaintext handled in `LoadAllTokensAsync`
- Client registration store (`clients.dat` / `clients.json`) follows same DPAPI pattern as token store
- Catalog aliases stored in `~/.a2a-ask/catalog-aliases.json` (plaintext, no encryption); atomic write via temp-file + `File.Move`
- Agent target resolution: `TargetParser.Parse` → `DirectUrl` | `CatalogTarget` | `UnqualifiedName`; resolution chain in `CommonOptions.ResolveTargetAsync`
- Cross-catalog search logic duplicated between `CommonOptions.SearchAllCatalogsForAgent` and `CatalogCommand.SearchRegisteredCatalogsForAgentAsync` — CommonOptions version has per-catalog timeout (10 s), CatalogCommand version does not
- `FindMatchingCatalogAgents` logic (exact ID → exact display name → exact tag → fuzzy substring) duplicated in three places: `CommonOptions`, `CatalogCommand`, `CatalogInputResolver`
- `GetStateIcon` duplicated between `ConsoleFormatter` and `ProgressRenderer`
- `StreamCommand` deserializes `--data` JSON via `Dictionary<string,JsonElement>` + re-serialize, while `SendCommand` uses `JsonSerializer.Deserialize<JsonElement>` directly — SendCommand approach is correct

### SDK Usage
- SharpA2A NuGet: `A2A` (1.0.0-preview2) + `A2A.V0_3Compat` (1.0.0-preview2)
- Client creation: `A2AClientFactory.Create(card, httpClient)` after card fetch, or `new A2AClient(uri, httpClient)` for direct
- V0.3 compat: `V03CompatClientFactory.CreateAsync` / `V03CompatClientFactory.Create`
- Streaming: `client.SendStreamingMessageAsync` returns `IAsyncEnumerable<StreamResponse>`
- `IA2AClient` interface used for abstraction

### Critical Bugs Found
1. `HttpClient` created inside `DeviceCodeFlow` constructor (`_httpClient = httpClient ?? new HttpClient()`) is never disposed — `DeviceCodeFlow` has no `IDisposable`
2. `DeviceCodeFlow.RefreshTokenAsync` and `ClientCredentialsFlow.AuthenticateAsync` both create internal `HttpClient` via `?? new HttpClient()` — never disposed
3. All command handlers receive an `HttpClient` from `AuthConfigurator.CreateHttpClientWithStoredTokenAsync` without `using` — unmanaged resource leak per invocation
4. `TaskCommand list` parses `--status` option but never sends it to `ListTasksRequest`
5. `AuthCodeFlow` hardcodes port 29080; no fallback if port is in use — throws `HttpListenerException`

## Team Review Update - 2026-05-24T22:41:52Z
Scribe completed decision inbox processing and session documentation.
All team findings consolidated into decisions.md and orchestration logs.
