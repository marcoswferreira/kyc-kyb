# Pilar 1: Validação de Documentos e o Novo CNPJ Alfanumérico

A correta validação, normalização e mascaramento de identificadores de pessoas físicas (CPF) e jurídicas (CNPJ) é a primeira linha de defesa em qualquer plataforma de Background Check.

A plataforma **MiniKycKyb** traz suporte nativo e antecipado ao **CNPJ Alfanumérico**, instituído pela Receita Federal do Brasil através da **Instrução Normativa RFB nº 2.229, de 15 de outubro de 2024** (com cronograma oficial de implantação em produção previsto para **julho de 2026**).

---

## 🏛️ Contexto Regulatório: Por que o CNPJ Alfanumérico?

O formato tradicional de 14 dígitos exclusivamente numéricos do CNPJ (`XX.XXX.XXX/YYYY-ZZ`) possui uma capacidade teórica finita de 100 milhões de raízes básicas. Com o ritmo acelerado de abertura de microempresas (MEIs), startups e holdings no Brasil, a RFB projetou o esgotamento do espaço numérico.

A **IN RFB nº 2.229/2024** introduziu:
1. **Preservação do Tamanho**: O CNPJ continua com **14 posições**.
2. **Formato Híbrido**:
   - As **primeiras 12 posições** (as 8 da raiz e as 4 da ordem/filial) podem conter tanto números (`0-9`) quanto letras maiúsculas (`A-Z`).
   - As **duas últimas posições** (posições 13 e 14) permanecem estritamente **numéricas** (`00` a `99`), sendo os Dígitos Verificadores (DV1 e DV2).
3. **Compatibilidade com CNPJs Antigos**: Todos os CNPJs numéricos emitidos no padrão antigo continuam válidos, pois o algoritmo alfanumérico é um superconjunto matemático retrocompatível com o algoritmo puramente numérico.

---

## 🧮 Algoritmo Matemático de Validação (IN RFB 2.229/2024)

### 1. Conversão de Caracteres (Tabela ASCII - 48)
Diferente de sistemas que usam posições alfabéticas puras (1-26), a Receita Federal determinou a subtração direta do valor ASCII do caractere por 48:
$$\text{Valor}(c) = \text{ASCII}(c) - 48$$

Exemplos práticos:
- `'0'` $\rightarrow 48 - 48 = \mathbf{0}$
- `'9'` $\rightarrow 57 - 48 = \mathbf{9}$
- `'A'` $\rightarrow 65 - 48 = \mathbf{17}$
- `'B'` $\rightarrow 66 - 48 = \mathbf{18}$
- `'Z'` $\rightarrow 90 - 48 = \mathbf{42}$

### 2. Cálculo do Primeiro Dígito Verificador (DV1)
Multiplica-se os valores inteiros das 12 primeiras posições pelos pesos decrescentes fixos `[5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]`:
$$\text{Soma}_1 = \sum_{i=0}^{11} \text{Valor}(c_i) \times \text{Peso}_1[i]$$
$$\text{Resto}_1 = \text{Soma}_1 \pmod{11}$$
$$\text{DV}_1 = \begin{cases} 0, & \text{se } \text{Resto}_1 < 2 \\ 11 - \text{Resto}_1, & \text{se } \text{Resto}_1 \ge 2 \end{cases}$$

### 3. Cálculo do Segundo Dígito Verificador (DV2)
Inclui-se o DV1 recém-calculado na posição 13 e aplica-se o vetor de pesos `[6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]`:
$$\text{Soma}_2 = \left( \sum_{i=0}^{11} \text{Valor}(c_i) \times \text{Peso}_2[i] \right) + (\text{DV}_1 \times \text{Peso}_2[12])$$
$$\text{Resto}_2 = \text{Soma}_2 \pmod{11}$$
$$\text{DV}_2 = \begin{cases} 0, & \text{se } \text{Resto}_2 < 2 \\ 11 - \text{Resto}_2, & \text{se } \text{Resto}_2 \ge 2 \end{cases}$$

---

## 💻 Implementação no Value Object `Cnpj` (`src/Domain/ValueObjects/Cnpj.cs`)

O código em C# implementa essa lógica sem alocação desnecessária de strings (`ReadOnlySpan<char>`):

```csharp
public static bool IsValid(string? rawCnpj)
{
    if (string.IsNullOrWhiteSpace(rawCnpj)) return false;
    var cleaned = Normalize(rawCnpj);
    if (cleaned.Length != 14) return false;

    // As 12 primeiras posições devem ser alfanuméricas (A-Z ou 0-9)
    for (int i = 0; i < 12; i++)
    {
        char c = cleaned[i];
        if (!char.IsAsciiLetterOrDigit(c)) return false;
    }

    // Os 2 dígitos verificadores devem ser puramente numéricos
    if (!char.IsAsciiDigit(cleaned[12]) || !char.IsAsciiDigit(cleaned[13]))
        return false;

    // Cálculo do primeiro dígito verificador
    int[] weights1 = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    int sum1 = 0;
    for (int i = 0; i < 12; i++)
    {
        sum1 += (cleaned[i] - 48) * weights1[i];
    }
    int rem1 = sum1 % 11;
    int dv1 = rem1 < 2 ? 0 : 11 - rem1;
    if (cleaned[12] - '0' != dv1) return false;

    // Cálculo do segundo dígito verificador
    int[] weights2 = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
    int sum2 = 0;
    for (int i = 0; i < 12; i++)
    {
        sum2 += (cleaned[i] - 48) * weights2[i];
    }
    sum2 += dv1 * weights2[12];
    int rem2 = sum2 % 11;
    int dv2 = rem2 < 2 ? 0 : 11 - rem2;

    return cleaned[13] - '0' == dv2;
}
```

---

## 🧪 Casos de Teste Validados

A suíte de testes unitários `Domain.UnitTests` cobre os seguintes vetores:
- **CNPJ Tradicional Válido**: `00.000.000/0001-91` (Banco do Brasil) $\rightarrow$ Válido.
- **CNPJ Alfanumérico Válido**: `12.ABC.345/0001-88` $\rightarrow$ Válido (DV1 e DV2 calculados e batem com 88).
- **CNPJs com DVs adulterados**: Detectados e rejeitados com `Result.Failure`.
- **DVs não numéricos**: Ex: `12ABC3450001AB` $\rightarrow$ Imediatamente rejeitado (DVs devem ser numéricos).

---

## 🔒 Mascaramento em Conformidade com a LGPD

O Value Object `Cnpj` e o serviço `DataMasker` expõem métodos de anonimização:
- Formatação Canônica: `12.ABC.345/0001-88`
- Máscara LGPD (para perfis sem permissão Irrestrita): `**.ABC.***0001-**`

Dessa forma, analistas em operação diária visualizam apenas os caracteres necessários para desambiguação sem expor o identificador completo publicamente.

