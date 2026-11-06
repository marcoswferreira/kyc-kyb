# Runbook: Implantação, Operação e Troubleshooting

Este documento serve como manual operacional para engenheiros DevOps, SREs e administradores de infraestrutura responsáveis por manter a **Mini-Plataforma KYC/KYB Background Check** em ambientes de desenvolvimento, homologação e produção.

---

## 🐳 Implantação com Docker Compose

A forma recomendada de execução completa com banco de dados PostgreSQL é através do `docker-compose.yml` localizado na raiz do repositório:

```bash
# Iniciar a infraestrutura e a API em segundo plano
docker compose up -d --build

# Acompanhar os logs da aplicação em tempo real
docker compose logs -f kyc-kyb-api

# Parar a aplicação e os contêineres
docker compose down
```

### Arquitetura de Contêineres

- **`kyc-kyb-postgres`**: Imagem oficial `postgres:17-alpine`, expondo a porta `5432`. Usuário padrão `kyc_admin`, banco `kyc_kyb_db`. Os dados persistem no volume Docker `postgres_data`.
- **`kyc-kyb-api`**: Contêiner otimizado multi-stage baseado em `mcr.microsoft.com/dotnet/aspnet:10.0`, expondo as portas **`5000:5000`** e **`8080:8080`**.

---

## 💻 Execução Local com .NET 10 CLI (Zero Dependências)

A plataforma conta com fallback automático para banco de dados em memória. Se nenhuma string de conexão PostgreSQL for fornecida, a aplicação inicializa sem necessidade de containers externos:

```bash
# Restaurar dependências
XDG_RUNTIME_DIR=/tmp dotnet restore MiniKycKyb.slnx

# Compilar a solução
XDG_RUNTIME_DIR=/tmp dotnet build MiniKycKyb.slnx --configuration Release

# Executar a suíte de testes com barreira de cobertura (>80%)
bash .agents/skills/kyc-kyb-standards/scripts/run-coverage.sh

# Executar a API localmente
dotnet run --project src/MiniKycKyb.API
```

A API inicializará em **`http://localhost:5000`** e **`http://localhost:8080`**.

---

## ⚙️ Variáveis de Ambiente e Configuração

As configurações podem ser injetadas via variáveis de ambiente do sistema operacional, Docker Compose ou no arquivo `appsettings.json`:

| Variável de Ambiente | Descrição | Valor Padrão / Exemplo |
| --- | --- | --- |
| `ConnectionStrings__DefaultConnection` | String de conexão para o PostgreSQL | `Host=postgres;Port=5432;Database=kyc_kyb_db;Username=kyc_admin;Password=kyc_secret_password` |
| `Database__Provider` | Provedor de banco (`PostgreSql` ou `InMemory`) | `PostgreSql` |
| `Security__EncryptionKey` | Chave de 256 bits (64 hex chars) para AES-256-GCM | Opcional (possui chave padrão segura) |
| `GEMINI_API_KEY` | Chave de API do Google Gemini (se ausente, ativa o modo heurístico) | `AIzaSy...` |
| `ASPNETCORE_ENVIRONMENT` | Ambiente de execução | `Development` ou `Production` |
| `ASPNETCORE_URLS` | Portas e protocolos de escuta HTTP | `http://+:8080;http://+:5000` |

---

## 🔍 Verificação de Saúde e Diagnóstico Operacional

### 1. Teste de Acesso ao Swagger UI
Navegue no navegador ou via curl:
```bash
curl -I http://localhost:5000/swagger
# Retorna 301 para swagger/index.html e HTTP 200 OK
```

### 2. Validação da Criptografia em Repouso no PostgreSQL
Inspecione diretamente no banco se os CPFs e dados pessoais estão salvos cifrados:
```bash
docker compose exec -T postgres psql -U kyc_admin -d kyc_kyb_db -c 'SELECT "Id", "DocumentNumber", "Name" FROM background_checks LIMIT 3;'
```
*(As colunas devem iniciar com prefixo `enc:det:...` e `enc:rnd:...`)*.

### 3. Validação da Trilha de Auditoria Encadeada (Hash Chain)
Verifique os elos criptográficos SHA-256:
```bash
docker compose exec -T postgres psql -U kyc_admin -d kyc_kyb_db -c 'SELECT "Action", "PreviousHash", "Hash" FROM audit_log_entries LIMIT 5;'
```
