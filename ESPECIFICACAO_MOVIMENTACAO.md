# Entrega B — Especificação do serviço de movimentação

## 1. Escopo e estado

Esta entrega, planejada em 1º de outubro de 2026, fecha o contrato e o desenho técnico necessários para implementar o TODO-002. Ela não adiciona endpoint, persistência funcional, rate limiting ou migração de esquema.

Fazem parte desta especificação:

- contrato HTTP e DTOs da movimentação;
- validações de entrada e de negócio;
- representação monetária na aplicação;
- semântica da chave de idempotência;
- normalização determinística da requisição;
- algoritmo transacional e comportamento concorrente;
- limites de entrada e proteção contra abuso;
- eventos estruturados e dados proibidos em logs;
- arquitetura prevista e matriz mínima de testes.

Permanecem fora do escopo:

- consulta de saldo;
- cadastro de contas;
- autenticação e autorização, dispensadas somente para este exercício;
- migração de `movimento.valor` de `REAL` para centavos inteiros;
- correção do tipo histórico de `movimento.idcontacorrente`;
- implementação do TODO-002, que dependerá de autorização específica.

## 2. Contrato HTTP

### 2.1 Rota

```http
POST /api/v1/movimentos
Content-Type: application/json
```

O versionamento na URL torna o endpoint inventariável e permite evolução sem alteração silenciosa do contrato.

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

O DTO será fechado e conterá somente esses quatro campos. Propriedades desconhecidas deverão ser rejeitadas para impedir overposting e erros silenciosos de contrato.

`idRequisicao` não será aceito no header `X-Correlation-ID`. Correlação e idempotência têm semânticas distintas.

### 2.3 Sucesso e repetição idêntica

Tanto a primeira execução bem-sucedida quanto uma repetição idêntica retornarão:

```http
HTTP/1.1 200 OK
Content-Type: application/json
```

```json
{
  "idMovimento": "34e56aa7-703f-46d8-8f65-100f2795a87b"
}
```

A repetição idêntica devolverá exatamente o mesmo `idMovimento`, sem inserir outro movimento. O `X-Correlation-ID` refletirá a tentativa HTTP atual, não a tentativa original.

### 2.4 Erros

Erros usarão `application/problem+json`, o tratamento global existente e a extensão `correlationId`.

| Situação | HTTP | `code` | Observação |
| --- | --- | --- | --- |
| JSON inválido, campo ausente, formato ou limite inválido | 400 | validação por campo | resposta de validação do ASP.NET Core |
| Conta não cadastrada | 400 | `INVALID_ACCOUNT` | exigido pelo enunciado |
| Conta inativa | 400 | `INACTIVE_ACCOUNT` | exigido pelo enunciado |
| Valor não positivo, acima do limite ou com escala maior que 2 | 400 | `INVALID_VALUE` | regra de negócio estável |
| Tipo diferente de `C` ou `D` | 400 | `INVALID_TYPE` | regra de negócio estável |
| Mesma chave com requisição normalizada diferente | 409 | `IDEMPOTENCY_CONFLICT` | conflito, não uma nova execução |
| Limite de frequência excedido | 429 | `RATE_LIMIT_EXCEEDED` | inclui `Retry-After` quando calculável |
| Falha inesperada ou indisponibilidade do banco | 500 | sem detalhe interno | não expõe SQL, caminho ou exceção |

O conflito idempotente usa HTTP 409 porque a requisição isoladamente pode ser válida, mas é incompatível com o recurso de idempotência já identificado pela chave.

## 3. Contrato monetário

- A API, a aplicação e os testes usarão `decimal`; `double` e `float` são proibidos fora da adaptação inevitável ao esquema legado.
- A escala aceita é de zero a duas casas decimais. Não haverá arredondamento implícito de entrada: valores com mais de duas casas serão rejeitados como `INVALID_VALUE`.
- A forma canônica sempre terá duas casas e usará cultura invariável, por exemplo `125.50`.
- O limite por movimento será `9999999999.99`, reduzindo abuso e mantendo o contrato abaixo da capacidade de `decimal` e do texto canônico.
- IDs e valores gerados pelo servidor não dependerão da cultura ou do fuso da máquina.
- Temporariamente, o repositório converterá o `decimal` validado para o tipo aceito pelo `REAL` do SQLite somente no limite de persistência. Essa compatibilidade não elimina o risco de ponto flutuante e não conclui SEC-005.
- A futura consulta de saldo deverá converter cada valor persistido para `decimal`, normalizá-lo para escala 2 e efetuar toda soma/subtração em `decimal`. A política final de arredondamento de cálculos derivados será `MidpointRounding.ToEven`, aplicada somente quando uma operação produzir escala superior a 2.

