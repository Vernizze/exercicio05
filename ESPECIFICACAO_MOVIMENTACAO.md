# Serviço de movimentação — especificação e estado implementado

## 1. Escopo e estado

Este documento foi criado na Entrega B, em 1º de outubro de 2026, para fechar o contrato e o desenho técnico do TODO-002. A implementação correspondente foi concluída nas entregas C1 a C3 e reconciliada documentalmente na C4. As entregas F1 e F2 acrescentaram autenticação por JWT, autorização por titular, idempotência amarrada ao correntista e manutenção da projeção de saldo; este documento foi reconciliado com esse estado na F2.

Fazem parte desta especificação:

- contrato HTTP e DTOs da movimentação;
- validações de entrada e de negócio;
- representação monetária na aplicação;
- semântica da chave de idempotência;
- normalização determinística da requisição;
- algoritmo transacional e comportamento concorrente;
- limites de entrada e proteção contra abuso;
- eventos estruturados e dados proibidos em logs;
- arquitetura implementada e matriz de testes.

Permanecem fora do escopo:

- consulta de saldo;
- cadastro de contas;
- emissão de tokens, login e autorização por função, conforme `ESPECIFICACAO_AUTENTICACAO.md`;
- migração de `movimento.valor` de `REAL` para centavos inteiros;
- correção do tipo histórico de `movimento.idcontacorrente`;
- mudanças de contrato ou de persistência além das decisões registradas neste documento.

## 2. Contrato HTTP

### 2.1 Rota

```http
POST /api/v1/movimentos
Content-Type: application/json
Authorization: Bearer <JWT>
```

O versionamento na URL torna o endpoint inventariável e permite evolução sem alteração silenciosa do contrato.

O endpoint exige JWT válido. O correntista é identificado somente pela claim `sub` do token, nunca por campo da requisição; as regras de validação do token estão em `ESPECIFICACAO_AUTENTICACAO.md`.

### 2.2 Requisição

```json
{
  "idRequisicao": "d743abe7-40ed-4a7d-b0a5-8f876e916a07",
  "idContaCorrente": "FA99D033-7067-ED11-96C6-7C5DFA4A16C9",
  "valor": 125.50,
  "tipoMovimento": "C"
}
```

| Campo | Tipo | Obrigatório | Regras |
| --- | --- | --- | --- |
| `idRequisicao` | UUID em texto | Sim | formato canônico `D`, 36 caracteres; representa a chave de idempotência |
| `idContaCorrente` | texto | Sim | 1 a 37 caracteres após remoção de espaços externos |
| `valor` | número decimal JSON | Sim | maior que zero, no máximo duas casas decimais e no máximo `9999999999.99` |
| `tipoMovimento` | texto | Sim | um único caractere; somente `C` ou `D`, sem conversão silenciosa de outros valores |

O DTO é fechado e contém somente esses quatro campos. Propriedades desconhecidas são rejeitadas para impedir overposting e erros silenciosos de contrato.

O DTO valida somente a **estrutura** da requisição: presença dos quatro campos, formato do UUID, tamanho da conta e `valor` numérico. As regras de **negócio** sobre valor e tipo não ficam no DTO: são aplicadas pelo normalizador, para que cheguem ao cliente com os códigos `INVALID_VALUE` e `INVALID_TYPE` exigidos pelo enunciado. Um campo ausente ou `null` é erro estrutural, sem `code`; um valor ou tipo presente e inválido, inclusive texto vazio em `tipoMovimento`, é erro de negócio.

`idRequisicao` não é aceito no header `X-Correlation-ID`. Correlação e idempotência têm semânticas distintas.

### 2.3 Sucesso e repetição idêntica

Tanto a primeira execução bem-sucedida quanto uma repetição idêntica retornam:

```http
HTTP/1.1 200 OK
Content-Type: application/json
```

```json
{
  "idMovimento": "34e56aa7-703f-46d8-8f65-100f2795a87b"
}
```

