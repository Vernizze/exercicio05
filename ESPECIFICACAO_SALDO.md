# Serviço de consulta de saldo — especificação planejada

## 1. Escopo e estado

Este documento consolida a Entrega D, realizada em 1º de outubro de 2026, e define o contrato e o desenho técnico da futura consulta de saldo. A entrega é exclusivamente de planejamento: nenhum endpoint, query, handler, store, configuração operacional ou teste executável de saldo foi implementado.

Fazem parte desta especificação:

- requisitos funcionais do enunciado;
- contrato HTTP versionado e DTO de resposta;
- validações de entrada e regras de negócio;
- cálculo monetário determinístico sobre o esquema legado;
- formato externo da data e hora da consulta;
- limites operacionais e proteção contra abuso;
- eventos estruturados e dados proibidos em logs;
- arquitetura planejada e matriz de testes;
- riscos residuais e critérios para autorizar a implementação.

Permanecem fora do escopo desta entrega:

- implementação da consulta de saldo;
- alteração do endpoint de movimentação;
- cadastro ou manutenção de contas;
- autenticação e autorização, dispensadas somente para este exercício;
- migração de `movimento.valor` de `REAL` para centavos inteiros;
- correção do tipo histórico de `movimento.idcontacorrente`;
- mudanças no schema, na fixture ou nos dados operacionais;
- inclusão de dependências, CI/CD ou infraestrutura.

## 2. Requisitos consolidados

O enunciado exige que o serviço:

1. receba a identificação da conta corrente;
2. aceite somente conta cadastrada, retornando `INVALID_ACCOUNT` quando ela não existir;
3. aceite somente conta ativa, retornando `INACTIVE_ACCOUNT` quando ela estiver inativa;
4. calcule o saldo pelos movimentos persistidos até o momento;
5. use a fórmula `soma dos créditos - soma dos débitos`;
6. devolva `0.00` quando a conta não possuir movimentos;
7. retorne HTTP 200 com número da conta, nome do titular, data e hora da consulta e saldo atual;
8. retorne HTTP 400 com mensagem descritiva e tipo da falha quando uma regra de negócio não for atendida.

As contas já são fornecidas pelo bootstrap. Não será criado serviço de cadastro.

## 3. Contrato HTTP

### 3.1 Rota

```http
GET /api/v1/contas/{idContaCorrente}/saldo
```

Decisões:

- `GET` representa uma consulta sem efeito colateral intencional;
- a versão permanece explícita na URL;
- a identificação da conta é um parâmetro de rota, sem body;
- query string alternativa e consulta por número da conta não serão aceitas;
- a rota não diferencia maiúsculas e minúsculas ao localizar o identificador persistido, preservando o comportamento da movimentação;
- respostas bem-sucedidas não devem ser armazenadas por caches compartilhados: a implementação deverá enviar `Cache-Control: no-store`.

### 3.2 Parâmetro de rota

| Parâmetro | Tipo | Obrigatório | Regras |
| --- | --- | --- | --- |
| `idContaCorrente` | texto | Sim | remover somente espaços externos; resultado entre 1 e 37 caracteres; usar o valor normalizado apenas em SQL parametrizado |

Quando o segmento não existe, a rota não corresponde e a API retorna HTTP 404. Um valor presente, mas vazio após normalização ou acima de 37 caracteres, é erro estrutural HTTP 400 produzido pela validação da API. Um identificador estruturalmente válido, mas não cadastrado, produz `INVALID_ACCOUNT`.

Não será exigido formato UUID porque a fixture histórica contém um identificador com espaço interno e o schema declara apenas `TEXT(37)`.

### 3.3 Resposta de sucesso

```http
HTTP/1.1 200 OK
Content-Type: application/json
Cache-Control: no-store
X-Correlation-ID: 2dfe616b5f034b169b3e81745e189f68
```

```json
{
  "numeroContaCorrente": 456,
  "nomeTitular": "Eva Woodward",
  "dataHoraConsulta": "2026-10-01T23:59:58.0000000+00:00",
  "saldoAtual": 115.25
}
```

