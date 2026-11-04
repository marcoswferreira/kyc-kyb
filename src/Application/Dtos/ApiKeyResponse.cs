namespace Application.Dtos;

public record ApiKeyResponse(
    string RawApiKey,
    string Prefix,
    string Warning = "Guarde esta chave em local seguro. Ela não poderá ser exibida novamente.");

