# Documentation Review — A2A-Ask

**Reviewed by:** Docs Writer  
**Date:** 2026-05-24  
**Scope:** README.md, SKILL.md, plugin setup, changelog, docs-to-code sync

---

## Executive Summary

A2A-Ask documentation is **well-structured and comprehensive overall**, with one major gap (no changelog) and a critical style convention violation (bash syntax instead of PowerShell). All CLI commands are documented; versions are consistent. Recommend prompt fixes to align with Docs Writer charter.

---

## ✅ What's Good

### README.md
- **Structure** follows charter guidelines: description → installation → quick start → CLI reference → auth → advanced
- **Installation** is clear and cross-platform aware
- **Quick start** examples are concrete and immediately usable
- **Commands table** provides at-a-glance reference (11 commands listed)
- **Catalog integration** section explains the target syntax well
- **Authentication** section covers multiple auth methods with examples
- **Multi-turn conversations** documented with clear task-id workflow
- **Streaming** section explains real-time progress
- **Agent skill** section points users to skills installation

### SKILL.md (skill/SKILL.md)
- **YAML frontmatter** is complete and correct
  - `name`: a2a-ask-cli ✅
  - `description`: thorough and accurate ✅
  - `version`: "1.4" matches .csproj version (1.4.0) ✅
  - `license`, `compatibility`, `metadata` all present ✅
- **Comprehensive command reference** with proper tables and examples
- **Prerequisites section** walks through .NET 10 installation per OS
- **Workflow section** provides step-by-step guidance for discovery → messaging → handling responses
- **Catalog commands** (add, remove, list, show) fully documented with target syntax
- **All auth commands documented:**
  - `auth login` (interactive + client credentials flow) ✅
  - `auth logout` ✅
  - `auth status` ✅
  - `auth register-client` ✅
  - `auth list-clients` ✅
  - `auth remove-client` ✅
- **Task commands** (get, list, cancel) complete ✅
- **Error handling guide** with common errors and solutions
- **Global options** clearly documented
- **A2A protocol quick reference** explains agent cards, task states, protocol versions
- **Limitations section** honest about constraints (mTLS, push notifications, etc.)
- **Installation instructions** for multiple platforms (GitHub Copilot CLI, Claude Code, VS Code/Cursor)

### Command Documentation vs Code
**Cross-reference verification:** All commands in Program.cs are documented:
- ✅ `discover` — documented in both README and SKILL.md
- ✅ `catalog` (list, show, add, remove) — fully documented
- ✅ `send` — documented with all options
- ✅ `stream` — documented with subscribe option
- ✅ `task` (get, list, cancel) — documented
- ✅ `auth` (all 6 subcommands) — documented
- ✅ `version` — documented

### Plugin & Version Alignment
- ✅ `.squad/plugin-marketplace.md` exists and documents the plugin system
- ✅ Version consistency: A2A-Ask.csproj (1.4.0) ↔ SKILL.md metadata (1.4) — aligned

### Additional Documentation
- ✅ `docs/` folder with DocFX structure exists
- ✅ `A2A-Ask-CLI-Guide.md` provides supplementary reference
- ✅ `docs/cli-reference.md` offers structured command reference

---

## ⚠️ Gaps (Missing or Incomplete)

### 1. **No Changelog (Critical)**
- **Gap:** No CHANGELOG.md file exists at repository root
- **Impact:** Users have no record of what changed between versions; difficult to understand breaking changes or new features
- **Fix:** Create CHANGELOG.md with entries for v1.4.0 and earlier versions; link from README

### 2. **README missing Auth Commands**
- **Gap:** README.md command table doesn't list `auth logout` and `auth status`
- **Impact:** New users might miss these utility commands
- **Present in code:** AuthCommand.cs defines both ✅
- **Documented in:** SKILL.md ✅ (but not README)
- **Fix:** Add to README command table:
  ```
  | `a2a-ask auth logout <url>` | Remove stored authentication token |
  | `a2a-ask auth status <url>` | Show authentication status |
  ```

### 3. **README Missing Task Commands**
- **Gap:** README.md command table lists `task get` and `task cancel` but omits `task list`
- **Impact:** Users might not know they can list tasks for an agent
- **Present in code:** TaskCommand.cs defines CreateListCommand() ✅
- **Documented in:** SKILL.md ✅ (at line ~343, `a2a-ask task list <url>`)
- **Fix:** Add to README command table:
  ```
  | `a2a-ask task list <url>` | List tasks with optional filtering |
  ```

