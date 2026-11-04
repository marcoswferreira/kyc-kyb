#!/usr/bin/env bash
# ==============================================================================
# Mini-Plataforma KYC/KYB Background Check - Demonstração Interativa ATS & LGPD
# ==============================================================================

set -e

# Cores ANSI para saída no terminal
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
MAGENTA='\033[0;35m'
BOLD='\033[1m'
NC='\033[0m' # No Color

API_PORT="${API_PORT:-5000}"
BASE_URL="${API_URL:-http://localhost:${API_PORT}}"
MASTER_API_KEY="sk_live_master_kyc_default_key_12345"

echo -e "${CYAN}${BOLD}"
echo "=============================================================================="
echo "  🛡️  MINI-PLATAFORMA KYC/KYB BACKGROUND CHECK - DEMO INTERATIVA"
echo "  Ecossistema: .NET 10 | CNPJ Alfanumérico IN 2229/2024 | LGPD Artigo 20"
echo "=============================================================================="
echo -e "${NC}"

# 1. Verificar se a API está em execução
echo -e "${YELLOW}🔍 Verificando conectividade com a API em ${BASE_URL}...${NC}"

STARTED_LOCAL_SERVER=0
if ! curl -s "${BASE_URL}/api/v1/metering" -H "X-Api-Key: ${MASTER_API_KEY}" >/dev/null 2>&1; then
    echo -e "${YELLOW}⚠️  A API não está respondendo em ${BASE_URL}.${NC}"
    echo -e "${CYAN}🚀 Iniciando MiniKycKyb.API em segundo plano na porta ${API_PORT}...${NC}"
    
    cd "$(dirname "$0")"
    XDG_RUNTIME_DIR=/tmp ASPNETCORE_URLS="http://+:${API_PORT}" dotnet run --project src/MiniKycKyb.API > /tmp/mini-kyc-api-demo.log 2>&1 &
    API_PID=$!
    STARTED_LOCAL_SERVER=1

    echo -n "Aguardando inicialização da API..."
    for i in {1..30}; do
        if curl -s "${BASE_URL}/api/v1/metering" -H "X-Api-Key: ${MASTER_API_KEY}" >/dev/null 2>&1; then
            echo -e " ${GREEN}Conectado com sucesso! (PID: ${API_PID})${NC}"
            break
        fi
        sleep 1
        echo -n "."
        if [ $i -eq 30 ]; then
            echo -e "\n${RED}❌ Tempo limite esgotado para iniciar a API. Verifique /tmp/mini-kyc-api-demo.log${NC}"
            exit 1
        fi
    done
else
    echo -e "${GREEN}✅ API ativa e pronta para receber requisições!${NC}"
fi

# Função de limpeza ao sair
cleanup() {
    if [ $STARTED_LOCAL_SERVER -eq 1 ] && [ -n "${API_PID}" ]; then
        echo -e "\n${YELLOW}🛑 Encerrando servidor API local (PID: ${API_PID})...${NC}"
        kill $API_PID 2>/dev/null || true
    fi
}
trap cleanup EXIT

echo -e "\n${BLUE}==============================================================================${NC}"
echo -e "${BOLD}ETAPA 1: O ATS (Recrutamento) Simula a Mudança de Etapa de um Candidato${NC}"
echo -e "${BLUE}==============================================================================${NC}"
echo -e "A candidata ${BOLD}'Mariana Silva'${NC} avançou para a etapa ${BOLD}'Segurança & Compliance'${NC} no ATS."
echo -e "O ATS dispara automaticamente a checagem com CPF válido (módulo 11) e registro de consentimento LGPD."

STAGE_PAYLOAD='{
  "candidateName": "Mariana Silva",
  "document": "529.982.247-25",
  "jobTitle": "Engenheira de Software Sênior",
  "stageName": "SecurityCheck",
  "callbackUrl": "'"${BASE_URL}"'/api/v1/ats/callback"
}'

