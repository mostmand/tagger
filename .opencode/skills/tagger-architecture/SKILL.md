---
name: tagger-architecture
description: Architecture and SOLID rules for the Tagger codebase. Use when adding projects, services, domain models, CLI/MCP tools, or UI features to Tagger.
---

# Tagger architecture

Read `AGENTS.md` first. This skill is the short operational checklist.

## Layer boundaries

- `Tagger.Domain`: records, enums, validation, pattern rendering/parsing, search, journal models.
  No `Avalonia.*`, no I/O, no DI.
- `Tagger.Application`: interfaces (`ITrackReader`, `ITrackWriter`, `IFileSystem`, `IChangeJournal`,
  `ISettingsStore`, `IClock`, `IImageInspector`) and use cases. No `Avalonia.*`.
- `Tagger.Infrastructure`: adapters only. Map external exceptions to application failures.
- `Tagger.Desktop`: MVVM. View models depend on application interfaces; composition happens in
  `Bootstrapper`/`App.axaml.cs`.
- `Tagger.Cli` and `Tagger.Mcp`: parse input, call application services, serialize output.

## Adding a feature

1. Model the pure concept in Domain (immutable record + small behavior class).
2. Define the use case and any ports in Application. Return structured results.
3. Implement adapters in Infrastructure.
4. Expose it through UI/CLI/MCP without adding logic there.
5. Add a unit test class per new production class, using `_sut` and AAA comments.

## Safety invariants

- Preview before write. `ChangeSetPreviewService` produces field-level diffs.
- Every write/move goes through `IChangeJournal`; `UndoService` must be able to reverse it.
- Artwork updates are explicit: `CoverArtUpdate.Keep`, `.Set(art)`, `.Remove`.
- Rename targets are sanitized per path segment and collision-checked.
- MCP roots are checked by `PathGuard`; never accept arbitrary paths.

## Class shape

- Constructor injection, readonly fields, interfaces only.
- Immutable records for data; no public setters on domain types.
- Small focused classes over facades. If a class exceeds ~150 lines, extract a collaborator.
- No static state except pure catalogs/constants.