## 4. Idempotência

### 4.1 Identidade da chave

- `idRequisicao` será validado como UUID e normalizado para `Guid.ToString("D")` em minúsculas.
- A chave normalizada será armazenada em `idempotencia.chave_idempotencia`.
- A chave é global para o endpoint de movimentação, não por conta.
- Chaves não serão reutilizáveis com outro payload, mesmo após conflito ou repetição.

### 4.2 Representação canônica da requisição

O campo `idempotencia.requisicao` não armazenará o JSON bruto. Ele armazenará uma representação interna versionada, sem espaços e independente da ordem das propriedades:

```text
v1|conta=FA99D033-7067-ED11-96C6-7C5DFA4A16C9|valor=125.50|tipo=C
```

Regras:

1. `idRequisicao` não entra no conteúdo canônico porque já é a chave de busca.
2. A conta é canonicalizada sem acesso ao banco: espaços externos são removidos e letras ASCII são convertidas para maiúsculas de forma invariável. Espaços internos não são corrigidos. Na primeira execução, a busca usa comparação ordinal sem diferenciar maiúsculas/minúsculas, e o valor efetivamente persistido em `contacorrente.idcontacorrente` é usado no movimento.
3. O valor usa cultura invariável e exatamente duas casas.
4. O tipo preserva somente `C` ou `D` após validação; valores alternativos não são convertidos.
5. A versão `v1` permite mudar o algoritmo futuramente sem reinterpretar registros antigos.
6. A comparação será ordinal e exata.

### 4.3 Resultado armazenado

`idempotencia.resultado` armazenará somente o resultado interno mínimo e versionado:

```text
v1|idMovimento=34e56aa7-703f-46d8-8f65-100f2795a87b
```

O serviço reconstruirá o DTO HTTP a partir desse valor. Headers transitórios, correlation ID, timestamp, mensagem localizada e JSON bruto não serão persistidos.

### 4.4 Semântica das execuções

#### Primeira execução válida

Cria exatamente um movimento e um registro idempotente na mesma transação, e retorna o ID criado.

#### Repetição idêntica

Encontra a chave, compara a requisição canônica, reconstrói o resultado original e não executa novamente as validações mutáveis da conta. Assim, uma resposta bem-sucedida continua recuperável mesmo que a conta seja desativada depois.

#### Mesma chave com payload diferente

Não cria ou altera dados e retorna `409 IDEMPOTENCY_CONFLICT`.

#### Requisição inválida

Falhas de transporte ou de negócio não serão gravadas na tabela de idempotência. A mesma chave poderá ser tentada novamente após correção da causa, desde que nenhuma execução bem-sucedida tenha sido confirmada.

#### Falha inesperada

Movimento e registro idempotente sofrem rollback. Uma nova tentativa pode executar normalmente.

### 4.5 Algoritmo transacional e concorrência

1. Validar JSON, tamanhos, UUID, escala, limite e tipo e construir a requisição canônica antes de abrir a transação.
2. Abrir uma conexão pela `ISqliteConnectionFactory`.
3. Abrir transação imediata com `BeginTransaction(deferred: false)`, obtendo a reserva de escritor antes das leituras decisórias.
4. Consultar `idempotencia` pela chave normalizada.
5. Se existir, comparar o conteúdo canônico e retornar repetição ou conflito sem inserir dados.
6. Se não existir, buscar a conta por comparação ordinal sem diferenciar maiúsculas/minúsculas, usando o identificador persistido no movimento.
7. Validar existência e situação ativa.
8. Gerar `idMovimento` como UUID canônico e `datamovimento` em UTC, no formato legado `dd/MM/yyyy` com cultura invariável.
9. Inserir o movimento com SQL parametrizado.
10. Inserir chave, requisição canônica e resultado mínimo com SQL parametrizado.
11. Confirmar a transação.
12. Em qualquer exceção anterior ao commit, executar rollback por descarte da transação e propagar um erro seguro.

Como o SQLite admite um escritor por vez, a transação imediata serializa duas primeiras execuções simultâneas. A vencedora confirma movimento e chave; a seguinte lê o registro confirmado e retorna repetição ou conflito. A chave primária de `idempotencia` continua sendo a defesa final contra duplicidade.

Não haverá lock em memória, pois ele não protege múltiplos processos e criaria uma falsa garantia diferente da atomicidade do banco.

## 5. Limites e proteção contra abuso

Na implementação do endpoint:

