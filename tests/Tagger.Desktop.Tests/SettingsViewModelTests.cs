using NSubstitute;
using Tagger.Application.Settings;
using Tagger.Desktop.ViewModels;
using Xunit;

namespace Tagger.Desktop.Tests;

public sealed class SettingsViewModelTests
{
    private readonly ISettingsService _settingsService = Substitute.For<ISettingsService>();
    private readonly SettingsViewModel _sut;

    public SettingsViewModelTests()
    {
        _sut = new SettingsViewModel(_settingsService);
    }

    [Fact]
    public void Initialize_ShouldCopySettings_WhenCalled()
    {
        // Arrange
        var settings = AppSettings.Default with { Theme = AppTheme.Light, Recursive = false };

        // Act
        _sut.Initialize(settings);

        // Assert
        Assert.Equal(AppTheme.Light, _sut.Theme);
        Assert.False(_sut.Recursive);
    }

    [Fact]
    public async Task SaveAsync_ShouldReturnFalse_WhenRenamePatternIsInvalid()
    {
        // Arrange
        _sut.Initialize(AppSettings.Default);
        _sut.RenamePattern = "%mystery%";

        // Act
        var saved = await _sut.SaveAsync();

        // Assert
        Assert.False(saved);
        Assert.NotNull(_sut.ErrorMessage);
        await _settingsService.DidNotReceiveWithAnyArgs().SaveAsync(default!, default);
    }

    [Fact]
    public async Task SaveAsync_ShouldPersistSettings_WhenPatternsAreValid()
    {
        // Arrange
        _sut.Initialize(AppSettings.Default);
        _sut.RenamePattern = "%artist% - %title%";
        _sut.AutoTagPattern = "%artist% - %title%";
        _sut.Theme = AppTheme.Light;

        // Act
        var saved = await _sut.SaveAsync();

        // Assert
        Assert.True(saved);
        Assert.NotNull(_sut.UpdatedSettings);
        Assert.Equal(AppTheme.Light, _sut.UpdatedSettings.Theme);
        await _settingsService.Received(1).SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>());
    }
}
