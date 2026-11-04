using System.Net.Http.Json;
using System.Text.Json;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Application.AI;

public class GeminiAiAssistantService : IAiAssistantService
{
    private readonly HttpClient _httpClient;
    private readonly IAiAssistantService _fallbackService;
    private readonly string? _apiKey;
    private readonly ILogger<GeminiAiAssistantService> _logger;

    public GeminiAiAssistantService(
        HttpClient httpClient,
        IAiAssistantService fallbackService,
        string? apiKey,
        ILogger<GeminiAiAssistantService> logger)
    {
        _httpClient = httpClient;
        _fallbackService = fallbackService;
        _apiKey = apiKey;
        _logger = logger;
    }

    public async Task<string> GenerateReportSummaryAsync(
        BackgroundCheck check,
        IReadOnlyList<CheckFinding> findings,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogInformation("GEMINI_API_KEY não informada. Utilizando sintetizador heurístico de evidências.");
            return await _fallbackService.GenerateReportSummaryAsync(check, findings, cancellationToken);
        }

        try
        {
            var systemPrompt = """
                Você é um Assistente Sênior de Compliance e Prevenção à Fraude para background checks (KYC/KYB).
                Diretrizes Mandatórias de LGPD (Art. 20):
                1. NUNCA decida ou afirme 'candidato reprovado' ou 'empresa desqualificada'. Decisões terminativas são de competência humana exclusiva.
                2. Apresente um resumo objetivo, neutro e estritamente factual.
                3. Cite expressamente as evidências, números de processos e fontes informadas.
                4. Conclua sempre ressaltando os pontos de atenção que o analista humano deve verificar antes de deliberar.
                Formate em Markdown claro e profissional.
                """;

            var userPrompt = $"""
                Analise o seguinte background check:
                Alvo: {check.SubjectType}
                Documento Mascarado: {check.DocumentMasked}
                Finalidade: {check.Purpose}
                Classificação de Risco Consolidada: {check.OverallRisk}

                Apontamentos e Evidências:
                {string.Join("\n", findings.Select(f => $"- [{f.RiskLevel}] {f.Category} ({f.ProviderName}): {f.Title} | Evidência: {f.Description} | Motivo: {f.ExplanationWhy}"))}
                """;

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[]
                        {
                            new { text = $"{systemPrompt}\n\n{userPrompt}" }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.2,
                    maxOutputTokens = 800
                }
            };

            var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={_apiKey}";
            var response = await _httpClient.PostAsJsonAsync(endpoint, requestBody, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Chamada à API do Gemini retornou status code {StatusCode}. Usando fallback.", response.StatusCode);
                return await _fallbackService.GenerateReportSummaryAsync(check, findings, cancellationToken);
            }

            var responseJson = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            var candidates = responseJson.GetProperty("candidates");
            if (candidates.GetArrayLength() > 0)
            {
                var text = candidates[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text.Trim();
                }
            }

            return await _fallbackService.GenerateReportSummaryAsync(check, findings, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exceção ao chamar a API do Gemini. Acionando fallback heurístico.");
            return await _fallbackService.GenerateReportSummaryAsync(check, findings, cancellationToken);
        }
    }
}

