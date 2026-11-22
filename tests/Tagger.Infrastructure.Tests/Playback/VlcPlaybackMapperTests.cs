using LibVLCSharp.Shared;
using Tagger.Application.Playback;
using Tagger.Infrastructure.Playback;
using Xunit;

namespace Tagger.Infrastructure.Tests.Playback;

public sealed class VlcPlaybackMapperTests
{
    [Theory]
    [InlineData(VLCState.Opening, PlaybackState.Opening)]
    [InlineData(VLCState.Buffering, PlaybackState.Opening)]
    [InlineData(VLCState.Playing, PlaybackState.Playing)]
    [InlineData(VLCState.Paused, PlaybackState.Paused)]
    [InlineData(VLCState.Stopped, PlaybackState.Stopped)]
    [InlineData(VLCState.Ended, PlaybackState.Ended)]
    [InlineData(VLCState.Error, PlaybackState.Failed)]
    [InlineData(VLCState.NothingSpecial, PlaybackState.Idle)]
    public void MapState_ShouldTranslateNativeState_WhenStateIsKnown(VLCState native, PlaybackState expected)
    {
        // Arrange

        // Act
        var mapped = VlcPlaybackMapper.MapState(native);

        // Assert
        Assert.Equal(expected, mapped);
    }

    [Theory]
    [InlineData(-10, 0d)]
    [InlineData(0, 0d)]
    [InlineData(55, 0.55d)]
    [InlineData(100, 1d)]
    [InlineData(150, 1d)]
    public void MapVolume_ShouldClamp_WhenNativeVolumeIsOutOfRange(int native, double expected)
    {
        // Arrange

        // Act
        var mapped = VlcPlaybackMapper.MapVolume(native);

        // Assert
        Assert.Equal(expected, mapped);
    }

    [Fact]
    public void NormalizeTime_ShouldReturnZero_WhenDurationIsMinusOne()
    {
        // Arrange

        // Act
        var mapped = VlcPlaybackMapper.NormalizeTime(-1);

        // Assert
        Assert.Equal(TimeSpan.Zero, mapped);
    }

    [Fact]
    public void NormalizeTime_ShouldReturnTimeSpan_WhenDurationIsPositive()
    {
        // Arrange

        // Act
        var mapped = VlcPlaybackMapper.NormalizeTime(1500);

        // Assert
        Assert.Equal(TimeSpan.FromMilliseconds(1500), mapped);
    }

    [Fact]
    public void MapError_ShouldUseReadableMessage_WhenCodecIsUnsupported()
    {
        // Arrange

        // Act
        var message = VlcPlaybackMapper.MapError("no suitable decoder module");

        // Assert
        Assert.Contains("cannot decode", message);
    }

    [Fact]
    public void MapError_ShouldFallBack_WhenMessageIsEmpty()
    {
        // Arrange

        // Act
        var message = VlcPlaybackMapper.MapError(" ");

        // Assert
        Assert.Equal("This file could not be played.", message);
    }
}
