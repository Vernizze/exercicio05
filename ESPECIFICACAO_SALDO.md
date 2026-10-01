# Serviço de consulta de saldo — especificação planejada

## 1. Escopo e estado

Este documento consolida a Entrega D, realizada em 1º de outubro de 2026, e define o contrato e o desenho técnico da futura consulta de saldo. A entrega é exclusivamente de planejamento: nenhum endpoint, query, handler, store, configuração operacional ou teste executável de saldo foi implementado.

Uma revisão documental posterior, também sem implementação funcional, substituiu o cálculo do saldo em tempo real pela **projeção persistida de saldo**. O saldo deixa de ser recalculado a partir de todo o histórico a cada consulta e passa a ser mantido em uma tabela consolidada, atualizada na mesma transação da movimentação. Um cache em memória foi avaliado e **descartado explicitamente** nesta etapa. O cenário foi reconfirmado, naquele momento, como centrado em conta, sem identidade de titular.

Uma segunda revisão documental, a Entrega F0, também de 1º de outubro de 2026, revogou a exceção de autenticação: a consulta passa a exigir JWT e a conta só pode ser consultada por seu titular. As regras de identidade, titularidade e token estão em `ESPECIFICACAO_AUTENTICACAO.md`; este documento registra apenas seus efeitos sobre a consulta de saldo.

Fazem parte desta especificação:

- requisitos funcionais do enunciado;
- efeitos da autenticação e da titularidade sobre a consulta;
- contrato HTTP versionado e DTO de resposta;
- validações de entrada e regras de negócio;
- modelo de leitura baseado em projeção persistida de saldo;
- cálculo monetário determinístico sobre o esquema legado;
- atualização transacional da projeção com o movimento e a idempotência;
- preenchimento inicial e reconciliação da projeção;
- formato externo da data e hora da consulta;
- limites operacionais e proteção contra abuso;
- eventos estruturados e dados proibidos em logs;
- arquitetura planejada e matriz de testes;
- riscos residuais e critérios para autorizar a implementação.

Permanecem fora do escopo desta entrega:

- implementação da consulta de saldo;
- alteração do endpoint de movimentação;
- cadastro ou manutenção de contas;
- emissão de tokens, login e cadastro de correntistas, conforme `ESPECIFICACAO_AUTENTICACAO.md`;
- migração de `movimento.valor` de `REAL` para centavos inteiros;
- correção do tipo histórico de `movimento.idcontacorrente`;
- cache em memória, cache distribuído ou qualquer acelerador de leitura descartável;
- mensageria, projeção assíncrona ou consistência eventual;
- mudanças no schema legado, na fixture ou nos dados operacionais;
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

### 2.1 Conta, titular e identidade

O schema recebido é centrado na **conta corrente**: a tabela `contacorrente` possui apenas `idcontacorrente`, `numero`, `nome` e `ativo`, sem tabela de pessoas, coluna de proprietário ou chave estrangeira para um cliente.

A Entrega F0 acrescentou a identidade do correntista sem alterar essa tabela:

- o correntista é identificado por um UUID recebido na claim `sub` do JWT;
- o vínculo entre conta e correntista é persistido na tabela `titularidade_conta`;
- `nomeTitular` continua sendo o conteúdo de `contacorrente.nome`;
- a conta permanece a unidade de cálculo e de projeção; `saldo_conta` continua indexada por conta;
- somente o titular consulta o saldo da conta.

Antes dessa entrega, o cenário havia sido registrado como sem identidade de titular, com o risco de autorização aceito para o exercício anônimo. Esse registro está superado.

## 3. Contrato HTTP

### 3.1 Rota

```http
GET /api/v1/contas/{idContaCorrente}/saldo
Authorization: Bearer <JWT>
```

Decisões:

- a requisição exige JWT válido; o correntista é identificado somente pela claim `sub`;
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

`nomeTitular` é o nome persistido em `contacorrente.nome`. A identidade de quem consulta vem do token e é conferida contra `titularidade_conta`; o nome não participa da autorização.

`dataHoraConsulta` representa o instante em que a leitura consistente foi concluída. O valor será obtido de `TimeProvider.GetUtcNow()` somente depois de conta, titularidade e saldo consolidado terem sido lidos e validados, e será formatado explicitamente com `ToString("O", CultureInfo.InvariantCulture)`.

### 3.4 Erros

Erros usam `application/problem+json`, o tratamento global existente e a extensão `correlationId`.

