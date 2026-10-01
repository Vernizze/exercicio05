# Autenticação e titularidade de conta — especificação planejada

## 1. Escopo e estado

Este documento consolida a Entrega F0, realizada em 1º de outubro de 2026, e define o contrato e o desenho técnico da autenticação por JWT e da autorização por titular da conta. A entrega é exclusivamente de planejamento: nenhum pacote, middleware, tabela, migração, configuração ou teste executável de autenticação foi implementado nela.

Estado de implementação: a Entrega F1, concluída em 1º de outubro de 2026, implementou a autenticação descrita nas seções 5, 6.3 (resposta 401), 9 (evento 5300) e 11 (testes de autenticação). A Entrega F2, concluída na mesma data, implementou o vínculo conta–correntista e a migração (seção 4), a autorização por titular na movimentação (seção 6), a idempotência `v2` (seção 7), o limite específico por correntista (seção 8) e o evento 5301 (seção 9). A Entrega E aplicou a mesma regra de titularidade à consulta de saldo e passou a contar o limite específico do saldo por correntista. Resta o empacotamento da seção 10, previsto para a Entrega G.

A decisão substitui a exceção registrada anteriormente em `DIRETRIZES_SEGURANCA.md`, segundo a qual o exercício permaneceria anônimo. SEC-001 e SEC-002 deixam de ser riscos aceitos e passam a ser itens em andamento.

Fazem parte desta especificação:

- origem e validação do token JWT;
- identidade do correntista e sua representação;
- vínculo persistido entre conta e correntista;
- regra de autorização aplicada à movimentação e à consulta de saldo;
- respostas HTTP para falha de autenticação e de titularidade;
- amarração da idempotência ao correntista;
- limites operacionais por identidade;
- eventos estruturados e dados proibidos em logs;
- emissor usado no ambiente do desafio e suas limitações;
- matriz de testes e sequência de entregas.

Permanecem fora do escopo:

- emissão de tokens pela própria API, login, cadastro de usuários, senhas e recuperação de acesso;
- papéis, perfis administrativos e autorização por função;
- cadastro ou manutenção de correntistas e de titularidades por endpoint;
- múltiplos titulares por conta;
- revogação de tokens, refresh tokens e sessão;
- uso do emissor de teste fora do ambiente do desafio.

O enunciado em `Questao5/Questão 5.docx` não solicita autenticação. Ela foi incluída por decisão do responsável pelo projeto, para dar consistência ao vínculo entre conta e correntista, e não altera os requisitos funcionais exigidos.

## 2. Situação de partida

O schema recebido do proponente não possui relação entre conta e dono. A tabela `contacorrente` contém apenas `idcontacorrente`, `numero`, `nome` e `ativo`; `nome` é um texto descritivo do titular. Não existe tabela de pessoas, identificador de correntista ou chave estrangeira para um cliente, e a API não registra autenticação.

Consequência: os identificadores de correntista não existem nos dados originais e são definidos por este projeto.

## 3. Identidade do correntista

- O correntista é identificado por um UUID, representado no formato canônico `D` em minúsculas.
- O nome do correntista continua sendo `contacorrente.nome`; não é criada tabela de pessoas.
- O identificador chega à API exclusivamente pela claim `sub` de um JWT validado. Nenhum identificador de correntista é aceito em body, rota, query string ou header próprio.
- Um token cuja claim `sub` esteja ausente ou não seja um UUID canônico é tratado como token inválido.

## 4. Vínculo conta–correntista

### 4.1 Estrutura

Estrutura lógica planejada:

```sql
CREATE TABLE titularidade_conta (
    idcontacorrente TEXT(37) PRIMARY KEY,
    idcorrentista TEXT(36) NOT NULL,
    FOREIGN KEY(idcontacorrente) REFERENCES contacorrente(idcontacorrente)
);
```

Decisões:

- a tabela é nova e aditiva; `contacorrente`, `movimento` e `idempotencia` permanecem como recebidas do proponente;
- `idcontacorrente` é a chave primária, portanto cada conta possui no máximo um titular;
- um mesmo correntista pode ser titular de várias contas;
- conta sem linha de titularidade não é acessível por ninguém (falha fechada);
- a fixture versionada `Questao5/database.sqlite` não é alterada.

### 4.2 Migração

A tabela é criada na migração que eleva `PRAGMA user_version` para `2`, a mesma que cria a projeção `saldo_conta` definida em `ESPECIFICACAO_SALDO.md`. A migração é transacional e idempotente, e o validador de schema passa a exigir as duas tabelas novas com a mesma estrita verificação aplicada às existentes.