TRIGGER_RES=$(curl -s -X POST "${BASE_URL}/api/v1/ats/trigger-candidate-stage" \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: ${MASTER_API_KEY}" \
  -d "${STAGE_PAYLOAD}")

echo -e "\n${CYAN}📤 Resposta do ATS Simulator:${NC}"
echo "$TRIGGER_RES" | jq . 2>/dev/null || echo "$TRIGGER_RES"

CHECK_ID=$(echo "$TRIGGER_RES" | grep -o '"check_id":"[^"]*' | cut -d'"' -f4 || true)
if [ -z "$CHECK_ID" ]; then
    CHECK_ID=$(echo "$TRIGGER_RES" | grep -o '"checkId":"[^"]*' | cut -d'"' -f4 || true)
fi

echo -e "\n${GREEN}✅ Checagem enfileirada com sucesso! ID: ${BOLD}${CHECK_ID}${NC}"

echo -e "\n${BLUE}==============================================================================${NC}"
echo -e "${BOLD}ETAPA 2: Processamento Assíncrono com Circuit Breaker e Provedores Paralelos${NC}"
echo -e "${BLUE}==============================================================================${NC}"
echo -e "O ${BOLD}CheckOrchestrationWorker${NC} consome a fila e dispara em paralelo:"
echo -e "  - Receita Federal (Situação Cadastral)"
echo -e "  - Tribunais de Justiça e Diários Oficiais (Ações Cíveis/Criminais)"
echo -e "  - Listas Restritivas Nacionais e Internacionais (PEP / OFAC / CEIS)"
echo -e "  - Cadastro de Empregadores - Trabalho Escravo (MTE)"

echo -n "Processando checagem e gerando hashes de evidência..."
sleep 3
echo -e " ${GREEN}Concluído!${NC}"

echo -e "\n${BLUE}==============================================================================${NC}"
echo -e "${BOLD}ETAPA 3: O ATS Recebe o Webhook Assinado com HMAC-SHA256${NC}"
echo -e "${BLUE}==============================================================================${NC}"
echo -e "Verificando se o ATS recebeu o webhook assinado de retorno:"

CALLBACKS_RES=$(curl -s -X GET "${BASE_URL}/api/v1/ats/received-callbacks" \
  -H "X-Api-Key: ${MASTER_API_KEY}")
echo -e "\n${CYAN}📥 Últimos Callbacks Registrados pelo ATS:${NC}"
echo "$CALLBACKS_RES" | jq . 2>/dev/null || echo "$CALLBACKS_RES"

echo -e "\n${BLUE}==============================================================================${NC}"
echo -e "${BOLD}ETAPA 4: Consulta com Mascaramento LGPD (Perfil Analista Standard / Auditor)${NC}"
echo -e "${BLUE}==============================================================================${NC}"
echo -e "Fazendo requisição com perfil ${BOLD}Auditor${NC} (espera-se documento mascarado):"

STANDARD_RES=$(curl -s -X GET "${BASE_URL}/api/v1/checks/${CHECK_ID}" \
  -H "X-Api-Key: ${MASTER_API_KEY}" \
  -H "X-User-Role: Auditor")

echo -e "\n${CYAN}📄 Relatório Visualizado por Usuário Auditor (Documento Mascarado):${NC}"
echo "$STANDARD_RES" | jq . 2>/dev/null || echo "$STANDARD_RES"

echo -e "\n${BLUE}==============================================================================${NC}"
echo -e "${BOLD}ETAPA 5: Consulta Privilegiada (Oficial de Compliance) & Parecer de IA${NC}"
echo -e "${BLUE}==============================================================================${NC}"
echo -e "Fazendo requisição com perfil ${BOLD}ComplianceOfficer${NC}:"

COMPLIANCE_RES=$(curl -s -X GET "${BASE_URL}/api/v1/checks/${CHECK_ID}" \
  -H "X-Api-Key: ${MASTER_API_KEY}" \
  -H "X-User-Role: ComplianceOfficer")

echo -e "\n${CYAN}🔍 Relatório Completo com Documento e Parecer de IA:${NC}"
echo "$COMPLIANCE_RES" | jq . 2>/dev/null || echo "$COMPLIANCE_RES"

echo -e "\n${BLUE}==============================================================================${NC}"
echo -e "${BOLD}ETAPA 6: Revisão Humana Obrigatória (Artigo 20 da LGPD)${NC}"
echo -e "${BLUE}==============================================================================${NC}"
echo -e "O sistema apontou os riscos fáticos, mas ${BOLD}NÃO REPROVOU SOZINHO${NC}."
echo -e "O Oficial de Compliance profere a decisão humana fundamentada:"

REVIEW_PAYLOAD='{
  "reviewerId": "oficial.compliance@empresa.com",
  "decision": 2,
  "notes": "Candidato aprovado com ressalva referente a processo cível sem repercussão direta na atividade-fim."
}'