### 4. **README Skill Installation Section**
- **Gap:** The skill installation command paths are incorrect
- **Current:** Refers to `skills/a2a-ask-cli/SKILL.md` (line 107)
- **Actual location:** `skill/SKILL.md` (singular, not plural)
- **Impact:** Users following instructions will not find the file
- **Fix:** Change all three examples to use `skill/SKILL.md` (not `skills/a2a-ask-cli`)

### 5. **Docs File Synchronization Risk**
- **Gap:** Documentation exists in four places:
  1. README.md (136 lines)
  2. skill/SKILL.md (811 lines) — most comprehensive
  3. A2A-Ask-CLI-Guide.md (200+ lines)
  4. docs/cli-reference.md (partial reference)
- **Impact:** Risk of drift; updates to one file may not propagate to others
- **Recommendation:** 
  - Designate SKILL.md as the source of truth for command reference
  - README should be a high-level intro + quick start + link to SKILL.md for details
  - docs/ folder should reference or embed the main documentation
  - Consider using single source (e.g., templating or link-based references) to avoid copy-paste drift

---

## 🔴 Inaccuracies & Convention Violations

### 1. **PowerShell Syntax Convention Not Followed**
- **Charter requirement:** "Use PowerShell syntax in code blocks (not bash) — cross-platform preference"
- **Current state:**
  - README.md: **All code blocks use `bash`** (7 instances)
  - SKILL.md: **Mostly `bash`** (20+ instances), with only 1 `powershell` block at line 29
  - A2A-Ask-CLI-Guide.md: **Uses `bash`** (mixed examples)
- **Examples that should be PowerShell:**
  ```bash
  # CURRENT (bash)
  mkdir -p .github/skills/a2a-ask-cli
  cp -r skills/a2a-ask-cli/* .github/skills/a2a-ask-cli/
  ```
  Should be:
  ```powershell
  # SHOULD BE (PowerShell)
  New-Item -ItemType Directory -Force -Path ".github\skills\a2a-ask-cli"
  Copy-Item -Path "skill\SKILL.md" -Destination ".github\skills\a2a-ask-cli\SKILL.md"
  ```
- **Impact:** Inconsistent with team style guide; may confuse Windows users
- **Fix:** Convert all bash code blocks to PowerShell equivalents across README, SKILL.md, and related docs

### 2. **Incomplete Flag Documentation in README Quick Start**
- **Gap:** README quick start examples (lines 11–23) use flags not explained until later
  - `--output text` introduced without context
  - Examples show short-form (`-m`) and long-form (`--message`) mixed without explanation
- **Fix:** Either add a brief "Options explained below" note or move to a fuller example with comments

### 3. **Skill Installation Paths Still Reference Old Structure**
- **Location:** SKILL.md lines 774–808
- **Issue:** `skill/SKILL.md` installation examples mix directory structures and may confuse users about where to put the file
- **Fix:** Clarify that `SKILL.md` is a single file, not a directory; ensure examples are consistent

---

## 📋 Recommendations (Prioritized)

### Priority 1 (Critical — Do First)
1. **Create CHANGELOG.md** with v1.4.0 entry and version history
   - Format: Keep It Simple Style (KISS)
   - Link from README
   - Include upgrade instructions if any breaking changes

2. **Fix skill path in README** (line 107–113)
   - Change `skills/a2a-ask-cli/SKILL.md` → `skill/SKILL.md`
   - Verify all three code examples (GitHub Copilot, Claude, VS Code) use correct paths

3. **Add missing commands to README table**
   - Add `a2a-ask auth logout <url>`
   - Add `a2a-ask auth status <url>`
   - Add `a2a-ask task list <url>`

### Priority 2 (High — Convert Syntax)
4. **Convert all code blocks to PowerShell**
   - README.md: Replace 7 `bash` blocks with `powershell`
   - SKILL.md: Replace 20+ `bash` blocks with `powershell` equivalents
   - A2A-Ask-CLI-Guide.md: Convert examples to PowerShell

### Priority 3 (Medium — Consolidation)
5. **Establish single source of truth for documentation**
   - Option A: Make SKILL.md the canonical reference; README/docs link to it
   - Option B: Use a documentation generator (e.g., DocFX) to pull from single source
   - Document which file is authoritative in .squad/decisions

6. **Reconcile docs/ folder with main documentation**
   - Review docs/cli-reference.md for completeness
   - Decide: Is docs/ for DocFX publishing, or is it redundant?

### Priority 4 (Nice to Have)
7. **Add configuration guide** (if users need to customize token storage, cache locations, etc.)
   - Note: Currently documented briefly in auth section; could be expanded
   
8. **Add troubleshooting guide beyond error codes**
   - e.g., "Token expired but auto-refresh failed"
   - e.g., "How to debug connection issues"

---

## Summary Table

