using Tagger.Application.Settings;
using Tagger.Infrastructure.Settings;
using Xunit;

namespace Tagger.Infrastructure.Tests;

public sealed class JsonSettingsStoreTests
{
    [Fact]
    public async Task SaveAsync_ShouldRoundTripSettings_WhenFileIsWritable()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), "tagger-tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "settings.json");
        var _sut = new JsonSettingsStore(path);
        var settings = AppSettings.Default with
        {
            Theme = AppTheme.Light,
            RenamePattern = "%title%",
            LastFolder = "/music"
        };

        try
        {
            // Act
            await _sut.SaveAsync(settings);
            var loaded = await _sut.LoadAsync();

            // Assert
            Assert.NotNull(loaded);
            Assert.Equal(AppTheme.Light, loaded.Theme);
            Assert.Equal("%title%", loaded.RenamePattern);
            Assert.Equal("/music", loaded.LastFolder);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LoadAsync_ShouldReturnNull_WhenFileDoesNotExist()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), "tagger-tests", Guid.NewGuid().ToString("N"), "missing.json");
        var _sut = new JsonSettingsStore(path);

        // Act
        var result = await _sut.LoadAsync();

        // Assert
        Assert.Null(result);
    }
}
