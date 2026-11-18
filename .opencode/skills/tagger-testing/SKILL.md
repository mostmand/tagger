---
name: tagger-testing
description: Unit-test conventions for Tagger. Use whenever writing or changing tests, or adding a class that needs coverage.
---

# Tagger testing rules

Read `AGENTS.md` first. Tests are xUnit v3 and NSubstitute.

## Required shape

```csharp
public sealed class RenameServiceTests
{
    private readonly IFileSystem _fileSystem = Substitute.For<IFileSystem>();
    private readonly IChangeJournal _journal = Substitute.For<IChangeJournal>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly RenameService _sut;

    public RenameServiceTests()
    {
        _sut = new RenameService(_fileSystem, _journal, _clock);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMoveFilesAndRecordJournal_WhenPlanIsValid()
    {
        // Arrange

        // Act

        // Assert
    }
}
```

## Rules

- System under test is always `_sut`.
- Exactly one `[Fact]` behavior per test. Name: `Method_ShouldOutcome_WhenCondition`.
- The three comments `// Arrange`, `// Act`, `// Assert` are mandatory.
- Mock all collaborators with NSubstitute. No real files, clock, or network.
- Pure domain logic still uses `_sut` + AAA, with no mocks if there are no dependencies.
- Assert observable behavior. Use `Received(1)` only for meaningful interactions.
- Cover success, validation failure, and partial-failure paths where relevant.
- No `Thread.Sleep`, no order-dependent tests, no shared fixtures.
- Add one test class per production class in the matching tests project.
