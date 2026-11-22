using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NSubstitute;
using Tagger.Application.Playback;
using Tagger.Desktop.ViewModels;
using Tagger.Desktop.Views;
using Xunit;

namespace Tagger.Desktop.Tests;

/// <summary>
/// Guards the seek-bar interaction. The stock Slider marks pointer events handled
/// on its Thumb/track, so a naive handler never runs; these tests drive real
/// pointer input through a headless window and assert the seek reaches playback.
/// </summary>
public sealed class PlayerBarTests
{
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(120);

    private static readonly HeadlessUnitTestSession Session = HeadlessUnitTestSession.StartNew(typeof(App));

    [Fact(Timeout = 15000)]
    public Task SeekBar_ShouldSeekToPointerPosition_WhenTrackAreaIsClicked()
    {
        return Session.Dispatch(() =>
        {
            // Arrange
            var (window, slider, playback) = ShowPlayerBar();

            try
            {
                // Act
                var origin = slider.TranslatePoint(default, window)!.Value;
                var point = new Point(
                    origin.X + slider.Bounds.Width * 0.75,
                    origin.Y + slider.Bounds.Height / 2);
                window.MouseMove(point);
                window.MouseDown(point, MouseButton.Left);
                window.MouseUp(point, MouseButton.Left);

                // Assert
                playback.Received(1).Seek(Arg.Is<TimeSpan>(value => Math.Abs(value.TotalSeconds - 90) < 5));
            }
            finally
            {
                window.Close();
            }
        }, TestContext.Current.CancellationToken);
    }

    [Fact(Timeout = 15000)]
    public Task SeekBar_ShouldSeekToReleasedPosition_WhenThumbIsDragged()
    {
        return Session.Dispatch(() =>
        {
            // Arrange
            var (window, slider, playback) = ShowPlayerBar();

            try
            {
                var origin = slider.TranslatePoint(default, window)!.Value;
                var y = origin.Y + slider.Bounds.Height / 2;

                // Act
                var start = new Point(origin.X + slider.Bounds.Width * 0.25, y);
                window.MouseMove(start);
                window.MouseDown(start, MouseButton.Left);
                for (var step = 1; step <= 10; step++)
                {
                    window.MouseMove(
                        new Point(origin.X + slider.Bounds.Width * (0.25 + 0.05 * step), y),
                        RawInputModifiers.LeftMouseButton);
                }

                var released = new Point(origin.X + slider.Bounds.Width * 0.75, y);
                window.MouseMove(released, RawInputModifiers.LeftMouseButton);
                window.MouseUp(released, MouseButton.Left);

                // Assert
                playback.Received(1).Seek(Arg.Is<TimeSpan>(value => Math.Abs(value.TotalSeconds - 90) < 5));
            }
            finally
            {
                window.Close();
            }
        }, TestContext.Current.CancellationToken);
    }

    private static (Window Window, Slider Slider, IPlaybackService Playback) ShowPlayerBar()
    {
        var playback = Substitute.For<IPlaybackService>();
        playback.Snapshot.Returns(new PlaybackSnapshot(
            PlaybackState.Playing,
            TimeSpan.FromSeconds(30),
            Duration,
            1d,
            false,
            "song.mp3",
            null));
        playback.CurrentTrack.Returns(TestTrack.Create());
        playback.Repeat.Returns(RepeatMode.Off);

        var viewModel = new PlayerViewModel(playback, new ImmediateUiDispatcher())
        {
            ShowBar = true
        };

        var bar = new PlayerBar { DataContext = viewModel };
        var window = new Window
        {
            Width = 900,
            Height = 240,
            Content = bar
        };

        window.Show();
        window.Activate();
        Dispatcher.UIThread.RunJobs();

        var slider = bar.GetVisualDescendants().OfType<Slider>().First();
        Assert.True(slider.Bounds.Width > 0, "The seek slider was not laid out.");

        return (window, slider, playback);
    }
}