A titularidade é gravada somente para as seis contas do seed e somente quando a conta ainda não possui titular; titularidades existentes nunca são sobrescritas. Contas criadas fora do seed ficam sem titular e, portanto, inacessíveis até que uma titularidade seja registrada diretamente no banco.

### 4.3 Titularidades iniciais

Os identificadores abaixo foram gerados para este projeto e são gravados pelo seed, de forma parametrizada e idempotente:

| Conta | Titular (`contacorrente.nome`) | Situação | `idcorrentista` |
| --- | --- | --- | --- |
| 123 | Katherine Sanchez | ativa | `7d85c0f1-c90c-49e6-a2c7-0fb988c3d943` |
| 456 | Eva Woodward | ativa | `04b276dc-0f45-4efc-bffc-911110198733` |
| 789 | Tevin Mcconnell | ativa | `06dc3a47-fb77-4589-9e18-076f3860d1d2` |
| 741 | Ameena Lynn | inativa | `cf18e8e5-35f2-498d-a77d-4dd6828316d4` |
| 852 | Jarrad Mckee | inativa | `dbb66add-5f1c-412e-9917-ac6048ea22da` |
| 963 | Elisha Simons | inativa | `3d03eefd-9941-44c8-b6bb-5644eff438e8` |

Esses valores são dados de demonstração, não segredos.

## 5. Autenticação

### 5.1 Modelo

A API é um resource server: apenas valida tokens emitidos por um emissor externo configurável. Ela não emite tokens e não conhece credenciais.

Todos os endpoints bancários exigem usuário autenticado por política padrão. Um endpoint só pode ser anônimo por declaração explícita e justificada; hoje nenhum é.

### 5.2 Validação do token

| Verificação | Regra |
| --- | --- |
| assinatura | chave obtida do JWKS publicado pelo emissor configurado |
| algoritmo | lista fechada; inicialmente `RS256`, a confirmar contra o emissor quando a imagem for fixada; `none` e algoritmos simétricos são rejeitados |
| emissor (`iss`) | igual ao emissor configurado |
| audiência (`aud`) | contém a audiência configurada da API |
| validade (`exp`, `nbf`) | obrigatória, com tolerância de relógio de no máximo 30 segundos |
| sujeito (`sub`) | obrigatório e UUID canônico |

O mapeamento automático de claims de entrada é desabilitado, para que `sub` seja lido com o nome original.

### 5.3 Configuração

A configuração fica na seção `Jwt`:

| Chave | Finalidade |
| --- | --- |
| `Jwt:MetadataAddress` | endereço do documento de descoberta OIDC do emissor |
| `Jwt:Issuer` | valor exato esperado em `iss` |
| `Jwt:Audience` | audiência exigida |
| `Jwt:RequireHttpsMetadata` | `true` por padrão; `false` somente no ambiente de contêineres do desafio |

Ausência ou valor inválido de `MetadataAddress`, `Issuer` ou `Audience` impede a inicialização. Nenhum segredo de autenticação é versionado: a API usa apenas chaves públicas do emissor.

`appsettings.json` não define a seção `Jwt`. `appsettings.Development.json` aponta para o emissor local previsto para o ambiente do desafio (`http://localhost:8081/default`, audiência `questao5-api`), com `RequireHttpsMetadata=false`; esses valores serão confirmados na Entrega G. O documento de descoberta é buscado somente na primeira validação de token, de modo que a API inicia mesmo com o emissor indisponível e responde 401 enquanto ele não estiver acessível.

### 5.4 Dependência

A implementação exige o pacote `Microsoft.AspNetCore.Authentication.JwtBearer`, mantido pela Microsoft e alinhado à versão do runtime. A inclusão seguirá a avaliação de dependência, o lock file e a auditoria definidos em `DIRETRIZES_SEGURANCA.md`.

## 6. Autorização por titular

### 6.1 Regra

Uma conta só pode ser movimentada ou consultada pelo correntista registrado em `titularidade_conta` para aquela conta. A regra vale igualmente para crédito, débito e saldo.

A verificação ocorre no servidor, dentro da mesma transação ou snapshot que lê a conta, e nunca com base em identificador de correntista enviado pelo cliente.

### 6.2 Ordem das validações

1. token válido; caso contrário, HTTP 401;
2. conta cadastrada; caso contrário, `INVALID_ACCOUNT`;
3. conta pertencente ao correntista do token; caso contrário, `INVALID_ACCOUNT`;
4. conta ativa; caso contrário, `INACTIVE_ACCOUNT`.

