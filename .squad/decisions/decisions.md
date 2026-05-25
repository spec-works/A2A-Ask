# A2A-Ask Decisions Log

## 2026-05-25T16:26:35Z: User directive - Frontmatter key rename

**By:** Darrel (via Copilot)

**Decision:** Rename the frontmatter key from `a2a-ask` to `remote-agent` for agent.md files installed from catalogs. The `remote-agent` key is protocol-agnostic and nests all provenance metadata (catalog URL, entry-id, card-url, card-etag, card-hash, installed-at).

**Rationale:** The key should not be protocol-specific since it could apply to any remote agent technology, not just A2A.

---

## 2026-05-25T20:36:51Z: User directive - Catalog-wide install concerns

**By:** Darrel (via Copilot)

**Decision:** Skeptical of catalog-wide install (`catalog install catalogAlias` installing all agents from a catalog). Feels like it will create a mess that needs cleaning up afterwards. Consider deprioritizing or removing M2 from the spec.

**Rationale:** User feedback — the value proposition of bulk-installing every agent from a catalog is questionable. Single-agent install (M1) is the core use case.

---

## 2026-05-25T20:40:07Z: User directive - Scope and milestone simplification

**By:** Darrel (via Copilot)

**Decision:** Remove M2 (catalog-wide install), M3 (broker), and restrict M4 (scopes) to user scope only (`~/.copilot/agents/`). No repo scope, no org scope.

**Rationale:**
- Catalog-wide install creates mess; single-agent install is the core use case
- Broker adds complexity without clear value
- Shared scope is problematic (imposes on collaborators, breaks for anyone who doesn't have a2a-ask installed)
- Installing an agent is a personal choice — shared scopes remove choice from collaborators
- If someone wants to move an agent from user scope to repo scope, they can do it manually

---

## 2026-05-25: Frontmatter-Based Provenance for A2A-Ask Agent Catalog

**Status:** Decided

**Owner:** Docs Writer (Darrel)

**Overview:**

Restructured the A2A-Ask Copilot CLI integration spec to move agent provenance metadata from a separate bookkeeping file (`.a2a-ask/installed.json`) into the agent file frontmatter under a `remote-agent` key. This change is reflected in four sections:

1. Per-agent bridge template (§6.1) — added `remote-agent` frontmatter structure
2. Bookkeeping section (§6.3) — renamed to "Provenance metadata" and rewritten to explain frontmatter-based approach
3. Telemetry section (§10) — updated to reference frontmatter instead of installed.json
4. Template fill-ins table (§6.1) — added rows documenting the six new frontmatter properties

**Frontmatter structure:**

```markdown
remote-agent:
  catalog: <catalog-url>
  entry-id: <catalog-entry-id>
  card-url: <agent-card-url>
  card-etag: <etag>          # optional — from HTTP ETag header
  card-hash: <sha256-hash>   # optional — SHA-256 of card content
  installed-at: <iso-8601>
```

**Key benefits:**

- Single source of truth: Agent.md contains all metadata needed to track and sync itself
- No sidecar cleanup: `catalog uninstall` deletes one file; no registry entry to remove
- Manual deletion safe: If user manually deletes agent.md, no orphaned state remains
- ETag + Hash dual approach: ETag enables cheap HTTP 304 checks; Hash detects content drift
- Protocol-agnostic: The `remote-agent` convention is not A2A-specific

**Implementation impact:**

- **Install:** Fetch agent card, generate agent.md with frontmatter containing catalog URL, entry ID, card URL, ETag, hash, and install timestamp
- **Sync:** Scan for .md files with `remote-agent` frontmatter, re-fetch cards, compare ETag or hash, rewrite if changed, update `installed-at`
- **Uninstall:** Delete the .md file (no registry cleanup needed)
- **Installed listing:** `catalog installed` scans for .md files with `remote-agent` frontmatter

---

## 2026-05-25: Scope Simplification for A2A-Ask Copilot CLI Integration

**Status:** Decided

**Author:** Docs Writer (on behalf of Darrel Miller)

**Summary:**

Three major architectural changes to reduce complexity:

1. **Remove M2 (catalog-wide install)** — eliminates `--include`, `--exclude`, `--prefix` flags; focus on per-agent install
2. **Remove M3 (broker)** — eliminates `a2a-broker.md`, `catalogs.json`, `--with-broker`, `catalog add/remove` commands
3. **User scope only** — eliminates `--repo`, `--user`, `--org` flags; agents install only to `~/.copilot/agents/`

**Rationale:**

- **Catalog-wide installs:** Encourage spray-and-pray installation; users typically want specific agents
- **Broker:** Per-agent bridge pattern is sufficient for MVP; broker's value prop (runtime agent selection) is niche
- **Multi-scope:** Personal choice principle — agent installation is user preference, not project mandate. User scope respects this and avoids imposing on collaborators

**Implications:**

- Simpler CLI with fewer flags
- Faster MVP: no `catalogs.json` registry, no broker logic, no scope-interaction bugs
- Clear mental model: "Install agents to `~/.copilot/agents/`, they show up in Copilot CLI"
- If users want shared agents: manual copy to `.github/agents/` or commit to `.github-private`

**Milestones reduced:** From 5 (M1–M5) to 2 (M1 single-agent install, M2 lifecycle polish)

---

## 2026-05-25T17:15:15Z: Catalog bridge lifecycle format

**Status:** Decided

**Date:** 2026-05-25T17:15:15.693-04:00

**Decision:**

- Install A2A catalog bridges only into `~/.copilot/agents/`.
- Stamp each generated bridge with a `remote-agent` frontmatter block containing catalog provenance (`catalog`, `entry-id`, `card-url`, `card-etag`, `card-hash`, `installed-at`).
- Regenerate only content between `<!-- a2a:begin-generated -->` and `<!-- a2a:end-generated -->` during `catalog sync`, so user-authored content below the preservation line survives refreshes.

**Rationale:**

- Keeps bridges user-scoped, traceable back to source catalog entry
- Safe to refresh without clobbering hand-edited notes
- Hash/etag provenance gives `catalog sync` a deterministic way to detect when an agent card changed

---

## 2026-05-24T19:03:04Z: .NET Dev Fixes

**Status:** Decided

**Date:** 2026-05-24T19:03:04.203-04:00

**Decision:**

- Remove the unsupported `--binding` option instead of keeping a placeholder transport selector
- Standardize global CLI option access through a shared `GlobalOptions` helper rather than per-handler LINQ lookups
- Keep `--client-id` and `--client-secret` on `send` and `stream`, but honor them by fetching a client_credentials token inline from the resolved agent card
- Support `--api-key-location query` with an HttpClient handler so API keys are actually emitted on outgoing requests

**Rationale:**

- Eliminate silent no-op options and duplicated command plumbing
- Keep authentication behavior aligned with what the CLI advertises
- Centralize option resolution and auth setup for less brittle future command handlers
