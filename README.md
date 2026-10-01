# Questão 5 — API de conta corrente

API REST em .NET 10 para **movimentar** uma conta corrente e **consultar o saldo**, construída sobre um projeto semipronto recebido como desafio técnico (`Questao5/Questão 5.docx`).

Este README tem duas partes. A primeira é a de sempre: o que a API faz e como rodá-la. A segunda conta **como o trabalho foi conduzido**: as decisões, as dúvidas, os ajustes de curso e os erros encontrados pelo caminho. Ela foi escrita para quem vai avaliar o teste e quer entender o raciocínio, não só o resultado.

O desenvolvimento foi feito com assistentes de IA, e todas as conversas estão registradas em [`CONVERSAS.md`](CONVERSAS.md). O que está contado aqui é um resumo fiel daquele histórico; cada trecho aponta a interação de origem.

## Sumário

- [O que foi pedido e o que foi entregue](#o-que-foi-pedido-e-o-que-foi-entregue)
- [Como executar](#como-executar)
- [Como usar a API](#como-usar-a-api)
- [Arquitetura em resumo](#arquitetura-em-resumo)
- [Testes e verificação](#testes-e-verificação)
- [A história do desenvolvimento](#a-história-do-desenvolvimento)
- [Decisões e alternativas descartadas](#decisões-e-alternativas-descartadas)
- [O que deu errado e como foi corrigido](#o-que-deu-errado-e-como-foi-corrigido)
- [Limitações conhecidas](#limitações-conhecidas)
- [Estrutura do repositório](#estrutura-do-repositório)
- [Documentação do projeto](#documentação-do-projeto)

## O que foi pedido e o que foi entregue

| Enunciado | Situação |
| --- | --- |
| Movimentação com identificação da requisição, conta, valor e tipo (`C`/`D`) | Entregue: `POST /api/v1/movimentos` |
| Idempotência: repetir a requisição não pode gerar outro movimento | Entregue, com tratamento de concorrência e de conflito de chave |
| Validações `INVALID_ACCOUNT`, `INACTIVE_ACCOUNT`, `INVALID_VALUE`, `INVALID_TYPE`, em HTTP 400 com mensagem e tipo | Entregue |
| Consulta de saldo = créditos − débitos, `0.00` sem movimentos | Entregue: `GET /api/v1/contas/{idContaCorrente}/saldo` |
| Resposta do saldo com número da conta, titular, data e hora e saldo | Entregue |
| Dapper, CQRS e Mediator (pontos extras) | Entregue: Dapper, MediatR e separação entre escrita e leitura |
| Swagger com atributos, retornos e exemplos (ponto extra) | Entregue, com um exemplo para cada situação de erro |
| Testes unitários com NSubstitute (ponto extra) | Entregue para os dois handlers, além dos testes com SQLite real |

Além do enunciado, por decisão minha durante o trabalho:

- análise de segurança do material recebido antes de executá-lo e migração de .NET 6 (fora de suporte) para .NET 10;
- autenticação por JWT e regra de que cada conta só é acessada pelo seu titular;
- limites de requisições, tempo limite, logs estruturados sem dados bancários e identificador de correlação;
- ambiente Docker Compose para rodar tudo com um comando;
- um gate local que roda auditoria de dependências, verificação de segredos, formatação, build e testes.

A suíte tem 340 testes. O estado detalhado e as pendências estão em [`TODO.md`](TODO.md).

## Como executar

### Com Docker Compose

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

### Sem Docker

Requisito: SDK do .NET 10 (versão fixada em `global.json`).

```bash
dotnet run --project Questao5
```

A API sobe em `https://localhost:7140` e `http://localhost:5189`, grava o banco em `.data/database.sqlite` (caminho relativo ao diretório de execução, ignorado pelo Git) e, em Development, espera o emissor em `http://localhost:8081/default`. Para ter só o emissor em contêiner:

```bash
docker compose up -d auth
```

## Como usar a API

### 1. Obter um token

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

| `client_id` | Titular | Conta | `idContaCorrente` | Situação |
| --- | --- | --- | --- | --- |
| `katherine` | Katherine Sanchez | 123 | `B6BAFC09 -6967-ED11-A567-055DFA4A16C9` | ativa |
| `eva` | Eva Woodward | 456 | `FA99D033-7067-ED11-96C6-7C5DFA4A16C9` | ativa |
| `tevin` | Tevin Mcconnell | 789 | `382D323D-7067-ED11-8866-7D5DFA4A16C9` | ativa |
| `ameena` | Ameena Lynn | 741 | `F475F943-7067-ED11-A06B-7E5DFA4A16C9` | inativa |
| `jarrad` | Jarrad Mckee | 852 | `BCDACA4A-7067-ED11-AF81-825DFA4A16C9` | inativa |
| `elisha` | Elisha Simons | 963 | `D2E02051-7067-ED11-94C0-835DFA4A16C9` | inativa |

O identificador da conta 123 contém um espaço, como veio do enunciado. Na URL do saldo ele deve ser escrito como `%20`.

### 2. Movimentar

Crédito de 125,50 na conta da Eva:

```bash
curl -s -X POST http://localhost:8080/api/v1/movimentos \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"idRequisicao":"d743abe7-40ed-4a7d-b0a5-8f876e916a07","idContaCorrente":"FA99D033-7067-ED11-96C6-7C5DFA4A16C9","valor":125.50,"tipoMovimento":"C"}'
```

- `idRequisicao` é a chave de idempotência (UUID). Repetir a mesma requisição devolve o mesmo `idMovimento`, sem criar outro movimento.
- A mesma chave com dados diferentes devolve `409 IDEMPOTENCY_CONFLICT`.

### 3. Consultar o saldo

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
| valor zero, negativo, acima do limite ou com mais de duas casas | 400 | `INVALID_VALUE` |
| tipo de movimento diferente de `C` ou `D` | 400 | `INVALID_TYPE` |
| conta não cadastrada, ou de outro correntista | 400 | `INVALID_ACCOUNT` |
| conta própria inativa | 400 | `INACTIVE_ACCOUNT` |
| campo ausente ou em formato inválido | 400 | erro de validação por campo, sem `code` |
| chave de idempotência reutilizada com outros dados | 409 | `IDEMPOTENCY_CONFLICT` |
| limite de requisições excedido | 429 | `RATE_LIMIT_EXCEEDED` |
| tempo limite excedido | 504 | `REQUEST_TIMEOUT` |

Valor e tipo são conferidos antes da conta: uma requisição com valor inválido responde `INVALID_VALUE` mesmo que a conta não exista. O Swagger traz um exemplo de corpo para cada uma dessas situações.

## Arquitetura em resumo

```text
POST /api/v1/movimentos                      GET /api/v1/contas/{id}/saldo
        |                                              |
  MovementController                            BalanceController
        |  MediatR                                     |  MediatR
  CreateMovementCommandHandler                  GetBalanceQueryHandler
        |  unidade de trabalho:                        |  unidade de trabalho:
        |  uma transação imediata                      |  um snapshot de leitura
        v                                              v
  repositórios de escrita e leitura             repositórios de leitura
  (CommandStore e QueryStore)                   (QueryStore)
        |                                              |
  movimento + idempotencia + saldo_conta        contacorrente + titularidade_conta + saldo_conta
```

- **Entidades e repositórios.** Cada tabela tem uma entidade em `Domain/Entities` (`ContaCorrente`, `Movimento`, `Idempotencia`, `SaldoConta`, `TitularidadeConta`) e um repositório por entidade, com SQL parametrizado via Dapper: leitura em `QueryStore`, escrita em `CommandStore`. Os handlers orquestram; as regras ficam nas entidades.
- **Uma transação para tudo.** A unidade de trabalho entrega todos os repositórios ligados à mesma conexão e à mesma transação. É ela que garante que movimento, saldo e idempotência sejam gravados juntos ou desfeitos juntos.
- **Escrita e leitura separadas (CQRS local).** A movimentação grava o movimento, o registro de idempotência e o saldo consolidado na mesma transação. A consulta lê só o saldo consolidado, por chave primária, sem percorrer o histórico.
- **Banco.** As três tabelas do proponente (`contacorrente`, `movimento`, `idempotencia`) não foram alteradas. Foram acrescentadas duas: `saldo_conta` (saldo em centavos inteiros) e `titularidade_conta` (conta → correntista). A fixture `Questao5/database.sqlite` recebida permanece intacta, conferida por hash.
- **Dinheiro.** `decimal` em toda a aplicação e centavos inteiros no saldo consolidado. O `REAL` do schema recebido só é tocado na fronteira de persistência.
- **Segurança.** JWT validado por assinatura, emissor, audiência e validade; a claim `sub` identifica o correntista. Conta de outro correntista responde igual a conta inexistente, para não revelar quais contas existem.

Os contratos completos estão nas três especificações listadas em [Documentação do projeto](#documentação-do-projeto).

## Testes e verificação

```bash
dotnet test Exercicio.sln
```

Gate completo, a partir da raiz:

```powershell
powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\scripts\Invoke-SecurityGate.ps1
```

O gate executa, nesta ordem: restore em modo bloqueado, auditoria de dependências diretas e transitivas, verificação de segredos nos arquivos, formatação e analisadores, build Release com avisos tratados como erro, testes e cobertura. Cada entrega só foi fechada com o gate aprovado.

Sobre os 340 testes:

- não dependem de Docker: usam bancos SQLite temporários e tokens assinados por uma chave gerada no próprio teste;
- a maior parte exercita a API de verdade, por HTTP e com SQLite real, incluindo concorrência, rollback, timeout e limites;
- os dois handlers têm testes unitários com a unidade de trabalho e os repositórios substituídos por NSubstitute, e as entidades têm testes próprios;
- os exemplos de erro do Swagger são comparados com as respostas reais, para a documentação não divergir do comportamento.

Reconciliação do saldo consolidado com os movimentos (encerra com código 0 quando não há divergência):

```bash
docker compose run --rm api --reconciliar-saldos
```

---

## A história do desenvolvimento

O trabalho aconteceu em dois dias, 30 de setembro e 1º de outubro de 2026, em 46 interações registradas. A regra que estabeleci logo na primeira foi esta: toda conversa vai para um arquivo, deixando claro quem fala e se estamos **planejando** ou **executando**. O resto desta seção segue a ordem em que as coisas aconteceram.

### 1. Antes de rodar qualquer coisa

Recebi um projeto semipronto de origem externa. Antes de compilar ou executar, pedi uma varredura em busca de código suspeito, partindo de Zero Trust (Interação 003).

A análise estática não achou código malicioso, mas levantou sete pontos de atenção e propôs sete marcos de segurança: registrar a análise, preservar evidências, limpar o workspace, rodar antimalware, proteger as dependências, reconstruir do zero e só então executar de forma controlada. Foram todos executados (Interação 007), e o resultado está em [`RELATORIO_SEGURANCA.md`](RELATORIO_SEGURANCA.md). O que mudou o rumo do projeto:

- o .NET 6 estava fora de suporte desde novembro de 2024;
- a auditoria apontou uma vulnerabilidade de severidade alta na cadeia do SQLite e uma moderada no Swagger UI;
- os binários que vieram no pacote não foram reaproveitados.

A infraestrutura foi migrada para .NET 10 com pacotes auditados, lock file e fonte NuGet restrita, sem tocar em regra funcional.

### 2. Um freio de propósito

Ao autorizar os marcos de segurança, deixei uma condição: quando terminassem, **não começar** as pendências funcionais, porque havia mais detalhes a tratar antes (Interação 006). Isso virou uma regra do projeto: nada funcional começa sem planejamento e autorização específicos. É por isso que o histórico alterna interações de planejamento e de execução, e que cada entrega tem começo e fim marcados.

Na mesma linha, defini os referenciais que valeriam como guardrails em toda mudança: OWASP API Security Top 10, CWE Top 25, NIST SSDF, OpenSSF, CIS Benchmarks e MITRE ATT&CK (Interação 010). Uma análise do código contra eles gerou 23 itens de segurança, SEC-001 a SEC-023, registrados no `TODO.md` com evidência e critério de aceite (Interações 011 e 012).

Uma decisão dessa fase seria revertida depois: aceitei, só para o exercício, que a API ficasse **sem autenticação**, documentando isso como risco (Interação 013).

### 3. O primeiro defeito estava no que recebi

A varredura inicial já tinha notado: o script de inicialização do banco encerrava ao encontrar **qualquer uma** das três tabelas. Um banco com uma tabela só era tratado como completo, e a tabela de idempotência podia nunca ser criada.

A correção tornou a inicialização transacional, versionada, segura para execuções simultâneas e estrita: um schema incompatível é rejeitado em vez de aceito em silêncio (Interação 015).

### 4. Idempotência: primeiro o contrato, depois o código

A tabela `idempotencia` existia no schema, mas nada na aplicação a usava. Antes de escrever código, pedi uma especificação fechada (Interação 017). As decisões que saíram dela:

- a chave é um UUID no corpo da requisição, separada do identificador de correlação;
- o que se guarda é uma representação canônica da requisição, não o JSON bruto, para que a ordem dos campos não mude o resultado;
- a mesma chave com dados diferentes é **conflito** (HTTP 409), nunca uma nova execução silenciosa;
- movimento e chave são gravados na mesma transação, e falhas não reservam a chave;
- a concorrência é resolvida pelo banco, não por lock em memória, que não protegeria mais de um processo.

A implementação veio em três entregas pequenas: núcleo transacional, contrato HTTP e, por fim, limites e logs (Interações 019 a 021).

### 5. Uma pausa para arrumar a casa

No meio do caminho, percebi que o roadmap e o estado real tinham se desencontrado. Parei o desenvolvimento para conferir se havia mais de um arquivo de controle e unificar as pendências (Interação 022). O resultado foi a regra de governança que vale até hoje: `TODO.md` é a fonte de estado, `CONVERSAS.md` é a evidência cronológica, e relatórios históricos não são reescritos para parecerem atuais.

### 6. Primeiro ajuste de curso: o saldo

A primeira especificação do saldo calculava o valor na hora, somando todos os movimentos da conta a cada consulta (Interação 024).

Antes de implementar, levantei uma preocupação: com muitos acessos simultâneos, esse cálculo em tempo real poderia travar a base. A análise confirmou que o que estava planejado não fechava o cenário de alta simultaneidade e apontou como ponto mais crítico a falta de um índice para localizar os movimentos por conta. Propus então aplicar CQRS de fato, com uma tabela de saldo consolidado atualizada junto com a movimentação e um cache em memória para a leitura (Interação 025).

A resposta foi favorável à tabela e cautelosa com o cache, que traria uma janela de leitura desatualizada e mais pontos de falha. Fiquei só com a tabela consolidada e deixei o cache explicitamente fora. A especificação foi reescrita antes de existir qualquer código do saldo (Interação 026).

### 7. Troca de assistente

Até a Interação 026 o assistente foi o Cline, com um modelo da OpenAI. A partir da 027 passou a ser o Claude Code, da Anthropic. A troca está registrada no histórico, como eu havia exigido desde o início (Interação 002).

O novo assistente não tinha memória das sessões anteriores, então começou lendo toda a documentação. Em seguida pedi um levantamento do que faltava para fechar o escopo, cruzando o enunciado com o código (Interação 029). Ele encontrou o esperado — o saldo — e três coisas que nenhum documento registrava:

- os códigos `INVALID_VALUE` e `INVALID_TYPE` existiam no código, mas **nunca chegavam ao cliente**;
- o Swagger estava praticamente sem documentação;
- o NSubstitute estava instalado e não era usado por nenhum teste.

### 8. Segundo ajuste de curso: "está muito frágil essa ligação"

Ao voltar para o saldo, fiz uma pergunta simples: existe relação entre a conta e o dono dela? A resposta foi não. No schema do proponente, o titular é só um texto dentro da conta (Interação 030).

Considerei essa ligação frágil demais. Decidi ter um identificador de correntista, recebido por token JWT — o que significava implementar autenticação e reverter o risco aceito no começo (Interação 031).

Três decisões fecharam o desenho (Interação 032):

- **A titularidade vale também para a movimentação**, não só para o saldo.
- **Conta de outro correntista responde igual a conta inexistente.** Um 403 confirmaria que a conta existe.
- **Emissor de tokens.** Isto é um desafio técnico, e autenticação nem foi pedida; o objetivo era manter a consistência do modelo, com algo leve e simples de usar. Entre o Keycloak, com login real e bem mais pesado, e o `mock-oauth2-server`, um contêiner único, fiquei com o segundo.

A limitação dessa última escolha está dita sem rodeios na documentação: a API valida o token de verdade, mas o emissor de teste não autentica ninguém.

Autorizei então uma sequência de cinco entregas, cada uma com seu gate e seu commit (Interação 033): documentação, autenticação, schema novo com autorização na movimentação, consulta de saldo e empacotamento com Docker Compose.

### 9. O que os testes revelaram sobre o próprio projeto

Três achados dessa reta final não estavam em nenhum plano. Conto porque mostram a verificação funcionando.

- **Os testes HTTP não estavam isolados.** A documentação afirmava que cada teste usava um banco descartável. Não era verdade para os testes HTTP: por um detalhe de ordem de configuração, todos gravavam em um banco compartilhado na pasta de saída do projeto de testes. Foi corrigido, com um teste que impede a volta do problema (Interação 033).
- **A documentação divergia da API.** Ao documentar o Swagger, criei um teste que compara cada exemplo de erro com a resposta real. Na primeira execução ele mostrou que o 429 e o 413 reais eram diferentes do que os exemplos diziam. Corrigi os exemplos (Interação 037).
- **Uma falha intermitente.** Um teste que ninguém tinha alterado passou a falhar uma vez a cada oito execuções. A causa era antiga: o descarte de um banco de teste fechava conexões de outros testes em paralelo. Depois da correção, a suíte rodou 40 vezes seguidas sem falha (Interação 037).

### 10. Fechando o enunciado

Por último vieram os três itens do levantamento. O dos códigos de erro era requisito, e a correção foi pequena: as regras de valor e tipo estavam no lugar errado, validadas cedo demais, e bastou tirá-las de lá para que os códigos chegassem ao cliente (Interação 035). Depois, a documentação do Swagger e os testes unitários que faltavam (Interação 037).

### 11. Terceiro ajuste de curso: entidades e repositórios

Com tudo funcionando, fiz uma pergunta de revisão: criamos entidades? Como gravamos e lemos do banco? A resposta foi que não havia entidades. O acesso a dados estava em dois "stores" que executavam SQL e também decidiam as regras, e as pastas `Domain/Entities` e `Domain/Enumerators`, que o projeto recebido já previa, tinham ficado vazias (Interação 039).

Não gostei da abordagem e pedi uma mais tradicional: uma entidade por tabela, um repositório por entidade e as controllers em uma pasta na raiz do projeto. Antes de planejar, o levantamento trouxe um fato relevante: a posição das controllers e o esqueleto de pastas tinham vindo do proponente, não eram escolha nossa. Decidi mover as controllers mesmo assim, por ser o convencional, e manter os repositórios dentro das pastas `QueryStore` e `CommandStore` que ele desenhou (Interação 040).

O cuidado principal foi a transação. Antes, movimento, saldo e idempotência eram gravados juntos porque estavam no mesmo store; com um repositório por tabela, isso passou a ser garantido por uma unidade de trabalho. A refatoração não mudou a API, o banco nem os logs: os testes de atomicidade, concorrência e reconciliação que já existiam passaram sem mudar de expectativa, e o ambiente em contêiner reproduziu os mesmos resultados (Interação 041).

### Linha do tempo

| Quando | Entrega | Testes |
| --- | --- | --- |
| 30/09 | Análise Zero Trust, sete marcos de segurança e migração para .NET 10 | 0 |
| 30/09 | Guardrails, backlog de segurança, tratamento de erros, correlação e gate | 16 |
| 01/10 | Inicialização do banco corrigida | 31 |
| 01/10 | Movimentação idempotente, em três entregas | 79 |
| 01/10 | Limpeza do template e especificação do saldo, revista após a discussão de concorrência | 80 |
| 01/10 | Autenticação JWT | 106 |
| 01/10 | Schema novo, titularidade e saldo consolidado | 144 |
| 01/10 | Consulta de saldo | 211 |
| 01/10 | Docker Compose | 211 |
| 01/10 | Códigos `INVALID_VALUE` e `INVALID_TYPE` | 233 |
| 01/10 | Testes unitários do handler e Swagger documentado | 294 |
| 01/10 | Controllers na raiz, entidades, repositórios e unidade de trabalho | 340 |

## Decisões e alternativas descartadas

| Decisão | Alternativa descartada | Por quê |
| --- | --- | --- |
| Analisar o material antes de executar | Compilar e rodar direto | Projeto de origem externa, com binários prontos no pacote |
| Migrar para .NET 10 | Manter o .NET 6 recebido | Fora de suporte e com vulnerabilidades conhecidas nas dependências |
| Especificar antes de implementar | Ir direto ao código | Contrato e casos de borda discutidos enquanto mudar ainda era barato |
| Concorrência resolvida pela transação do banco | Lock em memória | Lock em memória não protege mais de um processo |
| Mesma chave com dados diferentes → 409 | Aceitar como nova requisição | Evita execução silenciosa de algo que o cliente não repetiu de fato |
| Saldo consolidado em tabela própria | Somar os movimentos a cada consulta | O custo da leitura não cresce com o histórico |
| Saldo em centavos inteiros | Ponto flutuante | Precisão: 200 créditos de 0,10 precisam dar exatamente 20,00 |
| Sem cache em memória | Cache carregado na inicialização | Janela de leitura desatualizada e mais pontos de falha, por ganho pequeno |
| Tabelas novas e aditivas | Alterar as tabelas do proponente | Preserva o schema e a fixture recebidos |
| Conta alheia responde como inexistente | HTTP 403 | Um 403 confirmaria que a conta existe |
| `mock-oauth2-server` como emissor | Keycloak | Proporcional a um desafio técnico; a limitação está documentada |
| Uma entidade por tabela e um repositório por entidade, com unidade de trabalho | Stores que concentravam SQL e regras | Abordagem mais tradicional e fácil de reconhecer; a unidade de trabalho preserva a transação única |
| Repositórios nas pastas `QueryStore` e `CommandStore` | Pasta `Repositories` nova | Aproveita o esqueleto CQRS que veio no projeto recebido |
| Controllers em `Questao5/Controllers` | Mantê-las em `Infrastructure/Services/Controllers`, onde vieram | É a posição convencional em projetos ASP.NET |
| Um exemplo de erro por situação no Swagger, verificado por teste | Exemplos escritos à mão, sem verificação | Documentação que diverge do comportamento é pior que nenhuma |

## O que deu errado e como foi corrigido

| O que aconteceu | Como apareceu | O que foi feito |
| --- | --- | --- |
| A inicialização do banco aceitava schema incompleto | Varredura inicial do material recebido | Inicialização transacional, versionada e estrita |
| Roadmap e estado real se desencontraram | Percebi durante o desenvolvimento | Pausa para unificar o controle no `TODO.md` |
| Saldo calculado na hora poderia travar a base sob carga | Minha preocupação, antes de implementar | Saldo consolidado; especificação reescrita antes do código |
| A API ficaria anônima, com o titular sendo só um texto | Minha pergunta sobre o dono da conta | Autenticação e titularidade; risco aceito revogado |
| `INVALID_VALUE` e `INVALID_TYPE` não chegavam ao cliente | Levantamento cruzando enunciado e código | Regras movidas para o lugar certo, com testes |
| Testes HTTP gravavam em um banco compartilhado | Encontrado ao adaptar os testes para autenticação | Isolamento corrigido e coberto por teste |
| Exemplos do Swagger diferentes da resposta real | Teste que compara os dois | Exemplos corrigidos |
| Teste falhando uma vez a cada oito execuções | Repetição da suíte | Causa removida; 40 execuções seguidas sem falha |
| Gate reprovou uma entrega por formatação | O próprio gate | Arquivo reformatado e gate executado de novo |
| Acesso a dados sem entidades, com as pastas de domínio vazias | Minha pergunta de revisão, com tudo já funcionando | Entidades, repositórios e unidade de trabalho, sem mudar comportamento |

## Limitações conhecidas

- **O emissor de tokens é de teste.** Ele entrega token de qualquer correntista a quem o alcançar; por isso o Compose publica as portas só em `127.0.0.1`. Em um ambiente real seria trocado por um provedor de identidade, mudando apenas a configuração.
- **Os identificadores de correntista foram criados por mim.** Os dados do proponente não trazem dono de conta.
- **`movimento.valor` continua `REAL`**, como veio no schema. A aplicação usa `decimal` e centavos inteiros, mas a migração dessa coluna ficou fora do escopo.
- **SQLite em um único nó.** Não há alta disponibilidade, réplicas nem teste de carga; a consistência sob concorrência foi verificada com poucas requisições simultâneas.
- **Sem TLS de produção, política de hosts, monitoramento ou pipeline de release.** Esses itens dependem de uma infraestrutura que o desafio não define. Foram encerrados como fora do escopo, cada um com motivo e risco residual, no `TODO.md` (SEC-007, SEC-011, SEC-012, SEC-021, SEC-022 e SEC-023), e devem ser reabertos antes de qualquer uso real.
- **O Swagger só é publicado em Development**, que é como o Compose executa a API.
- **Mensagens de validação do framework estão em inglês** (por exemplo, campo obrigatório ausente); as mensagens próprias da API estão em português.
- **A entrega é por pull request** do branch `20260930` para `master`. Todo o desenvolvimento foi feito nesse branch; a abertura e o merge da PR são feitos por mim, no GitHub.

## Estrutura do repositório

```text
Questao5/                     API
  Controllers/                controllers, DTOs (Models) e filtros
  Application/                comandos, consultas, handlers e regras de aplicação
  Domain/
    Entities/                 uma entidade por tabela
    Enumerators/              tipo de movimento
    Repositories/             interfaces dos repositórios e da unidade de trabalho
  Infrastructure/
    Database/                 unidade de trabalho e repositórios: leitura (QueryStore) e escrita (CommandStore)
    Services/                 autenticação, limites, logs, erros, correlação, OpenAPI
    Sqlite/                   conexão, inicialização e validação de schema
  database.sqlite             fixture recebida, mantida intacta
  Questão 5.docx              enunciado
Questao5.Tests/               testes unitários e de integração
scripts/                      gate local e verificação de segredos
docker/                       configuração do emissor de teste
Dockerfile, docker-compose.yml
```

## Documentação do projeto

| Documento | Conteúdo |
| --- | --- |
| [`CONVERSAS.md`](CONVERSAS.md) | histórico completo das solicitações, decisões e ações, interação por interação |
| [`TODO.md`](TODO.md) | escopo, estado, pendências e critérios de aceite |
| [`ESPECIFICACAO_MOVIMENTACAO.md`](ESPECIFICACAO_MOVIMENTACAO.md) | contrato e desenho da movimentação |
| [`ESPECIFICACAO_SALDO.md`](ESPECIFICACAO_SALDO.md) | contrato e desenho da consulta de saldo |
| [`ESPECIFICACAO_AUTENTICACAO.md`](ESPECIFICACAO_AUTENTICACAO.md) | autenticação JWT, titularidade de conta e ambiente do desafio |
| [`DIRETRIZES_SEGURANCA.md`](DIRETRIZES_SEGURANCA.md) | guardrails de segurança aplicados a cada mudança |
| [`RELATORIO_SEGURANCA.md`](RELATORIO_SEGURANCA.md) | relatório da análise Zero Trust do material recebido |
