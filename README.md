# Questão 5 — API de conta corrente

API REST em .NET 10 com duas funcionalidades pedidas pelo enunciado (`Questao5/Questão 5.docx`):

- **movimentação** de conta corrente, com idempotência: `POST /api/v1/movimentos`;
- **consulta de saldo**: `GET /api/v1/contas/{idContaCorrente}/saldo`.

Usa ASP.NET Core, MediatR (CQRS), Dapper e SQLite. Além do que o enunciado pede, os dois endpoints exigem um token JWT e só aceitam a conta do próprio correntista.

## Executar com Docker Compose

Requisito: Docker com Compose.

```bash
docker compose up --build -d
```

Sobem dois serviços, publicados somente em `127.0.0.1`:

| Serviço | Endereço | Papel |
| --- | --- | --- |
| `api` | `http://localhost:8080` | a API; Swagger em `http://localhost:8080/swagger` |
| `auth` | `http://localhost:8081` | emissor de tokens de teste |

Para encerrar e apagar os dados:

```bash
docker compose down -v
```

## Obter um token

Cada correntista do seed tem um `client_id` no emissor de teste. O valor de `client_secret` pode ser qualquer um.

Use `localhost` no endereço do emissor, não `127.0.0.1`: o emissor grava no token o endereço pelo qual foi chamado, e a API só aceita `http://localhost:8081/default`.

Bash:

```bash
TOKEN=$(curl -s -X POST http://localhost:8081/default/token \
  -d grant_type=client_credentials -d client_id=eva -d client_secret=demo \
  | tr -d '\n ' | grep -o '"access_token":"[^"]*"' | cut -d'"' -f4)
```

PowerShell:

```powershell
$resposta = Invoke-RestMethod -Method Post -Uri http://localhost:8081/default/token `
  -Body @{ grant_type = 'client_credentials'; client_id = 'eva'; client_secret = 'demo' }
$TOKEN = $resposta.access_token
```

O token vale 15 minutos. No Swagger, use o botão **Authorize** e cole apenas o token.

### Correntistas e contas do seed

| `client_id` | Titular | Conta | `idContaCorrente` | Situação |
| --- | --- | --- | --- | --- |
| `katherine` | Katherine Sanchez | 123 | `B6BAFC09 -6967-ED11-A567-055DFA4A16C9` | ativa |
| `eva` | Eva Woodward | 456 | `FA99D033-7067-ED11-96C6-7C5DFA4A16C9` | ativa |
| `tevin` | Tevin Mcconnell | 789 | `382D323D-7067-ED11-8866-7D5DFA4A16C9` | ativa |
| `ameena` | Ameena Lynn | 741 | `F475F943-7067-ED11-A06B-7E5DFA4A16C9` | inativa |
| `jarrad` | Jarrad Mckee | 852 | `BCDACA4A-7067-ED11-AF81-825DFA4A16C9` | inativa |
| `elisha` | Elisha Simons | 963 | `D2E02051-7067-ED11-94C0-835DFA4A16C9` | inativa |

O identificador da conta 123 contém um espaço, como veio do enunciado. Na URL do saldo ele deve ser escrito como `%20`.

## Chamar os endpoints

Movimentar (crédito de 125,50 na conta da Eva):

```bash
curl -s -X POST http://localhost:8080/api/v1/movimentos \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"idRequisicao":"d743abe7-40ed-4a7d-b0a5-8f876e916a07","idContaCorrente":"FA99D033-7067-ED11-96C6-7C5DFA4A16C9","valor":125.50,"tipoMovimento":"C"}'
```

- `idRequisicao` é a chave de idempotência (UUID). Repetir a mesma requisição devolve o mesmo `idMovimento` sem criar outro movimento.
- A mesma chave com dados diferentes devolve `409 IDEMPOTENCY_CONFLICT`.

Consultar o saldo:

```bash
curl -s http://localhost:8080/api/v1/contas/FA99D033-7067-ED11-96C6-7C5DFA4A16C9/saldo \
  -H "Authorization: Bearer $TOKEN"
```

```json
{"numeroContaCorrente":456,"nomeTitular":"Eva Woodward","dataHoraConsulta":"2026-10-01T13:11:36.6605974+00:00","saldoAtual":125.50}
```

### Respostas de erro

Os erros usam `application/problem+json`, com `code`, `detail` e `correlationId`.

| Situação | HTTP | `code` |
| --- | --- | --- |
| token ausente ou inválido | 401 | `UNAUTHENTICATED` |
| conta não cadastrada, ou de outro correntista | 400 | `INVALID_ACCOUNT` |
| conta própria inativa | 400 | `INACTIVE_ACCOUNT` |
| valor zero, negativo, acima do limite ou com mais de duas casas | 400 | `INVALID_VALUE` |
| tipo de movimento diferente de `C` ou `D` | 400 | `INVALID_TYPE` |
| campo ausente ou em formato inválido | 400 | erro de validação por campo, sem `code` |
| chave de idempotência reutilizada com outros dados | 409 | `IDEMPOTENCY_CONFLICT` |
| limite de requisições excedido | 429 | `RATE_LIMIT_EXCEEDED` |
| tempo limite excedido | 504 | `REQUEST_TIMEOUT` |

Valor e tipo são conferidos antes da conta: uma requisição com valor inválido responde `INVALID_VALUE` mesmo que a conta não exista.

## Sobre a autenticação

A API valida de verdade a assinatura, o emissor, a audiência e a validade do token, e usa a claim `sub` como identificador do correntista.

O emissor usado aqui, o `mock-oauth2-server`, é uma ferramenta de teste: **ele não autentica ninguém** e entrega um token de qualquer correntista a quem o alcançar. Serve apenas a este desafio técnico. Em um ambiente real ele seria trocado por um provedor de identidade, alterando somente a seção `Jwt` da configuração.

## Executar sem Docker

Requisito: SDK do .NET 10 (versão fixada em `global.json`).

```bash
dotnet run --project Questao5
```

A API sobe em `https://localhost:7140` e `http://localhost:5189`, grava o banco em `.data/database.sqlite` (caminho relativo ao diretório de execução, ignorado pelo Git) e, em Development, espera o emissor em `http://localhost:8081/default`. Para ter o emissor sem subir a API em contêiner:

```bash
docker compose up -d auth
```

## Testes e verificação

Testes:

```bash
dotnet test Exercicio.sln
```

Gate completo (restore bloqueado, auditoria de dependências, secret scanning, formatação e analisadores, build Release, testes e cobertura), a partir da raiz:

```powershell
powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\scripts\Invoke-SecurityGate.ps1
```

Os testes não dependem de Docker: usam bancos SQLite temporários e tokens assinados por uma chave gerada no próprio teste.

Reconciliação do saldo consolidado com os movimentos (encerra com código 0 quando não há divergência):

```bash
docker compose run --rm api --reconciliar-saldos
```

## Documentação do projeto

| Documento | Conteúdo |
| --- | --- |
| `TODO.md` | escopo, estado, pendências e próximos passos |
| `CONVERSAS.md` | histórico das solicitações, decisões e ações |
| `ESPECIFICACAO_MOVIMENTACAO.md` | contrato e desenho da movimentação |
| `ESPECIFICACAO_SALDO.md` | contrato e desenho da consulta de saldo |
| `ESPECIFICACAO_AUTENTICACAO.md` | autenticação JWT e titularidade de conta |
| `DIRETRIZES_SEGURANCA.md` | guardrails de segurança aplicados a cada mudança |
| `RELATORIO_SEGURANCA.md` | relatório da análise Zero Trust do material recebido |
