# Tagger — Agent Rules

These rules are mandatory for every change in this repository.

## Product

Tagger is a cross-platform, local-first music tag editor (an Mp3tag alternative) built with
Avalonia 12 and .NET 11. The desktop app, CLI, and MCP server are three thin surfaces over one
application core. The product spec lives in `README.md`.

## Architecture

```
Tagger.Domain          pure models, value objects, validation, patterns, search
Tagger.Application     use cases, interfaces, orchestration, previews, undo
Tagger.Infrastructure  file system, TagLib#, JSON settings/journal, image inspection
Tagger.Desktop         Avalonia views, view models, UI adapters (composition root)
Tagger.Cli             command-line surface
Tagger.Mcp             stdio MCP server surface
tests/*                xUnit v3 unit tests
```

Dependency direction is strictly inward:

- Domain references nothing.
- Application references Domain only.
- Infrastructure references Application and Domain.
- Desktop/CLI/MCP reference Application (+ Infrastructure only at the composition root).
- No UI type (`Avalonia.*`, `Bitmap`, `Window`, commands) may appear in Domain or Application.
- View models must not call infrastructure directly. They call interfaces from Application.
- Every surface must call the same application use cases. Never duplicate tag logic in a surface.

## SOLID and class size

- One reason to change per class. Prefer classes under ~150 lines; split facades when they grow.
- Program to interfaces. Constructor injection only; no service locator, no static mutable state.
- Keep domain logic pure so it can be tested without I/O.
- Use immutable records for data crossing layer boundaries.
- Methods should be small and intention-revealing. Avoid regions and comments that restate code.
- Async APIs take `CancellationToken` and use the `Async` suffix.
- Never swallow exceptions. Return structured failures (`TrackOperationFailure`, `Result`) at
  application boundaries and surface them to the user.

## Unit testing — mandatory pattern

Every test class tests one system under test named `_sut`. All dependencies are mocked
(NSubstitute). Tests use xUnit v3 and exactly this shape:

```csharp
[Fact]
public void MethodToTest_ShouldPostConditions_WhenPreConditions()
{
    // Arrange

    // Act

    // Assert
}
```

- The `// Arrange`, `// Act`, and `// Assert` comments are required.
- One behavior per test. Name states the expected post-condition and the pre-condition.
- No sleeps, no real clock, no real file system, no real network.
- Pure domain classes may be tested directly (no collaborators to mock), but still use `_sut`
  and the AAA layout.
- Mock interfaces, not concrete infrastructure. Verify observable outcomes and necessary
  interactions, not implementation details.
- New production class ⇒ new test class with at least happy-path and failure-path coverage.
- Tests must be deterministic and independent; no shared mutable fixtures.

## Avalonia UI

- MVVM with CommunityToolkit.Mvvm source generators. `partial` view models, `[ObservableProperty]`,
  `[RelayCommand]`.
- Compiled bindings are on by default. Every view declares `x:DataType`; no reflection bindings.
- Code-behind may only initialize the view, wire view-only concerns (focus, window chrome), and
  forward events. Business logic belongs in view models.
- Use design tokens from `Themes/Tokens.axaml`; no hard-coded colors in views.
- Modern, dense, keyboard-first UI: dark-first, subtle borders, rounded surfaces, clear hierarchy.
- Every async command must expose busy state and handle failures without crashing the UI.
- The inspector edits a selection; mixed values must be represented explicitly, never guessed.

## Safety

- Tag writes and renames are previewed before they are applied.
- Writes go through the journal and are undoable (tag patches and file moves).
- MCP access is limited to explicitly allowed roots.
- Never delete audio files from the UI, CLI, or MCP surface.
- Preserve unknown frames, existing artwork, and custom fields when writing tags.

## Commands

```bash
dotnet build Tagger.slnx
dotnet test Tagger.slnx
dotnet run --project src/Tagger.Desktop
dotnet run --project src/Tagger.Cli -- list /path/to/music
```

## Definition of done

1. Builds with zero warnings-as-errors regressions.
2. New/changed classes have tests following the required pattern.
3. `dotnet test` passes.
4. UI changes are verified by running the app (or a headless UI test).
5. Domain/Application layers contain no Avalonia references.
