using Tagger.Domain.Journal;

namespace Tagger.Application.Abstractions;

public interface IChangeJournal
{
    Task RecordAsync(ChangeTransaction transaction, CancellationToken cancellationToken = default);

    Task<ChangeTransaction?> GetLastUndoableAsync(CancellationToken cancellationToken = default);

    Task MarkUndoneAsync(Guid transactionId, CancellationToken cancellationToken = default);
}