| Situação | HTTP | `code` | Observação |
| --- | --- | --- | --- |
| token ausente ou inválido | 401 | `UNAUTHENTICATED` | header `WWW-Authenticate: Bearer`; motivo específico não devolvido |
| segmento de conta ausente | 404 | não aplicável | a rota não corresponde |
| parâmetro vazio após normalização ou acima do limite | 400 | validação por campo | erro estrutural, sem consulta ao banco |
| conta não cadastrada | 400 | `INVALID_ACCOUNT` | exigido pelo enunciado |
| conta de outro correntista ou sem titularidade | 400 | `INVALID_ACCOUNT` | corpo idêntico ao de conta não cadastrada |
| conta própria inativa | 400 | `INACTIVE_ACCOUNT` | exigido pelo enunciado |
| frequência ou concorrência excedida | 429 | `RATE_LIMIT_EXCEEDED` | Problem Details correlacionado |
| timeout do servidor | 504 | `REQUEST_TIMEOUT` | Problem Details correlacionado |
| dado monetário persistido incompatível ou falha inesperada | 500 | não exposto | mensagem genérica; diagnóstico somente em log seguro |

As mensagens de `INVALID_ACCOUNT` e `INACTIVE_ACCOUNT` seguirão o contrato já usado pela movimentação. A ordem das validações é: token, existência da conta, titularidade e, por último, situação ativa. Assim, `INACTIVE_ACCOUNT` só é devolvido ao próprio titular, e um correntista autenticado não consegue distinguir conta alheia de conta inexistente.

## 4. Modelo de leitura: projeção persistida de saldo

### 4.1 Fórmula

```text
saldo = soma(movimentos C) - soma(movimentos D)
```

Sem movimentos, o saldo é `0.00`.

### 4.2 Decisão arquitetural

O saldo **não** será recalculado a partir de todo o histórico a cada consulta. Em vez disso, será mantido em uma projeção persistida, a tabela `saldo_conta`, atualizada de forma síncrona na mesma transação que grava o movimento.

Motivos:

- a leitura deixa de percorrer todo o histórico e passa a ser uma consulta por chave primária;
- o custo da consulta não cresce com a quantidade de movimentos da conta;
- transações de leitura ficam curtas, reduzindo a contenção com a escrita;
- a consistência do saldo continua garantida pelo banco, sem depender de sincronização entre processos.

A separação entre o modelo de escrita, formado por `movimento` e `idempotencia`, e o modelo de leitura, formado por `saldo_conta`, caracteriza um CQRS local. Não haverá bancos distintos, mensageria ou consistência eventual.

### 4.3 Estrutura da tabela consolidada

Estrutura lógica planejada:

```sql
CREATE TABLE saldo_conta (
    idcontacorrente TEXT(37) PRIMARY KEY,
    saldo_centavos INTEGER NOT NULL,
    versao INTEGER NOT NULL,
    FOREIGN KEY(idcontacorrente) REFERENCES contacorrente(idcontacorrente)
);
```

Decisões:

- `idcontacorrente` é a chave primária e referencia a conta existente;
- `saldo_centavos` usa **centavos inteiros**, evitando introduzir um novo campo de ponto flutuante;
- `versao` é incrementada a cada movimento confirmado e serve de base para reconciliação e detecção de divergência;
- a projeção persistida é a única fonte autoritativa de leitura; não existe cache.

### 4.4 Atualização transacional

A movimentação confirmada atualizará a projeção na mesma transação imediata já usada hoje:

1. abrir conexão e transação imediata (`BeginTransaction(deferred: false)`);
2. consultar a idempotência pela chave normalizada;
3. em replay válido, **não** inserir movimento e **não** atualizar o saldo, retornando o resultado original;
4. validar existência, titularidade e atividade da conta;
5. inserir o movimento;
6. atualizar `saldo_conta`, somando ou subtraindo o valor e incrementando `versao`;
7. registrar o resultado idempotente;
8. confirmar a transação.

A idempotência é verificada antes de qualquer escrita, impedindo que a repetição de uma requisição aplique o valor duas vezes no saldo. Em qualquer exceção anterior ao commit, movimento, saldo e idempotência são revertidos juntos.

### 4.5 Representação monetária