| Aspect | Status | Notes |
|--------|--------|-------|
| README structure | ✅ Good | Follows charter format |
| SKILL.md completeness | ✅ Good | All 7 command groups documented |
| Command docs vs code | ✅ Aligned | All Program.cs commands documented |
| Version consistency | ✅ Matched | v1.4.0 (csproj) ↔ v1.4 (SKILL.md) |
| YAML frontmatter | ✅ Valid | Correct metadata |
| PowerShell syntax | 🔴 Violation | Only 1/20+ examples use PowerShell |
| Changelog | 🔴 Missing | No CHANGELOG.md exists |
| README skill paths | 🔴 Wrong | Should be `skill/` not `skills/` |
| README command table | ⚠️ Incomplete | Missing `task list`, `auth logout/status` |
| Plugin setup | ✅ OK | Version aligned; marketplace doc exists |
| Multi-docs sync | ⚠️ Risk | 4 doc sources could diverge |

---

## Files to Update

1. **CHANGELOG.md** — Create (NEW FILE)
2. **README.md** — Edit (3 issues: paths, table, convention)
3. **skill/SKILL.md** — Edit (syntax convention + clarify install paths)
4. **A2A-Ask-CLI-Guide.md** — Edit (syntax convention)
5. **.squad/decisions.md** — Record doc source-of-truth decision


# .NET Dev Code Review — A2A-Ask CLI

**Authored by:** .NET Dev  
**Date:** 2026-05-24  
**Scope:** All `.cs` files under `dotnet/src/A2A-Ask/`

---

## ✅ Strengths

1. **Command structure** — Static `Create()` factory per command, each in its own file. Validators on `send` and `stream` catch missing message content early.
2. **SDK integration** — `A2ACardResolver`, `A2AClientFactory`, `V03CompatClientFactory` used correctly. V0.3 compatibility layer is cleanly abstracted behind `IsV03()`.
3. **Cancellation propagation** — `context.GetCancellationToken()` used consistently in all async paths.
4. **Auth flows** — Device code polling with `slow_down` backoff, auto-refresh on expiry, DPAPI encryption on Windows with Unix file-permission tightening (600). Legacy plaintext-to-DPAPI migration handled gracefully.
5. **Atomic catalog registry writes** — Temp file + `File.Move(overwrite:true)` prevents data loss on write failure.
6. **Layered agent matching** — Exact identifier → exact display name → exact tag → fuzzy substring gives predictable, useful UX.
7. **Target parsing** — `TargetParser` cleanly separates direct URLs, qualified `agent@catalog`, and bare names.
8. **Error surfacing** — All handlers catch `Exception`, write to stderr, set `context.ExitCode = 1`.
9. **Nullable enabled project-wide** — `<Nullable>enable</Nullable>` in project file.
10. **`InternalsVisibleTo`** — Test assemblies can access internals without exposing public API.

---

## ⚠️ Code Issues

### 1. Global option retrieval is fragile and duplicated
Every command handler (10+ instances) contains this identical LINQ block:
```csharp
context.ParseResult.RootCommandResult.Command.Options
    .OfType<Option<string>>().First(o => o.Name == "output")
```
`First()` throws `InvalidOperationException` if the option is absent — brittle under test, and deeply coupled to option naming. This pattern appears at least 10 times.

### 2. `StreamCommand` data deserialization is wrong
`StreamCommand` deserializes `--data` as `Dictionary<string,JsonElement>` then re-serializes:
```csharp
var jsonElement = JsonSerializer.SerializeToElement(
    JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(data));
```
`SendCommand` correctly uses `JsonSerializer.Deserialize<JsonElement>(data)`. The stream command approach loses non-object JSON shapes (arrays, primitives) and adds unnecessary allocation.

### 3. `TaskCommand list` `--status` option is silently ignored
`status` is parsed but the value is never applied to `ListTasksRequest`. Users can supply the option without error but it has no effect.

### 4. Catalog search logic duplicated in three places
`FindMatchingCatalogAgents` (exact ID → display name → tag → fuzzy) appears nearly identically in:
- `CommonOptions.FindMatchingCatalogAgents`
- `CatalogCommand.FindMatchingCatalogAgents`
- `CatalogInputResolver.FindMatchingAgents`

`EncodeTargetComponent` is duplicated in `CommonOptions` and `CatalogCommand`. `GetStateIcon` switch expression is duplicated in `ConsoleFormatter` and `ProgressRenderer`.

### 5. Per-catalog timeout inconsistency
`CommonOptions.TryResolveCatalogAgentsAsync` applies a 10-second per-catalog timeout during cross-catalog search. `CatalogCommand.SearchRegisteredCatalogsForAgentAsync` performs the same cross-catalog search without any timeout — a hung catalog can block the command indefinitely.