- corpo máximo: 4 KiB;
- timeout da operação bancária: 5 segundos, respeitando o cancellation token da requisição;
- rate limit específico: 30 requisições por minuto por IP, janela fixa, fila desabilitada;
- rate limit global de segurança: 120 requisições por minuto por IP;
- concorrência do endpoint: no máximo 8 operações em execução no processo, sem fila;
- excesso de frequência ou concorrência: HTTP 429;
- chave idempotente e correlation ID continuam sujeitos aos respectivos limites de formato;
- tentativas idempotentes repetidas também consomem limite, evitando uso da tabela como canal de consulta irrestrito.

O IP é somente um controle compensatório para o exercício anônimo. Ele não substitui identidade de cliente, autenticação ou autorização e não deve ser reutilizado como modelo de produção.

## 6. Logs estruturados

Eventos previstos para o componente `Movement`:

| Event ID | Nível | Evento | Campos permitidos |
| --- | --- | --- | --- |
| 5100 | Information | primeira execução confirmada | `CorrelationId`, `MovementId`, `IdempotencyFingerprint`, `Outcome` |
| 5101 | Information | repetição idempotente | `CorrelationId`, `MovementId`, `IdempotencyFingerprint`, `Outcome` |
| 5102 | Warning | conflito de chave | `CorrelationId`, `IdempotencyFingerprint`, `Outcome` |
| 5103 | Warning | rejeição de regra de negócio | `CorrelationId`, `RuleCode`, `Outcome` |
| 5104 | Error | rollback inesperado | `CorrelationId`, `ExceptionType`, `Outcome` |
| 5105 | Warning | limite excedido | `CorrelationId`, `LimitName`, `Outcome` |

`IdempotencyFingerprint` será composto pelos primeiros 16 caracteres hexadecimais do SHA-256 da chave normalizada. Não serão registrados:

- chave idempotente integral;
- ID ou número da conta;
- nome do titular;
- valor e tipo do movimento;
- corpo ou representação canônica da requisição;
- connection string, caminho do banco, SQL, stack trace ou mensagem bruta de exceção.

Campos controlados pelo cliente serão valores estruturados validados, nunca interpolação livre de texto.

## 7. Arquitetura prevista

A implementação deverá manter as dependências já presentes: ASP.NET Core, MediatR, Dapper e Microsoft.Data.Sqlite, sem adicionar pacote novo.

Fluxo previsto:

```text
MovementController
  -> IMediator.Send(CreateMovementCommand)
    -> CreateMovementCommandHandler
      -> IMovementService/Store transacional
        -> ISqliteConnectionFactory
```

Responsabilidades:

- controller: contrato HTTP, status e envio ao Mediator;
- request/command: DTO mínimo e validações estruturais;
- handler: orquestração do caso de uso;
- store transacional: toda leitura e escrita dependente da atomicidade na mesma conexão/transação;
- normalizador idempotente: representação canônica e parse do resultado versionado;
- relógio injetável: obtenção de UTC testável, preferencialmente `TimeProvider` nativo;
- gerador de IDs injetável ou função isolada: testes determinísticos sem dependência adicional;
- exceções de negócio: códigos estáveis tratados pelo mecanismo global.

O acesso a conta, movimento e idempotência não será dividido em repositórios que abram conexões independentes dentro do mesmo caso de uso.

## 8. Matriz mínima de testes da implementação

### Contrato e validação HTTP

- sucesso de crédito e débito retorna HTTP 200 e UUID do movimento;
- campos obrigatórios, JSON inválido, propriedade desconhecida e corpo acima de 4 KiB;
- UUIDs, comprimentos, valor zero, negativo, acima do máximo e com mais de duas casas;
- tipos vazio, minúsculo, longo ou diferente de `C`/`D`;
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
- representações JSON com ordem ou formatação diferentes resultam na mesma forma canônica.

### Segurança, abuso e observabilidade

- rate limit e limite de concorrência retornam 429;
- cancellation token e timeout interrompem espera sem estado parcial;
- logs esperados são emitidos com Event IDs estáveis;
- logs não contêm payload, conta, valor, chave integral, SQL ou connection string;
- fingerprint é estável para a mesma chave;
- gate completo, auditoria, secret scanning, build sem avisos e cobertura são executados.

## 9. Critério para iniciar a implementação

O TODO-002 poderá sair do estado planejado somente após autorização específica para implementar esta especificação. Qualquer mudança de rota, DTO, status HTTP, semântica idempotente, limite monetário ou estratégia transacional deverá atualizar este documento antes do código.
