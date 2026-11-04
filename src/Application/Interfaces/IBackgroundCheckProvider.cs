using Domain.Enums;

namespace Application.Interfaces;

public interface IBackgroundCheckProvider
{
    string Name { get; }
    Task<ProviderResult> QueryAsync(string documentNumber, SubjectType subjectType, CancellationToken cancellationToken = default);
}