### 6. `AuthConfigurator.CreateHttpClient` — conflicting auth headers
If both `--auth-token` and `--api-key` (location=header) are passed, both write `DefaultRequestHeaders.Authorization` and `DefaultRequestHeaders.Add(headerName, ...)` respectively. No validation prevents this combination. The Bearer header from `--auth-token` is set first, then the API key header is added on top.

### 7. `AuthCodeFlow` hardcoded port 29080
The redirect URI and `HttpListener` prefix are hardcoded to port 29080 with no fallback. If another process holds that port, `listener.Start()` throws `HttpListenerException` with no guidance.

### 8. Query-string API key path is silently broken
`AuthConfigurator.CreateHttpClient` prints a warning for `apiKeyLocation=query` but does **not** add the key to requests. Users see a warning but get no actual auth.

### 9. `TokenStore.NormalizeUrl` double-normalization
`BuildStorageKey` normalizes the URL before calling `NormalizeUrl` inside `SaveTokenAsync` / `LoadTokenAsync`, which normalizes again. The pipe-check shortcut in `NormalizeUrl` does not fully compensate — a key built by `BuildStorageKey` is already normalized, so the `Uri` constructor call is redundant.

### 10. `CatalogRegistry.LoadAliases` is synchronous file I/O
Every command that resolves a target calls `LoadAliases()` synchronously. It reads from the file system on the calling thread. While typically fast, this is inconsistent with the async-everywhere pattern and could block in rare scenarios (e.g., network-mounted home directories).

---

## 🔴 Critical Issues

### C1. `HttpClient` leaks in auth flows

**`DeviceCodeFlow` constructor:**
```csharp
_httpClient = httpClient ?? new HttpClient();
```
`DeviceCodeFlow` does not implement `IDisposable`. When the class creates its own `HttpClient` (the common case via `auth login`), the socket is never released.

**`DeviceCodeFlow.RefreshTokenAsync`:**
```csharp
var client = httpClient ?? new HttpClient();
```
Called from `AuthConfigurator.CreateHttpClientWithStoredTokenAsync` on every command invocation when a token needs refresh. Creates a new unmanaged `HttpClient` per refresh with no disposal.

**`ClientCredentialsFlow.AuthenticateAsync`:**
```csharp
var client = httpClient ?? new HttpClient();
```
Same pattern — internally created `HttpClient` is never disposed.

**Recommended fix:** Implement `IDisposable` on `DeviceCodeFlow`, or accept `IHttpClientFactory`, or use `using` blocks around short-lived clients.

### C2. Command handlers never dispose `HttpClient`

`AuthConfigurator.CreateHttpClientWithStoredTokenAsync` returns a `new HttpClient()`. All command handlers (`SendCommand`, `StreamCommand`, `TaskCommand` get/list/cancel, `DiscoverCommand`) receive this client and neither `using` it nor disposing it. Every CLI invocation leaks one socket handle.

The `CatalogCommand` correctly uses `using var httpClient = new HttpClient()` for its own catalog resolution — this pattern should apply everywhere.

### C3. `DiscoverCommand` raw-JSON fallback — `httpClient.GetAsync` without disposal

In the fallback path:
```csharp
var response = await httpClient.GetAsync(cardUrl);
response.EnsureSuccessStatusCode();
```
`response` is a `HttpResponseMessage` that is never disposed. Minor compared to C1/C2 but adds to GC pressure for long-running tool use.

---

## 📋 Refactoring Suggestions (Prioritized)

| Priority | Description |
|----------|-------------|
| 🔴 High | **Fix `HttpClient` disposal** — Use `using` in all command handlers; implement `IDisposable` on `DeviceCodeFlow`; `ClientCredentialsFlow` internal client in `using` block. |
| 🔴 High | **Fix `StreamCommand` data deserialization** — Replace double-roundtrip with `JsonSerializer.Deserialize<JsonElement>(data)` matching `SendCommand`. |
| 🟠 Medium | **Extract `GetGlobalOptions(InvocationContext)`** — Create a helper returning `(string Output, bool Pretty, bool Verbose)` to replace the 10+ LINQ blocks. |
| 🟠 Medium | **Fix `TaskCommand list` status filter** — Wire `--status` value to `ListTasksRequest`. |
| 🟠 Medium | **Consolidate duplicate catalog search** — Move `FindMatchingCatalogAgents`, `EncodeTargetComponent`, `GetStateIcon` into shared helpers. Apply the 10-second timeout in `CatalogCommand`'s cross-catalog search. |
| 🟡 Low | **Port fallback in `AuthCodeFlow`** — Try a range of ports (29080–29090) for the browser callback listener and surface a clear error if all are busy. |
| 🟡 Low | **Fix or remove query-string API key support** — Either implement URL rewriting per request (e.g., via `DelegatingHandler`) or remove the broken code path entirely to avoid misleading users. |
| 🟡 Low | **`CatalogRegistry.LoadAliases` async** — Rename to `LoadAliasesAsync`, convert to `File.ReadAllTextAsync`, propagate `CancellationToken`. |
| 🟡 Low | **`TokenStore.NormalizeUrl` cleanup** — Verify the key contract between `BuildStorageKey` and internal normalization; remove redundant double-normalization. |


