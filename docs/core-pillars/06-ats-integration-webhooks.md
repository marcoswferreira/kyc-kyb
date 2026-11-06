# Pilar 6: Integração com ATS e Despacho de Webhooks Assinados

Em operações de Recursos Humanos em escala, analistas de recrutamento não devem alternar manualmente entre o sistema de gestão de vagas (ATS - Applicant Tracking System) e portais externos de checagem.

A plataforma **MiniKycKyb** fornece um fluxo integrado orientado a eventos para ATS simulados (como Gupy, Greenhouse, Lever ou Workable), além de um canal para ingestão assíncrona de webhooks de provedores externos.

---

## 🔄 Fluxo de Ponta a Ponta: Candidato Muda de Etapa

```mermaid
sequenceDiagram
    autonumber
    actor Recruiter as Recrutador no ATS
    participant ATS as ATS Simulator (/api/v1/ats)
    participant API as MiniKycKyb API (/api/v1/checks)
    participant Worker as Background Worker
    participant Webhook as Webhook Dispatcher
    participant DB as PostgreSQL

    Recruiter->>ATS: Move candidato para "Etapa 4: Checagem de Segurança"
    ATS->>API: Dispara POST /api/v1/checks com Documento, Finalidade e Consentimento
    API-->>ATS: Retorna 202 Accepted (CheckId gerado)
    ATS->>ATS: Atualiza card do candidato para "Aguardando Checagem"

    Worker->>Worker: Executa provedores em paralelo + Políticas de Risco
    Worker->>DB: Salva resultado e semáforo
    Worker->>Webhook: Dispara Webhook Event (CheckCompleted)
    Webhook->>Webhook: Assina payload com HMAC-SHA256 (Secret do Tenant)
    Webhook->>ATS: POST /api/v1/ats/callback (Payload + X-Signature-SHA256)
    ATS->>ATS: Valida HMAC-SHA256 e atualiza card do candidato (Verde/Amarelo/Vermelho)
    ATS-->>Webhook: 200 OK (Recebido com Sucesso)
```

---

## 📥 Ingestão Assíncrona de Webhooks de Provedores Externos

Provedores modernos de background check (como Idwall, Serasa e CAF) processam checagens complexas de forma assíncrona e notificam via Webhook.

A API disponibiliza o endpoint dedicado:
`POST /api/v1/webhooks/providers/{providerName}`

### Características do Endpoint:
1. **Público com Validação de Assinatura:** Não exige a API Key do Tenant, permitindo chamadas diretas dos servidores dos provedores externos.
2. **Desacoplamento por Mensageria:** O payload recebido é imediatamente enfileirado no `ChannelMessageBus` no tópico `providers.webhooks.{providerName}`, liberando a conexão HTTP com `202 Accepted` em poucos milissegundos.
3. **Sanitização de PII:** Dados sensíveis contidos no payload são anonimizados pelo `PiiSanitizer` antes de qualquer gravação em logs de aplicação.

```json
// Exemplo de Payload Inbound:
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

---

## 🔏 Segurança em Webhooks Outbound: Assinatura HMAC-SHA256

Para evitar ataques de falsificação de requisição (*spoofing*), todas as mensagens enviadas para a URL de webhook do cliente são assinadas criptograficamente no cabeçalho HTTP:

$$\text{Assinatura} = \text{HMAC-SHA256}(\text{TenantSecret}, \text{PayloadJson})$$

Cabeçalhos enviados:
```http
POST /webhook-endpoint HTTP/1.1
Host: client-ats.com
Content-Type: application/json
X-Event-Type: CheckCompleted
X-Timestamp: 1730721600
X-Signature-SHA256: 7f83b1657ff1fc53b92dc18148a1d65dfc2d4b1fa3d677284addd200126d9069
```
