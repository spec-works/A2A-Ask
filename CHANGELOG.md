# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.4.0] - 2026-05-17

### Added
- Catalog aliases for simplified agent targeting
- New `@agentName@catalogAlias` addressing syntax for cross-catalog search
- `@@catalogAlias` syntax for browsing catalogs
- Phase 2 catalog targeting with aliases and addressing syntax

### Changed
- Remove legacy `@@/` and `@` prefix syntax
- Remove CatalogBrowse type in favor of unified catalog interface
- Switch from local A2A SDK to NuGet packages

## [1.3.0] - 2026-05-16

### Added
- `a2a-ask auth register-client` command for persistent OAuth2 client registration
- Support for registering and storing OAuth2 client credentials

### Fixed
- NuGet configuration to remove local ai-catalog source breaking CI
- Direct send and v0.3 client selection logic

## [1.2.0] - 2026-05-16

### Added
- Catalog integration with `a2a-ask catalog` commands
- Support for catalog listing and agent resolution from catalogs

### Fixed
- NuGet package restoration issues

## [1.1.0] - 2026-03-28

### Added
- `a2a-ask auth` command group with full authentication support
- `a2a-ask auth login` for interactive OAuth2 authentication
- `a2a-ask auth logout` for clearing stored tokens
- `a2a-ask auth status` for checking authentication status
- `a2a-ask auth list-clients` and `a2a-ask auth remove-client` for client registration management
- Token store with DPAPI encryption support
- OIDC discovery and client credentials flow
- HTTP Basic authentication support
- Multi-tenant authentication support

### Changed
- Phase 2+3 auth implementation with Duende.IdentityModel

## [1.0.0] - 2026-03-25

### Added
- Initial release of A2A-Ask CLI
- Core commands: `a2a-ask discover`, `a2a-ask send`, `a2a-ask stream`
- Task management: `a2a-ask task get`, `a2a-ask task list`, `a2a-ask task cancel`
- Catalog commands: `a2a-ask catalog list`, `a2a-ask catalog show`, `a2a-ask catalog add`, `a2a-ask catalog remove`
- Version command: `a2a-ask version`
- CI/CD workflows
- DocFX documentation site
- SKILL.md for AI agent integration

## [0.3.0]

### Added
- Support for A2A v0.3 agents with `--a2a-version 0.3` flag

## [0.1.0]

### Added
- Project initialization