| Campo | Tipo JSON | Regra |
| --- | --- | --- |
| `numeroContaCorrente` | inteiro | número persistido da conta validada |
| `nomeTitular` | texto | nome persistido do titular da conta validada |
| `dataHoraConsulta` | texto | instante UTC no formato round-trip `O`, com offset `+00:00` e cultura invariável |
| `saldoAtual` | número decimal | créditos menos débitos, com escala lógica de duas casas |

O modelo de aplicação deve manter `saldoAtual` como `decimal`. O JSON deve usar número, não texto. O exemplo `0.00` expressa a escala monetária, mas consumidores não podem depender da preservação lexical de zeros finais em um número JSON.

`dataHoraConsulta` representa o instante em que a leitura consistente foi concluída. O valor será obtido de `TimeProvider.GetUtcNow()` somente depois de conta e movimentos terem sido lidos e validados, e será formatado explicitamente com `ToString("O", CultureInfo.InvariantCulture)`.

### 3.4 Erros

Erros usam `application/problem+json`, o tratamento global existente e a extensão `correlationId`.

| Situação | HTTP | `code` | Observação |
| --- | --- | --- | --- |
| segmento de conta ausente | 404 | não aplicável | a rota não corresponde |
| parâmetro vazio após normalização ou acima do limite | 400 | validação por campo | erro estrutural, sem consulta ao banco |
| conta não cadastrada | 400 | `INVALID_ACCOUNT` | exigido pelo enunciado |
| conta inativa | 400 | `INACTIVE_ACCOUNT` | exigido pelo enunciado |
| frequência ou concorrência excedida | 429 | `RATE_LIMIT_EXCEEDED` | Problem Details correlacionado |
| timeout do servidor | 504 | `REQUEST_TIMEOUT` | Problem Details correlacionado |
| dado monetário persistido incompatível ou falha inesperada | 500 | não exposto | mensagem genérica; diagnóstico somente em log seguro |

As mensagens de `INVALID_ACCOUNT` e `INACTIVE_ACCOUNT` seguirão o contrato já usado pela movimentação. A distinção exigida pelo enunciado permite enumeração do estado da conta; esse risco é aceito somente no exercício anônimo e deverá ser reavaliado com autenticação antes de uso real.

## 4. Cálculo monetário

### 4.1 Fórmula

```text
saldo = soma(movimentos C) - soma(movimentos D)
```

Sem movimentos, o saldo lógico é `0.00m`.

### 4.2 Estratégia sobre o `REAL` legado

O SQLite não deverá executar `SUM(valor)` para formar o resultado final, pois isso manteria a aritmética em ponto flutuante binário. A implementação planejada deverá:

1. abrir uma conexão pela `ISqliteConnectionFactory`;
2. iniciar uma transação de leitura para obter um snapshot consistente;
3. buscar a conta por SQL parametrizado e validar existência e atividade;
4. buscar `tipomovimento` e `valor` de todos os movimentos da conta no mesmo snapshot;
5. materializar o `REAL` legado como `double` somente no adaptador SQLite;
6. rejeitar valores não finitos e converter cada valor individualmente com `Convert.ToDecimal` para o domínio `decimal`;
7. rejeitar como erro interno valores não positivos, acima do limite histórico ou com escala lógica incompatível, sem arredondamento silencioso;
8. acumular créditos e débitos com operações `decimal` verificadas;
9. normalizar o zero para `0.00m` e retornar um `decimal` com escala lógica de duas casas.

A conversão compensatória pertence exclusivamente ao adaptador de persistência. Query, handler, controller e DTO não usarão `double`.

### 4.3 Limites e overflow

- cada movimento permanece sujeito ao limite histórico de `9999999999.99`;
- o acumulador usa `decimal` e operação verificada;
- overflow, tipo de movimento incompatível ou valor persistido fora do contrato produz falha interna segura;
- não haverá saturação, truncamento, arredondamento implícito ou retorno parcial;
- a futura migração para centavos inteiros continua sendo a correção estrutural recomendada e não faz parte da consulta de saldo.

## 5. Consistência e concorrência

A conta e seus movimentos deverão ser lidos na mesma conexão e transação de leitura. Isso evita combinar metadados de um instante com movimentos de outro durante gravações concorrentes.

O fluxo planejado é:

1. validar e normalizar o identificador recebido;
2. abrir conexão e transação de leitura;
3. ler e validar a conta;
4. ler os movimentos associados pelo identificador persistido da conta;
5. calcular o saldo em `decimal`;
6. concluir a leitura;
7. obter o instante UTC da resposta;
8. retornar o DTO.

A consulta é somente leitura e não usa idempotência. Cancelamento e timeout devem ser propagados até o acesso a dados. Não será usado lock de escrita nem transação imediata.

## 6. Limites operacionais

A consulta terá configuração própria, sem reutilizar semanticamente `MovementOperationalOptions`:

| Controle | Valor inicial planejado |
| --- | --- |
| timeout | 5 segundos |
| frequência específica | 30 requisições por minuto por IP |
| concorrência específica | 8 consultas em execução no processo, sem fila |
| limite global existente | 120 requisições por minuto por IP |

Não há body cujo tamanho precise ser configurado. O identificador é limitado pelo contrato e pelos limites gerais do servidor para URL e headers.

O IP continua sendo apenas controle compensatório do exercício anônimo. Limites por identidade e por conta dependem de autenticação futura. Tentativas inválidas também consomem os limites, reduzindo enumeração irrestrita.

## 7. Logs estruturados

Eventos planejados para o componente `Balance`:

| Event ID | Nível | Evento | Campos permitidos |
| --- | --- | --- | --- |
| 5200 | Information | consulta concluída | `CorrelationId`, `AccountFingerprint`, `Outcome` |
| 5201 | Warning | rejeição de regra de negócio | `CorrelationId`, `AccountFingerprint`, `RuleCode`, `Outcome` |
| 5202 | Error | falha inesperada | `CorrelationId`, `AccountFingerprint`, `ExceptionType`, `Outcome` |
| 5203 | Warning | limite excedido | `CorrelationId`, `LimitName`, `Outcome` |

`AccountFingerprint` será formado pelos primeiros 16 caracteres hexadecimais do SHA-256 do identificador normalizado. Ele permite correlacionar tentativas sem registrar o identificador em claro e não substitui autenticação.

Não serão registrados:

- identificador ou número da conta em claro;
- nome do titular;
- saldo, créditos, débitos ou valores individuais;
- lista ou quantidade de movimentos;
- payload, URL completa ou parâmetro de rota bruto;
- SQL, connection string, caminho do banco, stack trace ou mensagem bruta de exceção.

Campos controlados pelo cliente serão normalizados ou derivados antes do log. O timestamp será fornecido pelo pipeline de logging.

## 8. Arquitetura planejada

A implementação deverá usar apenas as dependências existentes: ASP.NET Core, MediatR, Dapper e Microsoft.Data.Sqlite.

Fluxo planejado:

```text
BalanceController
  -> IMediator.Send(GetBalanceQuery)
    -> GetBalanceQueryHandler
      -> IBalanceQueryStore
        -> ISqliteConnectionFactory
```

Responsabilidades:

- `BalanceController`: rota, validação estrutural, status HTTP, headers de cache e envio ao Mediator;
- `GetBalanceQuery`: identificador normalizado da conta;
- `GetBalanceQueryHandler`: orquestração da consulta, sem SQL ou regra de serialização;
- `IBalanceQueryStore`: contrato de leitura da conta e cálculo monetário;
- `BalanceQueryStore`: snapshot SQLite, SQL parametrizado, validações e adaptação do `REAL` para `decimal`;
- `GetBalanceResponse`: resposta interna com número, titular, instante UTC e saldo;
- `GetBalanceHttpResponse`: contrato público fechado;
- `BalanceLogger`: Event IDs 5200–5203 e fingerprint seguro;
- `TimeProvider`: relógio UTC injetável para resposta e testes determinísticos;
- `BusinessRuleException`: reutilização dos códigos `INVALID_ACCOUNT` e `INACTIVE_ACCOUNT`.

Pastas e namespaces devem seguir a separação atual entre `Application`, `Infrastructure/Database/QueryStore` e `Infrastructure/Services`.

## 9. Matriz de testes planejada

### Contrato HTTP e inventário

