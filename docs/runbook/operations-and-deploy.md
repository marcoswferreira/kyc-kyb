# Runbook: Implantação, Operação e Troubleshooting

Este documento serve como manual operacional para engenheiros DevOps, SREs e administradores de infraestrutura responsáveis por manter a **Mini-Plataforma KYC/KYB Background Check** em ambientes de desenvolvimento, homologação e produção.

---

## 🐳 Implantação com Docker Compose

A forma recomendada de execução completa com banco de dados PostgreSQL é através do `docker-compose.yml` localizado na raiz do repositório:

```bash
# Iniciar a infraestrutura e a API em segundo plano
docker compose up -d --build

# Acompanhar os logs da aplicação em tempo real
docker compose logs -f api

# Parar a aplicação e os contêineres
docker compose down
```

### Arquitetura de Contêineres

- **`postgres`**: Imagem oficial `postgres:16-alpine`, expondo a porta `5432`. Os dados persistem no volume Docker `postgres_data`.
- **`api`**: Contêiner otimizado multi-stage baseado em `mcr.microsoft.com/dotnet/aspnet:10.0`, expondo a porta `8080`.

---

## 💻 Execução Local com .NET 10 CLI (Zero Dependências)

A plataforma conta com fallback automático para banco de dados em memória. Se nenhuma string de conexão PostgreSQL for fornecida, a aplicação inicializa sem necessidade de containers externos:

```bash
# Restaurar dependências
dotnet restore

# Compilar a solução
dotnet build --configuration Release

# Executar a API localmente
dotnet run --project src/MiniKycKyb.API
```

A API inicializará em `http://localhost:5000` (ou porta configurada em `launchSettings.json`).

---

## ⚙️ Variáveis de Ambiente e Configuração

As configurações podem ser injetadas via variáveis de ambiente do sistema operacional ou no arquivo `appsettings.json`:

| Variável de Ambiente | Descrição | Valor Padrão / Exemplo |
| --- | --- | --- |
| `ConnectionStrings__DefaultConnection` | String de conexão para o PostgreSQL | `Host=localhost;Port=5432;Database=kyckyb_db;Username=postgres;Password=postgres` |
| `GEMINI_API_KEY` | Chave de API do Google Gemini (se ausente, ativa o modo heurístico) | `AIzaSy...` |
| `ASPNETCORE_ENVIRONMENT` | Ambiente de execução | `Development` ou `Production` |
| `ASPNETCORE_URLS` | Portas e protocolos de escuta HTTP | `http://+:8080` |

---

## 🗄️ Inicialização e Migrações de Banco de Dados

Ao iniciar a aplicação, a rotina `DatabaseMigrator.MigrateDatabaseAsync()`:

1. Aplica automaticamente quaisquer migrações pendentes no PostgreSQL (`context.Database.MigrateAsync()`).
2. Executa o `DatabaseSeeder`, garantindo a existência do Tenant Mestre e gerando o bloco gênese da trilha imutável de auditoria.

### Credenciais Iniciais Criadas Automaticamente

- **Tenant ID Padrão**: `11111111-1111-1111-1111-111111111111`
- **Master API Key**: `sk_live_master_kyc_default_key_12345`
- **Webhook Secret Padrão**: `whsec_default_master_secret_key_8899`

---

## 🔧 Diagnóstico e Resolução de Problemas (Troubleshooting)

### 1. Erro HTTP 401 Unauthorized

- **Causa**: O cabeçalho `X-Api-Key` está ausente ou a chave fornecida não corresponde ao hash SHA-256 de nenhum Tenant cadastrado.
- **Solução**: Verifique se enviou `X-Api-Key: sk_live_master_kyc_default_key_12345` na requisição.

### 2. Erro HTTP 429 Too Many Requests

- **Causa**: O inquilino excedeu a capacidade do Token Bucket (120 requisições/minuto).
- **Solução**: Aguarde os segundos indicados no cabeçalho `Retry-After: 60` ou ajuste o consumo de chamadas no cliente.

### 3. Falha de Validação na Assinatura do Webhook (HMAC-SHA256)

- **Causa**: O segredo compartilhado no cliente receptor difere do `WebhookSecret` configurado para o Tenant, ou o corpo da requisição sofreu alteração de encoding.
- **Solução**: Calcule o HMAC-SHA256 utilizando os bytes UTF-8 brutos do payload exatamente como recebidos na rede.

### 4. Consultas Retornando `CompletedWithWarnings`

- **Causa**: Um dos provedores governamentais simulados atingiu o timeout de 2 segundos ou disparou o Circuit Breaker.
- **Diagnóstico**: Inspecione o array de `findings` retornado na consulta da checagem para identificar qual provedor indicou indisponibilidade. O sistema continuará operando normalmente para os demais provedores.
