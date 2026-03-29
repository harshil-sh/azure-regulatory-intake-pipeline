using RegulatoryIntake.Domain.Events;

namespace RegulatoryIntake.Application.Abstractions;

public interface IIntakeOrchestrator
{
    Task HandleBlobCreatedEventsAsync(
        IReadOnlyCollection<EventGridEventEnvelope<BlobCreatedEventData>> events,
        CancellationToken cancellationToken = default);
}