# Lead Decision: Architecture & Code Quality Review Findings
**Date:** 2026-05-24  
**Author:** Lead (Darrel Miller / @darrelmiller)  
**Status:** Active — items require action

---

## Summary

Full architecture and code quality review of A2A-Ask CLI completed. Three confirmed bugs and several architectural concerns identified. Prioritized remediation list below.

---

## 🔴 Critical Issues (Bugs)

### 1. `--client-id` / `--client-secret` silently ignored in `send` and `stream`
Both options are declared, parsed, and extracted from the invocation context, but the values are **never passed** to `AuthConfigurator.CreateHttpClientWithStoredTokenAsync`. The method signature doesn't accept them. Users who attempt `a2a-ask send ... --client-id foo --client-secret bar` receive no authentication and no error message.

**Decision:** .NET Dev to either:
- (a) Wire client credentials into `AuthConfigurator.CreateHttpClientWithStoredTokenAsync` so it fetches a token on-the-fly when clientId+clientSecret are provided, or
- (b) Remove these options from `send`/`stream` with a note pointing users to `auth login`.

Prefer option (a) — it's a legitimate use case for non-interactive pipelines.

---

### 2. `--status` filter in `task list` silently dropped
`TaskCommand.CreateListCommand` reads the `--status` option value but never sets it on `ListTasksRequest`. Users get unfiltered results with no indication the filter was ignored.

**Decision:** .NET Dev to map `--status` to `ListTasksRequest` (parse `TaskState` enum from string, set on request). Add validation for unknown status values.

---

### 3. `--binding` option completely ignored
Declared and registered in `send`/`stream` command options, parsed in the handler, but the value is never used. No binding selection occurs.

**Decision:** Either implement binding selection (http vs jsonrpc route through client factory) or remove the option entirely to avoid misleading users. Lead recommends **removing the option for now** until the A2A SDK's binding selection API is stable, rather than shipping a fake option.

---

## ⚠️ Concerns (Worth Addressing Soon)

### 4. Global option access via brittle LINQ — duplicated in every command handler
Every handler contains:
```csharp
context.ParseResult.RootCommandResult.Command.Options
    .OfType<Option<string>>().First(o => o.Name == "output")
```
This pattern is fragile (breaks silently if option name changes), repeated 15+ times across all command files, and impossible to test in isolation.

**Decision:** Extract a `GlobalOptions` helper or use `System.CommandLine`'s binder pattern (`BinderBase<T>`) to pass global state into handlers as typed parameters. Assign to .NET Dev.

---

### 5. `FindMatchingCatalogAgents` logic triplicated
The priority-matching algorithm (exact EntryId → exact DisplayName → exact Tag → fuzzy substring) exists in three places:
- `CommonOptions.FindMatchingCatalogAgents`
- `CatalogCommand.FindMatchingCatalogAgents`
- `CatalogInputResolver.FindMatchingAgents`

They are nearly identical, creating a maintenance hazard.

**Decision:** Consolidate into `CatalogInputResolver.FindMatchingAgents` (already `internal static`). Update callers in CommonOptions and CatalogCommand to delegate to it. Assign to .NET Dev.

---

### 6. `StreamCommand` duplicates `SendCommand.BuildParts()` logic
`StreamCommand`'s inline message-building code for `--message`, `--file`, `--data` is a near-duplicate of `SendCommand.BuildParts()` (which is `internal static`). Also uses a different, unnecessarily complex JSON deserialization path for the data part.

**Decision:** Both commands should call `SendCommand.BuildParts()`. Make it `internal static` accessible from both (already is). Assign to .NET Dev.

---

### 7. `--api-key-location` not wired into `send`/`stream` commands
`CommonOptions.ApiKeyLocation()` exists and `AuthConfigurator.CreateHttpClient` accepts it, but the option is never added to `SendCommand` or `StreamCommand` option lists. Users cannot set cookie or query API key location for those commands.

**Decision:** Add `ApiKeyLocation()` option to `send` and `stream`, pass to `AuthConfigurator`. Note: query-string location is still unimplemented (see below).

---

### 8. Query-string API key is a stub
When `--api-key-location query`, `AuthConfigurator` prints a warning and does nothing. This is an unimplemented feature that produces no auth.