- o contrato de entrada, o domínio e a resposta HTTP usam `decimal`;
- o adaptador de persistência converte o valor validado para centavos inteiros ao atualizar `saldo_conta`;
- o acúmulo usa aritmética inteira verificada, sem ponto flutuante;
- na leitura, `saldo_centavos` é convertido para `decimal` no adaptador e normalizado para escala 2;
- `REAL` permanece somente em `movimento.valor`, por compatibilidade histórica;
- não haverá `SUM(valor)` sobre o `REAL` legado.

### 4.6 Preenchimento inicial e reconciliação

Como já podem existir movimentos antes da criação da projeção, a migração deverá:

1. criar as tabelas `saldo_conta` e `titularidade_conta` de forma idempotente, na mesma migração para `user_version = 2`;
2. criar uma linha de saldo para cada conta existente;
3. reconstruir o saldo a partir dos movimentos, convertendo cada `REAL` individualmente e acumulando em `decimal` verificado;
4. converter o resultado validado para centavos;
5. gravar saldo e versão;
6. validar a reconciliação antes de concluir a migração.

A reconstrução não usará `SUM(valor)` em ponto flutuante. Uma rotina de reconciliação, executável sob demanda, comparará o saldo consolidado com o recalculado a partir dos movimentos para detectar divergências.

### 4.7 Contas sem movimentos e overflow

- conta ativa sem movimentos tem projeção `saldo_centavos = 0`;
- cada movimento permanece sujeito ao limite histórico de `9999999999.99`;
- o acumulador usa aritmética inteira e decimal verificadas;
- overflow, tipo de movimento incompatível ou valor persistido fora do contrato produz falha interna segura;
- não haverá saturação, truncamento, arredondamento implícito ou retorno parcial;
- a migração definitiva de `movimento.valor` para centavos inteiros continua sendo a correção estrutural recomendada e permanece fora desta entrega.

### 4.8 Cache explicitamente fora de escopo

Um cache em memória foi avaliado e descartado nesta etapa. Os motivos registrados são:

- exigiria sincronizar o commit do banco com a publicação em memória;
- introduziria janela de leitura potencialmente desatualizada;
- aumentaria estados de falha, invalidação e recuperação;
- traria ganho marginal diante da leitura por chave primária já proporcionada pela projeção.

A decisão poderá ser revista após medição, caso a consulta sobre `saldo_conta` não atinja metas de desempenho, e apenas com critérios explícitos de consistência, versionamento e fallback.

## 5. Consistência e concorrência

A consulta lerá a conta, a titularidade e o saldo consolidado na mesma conexão e transação de leitura, garantindo que número, titular e saldo pertençam ao mesmo instante, mesmo durante gravações concorrentes.

O fluxo planejado é:

1. validar e normalizar o identificador recebido e obter o correntista do token já validado;
2. abrir conexão e transação de leitura;
3. ler a conta e validar existência, titularidade e situação ativa, nessa ordem;
4. ler `saldo_conta` pelo identificador persistido da conta;
5. converter centavos para `decimal` e normalizar a escala;
6. concluir a leitura;
7. obter o instante UTC da resposta;
8. retornar o DTO.

A escrita que mantém a projeção usa a transação imediata já existente; a consulta é somente leitura, não usa idempotência e não abre transação imediata. Cancelamento e timeout devem ser propagados até o acesso a dados. Como a leitura é uma consulta por chave primária, a transação permanece curta, reduzindo a contenção com escritores.

## 6. Limites operacionais

A consulta terá configuração própria, sem reutilizar semanticamente `MovementOperationalOptions`:

| Controle | Valor inicial planejado |
| --- | --- |
| timeout | 5 segundos |
| frequência específica | 30 requisições por minuto por correntista autenticado |
| concorrência específica | 8 consultas em execução no processo, sem fila |
| limite global existente | 120 requisições por minuto por IP |

Não há body cujo tamanho precise ser configurado. O identificador é limitado pelo contrato e pelos limites gerais do servidor para URL e headers.

O limite global por IP é aplicado antes da autenticação. O limite específico é contado por correntista; requisições sem identidade válida são contadas por IP. Tentativas inválidas, inclusive as rejeitadas com 401, também consomem os limites, reduzindo enumeração irrestrita.

## 7. Logs estruturados

Eventos planejados para o componente `Balance`:

| Event ID | Nível | Evento | Campos permitidos |
| --- | --- | --- | --- |
| 5200 | Information | consulta concluída | `CorrelationId`, `AccountFingerprint`, `Outcome` |
| 5201 | Warning | rejeição de regra de negócio | `CorrelationId`, `AccountFingerprint`, `RuleCode`, `Outcome` |
| 5202 | Error | falha inesperada | `CorrelationId`, `AccountFingerprint`, `ExceptionType`, `Outcome` |
| 5203 | Warning | limite excedido | `CorrelationId`, `LimitName`, `Outcome` |

