using System.Text.Json;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace Application.Rules;

public class RiskPolicyEngine : IRiskPolicyEngine
{
    public PolicyEvaluationResult Evaluate(Guid checkId, IEnumerable<ProviderResult> providerResults)
    {
        var findings = new List<CheckFinding>();

        foreach (var result in providerResults)
        {
            if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.RawDataJson))
            {
                // Se o provedor falhou ou caiu em timeout, registramos um finding informativo de advertência
                findings.Add(CheckFinding.Create(
                    checkId,
                    result.ProviderName,
                    "DISPONIBILIDADE_PROVEDOR",
                    RiskIndicator.Yellow,
                    $"Provedor {result.ProviderName} Indisponível",
                    result.ErrorMessage ?? "Falha na comunicação com o provedor simulado.",
                    "A consulta a este provedor falhou ou excedeu o timeout. O relatório prosseguiu com resultado parcial para não travar a esteira, mas requer atenção.",
                    $"{result.ProviderName}_partial_failure",
                    result.ErrorMessage ?? "Falha"));
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(result.RawDataJson);
                var root = doc.RootElement;

                switch (result.ProviderName)
                {
                    case "ReceitaFederalMockProvider":
                        EvaluateReceitaFederal(checkId, result.ProviderName, root, findings);
                        break;

                    case "TribunaisProcessosMockProvider":
                        EvaluateTribunais(checkId, result.ProviderName, root, findings);
                        break;

                    case "ListasRestritivasMockProvider":
                        EvaluateListasRestritivas(checkId, result.ProviderName, root, findings);
                        break;

                    case "TrabalhoEscravoMockProvider":
                        EvaluateTrabalhoEscravo(checkId, result.ProviderName, root, findings);
                        break;

                    case "QsaSociosMockProvider":
                        EvaluateQsaSocios(checkId, result.ProviderName, root, findings);
                        break;
                }
            }
            catch (JsonException)
            {
                findings.Add(CheckFinding.Create(
                    checkId,
                    result.ProviderName,
                    "ERRO_INTEGRIDADE_DADOS",
                    RiskIndicator.Yellow,
                    "Resposta do Provedor Não Pôde Ser Processada",
                    "Falha ao interpretar o JSON retornado pelo provedor.",
                    "O payload retornado está mal formatado ou corrompido.",
                    $"{result.ProviderName}_json_error",
                    result.RawDataJson[..Math.Min(100, result.RawDataJson.Length)]));
            }
        }

        // Consolidação do indicador semafórico geral
        var overallRisk = RiskIndicator.Green;
        if (findings.Any(f => f.RiskLevel == RiskIndicator.Red))
        {
            overallRisk = RiskIndicator.Red;
        }
        else if (findings.Any(f => f.RiskLevel == RiskIndicator.Yellow))
        {
            overallRisk = RiskIndicator.Yellow;
        }

        return new PolicyEvaluationResult(overallRisk, findings);
    }

    private static void EvaluateReceitaFederal(Guid checkId, string provider, JsonElement root, List<CheckFinding> findings)
    {
        if (root.TryGetProperty("situacao_cadastral", out var sitElem))
        {
            var situacao = sitElem.GetString()?.ToUpperInvariant() ?? "DESCONHECIDA";

            if (situacao == "REGULAR")
            {
                findings.Add(CheckFinding.Create(
                    checkId,
                    provider,
                    "SITUACAO_CADASTRAL",
                    RiskIndicator.Green,
                    "Situação Cadastral Regular na Receita Federal",
                    "O cadastro encontra-se plenamente ativo e regular perante a autoridade fiscal.",
                    "Nenhuma pendência ou irregularidade cadastral ativa foi detectada na base da Receita Federal.",
                    "rfb_situacao_regular",
                    situacao));
            }
            else if (situacao is "PENDENTE DE REGULARIZACAO" or "SUSPENSA")
            {
                findings.Add(CheckFinding.Create(
                    checkId,
                    provider,
                    "SITUACAO_CADASTRAL",
                    RiskIndicator.Yellow,
                    $"Situação Cadastral {situacao} na Receita Federal",
                    $"O documento apresenta status {situacao}.",
                    "Pode indicar omissão de declarações fiscais ou pendências administrativas leves que demandam regularização.",
                    "rfb_situacao_pendente",
                    situacao));
            }
            else // CANCELADA, NULA, INAPTA, BAIXADA
            {
                findings.Add(CheckFinding.Create(
                    checkId,
                    provider,
                    "SITUACAO_CADASTRAL",
                    RiskIndicator.Red,
                    $"Situação Cadastral Crítica: {situacao}",
                    $"O documento fiscal encontra-se {situacao} na Receita Federal.",
                    "Indica óbito registrado para CPF ou inaptidão/baixa definitiva de CNPJ por irregularidade grave, impedindo contratação ou transação regular.",
                    "rfb_situacao_critica",
                    situacao));
            }
        }
    }

    private static void EvaluateTribunais(Guid checkId, string provider, JsonElement root, List<CheckFinding> findings)
    {
        if (root.TryGetProperty("processos", out var procElem) && procElem.ValueKind == JsonValueKind.Array)
        {
            var totalProcessos = procElem.GetArrayLength();

            if (totalProcessos == 0)
            {
                findings.Add(CheckFinding.Create(
                    checkId,
                    provider,
                    "PROCESSOS_JUDICIAIS",
                    RiskIndicator.Green,
                    "Nada Consta em Tribunais Consultados",
                    "Nenhum processo cível, trabalhista ou criminal localizado para o documento consultado.",
                    "A busca nos tribunais integrados retornou certidão negativa de feitos ajuizados.",
                    "tribunais_nada_consta",
                    "total_processos: 0"));
                return;
            }

            foreach (var p in procElem.EnumerateArray())
            {
                var tipo = p.GetProperty("tipo").GetString() ?? "Civel";
                var numero = p.GetProperty("numero").GetString() ?? "N/A";
                var tribunal = p.GetProperty("tribunal").GetString() ?? "TJ";
                var status = p.GetProperty("status").GetString() ?? "Em andamento";
                var assunto = p.TryGetProperty("assunto", out var ass) ? ass.GetString() : "N/A";

                if (tipo.Equals("Criminal", StringComparison.OrdinalIgnoreCase) && status.Contains("andamento", StringComparison.OrdinalIgnoreCase))
                {
                    findings.Add(CheckFinding.Create(
                        checkId,
                        provider,
                        "PROCESSOS_JUDICIAIS",
                        RiskIndicator.Red,
                        $"Ação Penal em Andamento ({tribunal} - Proc. nº {numero})",
                        $"Processo criminal ativo sobre {assunto}.",
                        "A existência de processo criminal em trâmite acarreta risco de compliance e reputacional elevado, exigindo análise humana especializada dos autos.",
                        $"tribunais_crim_{numero}",
                        $"{tribunal} | {numero} | {assunto}"));
                }
                else if (status.Contains("arquivado", StringComparison.OrdinalIgnoreCase) || tipo.Equals("Trabalhista", StringComparison.OrdinalIgnoreCase))
                {
                    findings.Add(CheckFinding.Create(
                        checkId,
                        provider,
                        "PROCESSOS_JUDICIAIS",
                        RiskIndicator.Yellow,
                        $"Apontamento Processual ({tribunal} - {tipo} nº {numero})",
                        $"Processo {tipo} com status '{status}'. Assunto: {assunto}.",
                        "Processos cíveis ou trabalhistas arquivados ou de menor potencial lesivo. Não configuram impedimento automático sob a LGPD, mas exigem contexto.",
                        $"tribunais_civ_{numero}",
                        $"{tribunal} | {numero} | {status}"));
                }
            }
        }
    }

    private static void EvaluateListasRestritivas(Guid checkId, string provider, JsonElement root, List<CheckFinding> findings)
    {
        bool isPep = root.TryGetProperty("pep", out var pepElem) && pepElem.GetBoolean();
        bool isOfac = root.TryGetProperty("ofac_sanctioned", out var ofacElem) && ofacElem.GetBoolean();
        bool isCeis = root.TryGetProperty("ceis_cnep", out var ceisElem) && ceisElem.GetBoolean();

        if (isOfac)
        {
            findings.Add(CheckFinding.Create(
                checkId,
                provider,
                "LISTAS_RESTRITIVAS",
                RiskIndicator.Red,
                "Sanção Internacional Ativa (OFAC / Sanções Globais)",
                "Documento listado na lista de Sanções da OFAC (Office of Foreign Assets Control).",
                "Indivíduo ou entidade sancionada internacionalmente por envolvimento em financiamento ilícito, lavagem ou terrorismo. Alto risco regulatório e de bloqueio internacional.",
                "ofac_match_true",
                "OFAC SDN MATCH"));
        }

        if (isCeis)
        {
            findings.Add(CheckFinding.Create(
                checkId,
                provider,
                "LISTAS_RESTRITIVAS",
                RiskIndicator.Red,
                "Cadastro de Empresas Inidôneas e Suspensas (CEIS/CNEP - CGU)",
                "Documento listado no cadastro de sanções da Controladoria-Geral da União.",
                "Ente impedido de contratar com a administração pública ou condenado com base na Lei Anticorrupção (Lei 12.846/2013).",
                "cgu_ceis_match_true",
                "CEIS/CNEP MATCH"));
        }

        if (isPep)
        {
            var cargo = root.TryGetProperty("pep_cargo", out var cElem) ? cElem.GetString() : "Cargo público";
            findings.Add(CheckFinding.Create(
                checkId,
                provider,
                "LISTAS_RESTRITIVAS",
                RiskIndicator.Yellow,
                $"Pessoa Politicamente Exposta (PEP): {cargo}",
                "O titular ou seus sócios exercem ou exerceram função pública relevante nos últimos 5 anos.",
                "A qualificação como PEP não é um demérito, mas exige medidas de diligência reforçada (Enhanced Due Diligence) perante o Banco Central e COAF.",
                "pep_match_true",
                $"Cargo: {cargo}"));
        }

        if (!isPep && !isOfac && !isCeis)
        {
            findings.Add(CheckFinding.Create(
                checkId,
                provider,
                "LISTAS_RESTRITIVAS",
                RiskIndicator.Green,
                "Ausência de Sanções em Listas Restritivas",
                "Consulta negativa em listas PEP, OFAC, CEIS e CNEP.",
                "Nenhum vínculo restritivo foi encontrado nas bases de monitoramento de sanções e pessoas expostas politicamente.",
                "listas_clean",
                "Ausente"));
        }
    }

    private static void EvaluateTrabalhoEscravo(Guid checkId, string provider, JsonElement root, List<CheckFinding> findings)
    {
        bool constaSujo = root.TryGetProperty("consta_lista_suja", out var lElem) && lElem.GetBoolean();

        if (constaSujo)
        {
            var estabelecimento = root.TryGetProperty("estabelecimento", out var est) ? est.GetString() : "Estabelecimento não especificado";
            findings.Add(CheckFinding.Create(
                checkId,
                provider,
                "DIREITOS_HUMANOS",
                RiskIndicator.Red,
                "Inclusão no Cadastro de Empregadores (Lista Suja do Trabalho Escravo)",
                $"Documento consta no cadastro do Ministério do Trabalho e Emprego por submissão a condições análogas às de escravo. Local: {estabelecimento}.",
                "Grave violação de direitos humanos e critérios ESG com risco severo de dano à imagem e responsabilização jurídica solidária.",
                "trabalho_escravo_match",
                estabelecimento ?? "Alvo listado"));
        }
        else
        {
            findings.Add(CheckFinding.Create(
                checkId,
                provider,
                "DIREITOS_HUMANOS",
                RiskIndicator.Green,
                "Nada Consta na Lista Suja do Trabalho Escravo",
                "Ausência de registros na base do Ministério do Trabalho e Emprego.",
                "O documento não possui autuações ou inclusões no cadastro público de empregadores que submeteram trabalhadores a condições análogas às de escravo.",
                "trabalho_escravo_clean",
                "Não consta"));
        }
    }

    private static void EvaluateQsaSocios(Guid checkId, string provider, JsonElement root, List<CheckFinding> findings)
    {
        if (root.TryGetProperty("socios", out var sElem) && sElem.ValueKind == JsonValueKind.Array)
        {
            var count = sElem.GetArrayLength();
            bool hasPepSocio = false;

            foreach (var socio in sElem.EnumerateArray())
            {
                if (socio.TryGetProperty("is_pep", out var pElem) && pElem.GetBoolean())
                {
                    hasPepSocio = true;
                    var nome = socio.GetProperty("nome").GetString() ?? "Sócio";
                    findings.Add(CheckFinding.Create(
                        checkId,
                        provider,
                        "QUADRO_SOCIETARIO",
                        RiskIndicator.Yellow,
                        $"Sócio Vinculado como PEP: {nome}",
                        "Membro do Quadro de Sócios e Administradores (QSA) qualificado como Pessoa Politicamente Exposta.",
                        "Requer aplicação de diligência aprofundada (EDD) sobre a composição societária para conformidade com normas de PLD-FT.",
                        $"qsa_pep_{nome}",
                        nome));
                }
            }

            if (!hasPepSocio)
            {
                findings.Add(CheckFinding.Create(
                    checkId,
                    provider,
                    "QUADRO_SOCIETARIO",
                    RiskIndicator.Green,
                    $"Quadro Societário Verificado ({count} sócios)",
                    "QSA validado sem apontamentos restritivos entre os administradores.",
                    "Nenhum administrador ou sócio apresenta restrições imediatas identificadas no cruzamento de dados sintéticos.",
                    "qsa_clean",
                    $"Total socios: {count}"));
            }
        }
    }
}

