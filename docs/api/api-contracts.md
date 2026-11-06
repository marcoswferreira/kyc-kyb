# Especificação de Contratos de API REST v1

A API da **Mini-Plataforma KYC/KYB Background Check** segue os padrões arquiteturais RESTful, utiliza formato JSON para troca de mensagens e implementa a RFC 7807 (Problem Details) para respostas de erro.

Cada tipo de requisição, resposta e modelo de dados está organizado segundo o princípio **Single Type per File** no namespace `Application.Dtos`.

---

## 🔑 Cabeçalhos HTTP Comuns

| Cabeçalho | Obrigatório? | Descrição | Exemplo |
|---|---|---|---|
| `X-Api-Key` | **Sim** (exceto criação de Tenant) | Chave de API do inquilino SaaS | `sk_live_master_kyc_default_key_12345` |
| `X-User-Role` | Opcional (Default: `ComplianceOfficer`) | Papel RBAC do solicitante | `Admin`, `ComplianceOfficer`, `Operator`, `Auditor` |
| `Content-Type` | Sim (em requisições com corpo) | Tipo de mídia da requisição | `application/json` |

---

## 📋 Sumário de Endpoints

| Método | Rota | Descrição | DTO de Entrada | DTO de Saída |
|---|---|---|---|---|
| `POST` | `/api/v1/checks` | Dispara uma nova checagem cadastral assíncrona | `CreateCheckRequest` | `CreateCheckResponse` |
| `GET` | `/api/v1/checks/{id}` | Consulta o relatório com semáforo e parecer de IA | - | `CheckDetailsResponse` |
| `POST` | `/api/v1/checks/{id}/review` | Registra a decisão humana formal (LGPD Art. 20) | `ReviewCheckRequest` | `ReviewCheckResponse` |
| `POST` | `/api/v1/checks/purge-expired` | Executa o expurgo manual de dados retidos | - | `PurgeResult` |
| `POST` | `/api/v1/tenants` | Cria um novo inquilino SaaS | `CreateTenantRequest` | `TenantResponse` + `ApiKeyResponse` |
| `GET` | `/api/v1/tenants/me` | Obtém informações do inquilino autenticado | - | `TenantResponse` |
| `POST` | `/api/v1/tenants/{id}/api-keys` | Rotaciona/gera uma chave de API para o inquilino | - | `ApiKeyResponse` |
| `POST` | `/api/v1/tenants/webhooks` | Cadastra assinatura de webhook para notificações | `RegisterWebhookRequest` | `WebhookSubscription` |
| `POST` | `/api/v1/webhooks/providers/{providerName}` | Ingestão assíncrona de webhooks de provedores (Idwall, Serasa, CAF, RFB) | `ProviderWebhookPayload` | `ProviderWebhookResponse` |
| `GET` | `/api/v1/metering` | Consulta consumo e bilhetagem do inquilino | - | `UsageMetricResponse` |
| `GET` | `/api/v1/audit` | Consulta a trilha imutável encadeada de auditoria | - | `AuditLogDto[]` |
| `POST` | `/api/v1/ats/trigger-candidate-stage` | Simula transição de etapa no ATS integrado | `AtsTriggerRequest` | `AtsTriggerResponse` |
| `POST` | `/api/v1/ats/callback` | Endpoint simulado de escuta de webhooks do ATS | `AtsWebhookPayload` | `AtsCallbackResponse` |
| `GET` | `/api/v1/ats/received-callbacks` | Lista os webhooks recebidos pelo ATS simulado | - | `AtsReceivedEvent[]` |

---

## 🔍 Detalhamento das Requisições e Respostas

### 1. Disparar Checagem: `POST /api/v1/checks`
Cria e enfileira uma verificação assíncrona.

**Exemplo de Payload (Pessoa Física com CPF):**
```json
{
  "document": "529.982.247-25",
  "subjectType": 1,
  "name": "Mariana Silva",
  "purpose": 1,
  "legalBasis": 1,
  "consentObtained": true,
  "consentTimestamp": "2026-11-04T12:00:00Z",
  "webhookUrl": "http://localhost:5000/api/v1/ats/callback",
  "customReference": "VAGA-DEV-SR-042",
  "retentionDays": 30
}
```

> **Valores de Enum:**
> - `subjectType`: `1` (Person), `2` (Company)
> - `purpose`: `1` (RecrutamentoESelecao), `2` (PrevencaoAFraude), `3` (ComplianceKyb), `4` (ConcessaoDeCredito), `5` (OnboardingParceiro)
> - `legalBasis`: `1` (Consentimento), `2` (ObrigacaoLegal), `3` (ExercicioRegularDireito), `4` (LegitimoInteresse), `5` (PrevencaoAFraude)

