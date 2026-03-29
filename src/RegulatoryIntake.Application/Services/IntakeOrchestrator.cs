using RegulatoryIntake.Application.Abstractions;
using RegulatoryIntake.Domain.Events;
using Microsoft.Extensions.Logging;

namespace RegulatoryIntake.Application.Services;

public sealed class IntakeOrchestrator : IIntakeOrchestrator
{
    private readonly ILogger<IntakeOrchestrator> _logger;

    public IntakeOrchestrator(ILogger<IntakeOrchestrator> logger)
    {
        _logger = logger;
    }

    public Task HandleBlobCreatedEventsAsync(
        IReadOnlyCollection<EventGridEventEnvelope<BlobCreatedEventData>> events,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Received {EventCount} blob-created intake event(s) for orchestration.",
            events.Count);

        return Task.CompletedTask;
    }
}
