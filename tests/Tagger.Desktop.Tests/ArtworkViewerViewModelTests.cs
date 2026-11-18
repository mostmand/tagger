using Tagger.Desktop.ViewModels;
using Xunit;

namespace Tagger.Desktop.Tests;

public sealed class ArtworkViewerViewModelTests
{
    private readonly ArtworkViewerViewModel _sut = new();

    [Fact]
    public void Open_ShouldShowViewerAtFullZoom_WhenCalled()
    {
        // Arrange
        _sut.ZoomBy(-ArtworkViewerViewModel.ZoomStep);
        _sut.ZoomBy(-ArtworkViewerViewModel.ZoomStep);

        // Act
        _sut.Open();

        // Assert
        Assert.True(_sut.IsOpen);
        Assert.Equal(1, _sut.Zoom);
        Assert.Equal("100%", _sut.ZoomLabel);
    }

    [Fact]
    public void Close_ShouldHideViewer_WhenOpen()
    {
        // Arrange
        _sut.Open();

        // Act
        _sut.Close();

        // Assert
        Assert.False(_sut.IsOpen);
    }

    [Fact]
    public void ZoomIn_ShouldIncreaseZoomByOneStep_WhenBelowMaximum()
    {
        // Arrange
        _sut.Open();

        // Act
        _sut.ZoomInCommand.Execute(null);

        // Assert
        Assert.Equal(1 + ArtworkViewerViewModel.ZoomStep, _sut.Zoom);
        Assert.Equal("125%", _sut.ZoomLabel);
        Assert.True(_sut.CanZoomOut);
    }

    [Fact]
    public void ZoomIn_ShouldStopAtMaximum_WhenAlreadyFullyZoomed()
    {
        // Arrange
        _sut.Open();
        _sut.ZoomBy(ArtworkViewerViewModel.MaxZoom);

        // Act
        _sut.ZoomInCommand.Execute(null);

        // Assert
        Assert.Equal(ArtworkViewerViewModel.MaxZoom, _sut.Zoom);
        Assert.False(_sut.CanZoomIn);
    }

    [Fact]
    public void ZoomOut_ShouldStopAtMinimum_WhenAlreadyFullyZoomedOut()
    {
        // Arrange
        _sut.Open();
        _sut.ZoomBy(-ArtworkViewerViewModel.MaxZoom);

        // Act
        _sut.ZoomOutCommand.Execute(null);

        // Assert
        Assert.Equal(ArtworkViewerViewModel.MinZoom, _sut.Zoom);
        Assert.False(_sut.CanZoomOut);
    }

    [Fact]
    public void Reset_ShouldReturnToFullZoom_WhenZoomWasChanged()
    {
        // Arrange
        _sut.Open();
        _sut.ZoomBy(ArtworkViewerViewModel.ZoomStep);

        // Act
        _sut.ResetCommand.Execute(null);

        // Assert
        Assert.Equal(1, _sut.Zoom);
    }
}
