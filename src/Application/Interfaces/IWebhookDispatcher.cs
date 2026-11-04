using Domain.Entities;

namespace Application.Interfaces;

public interface IWebhookDispatcher
{
    Task DispatchAsync(BackgroundCheck check, CancellationToken cancellationToken = default);
}

