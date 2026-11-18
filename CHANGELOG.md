# Changelog

## 0.1.0 — 2026-11-13

First development preview.

- Avalonia 12 / .NET 11 desktop app with a modern dark-first library, multi-select table, artwork,
  tag inspector, and light theme.
- Domain/application/infrastructure split with interfaces for every side effect.
- Tag read/write via TagLib# with atomic temp-file replacement.
- Field-level change previews before writes.
- JSON change journal with undo for tag patches and renames.
- Rename files from tags and derive tags from filenames, both with previews and collision checks.
- Duplicate detection by artist/title and album/track.
- CLI: `list`, `read`, `write`, `from-filename`, `rename`, `mcp`.
- MCP stdio server with scoped roots: `library_scan`, `tags_read`, `tags_write`, `tags_diff`,
  `files_rename`, `tags_from_filename`, `artwork_set`, `changes_undo`.
- 147 unit tests using xUnit v3, NSubstitute, and the `_sut` AAA pattern.
