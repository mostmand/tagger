using NSubstitute;
using Tagger.Application.Abstractions;
using Tagger.Application.Settings;
using Xunit;

namespace Tagger.Application.Tests;

public sealed class SettingsServiceTests
{
    private readonly ISettingsStore _store = Substitute.For<ISettingsStore>();
    private readonly SettingsService _sut;

    public SettingsServiceTests()
    {
        _sut = new SettingsService(_store);
    }

    [Fact]
    public async Task LoadAsync_ShouldReturnDefaults_WhenStoreHasNoSettings()
    {
        // Arrange
        _store.LoadAsync(Arg.Any<CancellationToken>()).Returns((AppSettings?)null);

        // Act
        var result = await _sut.LoadAsync();

        // Assert
        Assert.Equal(AppSettings.Default.RenamePattern, result.RenamePattern);
        Assert.Equal(AppTheme.Dark, result.Theme);
    }

    [Fact]
    public async Task LoadAsync_ShouldReplaceInvalidPatterns_WhenStoredPatternCannotParse()
    {
        // Arrange
        _store.LoadAsync(Arg.Any<CancellationToken>())
            .Returns(AppSettings.Default with { RenamePattern = "%nope%", AutoTagPattern = "literal" });

        // Act
        var result = await _sut.LoadAsync();

        // Assert
        Assert.Equal(AppSettings.Default.RenamePattern, result.RenamePattern);
        Assert.Equal(AppSettings.Default.AutoTagPattern, result.AutoTagPattern);
    }

    [Fact]
    public async Task SaveAsync_ShouldNormalizeValues_WhenOutOfRange()
    {
        // Arrange
        AppSettings? saved = null;
        await _store.SaveAsync(Arg.Do<AppSettings>(x => saved = x), Arg.Any<CancellationToken>());
        var input = AppSettings.Default with
        {
            Id3Version = 9,
            Padding = 999999,
            Id3v1 = "sometimes"
        };

        // Act
        await _sut.SaveAsync(input);

        // Assert
        Assert.NotNull(saved);
        Assert.Equal(3, saved.Id3Version);
        Assert.Equal(65536, saved.Padding);
        Assert.Equal("auto", saved.Id3v1);
    }
}
