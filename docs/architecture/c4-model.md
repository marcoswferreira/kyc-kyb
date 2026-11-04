# C4 Model: Visão Arquitetural da Plataforma KYC/KYB

O modelo C4 (Contexto, Contêineres, Componentes e Código) fornece uma representação gráfica e conceitual da plataforma em múltiplos níveis de abstração.

---

## 🌐 Nível 1: Diagrama de Contexto de Sistema (Context)

Ilustra como a plataforma interage com usuários, sistemas externos e fontes de dados.

```mermaid
flowchart TD
    Officer["Oficial de Compliance / Analista\n[Pessoa]\nRevisa apontamentos e profere decisão humana (Art. 20)"]
    ATS["Sistema ATS (ex: Gupy, Greenhouse)\n[Sistema Externo]\nDispara checagem em mudança de etapa e recebe webhook"]
    ClientApp["Cliente SaaS / ERP / Fintech\n[Sistema Externo]\nConsome a API REST com API Key para KYC/KYB"]

    subgraph Platform["Mini-Plataforma KYC/KYB [Sistema de Software]"]
        CoreAPI["MiniKycKyb Engine\nOrquestração assíncrona, políticas de risco,\nLGPD por design e assistente de IA"]
    end

    RFB["Receita Federal (Simulada)\n[Provedor Externo]"]
    Judicial["Tribunais & Diários Oficiais (Simulado)\n[Provedor Externo]"]
    Watchlists["Listas Restritivas PEP / OFAC / CEIS (Simulado)\n[Provedor Externo]"]
    GeminiAPI["Google Gemini LLM\n[Serviço Externo de IA]"]

    ClientApp -->|"Submete solicitações e consulta relatórios [HTTPS / REST]"| CoreAPI
    ATS -->|"Dispara checagem de candidato [HTTPS / REST]"| CoreAPI
    CoreAPI -->|"Notifica conclusão com HMAC-SHA256 [Webhook HTTPS]"| ATS
    Officer -->|"Revisa evidências e emite parecer [HTTPS / REST]"| CoreAPI

    CoreAPI -->|"Consulta situação cadastral e QSA"| RFB
    CoreAPI -->|"Pesquisa processos e antecedentes"| Judicial
    CoreAPI -->|"Pesquisa sanções e listas restritivas"| Watchlists
    CoreAPI -->|"Solicita resumo explicativo estruturado"| GeminiAPI
```

---

## 📦 Nível 2: Diagrama de Contêineres (Container)

Detalha os serviços executáveis, bancos de dados e canais de mensageria da solução.

```mermaid
flowchart TD
    subgraph ClientZone["Zona do Cliente"]
        ExternalClient["Client Application / ATS"]
    end

    subgraph KycPlatform["Contêiner da Plataforma MiniKycKyb (.NET 10)"]
        WebAPI["ASP.NET Core Web API\n[Contêiner Docker / net10.0]\nControllers REST, Middlewares de Autenticação,\nRate Limiting e Mascaramento"]
        Queue["In-Memory Async Queue\n[Canal Assíncrono]\nDesacoplamento de solicitações e workers"]
        Worker["Check Orchestration Worker\n[HostedService / net10.0]\nExecuta providers em paralelo, avalia políticas e IA"]
        PurgeJob["Data Retention Purge Worker\n[BackgroundService / net10.0]\nExpurgo programado de dados pessoais (LGPD)"]
    end

    subgraph DataStore["Camada de Persistência"]
        PostgresDB[("PostgreSQL 16+\n[Banco de Dados Relacional]\nChecks, Findings, Audit Logs,\nTenants e Métricas de Consumo")]
    end

    subgraph ExternalServices["Serviços Externos / Mocks"]
        MockProviders["Mocks de Provedores Regulados\n(Receita, Tribunais, PEP, Trabalho Escravo)"]
        Gemini["Google Gemini AI API"]
    end

    ExternalClient -->|"REST v1 / JSON\n[Porta 8080]"| WebAPI
    WebAPI -->|"Enfileira checagem"| Queue
    Queue -->|"Processa assincronamente"| Worker
    Worker -->|"Executa com Circuit Breaker e Timeout"| MockProviders
    Worker -->|"Gera síntese explicativa"| Gemini
    WebAPI -->|"Grava e consulta relatórios"| PostgresDB
    Worker -->|"Persiste relatórios e hash chain"| PostgresDB
    PurgeJob -->|"Executa anonimização periódica"| PostgresDB
    Worker -->|"Despacha webhooks assinados"| ExternalClient
```