**Exemplo de Payload (Pessoa Jurídica com o novo CNPJ Alfanumérico):**
```json
{
  "document": "12.ABC.345/0001-88",
  "subjectType": 2,
  "name": "Alpha Beta Logística S.A.",
  "purpose": 3,
  "legalBasis": 1,
  "consentObtained": true,
  "consentTimestamp": "2026-11-04T12:00:00Z"
}
```

**Resposta de Sucesso (`202 Accepted`):**
```json
{
  "id": "fc812c34-4e66-4ad6-84e8-338e83dee2f0",
  "tenantId": "11111111-1111-1111-1111-111111111111",
  "documentMasked": "***.982.247-**",
  "subjectType": 1,
  "status": 1,
  "createdAt": "2026-11-04T12:00:00Z",
  "message": "Solicitação de background check registrada com sucesso na fila assíncrona."
}
```

---

### 2. Consultar Checagem: `GET /api/v1/checks/{id}`
Retorna o relatório completo, indicadores e parecer da IA. O número do documento é automaticamente mascarado caso o cabeçalho `X-User-Role` não seja `ComplianceOfficer` ou `Admin`.

**Resposta de Sucesso (`200 OK`):**
```json
{
  "id": "fc812c34-4e66-4ad6-84e8-338e83dee2f0",
  "tenantId": "11111111-1111-1111-1111-111111111111",
  "subjectType": 1,
  "documentNumber": "***.982.247-**",
  "documentFormatted": "***.982.247-**",
  "documentMasked": "***.982.247-**",
  "name": "Mariana Silva",
  "purpose": 1,
  "legalBasis": 1,
  "consentObtained": true,
  "consentTimestamp": "2026-11-04T12:00:00Z",
  "status": 3,
  "overallRisk": 1,
  "humanReviewStatus": 1,
  "humanReviewerId": null,
  "humanReviewNotes": null,
  "humanReviewedAt": null,
  "retentionUntil": "2026-12-04T12:00:00Z",
  "isPurged": false,
  "evidenceSha256": "4fdc758e28ca10f2fe38c5f43bdcbf67cf9ff7193b48cb0a3a2f3414ff8d666c",
  "aiSummary": "### 🤖 Parecer Preliminar de Compliance (Decisão Assistida - LGPD Art. 20)\n\n**Alvo:** Person | **Documento:** `***.982.247-**` | **Finalidade:** RecrutamentoESelecao\n**Classificação Preliminar de Risco:** `GREEN`\n\n#### 📌 Síntese dos Fatos e Evidências Apuradas:\n- 🟢 **[SITUACAO_CADASTRAL]** Situação Cadastral Regular na Receita Federal (Fonte: ReceitaFederalMockProvider)\n- 🟢 **[PROCESSOS_JUDICIAIS]** Nada Consta em Tribunais Consultados (Fonte: TribunaisProcessosMockProvider)\n- 🟢 **[LISTAS_RESTRITIVAS]** Ausência de Sanções em Listas Restritivas (Fonte: ListasRestritivasMockProvider)\n- 🟢 **[DIREITOS_HUMANOS]** Nada Consta na Lista Suja do Trabalho Escravo (Fonte: TrabalhoEscravoMockProvider)\n\n---\n*AVISO LEGAL (LGPD Art. 20): Este parecer gerado por inteligência artificial é estritamente informativo...*",
  "createdAt": "2026-11-04T12:00:00Z",
  "completedAt": "2026-11-04T12:00:03Z",
  "findings": [
    {
      "id": "b3e04289-...",
      "providerName": "ReceitaFederalMockProvider",
      "category": "SITUACAO_CADASTRAL",
      "riskLevel": 1,
      "title": "Situação Cadastral Regular na Receita Federal",
      "description": "O documento consultado apresenta situação cadastral REGULAR.",
      "explanationWhy": "Nenhuma pendência ou irregularidade cadastral ativa foi detectada na base da Receita Federal.",
      "evidenceKey": "SITUACAO_CADASTRAL",
      "rawExcerpt": "{\"provedor\":\"ReceitaFederalMockProvider\",\"documento\":\"52998224725\",\"situacao_cadastral\":\"REGULAR\"}",
      "createdAt": "2026-11-04T12:00:03Z"
    }
  ]
}
```

---

### 3. Revisão Humana Obrigatória: `POST /api/v1/checks/{id}/review`
Registra a decisão final tomada por um analista humano em atendimento ao Artigo 20 da LGPD.