**Decision:** Either implement (requires per-request URL mutation via a `DelegatingHandler`) or remove `query` as a valid value from option completions and validate against it. Do not silently no-op.

---

### 9. `SystemBrowser` (auth code flow) hardcoded to port 29080
If that port is in use the entire interactive login flow fails with a socket error. No fallback to another port.

**Decision:** .NET Dev to try a small range of ports (29080–29090) or use `TcpListener` with port 0 (OS-assigned) and set the redirect URI dynamically.

---

### 10. `RequireHttps = false` in OIDC discovery
`DeviceCodeFlow.DiscoverEndpointsAsync` and `ClientCredentialsFlow.AuthenticateAsync` both disable HTTPS requirement on OIDC discovery. This is useful for local dev agents but reduces security for production agents.

**Decision:** Keep `RequireHttps = false` for now (supports localhost agents), but add a `--insecure` / `--no-https-check` flag that defaults to `false` in a future release, enabling us to enforce HTTPS by default while keeping local dev working.

---

## 📋 Architectural Recommendations (Lower Priority)

### 11. Decompose `CommonOptions`
`CommonOptions` currently contains: option factory methods, target resolution, client factory, V0.3 compat detection, catalog search utilities, URL helpers, and two private record types. It is a utility dumping ground.

**Recommended split:**
- `TargetResolver` — `ResolveTargetAsync`, `SearchAllCatalogsForAgent`, `ResolvedTarget`
- `ClientFactory` — `CreateClientAsync` overloads, `IsV03`, `CreateDirectClient`
- Keep option factory methods in `CommonOptions` (appropriate there)

Assign to .NET Dev when refactoring bandwidth exists.

---

### 12. `DiscoverCommand` creates two `ConsoleFormatter` instances
Minor: the SDK parse path creates `formatter` then the fallback path creates `fmtr`. Should use one instance.

---

### 13. Silent exception swallowing in storage classes
`TokenStore.LoadAllTokensAsync` and `ClientRegistrationStore.LoadAllClientsAsync` both have bare `catch { return []; }` blocks that swallow all non-cryptographic errors (disk full, permission denied) silently. Users get no feedback when storage silently fails.

**Decision:** Log a warning to stderr (conditioned on verbose mode is acceptable) for non-CryptographicException failures.

---

## ✅ Strengths (No Action Needed)

- Clean folder organization: Commands / Auth / Catalog / Output
- v0.3/v1.0 compat gated cleanly via `IsV03()` — consistent across all call sites
- `TargetParser` record hierarchy (`DirectUrl`, `CatalogTarget`, `UnqualifiedName`) — clean discriminated union
- Token storage security: DPAPI on Windows, chmod 600 on Unix, migration from plaintext
- Atomic file writes in `CatalogRegistry` (temp + move pattern)
- Parallel catalog search with per-catalog 10-second timeout
- Multi-tier agent matching (exact ID → display name → tag → fuzzy) — good UX
- `DiscoverCommand` raw JSON fallback for non-conformant agents
- Auth priority hierarchy (explicit > stored+refresh > unauthenticated)
- `ClientRegistrationStore` for pre-registered OAuth2 clients — good enterprise pattern
- Input validation via `command.AddValidator` before handler runs

---

## Assignment Queue

| Priority | Item | Assignee |
|---|---|---|
| 🔴 P0 | Fix --client-id/--client-secret in send/stream | .NET Dev |
| 🔴 P0 | Fix --status filter in task list | .NET Dev |
| 🔴 P0 | Remove or implement --binding option | .NET Dev |
| ⚠️ P1 | Extract global option access pattern | .NET Dev |
| ⚠️ P1 | Deduplicate FindMatchingCatalogAgents | .NET Dev |
| ⚠️ P1 | StreamCommand reuse SendCommand.BuildParts | .NET Dev |
| ⚠️ P2 | Wire --api-key-location into send/stream | .NET Dev |
| ⚠️ P2 | Fix query-string API key stub | .NET Dev |
| ⚠️ P2 | Fix hardcoded port 29080 in SystemBrowser | .NET Dev |
| 📋 P3 | Decompose CommonOptions | .NET Dev |
| 📋 P3 | Fix bare catch in storage classes | .NET Dev |


# Test Coverage Review — 2026-05-24

**Submitted by:** Test Author  
**Reviewed:** 2026-05-24

## Summary

The project has a solid foundation of 124 tests (77 unit + 47 integration), all passing. Unit tests cover the data-layer components well (TokenStore, CatalogRegistry, TargetParser, AuthConfigurator, ConsoleFormatter basics). Integration tests exercise all auth schemes through a real in-process test server using WebApplicationFactory.

