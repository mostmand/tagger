using Tagger.Domain.Tracks;
using Xunit;

namespace Tagger.Domain.Tests;

public sealed class TagValidatorTests
{
    [Fact]
    public void Validate_ShouldReturnError_WhenYearIsOutsideAllowedRange()
    {
        // Arrange
        var tags = new TrackTags([new(TagField.Year, "3010")]);
        var currentYear = 2026;
        var _sut = TagValidator.Validate;

        // Act
        var result = _sut(tags, currentYear);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(TagField.Year, result.Errors.Keys);
    }

    [Fact]
    public void Validate_ShouldReturnError_WhenTrackTotalIsLowerThanTrack()
    {
        // Arrange
        var tags = new TrackTags(
        [
            new(TagField.Track, "8"),
            new(TagField.TrackTotal, "3")
        ]);
        var _sut = TagValidator.Validate;

        // Act
        var result = _sut(tags, 2026);

        // Assert
        Assert.Contains(TagField.TrackTotal, result.Errors.Keys);
    }

    [Fact]
    public void Validate_ShouldBeValid_WhenAllValuesAreReasonable()
    {
        // Arrange
        var tags = new TrackTags(
        [
            new(TagField.Title, "Northern Lights"),
            new(TagField.Year, "2026"),
            new(TagField.Track, "1"),
            new(TagField.TrackTotal, "10"),
            new(TagField.Bpm, "120")
        ]);
        var _sut = TagValidator.Validate;

        // Act
        var result = _sut(tags, 2026);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateField_ShouldReturnError_WhenValueContainsControlCharacter()
    {
        // Arrange
        var _sut = TagValidator.ValidateField;

        // Act
        var error = _sut(TagField.Title, "bad\u0001title", 2026);

        // Assert
        Assert.NotNull(error);
    }
}