**Cabeçalho Exigido:** `X-User-Role: ComplianceOfficer`

**Exemplo de Payload (`ReviewCheckRequest`):**
```json
{
  "reviewerId": "oficial.compliance@empresa.com",
  "decision": 2,
  "notes": "Candidato aprovado com ressalva referente a processo cível sem repercussão direta na atividade-fim."
}
```

> **Valores de Enum para `decision` (`HumanReviewDecision`):**
> - `1`: `AwaitingReview` (Pendente de Revisão)
> - `2`: `ApprovedByHuman` (Aprovado pelo Oficial)
> - `3`: `RejectedByHuman` (Reprovado pelo Oficial)
> - `4`: `AdditionalInformationRequested` (Solicitadas Mais Informações)

**Resposta de Sucesso (`200 OK`):**
```json
{
  "checkId": "fc812c34-4e66-4ad6-84e8-338e83dee2f0",
  "decision": 2,
  "reviewerId": "oficial.compliance@empresa.com",
  "reviewedAt": "2026-11-04T12:05:00Z",
  "message": "Revisão humana registrada com sucesso e em conformidade com o Art. 20 da LGPD."
}
```

---

### 4. Gestão de Inquilinos, Chaves e Webhooks

#### Cadastrar Tenant: `POST /api/v1/tenants`
```json
{
  "name": "Fintech Horizonte S.A.",
  "slug": "fintech-horizonte",
  "rateLimitPerMinute": 120
}
```
**Resposta (`201 Created`):**
```json
{
  "tenant": {
    "id": "e91a2b3c-...",
    "name": "Fintech Horizonte S.A.",
    "slug": "fintech-horizonte",
    "apiKeyPrefix": "sk_live_e91a",
    "rateLimitPerMinute": 120,
    "isActive": true,
    "createdAt": "2026-11-04T12:00:00Z"
  },
  "api_key": {
    "rawApiKey": "sk_live_e91a2b3c4d5e6f...",
    "prefix": "sk_live_e91a",
    "warning": "Guarde esta chave em local seguro. Ela não poderá ser exibida novamente."
  }
}
```

#### Cadastrar Webhook: `POST /api/v1/tenants/webhooks`
```json
{
  "targetUrl": "https://minha-empresa.com/webhooks/kyc",
  "events": "check.completed,check.failed"
}
```

---

### 5. Medição de Consumo SaaS: `GET /api/v1/metering`
Retorna as métricas de faturamento e chamadas do tenant no período:
```json
{
  "tenantId": "11111111-1111-1111-1111-111111111111",
  "yearMonth": "2026-11",
  "totalChecks": 15,
  "totalProviderCalls": 75,
  "billableUnits": 75,
  "updatedAt": "2026-11-04T12:05:00Z"
}
```

---

### 7. Ingestão Assíncrona de Webhooks de Provedores: `POST /api/v1/webhooks/providers/{providerName}`
Recebe retornos assíncronos de parceiros de background check (Idwall, Serasa, CAF, Receita Federal).
- **Autenticação:** Pública (Bypass de `X-API-Key` no middleware; validação por assinatura no payload).
- **Processamento:** Enfileirado no `ChannelMessageBus` sem bloqueio.

**Exemplo de Payload:**
```json
{
  "eventId": "evt_idwall_98765",
  "providerName": "Idwall",
  "checkReferenceId": "0094c220-2fba-4327-a7ad-a44bb03225a9",
  "documentNumber": "52998224725",
  "status": "APPROVED",
  "data": { "protocol": "IDW-2026-999", "score": 920 },
  "signature": "hmac_sha256_mock_valid_signature",
  "timestamp": "2026-11-06T13:14:00Z"
}
```

**Resposta HTTP 202 Accepted:**
```json
{
  "acknowledged": true,
  "message": "Webhook do provedor idwall aceito e enfileirado com sucesso para processamento assíncrono.",
  "ingestionReceiptId": "48edd9edb86a4a6a9bbdb49cf0261a49",
  "processedAt": "2026-11-06T13:14:00.830Z"
}
```

---

### 8. Perfil do Inquilino Autenticado: `GET /api/v1/tenants/me`
Retorna as configurações do inquilino associado à chave de API informada no header `X-API-Key` ou Bearer Token.

**Resposta HTTP 200 OK:**
```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "name": "Master Compliance Corp (Default Tenant)",
  "slug": "master-compliance",
  "apiKeyPrefix": "sk_live_master_",
  "rateLimitPerMinute": 120,
  "isActive": true,
  "createdAt": "2026-11-04T12:00:00Z"
}
```