A repetição idêntica devolve exatamente o mesmo `idMovimento`, sem inserir outro movimento. O `X-Correlation-ID` reflete a tentativa HTTP atual, não a tentativa original.

### 2.4 Erros

Erros usam `application/problem+json`, o tratamento global existente e a extensão `correlationId`.

| Situação | HTTP | `code` | Observação |
| --- | --- | --- | --- |
| Token ausente ou inválido | 401 | `UNAUTHENTICATED` | header `WWW-Authenticate: Bearer`; motivo específico não devolvido |
| JSON inválido, propriedade desconhecida, campo ausente ou `null`, `valor` não numérico, UUID fora do formato, conta vazia ou acima de 37 caracteres | 400 | validação por campo, sem `code` | resposta de validação do ASP.NET Core |
| Conta não cadastrada | 400 | `INVALID_ACCOUNT` | exigido pelo enunciado |
| Conta de outro correntista ou sem titularidade | 400 | `INVALID_ACCOUNT` | corpo idêntico ao de conta não cadastrada |
| Conta própria inativa | 400 | `INACTIVE_ACCOUNT` | exigido pelo enunciado |
| Valor numérico não positivo, acima do limite ou com escala maior que 2 | 400 | `INVALID_VALUE` | exigido pelo enunciado; Problem Details com `detail` e `code` |
| Tipo presente e diferente de `C` ou `D`, inclusive vazio, minúsculo ou com mais de um caractere | 400 | `INVALID_TYPE` | exigido pelo enunciado; Problem Details com `detail` e `code` |
| Mesma chave com requisição normalizada diferente | 409 | `IDEMPOTENCY_CONFLICT` | conflito, não uma nova execução |
| Corpo acima de 4 KiB | 413 | não aplicável | Problem Details correlacionado |
| Media type diferente de `application/json` | 415 | não aplicável | Problem Details correlacionado |
| Limite de frequência ou concorrência excedido | 429 | `RATE_LIMIT_EXCEEDED` | inclui `Retry-After` quando calculável |
| Falha inesperada ou indisponibilidade do banco | 500 | sem detalhe interno | não expõe SQL, caminho ou exceção |
| Timeout do servidor | 504 | `REQUEST_TIMEOUT` | Problem Details correlacionado |

O conflito idempotente usa HTTP 409 porque a requisição isoladamente pode ser válida, mas é incompatível com o recurso de idempotência já identificado pela chave.

A precedência das verificações é: autenticação, validação estrutural, `INVALID_VALUE`, `INVALID_TYPE`, idempotência, `INVALID_ACCOUNT`, titularidade e `INACTIVE_ACCOUNT`. Valor e tipo são conferidos antes de qualquer acesso ao banco: uma requisição com valor ou tipo inválido responde o código correspondente mesmo que a conta não exista, pertença a outro correntista ou esteja inativa, sem revelar nada sobre a conta e sem emitir o evento 5301. Com valor e tipo inválidos ao mesmo tempo, a resposta é `INVALID_VALUE`. As mensagens não repetem o valor recebido.

Até a correção do TODO-003, em 1º de outubro de 2026, o DTO também validava valor e tipo; por isso essas falhas eram respondidas como erro de validação por campo e os dois códigos não chegavam ao cliente.

## 3. Contrato monetário

- A API, a aplicação e os testes usam `decimal`; `double` e `float` são proibidos fora da adaptação inevitável ao esquema legado.
- A escala aceita é de zero a duas casas decimais. Não há arredondamento implícito de entrada: valores com mais de duas casas são rejeitados como `INVALID_VALUE`.
- A forma canônica sempre terá duas casas e usará cultura invariável, por exemplo `125.50`.
- O limite por movimento é `9999999999.99`, reduzindo abuso e mantendo o contrato abaixo da capacidade de `decimal` e do texto canônico.
- IDs e valores gerados pelo servidor não dependem da cultura ou do fuso da máquina.
- Temporariamente, o repositório converte o `decimal` validado para o tipo aceito pelo `REAL` do SQLite somente no limite de persistência. Essa compatibilidade não elimina o risco de ponto flutuante e não conclui SEC-005.
- A consulta de saldo não somará o histórico a cada requisição: o saldo será mantido em centavos inteiros na projeção `saldo_conta`, atualizada na mesma transação do movimento, conforme `ESPECIFICACAO_SALDO.md`. A conversão de cada valor persistido para `decimal` fica restrita ao preenchimento inicial e à reconciliação da projeção.