`AccountFingerprint` será formado pelos primeiros 16 caracteres hexadecimais do SHA-256 do identificador normalizado. Ele permite correlacionar tentativas sem registrar o identificador em claro e não substitui autenticação.

A negativa por titularidade responde ao cliente como `INVALID_ACCOUNT`, mas é registrada pelo evento 5301 do componente `Security`, definido em `ESPECIFICACAO_AUTENTICACAO.md`, para que o motivo real fique disponível à investigação. Token e identificador do correntista nunca são registrados em claro.

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

- `BalanceController`: rota, exigência de autenticação, validação estrutural, status HTTP, headers de cache e envio ao Mediator;
- `GetBalanceQuery`: identificador normalizado da conta e identificador do correntista obtido do token;
- `GetBalanceQueryHandler`: orquestração da consulta, sem SQL ou regra de serialização;
- `IBalanceQueryStore`: contrato de leitura da conta, da titularidade e do saldo consolidado;
- `BalanceQueryStore`: snapshot SQLite, SQL parametrizado, validação de conta e titularidade e conversão de centavos para `decimal`;
- `GetBalanceResponse`: resposta interna com número, titular, instante UTC e saldo;
- `GetBalanceHttpResponse`: contrato público fechado;
- `BalanceLogger`: Event IDs 5200–5203 e fingerprint seguro;
- `TimeProvider`: relógio UTC injetável para resposta e testes determinísticos;
- `BusinessRuleException`: reutilização dos códigos `INVALID_ACCOUNT` e `INACTIVE_ACCOUNT`.

No lado de escrita, `IMovementStore`/`MovementStore` passam a manter a projeção: dentro da transação imediata existente, após inserir o movimento e antes de confirmar, atualizam `saldo_conta` com o valor em centavos e o incremento de `versao`. A idempotência continua sendo verificada antes de qualquer escrita.

O schema evolui por migração versionada, elevando `PRAGMA user_version` para `2` e criando `saldo_conta` e `titularidade_conta` de forma idempotente, com preenchimento inicial transacional. Nenhuma alteração será feita na fixture versionada.

Pastas e namespaces devem seguir a separação atual entre `Application`, `Infrastructure/Database/CommandStore`, `Infrastructure/Database/QueryStore` e `Infrastructure/Services`.

## 9. Matriz de testes planejada

### Contrato HTTP e inventário

- conta ativa sem movimentos retorna HTTP 200 e saldo zero;
- crédito, débito e combinação retornam o saldo esperado;
- resposta contém número, titular, instante UTC canônico e saldo decimal;
- `Cache-Control: no-store` e `X-Correlation-ID` estão presentes;
- segmento ausente não corresponde à rota; identificador vazio após normalização e identificador acima do limite têm resposta controlada;
- Swagger passa a inventariar somente `POST /api/v1/movimentos` e `GET /api/v1/contas/{idContaCorrente}/saldo`;
- o DTO não expõe identificador interno, situação da conta ou movimentos.

### Autenticação e titularidade

- requisição sem token ou com token inválido retorna 401 `UNAUTHENTICATED`;
- titular consulta a própria conta;
- conta de outro correntista ou sem titularidade retorna `INVALID_ACCOUNT`, com corpo idêntico ao de conta não cadastrada;
- conta alheia inativa retorna `INVALID_ACCOUNT`, não `INACTIVE_ACCOUNT`;
- a negativa por titularidade emite o evento 5301 sem conta ou correntista em claro.

### Regras de negócio

- conta inexistente retorna `INVALID_ACCOUNT`;
- conta própria inativa retorna `INACTIVE_ACCOUNT`;
- comparação do identificador preserva o comportamento sem diferenciação de caixa;
- erros usam Problem Details e correlation ID;
- nenhuma falha retorna nome, saldo, SQL ou detalhe interno.

### Projeção persistida de saldo

