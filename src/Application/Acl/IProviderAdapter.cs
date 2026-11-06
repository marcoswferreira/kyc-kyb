using Domain.Enums;

namespace Application.Acl;

/// <summary>
/// Contrato canônico da Camada Anticorrupção (ACL) para provedores de dados externos.
/// Permite trocar o fornecedor (ex: Serasa por Idwall ou CAF) sem alterar regras de negócio ou domínio.
/// </summary>
public interface IProviderAdapter<TCanonical>
{
    string ProviderKey { get; }
    
    Task<ProviderAdapterResult<TCanonical>> QueryCanonicalAsync(
        string documentNumber,
        SubjectType subjectType,
        CancellationToken cancellationToken = default);
}