---

## 🧩 Nível 3: Diagrama de Componentes (Component)

Evidencia a composição interna da aplicação em termos de componentes e injeção de dependências.

```mermaid
flowchart LR
    subgraph MiniKycKyb_API["MiniKycKyb.API"]
        ChecksCtrl["ChecksController"]
        TenantsCtrl["TenantsController"]
        AuthMid["ApiKeyAuthMiddleware"]
        RateMid["RateLimitingMiddleware"]
    end

    subgraph Application_Layer["Application"]
        CreateUC["CreateCheckUseCase"]
        GetUC["GetCheckByIdUseCase"]
        ReviewUC["ReviewCheckUseCase"]
        Engine["RiskPolicyEngine"]
        AiSvc["Gemini / Heuristic AI Service"]
    end

    subgraph Infrastructure_Layer["Infrastructure"]
        Orchestrator["CheckOrchestrator"]
        CB["CircuitBreaker"]
        Cache["MemoryCheckCache"]
        Dispatcher["WebhookDispatcher"]
        EF["KycKybDbContext"]
    end

    ChecksCtrl --> CreateUC
    ChecksCtrl --> GetUC
    ChecksCtrl --> ReviewUC
    CreateUC --> Orchestrator
    Orchestrator --> CB
    Orchestrator --> Cache
    Orchestrator --> Engine
    Orchestrator --> AiSvc
    Orchestrator --> EF
    Orchestrator --> Dispatcher
    GetUC --> EF
    ReviewUC --> EF
```

---

## ⏱️ Nível 4: Diagrama de Sequência Dinâmica (Dynamic)

Representa o ciclo de vida completo de uma checagem disparada pelo ATS até a revisão humana.

```mermaid
sequenceDiagram
    autonumber
    actor ATS as ATS Simulator
    participant API as ChecksController
    participant Q as InMemoryQueue
    participant W as OrchestrationWorker
    participant O as CheckOrchestrator
    participant P as Mock Providers
    participant R as RiskPolicyEngine
    participant AI as Gemini/Heuristic AI
    participant DB as PostgreSQL
    actor Compliance as Oficial de Compliance

    ATS->>API: POST /api/v1/checks (Documento, Finalidade, Consentimento)
    API->>DB: Cria Check em status 'Pending' e registra AuditLog
    API->>Q: Enfileira CheckId
    API-->>ATS: 202 Accepted (CheckId, Protocolo)

    W->>Q: Desconecta CheckId da fila
    W->>O: ExecuteCheckAsync(Check)
    par Consultas Paralelas aos Provedores
        O->>P: ReceitaFederalMockProvider (Timeout 2s, CB)
        O->>P: TribunaisProcessosMockProvider (Timeout 2s, CB)
        O->>P: ListasRestritivasMockProvider (Timeout 2s, CB)
        O->>P: TrabalhoEscravoMockProvider (Timeout 2s, CB)
        O->>P: QsaSociosMockProvider (Timeout 2s, CB)
    end
    P-->>O: Retornos dos provedores com Hash SHA-256 de evidência
    O->>R: EvaluateFindings(Evidências)
    R-->>O: Lista de Findings (Verde, Amarelo, Vermelho + "Por que este item?")
    O->>AI: GenerateSummaryAsync(Findings)
    AI-->>O: Resumo neutro citando evidências (LGPD Art. 20)
    O->>DB: Atualiza Check ('Completed'/'CompletedWithWarnings') e grava Hash Chain
    O->>ATS: Dispara Webhook com HMAC-SHA256 (CheckConcluído)

    Compliance->>API: GET /api/v1/checks/{id}
    API-->>Compliance: 200 OK (Relatório completo com semáforo e IA)
    Compliance->>API: POST /api/v1/checks/{id}/review (Aprovado com Ressalvas, Justificativa)
    API->>DB: Registra Decisão Humana e cria AuditLog
    API-->>Compliance: 200 OK (Conclusão registrada com sucesso)
```