Um correntista que não é titular recebe sempre `INVALID_ACCOUNT`, mesmo quando a conta existe e está inativa.

### 6.3 Respostas

| Situação | HTTP | `code` | Observação |
| --- | --- | --- | --- |
| token ausente, malformado, expirado, com assinatura, emissor, audiência ou algoritmo inválidos, ou sem `sub` UUID | 401 | `UNAUTHENTICATED` | `application/problem+json` correlacionado e header `WWW-Authenticate: Bearer`; o motivo específico não é devolvido |
| conta de outro correntista ou sem titularidade | 400 | `INVALID_ACCOUNT` | resposta idêntica à de conta não cadastrada |
| conta própria inativa | 400 | `INACTIVE_ACCOUNT` | exigido pelo enunciado |

A resposta idêntica para conta inexistente e conta alheia impede que um correntista autenticado descubra quais identificadores de conta existem. O motivo real é registrado apenas em log. HTTP 403 não é usado para titularidade, pois confirmaria a existência da conta.

## 7. Idempotência amarrada ao correntista

A chave de idempotência da movimentação continua sendo o UUID `idRequisicao`, armazenado em `idempotencia.chave_idempotencia`. Para impedir que um correntista recupere o resultado de uma requisição feita por outro, a representação canônica passa a incluir o titular:

```text
v2|titular=04b276dc-0f45-4efc-bffc-911110198733|conta=FA99D033-7067-ED11-96C6-7C5DFA4A16C9|valor=125.50|tipo=C
```

Regras:

- repetição idêntica pelo mesmo correntista devolve o resultado original, como hoje;
- a mesma chave usada por outro correntista produz representação canônica diferente e retorna `409 IDEMPOTENCY_CONFLICT`, sem revelar o resultado original;
- registros `v1`, gravados antes da autenticação, não possuem titular e nunca coincidem com uma representação `v2`; a reutilização da chave retorna conflito (falha fechada);
- a tabela `idempotencia` não muda de estrutura.

## 8. Limites operacionais

- o limite global por IP permanece e é aplicado antes da autenticação, protegendo também o caminho de validação de token;
- os limites específicos de movimentação e de saldo passam a ser particionados pelo correntista autenticado; requisições sem identidade válida continuam particionadas por IP;
- requisições rejeitadas com 401 também consomem limite;
- valores, janelas e ausência de fila permanecem os definidos nas especificações de cada endpoint.

## 9. Logs estruturados

Eventos planejados para o componente `Security`:

| Event ID | Nível | Evento | Campos permitidos |
| --- | --- | --- | --- |
| 5300 | Warning | falha de autenticação | `CorrelationId`, `Reason`, `Outcome` |
| 5301 | Warning | acesso negado por titularidade | `CorrelationId`, `SubjectFingerprint`, `AccountFingerprint`, `Outcome` |

`Reason` é uma categoria fechada (por exemplo `MissingToken`, `InvalidToken`, `ExpiredToken`, `InvalidSubject`), nunca a mensagem da biblioteca. `SubjectFingerprint` e `AccountFingerprint` são os primeiros 16 caracteres hexadecimais do SHA-256 do valor normalizado.

Não serão registrados:

- o token, qualquer parte dele ou o header `Authorization`;
- o identificador do correntista ou da conta em claro;
- claims além das categorias acima;
- mensagem bruta de exceção da validação.

Os eventos existentes da movimentação (5100–5105) e os planejados do saldo (5200–5203) permanecem, sem passar a registrar identidade em claro.

## 10. Emissor no ambiente do desafio

### 10.1 Escolha

O ambiente do desafio será empacotado com Docker Compose e usará como emissor o `mock-oauth2-server` (imagem `ghcr.io/navikt/mock-oauth2-server`), um contêiner único, sem banco de dados, que publica descoberta OIDC e JWKS e permite fixar a claim `sub` por configuração (`JSON_CONFIG`).

O Keycloak foi avaliado e descartado por ser desproporcional ao desafio: contêiner pesado, inicialização lenta e arquivo de realm a manter.

### 10.2 Limitação aceita

O emissor escolhido é uma ferramenta de teste e **não autentica pessoas**: quem alcançar o contêiner obtém um token para qualquer correntista. A validação feita pela API é real, mas a origem da identidade é um substituto.