- conta ativa sem movimentos retorna HTTP 200 e saldo zero;
- crédito, débito e combinação retornam o saldo esperado;
- resposta contém número, titular, instante UTC canônico e saldo decimal;
- `Cache-Control: no-store` e `X-Correlation-ID` estão presentes;
- segmento ausente não corresponde à rota; identificador vazio após normalização e identificador acima do limite têm resposta controlada;
- Swagger passa a inventariar somente `POST /api/v1/movimentos` e `GET /api/v1/contas/{idContaCorrente}/saldo`;
- o DTO não expõe identificador interno, situação da conta ou movimentos.

### Regras de negócio

- conta inexistente retorna `INVALID_ACCOUNT`;
- conta inativa retorna `INACTIVE_ACCOUNT`;
- comparação do identificador preserva o comportamento sem diferenciação de caixa;
- erros usam Problem Details e correlation ID;
- nenhuma falha retorna nome, saldo, SQL ou detalhe interno.

### Monetário e persistência

- ausência de movimentos produz `0.00m`;
- apenas créditos, apenas débitos e saldo negativo;
- centavos como `0.01`, `0.10` e `9999999999.99`;
- somas repetidas, cancelamento exato de crédito e débito e grande quantidade de movimentos;
- resultado independente da cultura atual do processo;
- valor não finito, escala incompatível, tipo inválido e overflow persistidos artificialmente falham de forma segura;
- leitura não altera conta, movimento, idempotência ou fixture;
- consulta concorrente com movimentação observa um snapshot consistente.

### Data e hora

- relógio fixo produz exatamente o formato round-trip UTC esperado;
- cultura e fuso local do processo não alteram a resposta;
- transição de data em UTC é coberta;
- o instante é obtido depois da conclusão da leitura.

### Limites, abuso e logs

- frequência específica e global retornam HTTP 429;
- concorrência excedida não entra em fila;
- timeout retorna HTTP 504 e propaga cancelamento;
- tentativas inválidas consomem limite;
- eventos 5200–5203 possuem IDs, níveis e campos esperados;
- logs não contêm conta, titular, saldo, valores, SQL, caminho ou mensagem sensível;
- fingerprint é estável, truncado e distinto para identificadores distintos.

### Regressão

- todos os testes existentes de bootstrap, segurança e movimentação continuam passando;
- o endpoint de movimentação e seu contrato não mudam;
- a fixture mantém o SHA-256 esperado;
- o gate completo permanece reproduzível.

## 10. Riscos residuais e decisões explícitas

1. **Autenticação e autorização ausentes:** risco aceito somente para o exercício; qualquer conhecedor de um identificador pode consultar dados bancários.
2. **Enumeração de contas:** `INVALID_ACCOUNT` e `INACTIVE_ACCOUNT` distintos são exigidos pelo enunciado; rate limiting, mensagens controladas e ausência de dados adicionais são compensatórios incompletos.
3. **`REAL` legado:** a soma em `decimal` reduz propagação de ponto flutuante, mas não recupera precisão já perdida no armazenamento. A solução definitiva é migração separada para centavos inteiros.
4. **Tipo divergente da chave estrangeira:** SQLite permite o relacionamento histórico, mas SEC-007 continua bloqueado para migração própria.
5. **Consistência local:** a transação fornece snapshot no SQLite local; topologias distribuídas ou réplicas não fazem parte do projeto.
6. **Limites por IP:** NAT, proxies e endereços compartilhados reduzem precisão do controle; identidade autenticada é necessária em ambiente real.
7. **Dados pessoais na resposta:** nome e número são exigidos; `no-store`, TLS no ambiente e autorização futura são necessários para reduzir exposição.

## 11. Critérios para a futura implementação

A implementação somente poderá começar após autorização expressa e deverá ser dividida em mudanças pequenas e revisáveis. Antes do commit funcional será obrigatório:

1. confirmar branch e workspace limpos;
2. preservar fixture e schema;
3. implementar núcleo/query store e testes monetários primeiro;
4. implementar endpoint e contrato HTTP em etapa própria;
5. implementar limites e logs em etapa própria;
6. atualizar o inventário OpenAPI;
7. executar restore bloqueado, auditoria, secret scanning, formatação, build, testes e cobertura;
8. revisar riscos OWASP API/CWE, dados em logs e riscos residuais;
9. atualizar `TODO.md`, `CONVERSAS.md` e esta especificação com o estado efetivamente implementado;
10. criar commit funcional isolado somente após o gate completo passar.