- a migração cria `saldo_conta` de forma idempotente e eleva `PRAGMA user_version`;
- o preenchimento inicial reconstrói o saldo dos movimentos existentes e reconcilia com o conteúdo da projeção;
- conta ativa sem movimentos tem projeção `saldo_centavos = 0` e resposta `0.00`;
- crédito incrementa e débito decrementa `saldo_centavos` na mesma transação do movimento;
- `versao` é incrementada exatamente uma vez por movimento confirmado;
- replay idempotente não insere movimento e não altera saldo nem versão;
- falha entre movimento e saldo reverte ambos por rollback atômico;
- movimentações concorrentes não aplicam valor duas vezes nem perdem atualização;
- a rotina de reconciliação detecta divergência forçada entre projeção e movimentos;
- a consulta lê `saldo_conta` por chave primária e não percorre o histórico.

### Monetário e persistência

- ausência de movimentos produz `0.00`;
- apenas créditos, apenas débitos e saldo negativo;
- centavos como `0.01`, `0.10` e `9999999999.99`;
- somas repetidas, cancelamento exato de crédito e débito e grande quantidade de movimentos;
- resultado independente da cultura atual do processo;
- valor não finito, escala incompatível, tipo inválido e overflow persistidos artificialmente falham de forma segura;
- leitura não altera conta, movimento, idempotência, projeção ou fixture;
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

1. **Emissor de teste:** a consulta exige JWT e titularidade, mas o emissor usado no desafio não autentica pessoas; risco aceito somente para o exercício, conforme `ESPECIFICACAO_AUTENTICACAO.md`.
2. **Enumeração de contas:** `INVALID_ACCOUNT` e `INACTIVE_ACCOUNT` distintos são exigidos pelo enunciado. Com a titularidade, `INACTIVE_ACCOUNT` só chega ao próprio titular e conta alheia é indistinguível de conta inexistente; resta o canal de tempo de resposta, sem garantia de tempo constante.
3. **`REAL` legado:** a soma em `decimal` reduz propagação de ponto flutuante, mas não recupera precisão já perdida no armazenamento. A solução definitiva é migração separada para centavos inteiros.
4. **Tipo divergente da chave estrangeira:** SQLite permite o relacionamento histórico, mas SEC-007 continua bloqueado para migração própria.
5. **Consistência local:** a transação fornece snapshot no SQLite local; topologias distribuídas ou réplicas não fazem parte do projeto.
6. **Limite global por IP:** NAT, proxies e endereços compartilhados reduzem a precisão do limite global; o limite específico passa a ser contado por correntista.
7. **Dados pessoais na resposta:** nome e número são exigidos e só chegam ao titular; `no-store` e TLS no ambiente continuam necessários para reduzir exposição.
8. **Divergência da projeção:** se houver escrita direta em `movimento` fora da aplicação, o saldo consolidado não será atualizado. Toda movimentação deve passar pelo store autorizado, e a reconciliação cobre diagnóstico e correção.
9. **Titularidades fabricadas:** os identificadores de correntista não existem nos dados do proponente; são dados de demonstração gravados pelo seed deste projeto.
10. **Sem cache e sem alta disponibilidade:** a projeção melhora desempenho e previsibilidade de latência, mas aplicação e SQLite continuam sendo pontos únicos de falha. Esta entrega não fornece alta disponibilidade.

## 11. Critérios para a futura implementação

A implementação somente poderá começar após autorização expressa e deverá ser dividida em mudanças pequenas e revisáveis. Antes do commit funcional será obrigatório:

1. confirmar branch e workspace limpos;
2. preservar a fixture `Questao5/database.sqlite` e o SHA-256 esperado;
3. concluir antes as entregas F1 (autenticação) e F2 (schema versão 2, titularidade e projeção), definidas em `ESPECIFICACAO_AUTENTICACAO.md`;
4. na F2, implementar a migração versionada com `saldo_conta` e `titularidade_conta`, o preenchimento inicial, a reconciliação e a manutenção da projeção na mesma transação da escrita, sem duplicar valor em replay idempotente;
5. implementar o núcleo de consulta (`IBalanceQueryStore`), com verificação de titularidade, e os testes monetários;
6. implementar endpoint, contrato HTTP, `Cache-Control: no-store` e inventário OpenAPI em etapa própria;
7. implementar limites operacionais e logs 5200–5203 em etapa própria;
8. não introduzir cache em memória, mensageria ou consistência eventual nesta entrega;
9. executar restore bloqueado, auditoria direta e transitiva, secret scanning, formatação, build Release, testes e cobertura;
10. revisar riscos OWASP API/CWE, dados em logs, divergência de projeção e riscos residuais;
11. atualizar `TODO.md`, `CONVERSAS.md` e esta especificação com o estado efetivamente implementado;
12. criar commit funcional isolado somente após o gate completo passar.
