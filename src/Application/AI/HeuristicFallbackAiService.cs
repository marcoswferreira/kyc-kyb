using System.Text;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.AI;

public class HeuristicFallbackAiService : IAiAssistantService
{
    public Task<string> GenerateReportSummaryAsync(
        BackgroundCheck check,
        IReadOnlyList<CheckFinding> findings,
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();

        sb.AppendLine("### 🤖 Parecer Preliminar de Compliance (Decisão Assistida - LGPD Art. 20)");
        sb.AppendLine();
        sb.AppendLine($"**Alvo:** {check.SubjectType} | **Documento:** `{check.DocumentMasked}` | **Finalidade:** {check.Purpose}");
        sb.AppendLine($"**Classificação Preliminar de Risco:** `{check.OverallRisk.ToString().ToUpperInvariant()}`");
        sb.AppendLine();

        sb.AppendLine("#### 📌 Síntese dos Fatos e Evidências Apuradas:");

        if (!findings.Any())
        {
            sb.AppendLine("- Nenhum apontamento ou irregularidade foi detectado pelos provedores consultados.");
        }
        else
        {
            foreach (var finding in findings)
            {
                var emoji = finding.RiskLevel switch
                {
                    RiskIndicator.Red => "🔴",
                    RiskIndicator.Yellow => "🟡",
                    _ => "🟢"
                };

                sb.AppendLine($"- {emoji} **[{finding.Category}]** {finding.Title}");
                sb.AppendLine($"  - *Evidência (Fonte: {finding.ProviderName}):* {finding.Description}");
                sb.AppendLine($"  - *Motivo do Apontamento:* {finding.ExplanationWhy}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("#### ⚖️ Conformidade Regulatória & Direitos do Titular (LGPD Art. 20):");
        sb.AppendLine("> **Aviso Legal:** Em estrito cumprimento ao Artigo 20 da Lei Geral de Proteção de Dados (Lei nº 13.709/2018), ");
        sb.AppendLine("> as avaliações deste sistema têm caráter **estritamente informativo e de suporte à decisão**. ");
        sb.AppendLine("> A plataforma **não toma decisões automatizadas terminativas de reprovação**. ");
        sb.AppendLine("> O deferimento ou indeferimento compete única e exclusivamente à revisão humana de um Oficial de Compliance.");

        return Task.FromResult(sb.ToString());
    }
}

