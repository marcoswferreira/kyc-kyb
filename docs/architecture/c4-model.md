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
        CoreAPI["MiniKycKyb Engine\nOrquestração assíncrona, políticas de risco,\nCamada Anticorrupção (ACL), LGPD por design e IA"]
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
        WebAPI["ASP.NET Core Web API\n[Contêiner Docker / net10.0]\nControllers REST, Inbound Webhooks, Middlewares de Auth (X-API-Key),\nRate Limiting e Mascaramento (Portas 5000 / 8080)"]
        Bus["Channel Message Bus\n[Barramento Assíncrono]\nDesacoplamento de webhooks e esteiras com backpressure"]
        Worker["Check Orchestration Worker\n[HostedService / net10.0]\nExecuta adapters ACL, avalia políticas e IA"]
        PurgeJob["Data Retention Purge Worker\n[BackgroundService / net10.0]\nExpurgo programado de dados pessoais (LGPD)"]
        AuditDispatcher["Domain Event Audit Dispatcher\n[Event Sinks]\nEncadeamento de hash SHA-256 no banco"]
    end

    subgraph DataStore["Camada de Persistência"]
        PostgresDB[("PostgreSQL 17\n[Banco de Dados Relacional]\nDados Pessoais Criptografados (AES-256-GCM),\nTrilha Imutável de Auditoria, Tenants e Métricas")]
    end

    subgraph ExternalServices["Serviços Externos / Mocks"]
        MockProviders["Mocks de Provedores Regulados\n(Receita, Tribunais, PEP, Trabalho Escravo)"]
        Gemini["Google Gemini AI API"]
    end

    ExternalClient -->|"REST v1 / JSON\n[Portas 5000 / 8080]"| WebAPI
    WebAPI -->|"Enfileira no Message Bus"| Bus
    Bus -->|"Processa assincronamente"| Worker
    Worker -->|"Executa via Adapters ACL"| MockProviders
    Worker -->|"Gera síntese explicativa"| Gemini
    Worker -->|"Emite Eventos de Domínio"| AuditDispatcher
    AuditDispatcher -->|"Grava com Hash Chain"| PostgresDB
    WebAPI -->|"Grava e consulta relatórios criptografados"| PostgresDB
```

---

## 🧩 Nível 3: Diagrama de Componentes da Aplicação

```mermaid
flowchart TD
    subgraph ApplicationCore["Application & Domain Core"]
        UC["Use Cases (CreateCheck, GetCheck, ReviewCheck, Purge)"]
        SM["CheckStateMachine (Pending ➔ Processing ➔ Review ➔ Approved/Rejected)"]
        RPE["RiskPolicyEngine (Análise de Risco & Semáforo)"]
        Canon["Modelos Canônicos ACL (Cadastral, Judicial, Sanções, QSA)"]
    end

    subgraph InfraComponents["Infrastructure Components"]
        Adapters["Adapters Canônicos ACL (ReceitaFederal, Tribunais, etc.)"]
        Aes["AesEncryptionService (AES-256-GCM enc:det / enc:rnd)"]
        Sanitizer["PiiSanitizer (Mascaramento de Logs)"]
        Storage["InMemoryAttachmentStorageService (TTL)"]
        Dispatcher["DomainEventDispatcher (SHA-256 Hash-Chain)"]
    end

    UC --> SM
    UC --> RPE
    RPE --> Canon
    Adapters --> Canon
    UC --> Dispatcher
    UC --> Aes
    UC --> Storage
```
