# Documentação da Mini-Plataforma KYC/KYB Background Check

Bem-vindo à documentação técnica e arquitetural da **Mini-Plataforma KYC/KYB Background Check**, desenvolvida em **.NET 10** (`net10.0`), C# 14 e PostgreSQL, com arquitetura limpa (Clean Architecture), resiliência nativa, suporte ao novo padrão **CNPJ Alfanumérico (IN RFB nº 2.229/2024)**, conformidade estrita com o **Artigo 20 da LGPD** (Human-in-the-loop) e assistente de IA grounded em evidências auditáveis.

---

## 🗺️ Mapa da Documentação

A documentação está organizada em quatro seções principais:

```
docs/
├── README.md                                  # Este índice geral
├── architecture/                              # Visão e padrões arquiteturais
│   ├── clean-architecture.md                  # Camadas (Domain, Application, Infra, API)
│   └── c4-model.md                            # Diagramas C4 (Context, Container, Component, Dynamic)
├── core-pillars/                              # Mergulhos profundos nos pilares técnicos
│   ├── 01-cpf-cnpj-alfanumerico.md            # Validação e máscara do novo CNPJ Alfanumérico
│   ├── 02-provider-orchestrator-resilience.md # Circuit Breaker, timeout, cache e degradação
│   ├── 03-risk-rules-evidence.md              # Semáforo de risco, 'Por que este item?' e hash SHA-256
│   ├── 04-lgpd-art20-governance.md            # LGPD Art. 20, hash chain imutável e expurgo
│   ├── 05-multi-tenancy-metering.md           # Multi-tenancy, chaves de API, rate limit e métricas
│   ├── 06-ats-integration-webhooks.md         # Integração ATS simulada e webhooks HMAC-SHA256
│   └── 07-ai-assisted-decisions.md            # Assistente Gemini com fallback heurístico auditável
├── api/                                       # Especificação detalhada de interfaces
│   └── api-contracts.md                       # Contratos REST v1, headers, payloads e status codes
└── runbook/                                   # Operação e implantação
    └── operations-and-deploy.md               # Docker Compose, migrações, variáveis e troubleshooting
```

---

## 🚀 Leitura Recomendada por Perfil

| Perfil | Leituras Recomendadas |
|---|---|
| **Arquitetos de Software** | [`clean-architecture.md`](architecture/clean-architecture.md), [`c4-model.md`](architecture/c4-model.md) |
| **Engenheiros de Dados & Regulação** | [`01-cpf-cnpj-alfanumerico.md`](core-pillars/01-cpf-cnpj-alfanumerico.md), [`04-lgpd-art20-governance.md`](core-pillars/04-lgpd-art20-governance.md) |
| **Engenheiros de Confiabilidade (SRE)** | [`02-provider-orchestrator-resilience.md`](core-pillars/02-provider-orchestrator-resilience.md), [`operations-and-deploy.md`](runbook/operations-and-deploy.md) |
| **Desenvolvedores Integradores (ATS / SaaS)** | [`api-contracts.md`](api/api-contracts.md), [`06-ats-integration-webhooks.md`](core-pillars/06-ats-integration-webhooks.md) |
| **Especialistas em IA & Compliance** | [`07-ai-assisted-decisions.md`](core-pillars/07-ai-assisted-decisions.md), [`03-risk-rules-evidence.md`](core-pillars/03-risk-rules-evidence.md) |

---

## 📌 Convenções Adotadas

1. **Clean Architecture e DDD**: O domínio é estritamente isolado de bibliotecas de infraestrutura. Não há dependências de banco de dados ou frameworks HTTP em `Domain` ou `Application`.
2. **Imutabilidade e Não-Repúdio**: Toda evidência coletada recebe hash criptográfico SHA-256 e cada entrada no log de auditoria encadeia o hash do registro anterior (*Cryptographic Hash Chain*).
3. **Privacidade e LGPD**: Minimização de dados, expurgo automático após prazo de retenção (`RetentionUntilUtc`), mascaramento de documentos por perfil de acesso e proibição de decisões automatizadas finais (Art. 20).
4. **Resiliência e Disponibilidade**: Provedores externos simulados possuem isolamento via Circuit Breaker e timeout estrito de 2 segundos. Se um provedor falha, a consulta conclui com aviso (`CompletedWithWarnings`), nunca quebrando a experiência.
5. **Single Type per File**: Todos os tipos (entidades, value objects, enums, interfaces, DTOs, use cases, provedores e workers) são estritamente isolados em seus próprios arquivos, facilitando a navegação e garantindo adesão às boas práticas de arquitetura .NET e análise de código estático (SA1402).