However, several high-traffic, user-facing code paths have zero or very thin test coverage.

---

## 📊 Current Test State

| Project | Tests | Framework | Notes |
|---|---|---|---|
| A2A-Ask.Tests | 77 | xUnit, no mocking | Direct tests of real classes |
| A2A-Ask.IntegrationTests | 47 | xUnit + WebApplicationFactory | In-process TestAgentServer |
| **Total** | **124** | | All passing ✅ |

**Coverage estimate:** ~55% of production code paths. Core data layer is ~85%+ covered. Command handlers are ~5% covered.

---

## ✅ What's Well-Tested

- `TargetParser.Parse()` — 11 cases, good edge case coverage
- `CatalogRegistry` — CRUD, validation, atomic writes, case-insensitivity
- `TokenStore` — round-trip, normalization, overwrite, multi-agent, tenant isolation
- `ClientRegistrationStore` — resource matching priority, fallback logic
- `AuthConfigurator.CreateHttpClient()` — 9 cases covering all auth header types
- `ConsoleFormatter` basics — JSON mode, text mode, error output
- `ProgressRenderer` — 7 key streaming states
- Integration: all 10 auth agent types via Discover + Send happy paths
- Integration: multi-turn `input-required` and `auth-required` state flows
- Integration: v0.3 compatibility (direct URL, all four methods)
- Integration: catalog resolution (root, relative URL, multi-agent, tag-based match)

---

## 🔴 Critical Testing Gaps

### Priority 1 — User-Facing Bugs Waiting to Happen

**1. `AuthConfigurator.CreateHttpClientWithStoredTokenAsync()` — stored token path**  
- No test: stored valid token is loaded and used when no explicit auth given  
- No test: expired token with refresh token → refresh is attempted automatically  
- No test: expired token with no refresh token → warning printed, unauthenticated client returned  
- Impact: Token auto-login flow (the `auth login` UX) could silently break

**2. `SendCommand.BuildParts()` — file and JSON data paths**  
- Suggested tests:
  - `BuildParts_MessageOnly_ReturnsSingleTextPart`
  - `BuildParts_WithFile_IncludesFilePart`
  - `BuildParts_WithJsonData_IncludesDataPart`
  - `BuildParts_InvalidJson_Throws`
  - `BuildParts_AllThreeInputs_ReturnsThreeParts`
  - `GetMediaType_KnownExtensions_ReturnCorrectMimeType` (needs visibility)

**3. `CommonOptions.ResolveTargetAsync()` error branches**  
- No test: origin URL with multiple agents → throws informative error with candidates  
- No test: unqualified name with no matching catalogs → error mentioning `catalog add`  
- No test: ambiguous multi-catalog match → throws with qualified suggestions  
- No test: unreachable catalog during cross-catalog search → error names unreachable aliases  
- Impact: Users get unhelpful/incorrect error messages

### Priority 2 — Output Regressions Invisible Without Tests

**4. `ProgressRenderer` missing task states**  
- Not tested: `TaskState.Canceled`, `TaskState.Failed`, `TaskState.Rejected`, `TaskState.Submitted`  
- Not tested: ArtifactUpdate with binary (non-text) Part  
- Suggested tests:
  - `RenderStreamEvent_TextMode_Canceled_ShowsCanceledState`
  - `RenderStreamEvent_TextMode_Failed_ShowsFailedState`
  - `RenderStreamEvent_TextMode_ArtifactUpdate_BinaryPart_ShowsBytesInfo`

**5. `ConsoleFormatter.WriteTask()` — untested entirely**  
- Not tested: task with artifacts → shows artifact list  
- Not tested: task with verbose history → shows message history  
- Not tested: `AuthRequired` state → shows auth hint  
- Not tested: `InputRequired` state → shows task-id reply hint  
- Suggested tests:
  - `WriteTask_CompletedWithArtifacts_ShowsArtifactList`
  - `WriteTask_AuthRequiredState_ShowsLoginHint`
  - `WriteTask_InputRequiredState_ShowsReplyHint`
  - `WriteTask_Verbose_ShowsHistory`

**6. `ConsoleFormatter.WriteAgentCard()` verbose=true**  
- Currently only tested with `verbose: false`  
- Missing: security scheme warnings, skill examples in verbose mode

**7. `ArtifactSaver` edge cases**  
- Not tested: empty artifact list → no files written, no error  
- Not tested: artifact with both `Name` null and `ArtifactId` null  
- Not tested: raw binary part with no `Filename`

### Priority 3 — Command Handlers (Zero Coverage)

**8. `TaskCommand` (task get, task list, task cancel)**  
- All three subcommands have zero tests  
- Integration test with TestAgentServer would be straightforward