## 4. Idempotência

### 4.1 Identidade da chave

- `idRequisicao` é validado como UUID e normalizado para `Guid.ToString("D")` em minúsculas.
- A chave normalizada é armazenada em `idempotencia.chave_idempotencia`.
- A chave é global para o endpoint de movimentação, não por conta.
- Chaves não são reutilizáveis com outro payload, mesmo após conflito ou repetição.

### 4.2 Representação canônica da requisição

O campo `idempotencia.requisicao` não armazena o JSON bruto. Ele contém uma representação interna versionada, sem espaços e independente da ordem das propriedades:

```text
v2|titular=04b276dc-0f45-4efc-bffc-911110198733|conta=FA99D033-7067-ED11-96C6-7C5DFA4A16C9|valor=125.50|tipo=C
```

Regras:

1. `idRequisicao` não entra no conteúdo canônico porque já é a chave de busca.
2. A conta é canonicalizada sem acesso ao banco: espaços externos são removidos e letras ASCII são convertidas para maiúsculas de forma invariável. Espaços internos não são corrigidos. Na primeira execução, a busca usa comparação ordinal sem diferenciar maiúsculas/minúsculas, e o valor efetivamente persistido em `contacorrente.idcontacorrente` é usado no movimento.
3. O valor usa cultura invariável e exatamente duas casas.
4. O tipo preserva somente `C` ou `D` após validação; valores alternativos não são convertidos.
5. O titular é o UUID do correntista autenticado, em minúsculas. Assim, a mesma chave usada por outro correntista produz conteúdo diferente e resulta em conflito, sem revelar o resultado original.
6. A versão `v2` substituiu a `v1`, anterior à autenticação. Registros `v1` não possuem titular, nunca coincidem com uma representação `v2` e produzem conflito na reutilização da chave.
7. A comparação é ordinal e exata.

### 4.3 Resultado armazenado

`idempotencia.resultado` armazena somente o resultado interno mínimo e versionado:

```text
v1|idMovimento=34e56aa7-703f-46d8-8f65-100f2795a87b
```

O serviço reconstrói o DTO HTTP a partir desse valor. Headers transitórios, correlation ID, timestamp, mensagem localizada e JSON bruto não são persistidos.

### 4.4 Semântica das execuções

#### Primeira execução válida

Cria exatamente um movimento e um registro idempotente e atualiza o saldo consolidado da conta, tudo na mesma transação, e retorna o ID criado.

#### Repetição idêntica

Encontra a chave, compara a requisição canônica, reconstrói o resultado original e não executa novamente as validações mutáveis da conta. Assim, uma resposta bem-sucedida continua recuperável mesmo que a conta seja desativada depois.

#### Mesma chave com payload diferente

Não cria ou altera dados e retorna `409 IDEMPOTENCY_CONFLICT`.

#### Requisição inválida

Falhas de transporte ou de negócio não serão gravadas na tabela de idempotência. A mesma chave poderá ser tentada novamente após correção da causa, desde que nenhuma execução bem-sucedida tenha sido confirmada.

#### Falha inesperada

Movimento e registro idempotente sofrem rollback. Uma nova tentativa pode executar normalmente.

### 4.5 Algoritmo transacional e concorrência

