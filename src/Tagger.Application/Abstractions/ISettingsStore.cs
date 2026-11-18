using Tagger.Application.Settings;

namespace Tagger.Application.Abstractions;

public interface ISettingsStore
{
    Task<AppSettings?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}