**9. `StreamCommand`**  
- No integration test exercises the streaming path through the command handler  
- The TestAgentServer's `/open` agent supports SSE streaming  
- Suggested: `Stream_OpenAgent_NoAuth_RendersEvents`

**10. `DiscoverCommand`**  
- Zero tests  
- Complex with SDK parse → raw JSON fallback path  
- The `--extended` card path is untested

**11. `CatalogCommand` filter functions**  
- `ApplyAgentFilter()` and `ApplyCatalogFilter()` are private but contain branching logic  
- No test for filter that matches by description vs tag vs display name  
- No test for empty filter (returns all)

---

## 📋 Recommended Test Plan (Prioritized)

### Wave 1 — Unit tests for untested production logic (high ROI, fast to write)

1. `SendCommandTests.cs`
   - `BuildParts_MessageOnly_ReturnsSingleTextPart`
   - `BuildParts_WithTextFile_IncludesFilePartWithMediaType`
   - `BuildParts_WithJsonData_IncludesDataPart`
   - `BuildParts_InvalidJson_Throws`
   - `BuildParts_AllInputs_ReturnsThreeParts`

2. `ProgressRendererTests.cs` additions
   - `RenderStreamEvent_TextMode_Canceled_ShowsCanceledState`
   - `RenderStreamEvent_TextMode_Failed_ShowsFailedState`
   - `RenderStreamEvent_TextMode_Submitted_ShowsSubmittedState`
   - `RenderStreamEvent_TextMode_Rejected_ShowsRejectedState`
   - `RenderStreamEvent_TextMode_ArtifactUpdate_BinaryPart_ShowsByteCount`

3. `ConsoleFormatterTests.cs` additions
   - `WriteTask_CompletedWithArtifacts_ShowsArtifactList`
   - `WriteTask_AuthRequiredState_ShowsLoginHint`
   - `WriteTask_InputRequiredState_ShowsTaskIdHint`
   - `WriteTask_Verbose_ShowsHistory`
   - `WriteAgentCard_Verbose_ShowsSkillExamples`
   - `WriteAgentCard_WithSecuritySchemes_ShowsWarning`

4. `ArtifactSaverTests.cs` additions
   - `SaveArtifacts_EmptyList_NoFilesWritten`
   - `SaveArtifacts_RawPartNoFilename_SavesWithFallbackName`

### Wave 2 — Auth and token flow unit tests

5. `AuthConfiguratorStoredTokenTests.cs` (new file)
   - `CreateHttpClientWithStoredToken_ExplicitAuthToken_IgnoresStore`
   - `CreateHttpClientWithStoredToken_NoExplicitAuth_ValidToken_UsesStoredToken`
   - `CreateHttpClientWithStoredToken_NoExplicitAuth_ExpiredTokenWithRefresh_AttemptsRefresh`
   - `CreateHttpClientWithStoredToken_NoExplicitAuth_ExpiredTokenNoRefresh_PrintsWarning`
   - `CreateHttpClientWithStoredToken_NoExplicitAuth_NoToken_ReturnsUnauthenticated`

### Wave 3 — Integration tests for command handlers

6. `StreamIntegrationTests.cs` (new file)
   - `Stream_OpenAgent_NoAuth_RendersStreamEvents`
   - `Stream_BearerAgent_WithToken_RendersEvents`
   - `Stream_Subscribe_RequiresTaskId` (validation)

7. `TaskIntegrationTests.cs` (new file)
   - `TaskGet_ValidTaskId_ReturnsTask`
   - `TaskCancel_ValidTaskId_ReturnsCancelledTask`

8. `CommonOptionsResolveTargetTests.cs` (new file, unit with mocked HTTP)
   - `ResolveTarget_OriginWithMultipleAgents_ThrowsInformativeError`
   - `ResolveTarget_UnqualifiedName_NoRegisteredCatalogs_ThrowsWithHint`

---

## Testability Notes

The code is generally well-structured for testing:
- `TokenStore`, `ClientRegistrationStore`, `CatalogRegistry` all accept a path constructor argument — clean DI seam
- `SendCommand.BuildParts()` is `internal static` — needs `[assembly: InternalsVisibleTo("A2A-Ask.Tests")]` (likely already present via `AssemblyInfo.cs`)
- `AuthConfigurator.CreateHttpClientWithStoredTokenAsync()` reads from the real filesystem — integration-style testing needed (or `TokenStore` injection)
- Command handlers `SetHandler` lambdas are large and closure-heavy — hard to unit test without significant refactoring; integration approach is better

The `Auth`, `Commands`, and `Output` subdirectories inside `A2A-Ask.Tests/` are empty placeholders — they signal intent to add more granular test organization in the future.