1. Validar a estrutura da requisição no DTO; em seguida, no normalizador, validar valor (`INVALID_VALUE`) e tipo (`INVALID_TYPE`) e construir a requisição canônica, tudo antes de abrir a transação.
2. Abrir uma conexão pela `ISqliteConnectionFactory`.
3. Abrir transação imediata com `BeginTransaction(deferred: false)`, obtendo a reserva de escritor antes das leituras decisórias.
4. Consultar `idempotencia` pela chave normalizada.
5. Se existir, comparar o conteúdo canônico e retornar repetição ou conflito sem inserir dados.
6. Se não existir, buscar a conta por comparação ordinal sem diferenciar maiúsculas/minúsculas, usando o identificador persistido no movimento.
7. Validar, nesta ordem, existência da conta, titularidade em `titularidade_conta` e situação ativa.
8. Gerar `idMovimento` como UUID canônico e `datamovimento` em UTC, no formato legado `dd/MM/yyyy` com cultura invariável.
9. Inserir o movimento com SQL parametrizado.
10. Atualizar `saldo_conta`: ler o saldo em centavos, somar ou subtrair o valor com aritmética inteira verificada e incrementar `versao`.
11. Inserir chave, requisição canônica e resultado mínimo com SQL parametrizado.
12. Confirmar a transação.
13. Em qualquer exceção anterior ao commit, executar rollback por descarte da transação e propagar um erro seguro.

Como o SQLite admite um escritor por vez, a transação imediata serializa duas primeiras execuções simultâneas. A vencedora confirma movimento, saldo e chave; a seguinte lê o registro confirmado e retorna repetição ou conflito. A chave primária de `idempotencia` continua sendo a defesa final contra duplicidade.

Não há lock em memória, pois ele não protege múltiplos processos e criaria uma falsa garantia diferente da atomicidade do banco.

## 5. Limites e proteção contra abuso

O endpoint implementado aplica:

- corpo máximo: 4 KiB;
- timeout da operação bancária: 5 segundos, respeitando o cancellation token da requisição;
- rate limit específico: 30 requisições por minuto por correntista autenticado, janela fixa, fila desabilitada; requisições sem identidade válida são contadas por IP;
- rate limit global de segurança: 120 requisições por minuto por IP, aplicado antes da autenticação;
- concorrência do endpoint: no máximo 8 operações em execução no processo, sem fila;
- excesso de frequência ou concorrência: HTTP 429;
- chave idempotente e correlation ID continuam sujeitos aos respectivos limites de formato;
- tentativas idempotentes repetidas também consomem limite, evitando uso da tabela como canal de consulta irrestrito.

O limite global por IP protege também o caminho de validação de token; requisições rejeitadas com 401 consomem limite. NAT, proxies e endereços compartilhados reduzem a precisão desse limite global.

## 6. Logs estruturados

Eventos implementados para o componente `Movement`:

| Event ID | Nível | Evento | Campos permitidos |
| --- | --- | --- | --- |
| 5100 | Information | primeira execução confirmada | `CorrelationId`, `MovementId`, `IdempotencyFingerprint`, `Outcome` |
| 5101 | Information | repetição idempotente | `CorrelationId`, `MovementId`, `IdempotencyFingerprint`, `Outcome` |
| 5102 | Warning | conflito de chave | `CorrelationId`, `IdempotencyFingerprint`, `Outcome` |
| 5103 | Warning | rejeição de regra de negócio | `CorrelationId`, `RuleCode`, `Outcome` |
| 5104 | Error | rollback inesperado | `CorrelationId`, `ExceptionType`, `Outcome` |
| 5105 | Warning | limite excedido | `CorrelationId`, `LimitName`, `Outcome` |

A negativa por titularidade não emite o evento 5103: é registrada pelo evento 5301 do componente `Security`, com fingerprints do correntista e da conta, conforme `ESPECIFICACAO_AUTENTICACAO.md`. A falha de autenticação é registrada pelo evento 5300.

`IdempotencyFingerprint` é composto pelos primeiros 16 caracteres hexadecimais do SHA-256 da chave normalizada. Não são registrados:

- chave idempotente integral;
- ID ou número da conta;
- nome do titular;
- valor e tipo do movimento;
- corpo ou representação canônica da requisição;
- connection string, caminho do banco, SQL, stack trace ou mensagem bruta de exceção.

Campos controlados pelo cliente são valores estruturados validados, nunca interpolação livre de texto.

## 7. Arquitetura implementada

A implementação mantém as dependências já presentes: ASP.NET Core, MediatR, Dapper e Microsoft.Data.Sqlite, sem pacote funcional novo.

Fluxo implementado:

```text
Controllers/MovementController
  -> IMediator.Send(CreateMovementCommand)
    -> CreateMovementCommandHandler
      -> IUnitOfWork (uma conexão, uma transação imediata)
        -> repositórios de Idempotencia, ContaCorrente, TitularidadeConta, Movimento e SaldoConta
```

Responsabilidades:

- controller (`Questao5/Controllers`): contrato HTTP, status, obtenção do correntista a partir do token e envio ao Mediator;
- request/command: DTO mínimo e validações estruturais;
- normalizador: validação de valor e tipo e representação canônica da requisição;
- handler: orquestra o caso de uso, na ordem da seção 4.5, usando a unidade de trabalho, os repositórios e as entidades;
- entidades (`Domain/Entities`): `Movimento` cria o movimento com a data UTC no formato legado; `SaldoConta` aplica crédito ou débito em centavos com aritmética verificada; `Idempotencia` monta e lê o resultado versionado; `TitularidadeConta` diz se a conta pertence ao correntista; `ContaCorrente` traz a situação da conta;
- `AccountAccessPolicy`: regra única "conta existe, é do titular e está ativa", compartilhada com a consulta de saldo;
- repositórios: um por entidade, com leitura em `Infrastructure/Database/QueryStore` e escrita em `Infrastructure/Database/CommandStore`, e interfaces em `Domain/Repositories`; todo SQL é parametrizado;
- unidade de trabalho (`IUnitOfWork`): entrega todos os repositórios ligados à mesma conexão e à mesma transação; confirmada com `Commit`, e descartada sem confirmação desfaz tudo;
- relógio injetável: obtenção de UTC testável por `TimeProvider` nativo;
- gerador de IDs injetável: testes determinísticos sem dependência adicional;
- exceções de negócio: códigos estáveis tratados pelo mecanismo global.

Há um repositório por tabela, mas nenhum abre conexão própria: a atomicidade entre movimento, saldo e idempotência é garantida pela unidade de trabalho. A conversão do `decimal` para o `REAL` legado acontece somente no repositório de escrita de `Movimento`.

Até a refatoração de 1º de outubro de 2026, o acesso a dados ficava concentrado em um único `MovementStore`, sem entidades; o comportamento e o SQL executado são os mesmos.

## 8. Matriz de testes implementada

### Contrato e validação HTTP

- sucesso de crédito e débito retorna HTTP 200 e UUID do movimento;
- campos ausentes ou `null`, `valor` não numérico, JSON inválido, propriedade desconhecida, UUID fora do formato, conta vazia e corpo acima de 4 KiB retornam erro estrutural;
- valor zero, negativo, acima do máximo e com mais de duas casas retorna `INVALID_VALUE`;
- tipo vazio, em branco, minúsculo, longo ou diferente de `C`/`D` retorna `INVALID_TYPE`;
- valor e tipo inválidos juntos retornam `INVALID_VALUE`, e valor inválido para conta inexistente, alheia ou inativa retorna `INVALID_VALUE` sem evento 5301;
- as rejeições por valor e tipo não gravam dados, não reservam a chave de idempotência, emitem o evento 5103 com o código e não repetem o valor recebido;
- Problem Details contém código e correlation ID sem informação interna.

### Regras e persistência