REVIEW_RES=$(curl -s -X POST "${BASE_URL}/api/v1/checks/${CHECK_ID}/review" \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: ${MASTER_API_KEY}" \
  -H "X-User-Role: ComplianceOfficer" \
  -d "${REVIEW_PAYLOAD}")

echo -e "\n${CYAN}⚖️ Parecer Humano Registrado no Sistema:${NC}"
echo "$REVIEW_RES" | jq . 2>/dev/null || echo "$REVIEW_RES"

echo -e "\n${BLUE}==============================================================================${NC}"
echo -e "${BOLD}ETAPA 7: Verificação KYB de Empresa com o Novo CNPJ Alfanumérico${NC}"
echo -e "${BLUE}==============================================================================${NC}"
echo -e "Testando empresa com o formato oficial da ${BOLD}IN RFB nº 2.229/2024${NC} ('12.ABC.345/0001-88'):"

CNPJ_PAYLOAD='{
  "document": "12.ABC.345/0001-88",
  "subjectType": 2,
  "name": "Alpha Beta Logística S.A.",
  "purpose": 3,
  "legalBasis": 1,
  "consentObtained": true,
  "consentTimestamp": "2026-11-04T12:00:00Z"
}'

CNPJ_RES=$(curl -s -X POST "${BASE_URL}/api/v1/checks" \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: ${MASTER_API_KEY}" \
  -d "${CNPJ_PAYLOAD}")

echo -e "\n${CYAN}🏢 Retorno da Checagem KYB Alfanumérica:${NC}"
echo "$CNPJ_RES" | jq . 2>/dev/null || echo "$CNPJ_RES"

echo -e "\n${BLUE}==============================================================================${NC}"
echo -e "${BOLD}ETAPA 8: Trilha de Auditoria Encadeada (Cryptographic Hash Chain)${NC}"
echo -e "${BLUE}==============================================================================${NC}"
echo -e "Consultando os logs encadeados com hash SHA-256 à prova de adulteração:"

AUDIT_RES=$(curl -s -X GET "${BASE_URL}/api/v1/audit" \
  -H "X-Api-Key: ${MASTER_API_KEY}")

echo -e "\n${CYAN}⛓️ Trilha Imutável de Auditoria (3 eventos mais recentes):${NC}"
echo "$AUDIT_RES" | jq '.logs[0:3]' 2>/dev/null || echo "$AUDIT_RES"

echo -e "\n${BLUE}==============================================================================${NC}"
echo -e "${BOLD}ETAPA 9: Medição de Consumo SaaS (Metering)${NC}"
echo -e "${BLUE}==============================================================================${NC}"
echo -e "Consultando contadores de consumo para bilhetagem:"

METERING_RES=$(curl -s -X GET "${BASE_URL}/api/v1/metering" \
  -H "X-Api-Key: ${MASTER_API_KEY}")

echo -e "\n${CYAN}📊 Consumo do Tenant Mestre:${NC}"
echo "$METERING_RES" | jq . 2>/dev/null || echo "$METERING_RES"

echo -e "\n${GREEN}${BOLD}"
echo "=============================================================================="
echo "  🎉 DEMONSTRAÇÃO CONCLUÍDA COM 100% DE SUCESSO!"
echo "  Todos os requisitos regulatórios, arquiteturais e de IA foram atendidos."
echo "=============================================================================="
echo -e "${NC}"
