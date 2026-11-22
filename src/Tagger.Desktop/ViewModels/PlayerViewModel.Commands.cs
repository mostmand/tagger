using CommunityToolkit.Mvvm.Input;
using Tagger.Application.Playback;

namespace Tagger.Desktop.ViewModels;

public sealed partial class PlayerViewModel
{
    [RelayCommand]
    private Task TogglePlayPause() => TogglePlaybackAsync();

    [RelayCommand]
    private Task Next() => NextAsync();

    [RelayCommand]
    private Task Previous() => PreviousAsync();

    [RelayCommand]
    private void Stop()
    {
        _playback.Stop();
        ErrorMessage = null;
    }

    [RelayCommand]
    private void CycleRepeat() => Repeat = Repeat switch
    {
        RepeatMode.Off => RepeatMode.All,
        RepeatMode.All => RepeatMode.One,
        _ => RepeatMode.Off
    };

    [RelayCommand]
    private void ToggleMute() => IsMuted = !IsMuted;
}