- a limitação é aceita exclusivamente para o desafio técnico;
- o emissor de teste não pode ser usado em ambiente real;
- a troca por um provedor real exige apenas reconfigurar a seção `Jwt`, sem mudança de código.

### 10.3 Pontos a resolver na entrega de empacotamento

- fixar a imagem por versão ou digest e confirmar o algoritmo de assinatura;
- garantir que o emissor escrito no token coincida com `Jwt:Issuer` tanto para a API, que alcança o emissor pela rede interna do Compose, quanto para o avaliador, que o alcança por `localhost`;
- registrar `Jwt:RequireHttpsMetadata=false` como exceção restrita à rede interna do Compose;
- documentar para os avaliadores como obter um token de cada correntista.

## 11. Matriz de testes planejada

Os testes automatizados não dependem do contêiner do emissor: usam uma chave de assinatura gerada no próprio teste e injetada na configuração de validação, para que o gate continue executável sem Docker.

### Autenticação

- requisição sem token retorna 401 `UNAUTHENTICATED` com `WWW-Authenticate` e correlation ID;
- token expirado, ainda não válido, com assinatura de outra chave, emissor errado, audiência errada ou algoritmo `none` retorna 401;
- token sem `sub` ou com `sub` que não é UUID retorna 401;
- a resposta 401 não revela o motivo específico;
- a aplicação não inicia sem a configuração obrigatória de `Jwt`.

### Titularidade

- titular movimenta e consulta a própria conta;
- conta de outro correntista retorna `INVALID_ACCOUNT`, com corpo idêntico ao de conta não cadastrada;
- conta sem linha de titularidade retorna `INVALID_ACCOUNT`;
- conta alheia inativa retorna `INVALID_ACCOUNT`, não `INACTIVE_ACCOUNT`;
- acesso negado não grava movimento, saldo nem idempotência.

### Idempotência

- repetição pelo mesmo correntista devolve o mesmo `idMovimento`;
- a mesma chave usada por outro correntista retorna 409 e não altera dados;
- registro `v1` preexistente produz conflito quando a chave é reutilizada.

### Schema

- a migração cria `titularidade_conta` e `saldo_conta`, grava as seis titularidades e eleva `user_version` para `2`;
- a migração é idempotente, transacional e segura para inicializações concorrentes;
- tabela de titularidade incompatível é rejeitada pelo validador.

### Limites e logs

- limites específicos são contados por correntista, e requisições 401 consomem o limite por IP;
- eventos 5300 e 5301 possuem IDs, níveis e campos esperados;
- logs não contêm token, header `Authorization`, correntista ou conta em claro.

### Regressão

- todos os testes existentes continuam passando, agora enviando token;
- o inventário OpenAPI declara o esquema de segurança Bearer;
- a fixture mantém o SHA-256 esperado.

## 12. Riscos residuais e decisões explícitas

1. **Emissor de teste:** não autentica pessoas; risco aceito somente no desafio (seção 10.2).
2. **Titularidades fabricadas:** os identificadores de correntista não vêm do proponente; são dados de demonstração deste projeto.
3. **Sem revogação:** um token válido permanece aceito até expirar.
4. **Um titular por conta:** contas conjuntas não são modeladas.
5. **Canal de tempo:** a resposta para conta alheia e conta inexistente é idêntica no conteúdo, mas não há garantia de tempo constante.
6. **Conflito idempotente entre correntistas:** o 409 revela que a chave já foi usada, não o resultado; chaves são UUIDs aleatórios.
7. **HTTP entre contêineres:** a busca do JWKS sem TLS é restrita à rede interna do Compose.

## 13. Sequência de entregas

| Entrega | Escopo |
| --- | --- |
| F0 (concluída) | Revisão documental: esta especificação, diretrizes, especificações de movimentação e saldo e `TODO.md`. |
| F1 (concluída) | Autenticação JWT na API: pacote, validação, política padrão, resposta 401, evento 5300 e testes com chave local. |
| F2 (concluída) | Schema versão 2 (`titularidade_conta` e `saldo_conta`), manutenção da projeção de saldo, autorização por titular na movimentação, idempotência `v2`, limites por correntista e evento 5301. |
| E2–E4 (concluída) | Consulta de saldo já com autorização por titular, conforme `ESPECIFICACAO_SALDO.md`. |
| G | Empacotamento com Dockerfile e Docker Compose, incluindo o emissor de teste e as instruções para os avaliadores. |

Cada entrega termina com o gate completo e um commit isolado. Mudanças futuras de claim, emissor, regra de titularidade ou resposta de erro devem atualizar este documento antes do código.