- conta inexistente retorna `INVALID_ACCOUNT` e não grava;
- conta inativa retorna `INACTIVE_ACCOUNT` e não grava;
- movimento válido usa o ID persistido da conta e respeita a FK;
- data é UTC, invariável e gravada no formato legado definido;
- comandos Dapper são parametrizados;
- falha após inserir movimento causa rollback também do movimento.

### Idempotência

- primeira execução cria um movimento e uma chave;
- repetição idêntica retorna o mesmo ID e mantém um movimento;
- repetição após desativação da conta ainda recupera o resultado original;
- mesma chave com conta, valor ou tipo diferente retorna 409;
- conflito não altera movimento nem registro original;
- falha de negócio não reserva a chave;
- falha inesperada não reserva a chave;
- duas ou mais requisições idênticas concorrentes criam um movimento;
- requisições concorrentes conflitantes produzem um sucesso e conflitos, sem duplicidade;
- representações JSON com ordem ou formatação diferentes resultam na mesma forma canônica;
- a mesma chave usada por outro correntista retorna 409 sem revelar o resultado original;
- registro `v1` preexistente produz conflito na reutilização da chave.

### Autenticação, titularidade e projeção de saldo

- requisição sem token ou com token inválido retorna 401 e não grava;
- conta de outro correntista, inclusive inativa, ou sem titularidade retorna `INVALID_ACCOUNT` com corpo idêntico ao de conta não cadastrada e não grava;
- crédito e débito atualizam `saldo_conta` em centavos e incrementam `versao` exatamente uma vez;
- repetição idempotente não altera saldo nem versão;
- falha após inserir o movimento reverte também o saldo;
- movimentações concorrentes distintas não perdem atualização de saldo;
- ausência da linha de projeção impede a movimentação;
- a reconciliação detecta saldo ou movimento alterado fora da aplicação.

### Segurança, abuso e observabilidade

- rate limit e limite de concorrência retornam 429; o limite específico é contado por correntista e requisições sem identidade por IP;
- cancellation token e timeout interrompem espera sem estado parcial;
- logs esperados são emitidos com Event IDs estáveis;
- logs não contêm payload, conta, valor, chave integral, SQL ou connection string;
- fingerprint é estável para a mesma chave;
- gate completo, auditoria, secret scanning, build sem avisos e cobertura são executados.

### Documentação OpenAPI

O contrato desta especificação é publicado no documento OpenAPI (Swagger, somente em Development): resumo e descrição da operação, os retornos 200, 400, 401, 409, 413, 415, 429, 500 e 504, descrição e exemplo de cada atributo e exemplos nomeados de cada situação de erro. O HTTP 400 é documentado com um único schema e exemplos para os dois formatos de corpo, o de regra de negócio (com `code`) e o estrutural (com `errors`).

Um teste compara cada exemplo de erro com a resposta real do endpoint. Por isso os exemplos registram também as particularidades do comportamento atual: a resposta 429 não traz `type`, e a resposta 413 não traz `type` nem `traceId`.

## 9. Governança de mudanças

O TODO-002 foi concluído em 1º de outubro de 2026 após autorizações específicas para as entregas C1, C2 e C3. Qualquer mudança futura de rota, DTO, status HTTP, semântica idempotente, limite monetário ou estratégia transacional deverá atualizar este documento antes do código.

## 10. Histórico — autenticação, titularidade e projeção de saldo

Até a Entrega C4 o endpoint era anônimo, a chave de idempotência usava a representação `v1`, sem titular, e o limite específico era contado por IP. A Entrega F0, de 1º de outubro de 2026, planejou a autenticação por JWT, a autorização por titular, a idempotência `v2` e a manutenção da projeção `saldo_conta`. A Entrega F1 implementou a exigência de token e a resposta 401; a Entrega F2 implementou as demais alterações. As seções 1 a 8 já descrevem o estado resultante.

As regras de identidade, token e titularidade estão em `ESPECIFICACAO_AUTENTICACAO.md`; a estrutura, o preenchimento inicial e a reconciliação da projeção estão em `ESPECIFICACAO_SALDO.md`.
