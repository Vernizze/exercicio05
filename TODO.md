# TODO do projeto

Este documento registra pendências técnicas e funcionais identificadas durante a análise do projeto recebido.

## Governança documental e fontes de verdade

Os documentos do projeto possuem responsabilidades distintas e não constituem backlogs concorrentes:

- `TODO.md`: fonte canônica do escopo, estado atual, pendências, dependências e próximos passos;
- `CONVERSAS.md`: evidência cronológica das solicitações, decisões e ações, sem substituir o estado consolidado deste TODO;
- `ESPECIFICACAO_MOVIMENTACAO.md`: contrato técnico especializado da movimentação; divergências de estado devem ser reconciliadas neste TODO;
- `ESPECIFICACAO_SALDO.md`: contrato técnico planejado da consulta de saldo; não autoriza implementação sem entrega específica;
- `ESPECIFICACAO_AUTENTICACAO.md`: contrato técnico planejado da autenticação JWT e da titularidade de conta; não autoriza implementação sem entrega específica;
- `DIRETRIZES_SEGURANCA.md`: guardrails permanentes aplicáveis a cada mudança, não um cronograma paralelo;
- `RELATORIO_SEGURANCA.md`: fotografia histórica encerrada em 30 de setembro de 2026; estados antigos nele preservados não representam o andamento corrente;
- `Questao5/Questão 5.docx`: enunciado original e fonte de requisitos, não documento de controle.

Pendências acionáveis descobertas em qualquer especificação, relatório, diretriz ou conversa devem ser registradas ou referenciadas neste arquivo. Documentos históricos não serão reescritos para aparentar estado corrente; a atualização ocorrerá aqui e em `CONVERSAS.md`.

## Roadmap consolidado

| Entrega | Estado | Escopo consolidado |
| --- | --- | --- |
| B.1 | Concluída | Revisar e commitar a especificação da movimentação. |
| C1 | Concluída | Implementar o núcleo transacional e idempotente. |
| C2 | Concluída | Implementar o endpoint e o contrato HTTP. |
| C3 | Concluída | Implementar limites operacionais, logs e testes de abuso. |
| C4 | Concluída | Limpeza do template, reconciliação documental, gate final e commit funcional consolidado. |
| D | Concluída | Planejar a consulta de saldo, sem implementação antecipada. |
| F0 | Concluída | Revisão documental: autenticação JWT, titularidade de conta e emissor do desafio. |
| F1 | Autorizada — próxima | Autenticação JWT na API. |
| F2 | Autorizada — após F1 | Schema versão 2, titularidade, projeção de saldo e autorização na movimentação. |
| E | Autorizada — após F2 | Implementar a consulta de saldo (E2 a E4), já com autorização por titular. |
| G | Autorizada — após E | Empacotamento com Dockerfile e Docker Compose, incluindo o emissor de teste. |

A sequência F0, F1, F2, E e G foi autorizada pelo usuário em 1º de outubro de 2026 (Interação 033 de `CONVERSAS.md`). Cada entrega termina com o gate completo e um commit isolado.

### Entrega C4 — limpeza, documentação, gate e commit funcional

- **Estado:** Concluída em 1º de outubro de 2026
- **Dependências:** C1, C2 e C3 concluídas; organização documental registrada no commit `55f3f0a`
- **Limite:** não implementar consulta de saldo, autenticação, autorização, CI/CD ou mudanças dependentes de infraestrutura

#### Escopo e critérios de aceite

- [x] Remover `WeatherForecastController`, `WeatherForecast` e referências residuais do template.
- [x] Confirmar que Swagger e o inventário expõem somente endpoints intencionais.
- [x] Reconciliar `TODO.md`, `CONVERSAS.md` e `ESPECIFICACAO_MOVIMENTACAO.md` com o estado final da movimentação, sem reescrever relatórios históricos.
- [x] Revisar comentários, arquivos gerados, configurações e dependências para detectar resíduos ou documentação desatualizada.
- [x] Executar o gate completo: restore bloqueado, auditoria direta/transitiva, secret scanning, formatação/analisadores, build Release determinístico, testes e cobertura.
- [x] Confirmar novamente a integridade da fixture `Questao5/database.sqlite` pelo SHA-256 esperado.
- [x] Revisar o diff final e garantir ausência de mudanças de escopo ou dados operacionais.
- [x] Criar um commit funcional isolado da C4 somente após todos os critérios anteriores passarem.
- [x] Encerrar com workspace e index limpos e registrar as evidências em `CONVERSAS.md` e neste TODO.

### Entrega D — planejamento da consulta de saldo

- **Estado:** Concluída em 1º de outubro de 2026 — especificação registrada em `ESPECIFICACAO_SALDO.md`
- **Escopo inicial:** extrair e consolidar requisitos; definir rota, DTOs, respostas HTTP, cálculo monetário, data/hora, segurança, logs, arquitetura e matriz de testes
- **Limite:** a Entrega D é de planejamento; implementação da consulta de saldo exigirá entrega e autorização posteriores
- **Revisão documental:** a estratégia de cálculo em tempo real foi substituída pela projeção persistida de saldo (`saldo_conta`), com atualização transacional, centavos inteiros, preenchimento inicial e reconciliação; o cache em memória foi avaliado e descartado nesta etapa; o cenário foi reconfirmado como centrado em conta, sem identidade de titular

#### Critérios de aceite do planejamento

- [x] Validar os requisitos do enunciado e as decisões monetárias já registradas.
- [x] Definir contrato HTTP versionado e comportamento para conta inexistente ou inativa.
- [x] Definir cálculo determinístico de créditos menos débitos, incluindo saldo `0.00` sem movimentos.
- [x] Definir formato externo, UTC/fuso e serialização determinística da data e hora da consulta.
- [x] Mapear riscos, limites, logs sem dados excessivos e redução de enumeração.
- [x] Definir arquitetura e testes positivos, negativos, monetários, temporais, de abuso e regressão.
- [x] Definir a projeção persistida de saldo, sua estrutura, atualização transacional e reconciliação.
- [x] Avaliar e descartar explicitamente o cache em memória nesta etapa.
- [x] Confirmar que o cenário é centrado em conta e não possui dono/identidade de titular.
- [x] Registrar a especificação antes de qualquer implementação.

### Entrega F0 — revisão documental de autenticação e titularidade

- **Estado:** Concluída em 1º de outubro de 2026 — especificação registrada em `ESPECIFICACAO_AUTENTICACAO.md`
- **Motivo:** o vínculo entre conta e titular era apenas o texto `contacorrente.nome`; o usuário decidiu acrescentar o identificador do correntista, recebido por JWT, e a autenticação necessária para isso
- **Limite:** entrega exclusivamente documental; nenhum código, pacote, schema, fixture ou configuração foi alterado

#### Decisões registradas

- [x] A API apenas valida tokens de um emissor externo configurável; não emite tokens.
- [x] O correntista é um UUID recebido na claim `sub`; o nome continua em `contacorrente.nome`.
- [x] O vínculo é persistido na tabela aditiva `titularidade_conta`, criada na migração para `user_version = 2`.
- [x] A titularidade é exigida na movimentação e na consulta de saldo.
- [x] Conta de outro correntista responde `400 INVALID_ACCOUNT`, idêntico a conta não cadastrada; o motivo real fica apenas em log.
- [x] A idempotência passa à representação `v2`, amarrada ao correntista.
- [x] O emissor do ambiente do desafio é o `mock-oauth2-server`, em Docker Compose; o Keycloak foi avaliado e descartado.
- [x] A exceção de autenticação de `DIRETRIZES_SEGURANCA.md` foi revogada; SEC-001 e SEC-002 foram reabertos.

### Entrega F1 — autenticação JWT na API

- **Estado:** Autorizada — próxima entrega
- **Especificação:** `ESPECIFICACAO_AUTENTICACAO.md`, seções 5, 6.3, 9 e 11
- **Limite:** não alterar schema, regra de titularidade, idempotência ou consulta de saldo

#### Critérios de aceite

- [ ] Pacote `Microsoft.AspNetCore.Authentication.JwtBearer` avaliado, fixado, com lock file e auditoria sem vulnerabilidades.
- [ ] Assinatura, algoritmo, emissor, audiência, validade e `sub` UUID são validados conforme a especificação.
- [ ] Todos os endpoints bancários exigem autenticação por política padrão.
- [ ] Token ausente ou inválido retorna 401 `UNAUTHENTICATED` correlacionado, com `WWW-Authenticate: Bearer` e sem motivo específico.
- [ ] A aplicação não inicia sem a configuração obrigatória de `Jwt`.
- [ ] O evento 5300 é emitido sem token, header `Authorization` ou identidade em claro.
- [ ] O documento OpenAPI declara o esquema de segurança Bearer.
- [ ] Os testes existentes passam a enviar token e existem testes negativos de autenticação com chave local, sem depender de contêiner.
- [ ] Gate completo aprovado e fixture com o SHA-256 esperado.

### Entrega F2 — schema versão 2, titularidade e projeção de saldo

- **Estado:** Autorizada — depende de F1
- **Especificação:** `ESPECIFICACAO_AUTENTICACAO.md`, seções 4, 6, 7, 8 e 9; `ESPECIFICACAO_SALDO.md`, seção 4; `ESPECIFICACAO_MOVIMENTACAO.md`, seção 10
- **Observação:** absorve a etapa E1 (migração e projeção persistida), porque `titularidade_conta` e `saldo_conta` pertencem à mesma migração de schema
- **Limite:** não criar endpoint, query ou handler de saldo

#### Critérios de aceite

- [ ] A migração cria `titularidade_conta` e `saldo_conta`, grava as seis titularidades, preenche e reconcilia o saldo e eleva `user_version` para `2`, de forma transacional, idempotente e segura para concorrência.
- [ ] O validador de schema exige as duas tabelas novas; a fixture versionada permanece intacta.
- [ ] A movimentação valida existência, titularidade e situação ativa, nessa ordem, dentro da transação.
- [ ] Conta de outro correntista ou sem titularidade retorna `INVALID_ACCOUNT` idêntico ao de conta não cadastrada e não grava dados.
- [ ] A movimentação confirmada atualiza `saldo_conta` e `versao` na mesma transação; replay não altera saldo.
- [ ] A idempotência usa a representação `v2`; a mesma chave por outro correntista retorna 409.
- [ ] O limite específico é contado por correntista; o limite global por IP permanece.
- [ ] O evento 5301 é emitido sem conta ou correntista em claro.
- [ ] Existe rotina de reconciliação que detecta divergência entre projeção e movimentos.
- [ ] Gate completo aprovado e fixture com o SHA-256 esperado.

### Entrega E — implementação da consulta de saldo

- **Estado:** Autorizada — depende de F2
- **Especificação:** `ESPECIFICACAO_SALDO.md`
- **Sequência:** E1 foi absorvida pela F2; E2 núcleo de consulta; E3 contrato HTTP; E4 limites, logs, inventário, gate e commit funcional
- **Decisão arquitetural:** CQRS local com projeção persistida (`saldo_conta`), sem cache em memória, mensageria ou consistência eventual; consulta restrita ao titular da conta

### Entrega G — empacotamento com Docker Compose

- **Estado:** Autorizada — depende de E
- **Especificação:** `ESPECIFICACAO_AUTENTICACAO.md`, seção 10
- **Limite:** ambiente do desafio; não constitui pipeline de CI/CD nem ambiente de produção

#### Critérios de aceite

- [ ] Dockerfile da API com imagens fixadas por versão ou digest e execução sem privilégios.
- [ ] `docker-compose.yml` com a API e o emissor `mock-oauth2-server`, imagem fixada e algoritmo de assinatura confirmado.
- [ ] O emissor escrito no token coincide com `Jwt:Issuer` para a API e para o avaliador.
- [ ] `Jwt:RequireHttpsMetadata=false` fica restrito ao ambiente do Compose e documentado como exceção.
- [ ] Instruções para os avaliadores: subir o ambiente, obter token de cada correntista e chamar os dois endpoints.
- [ ] Reavaliar SEC-011, SEC-012, SEC-021 e SEC-022 à luz do ambiente criado, registrando o que passa a ser avaliável e o que continua fora do escopo.

### Pendências identificadas em 1º de outubro de 2026 — sem autorização de execução

Achados do levantamento de fechamento de escopo (Interação 029 de `CONVERSAS.md`). O usuário decidiu tratar primeiro a consulta de saldo; estes itens ficam registrados e aguardam decisão.

#### TODO-003 — Devolver `INVALID_VALUE` e `INVALID_TYPE` na resposta HTTP

- **Estado:** Pendente — aguarda decisão
- **Prioridade:** Alta — requisito do enunciado
- **Evidência:** valor não positivo e tipo diferente de `C`/`D` são barrados pela validação do DTO `CreateMovementRequest` e respondidos como erro de validação por campo, sem `code`. Os códigos existem em `MovementRequestNormalizer`, mas não são alcançáveis pelo endpoint. A seção 2.4 de `ESPECIFICACAO_MOVIMENTACAO.md` promete os dois códigos.

- [ ] Valor não positivo retorna HTTP 400 com `code` `INVALID_VALUE`.
- [ ] Tipo diferente de `C` ou `D` retorna HTTP 400 com `code` `INVALID_TYPE`.
- [ ] Testes HTTP verificam os dois códigos.

#### TODO-004 — Completar a documentação Swagger

- **Estado:** Pendente — aguarda decisão
- **Prioridade:** Média — ponto extra do enunciado
- **Evidência:** `AddSwaggerGen()` sem configuração; não há descrição de atributos, exemplos nem os retornos 429, 500 e 504.

- [ ] Atributos, requisições e todos os retornos possíveis documentados, com exemplos.

#### TODO-005 — Testes unitários com NSubstitute

- **Estado:** Pendente — aguarda decisão
- **Prioridade:** Média — ponto extra do enunciado
- **Evidência:** o pacote NSubstitute está referenciado em `Questao5.Tests`, mas nenhum teste o utiliza; os handlers não possuem teste unitário com store mockado.

- [ ] Handlers cobertos por testes unitários com dependências mockadas.

#### TODO-006 — README e forma de entrega

- **Estado:** Pendente — aguarda decisão
- **Prioridade:** Baixa
- **Evidência:** não existe README com instruções de execução, e nenhum documento define como o trabalho é entregue (merge em `master`, tag ou pacote). As instruções de uso do ambiente Compose fazem parte da Entrega G.

- [ ] Instruções de execução da API, dos testes e do gate.
- [ ] Forma de entrega definida.

## Regra de priorização

As correções funcionais identificadas durante a análise Zero Trust não faziam parte da execução inicial, que ficou limitada às sete fases de segurança descritas abaixo. Após a conclusão desse marco, cada pendência funcional continua exigindo planejamento e autorização específicos antes da implementação.

Mesmo depois que as sete fases estiverem concluídas e validadas, os itens funcionais permanecem **Bloqueados** até que as etapas abaixo sejam cumpridas. A conclusão do marco de segurança não autoriza nem inicia automaticamente qualquer correção funcional.

Antes de iniciar as pendências funcionais, será necessário:

1. Encerrar formalmente as sete fases e apresentar seus resultados ao usuário.
2. Parar a execução sem modificar o código funcional.
3. Discutir e registrar os detalhes adicionais ainda pendentes.
4. Receber autorização expressa e específica do usuário para uma nova etapa de planejamento ou implementação.

---

## Marco de segurança — obrigatório antes das correções funcionais

### Fase 1 — Registrar a análise

- [x] Registrar no histórico a solicitação, o escopo, os procedimentos e os resultados da análise Zero Trust.
- [x] Documentar explicitamente quais ações foram apenas estáticas e quais envolveram execução.

### Fase 2 — Preservar evidências

- [x] Gerar e registrar hashes SHA-256 dos arquivos originais relevantes.
- [x] Preservar o inventário dos binários recebidos.
- [x] Preservar os fontes, o documento do exercício e o banco SQLite antes da higienização.

### Fase 3 — Higienizar o workspace

- [x] Criar um `.gitignore` adequado para .NET e Visual Studio.
- [x] Remover do workspace confiável os diretórios `.vs`, `bin` e `obj` recebidos.
- [x] Remover arquivos locais de usuário, como `*.user`, do workspace confiável.
- [x] Garantir que nenhum binário recebido seja reutilizado na compilação limpa.

### Fase 4 — Executar verificação antimalware

- [x] Verificar toda a pasta recebida com o Microsoft Defender ou ferramenta corporativa equivalente.
- [x] Verificar separadamente o documento, o banco SQLite e os binários recebidos.
- [x] Verificar o arquivo compactado original, caso esteja disponível.
- [x] Registrar ferramenta, data, escopo e resultado da verificação.

### Fase 5 — Proteger a cadeia de dependências

- [x] Conferir todos os pacotes e versões declarados no projeto.
- [x] Restringir a restauração às fontes NuGet aprovadas.
- [x] Auditar vulnerabilidades conhecidas nas dependências.
- [x] Avaliar dependências antigas, preview ou desnecessárias.
- [x] Separar dependências de teste do projeto web quando aplicável.
- [x] Adicionar mecanismo de resolução reproduzível, como lock file.

### Fase 6 — Realizar reconstrução limpa

- [x] Restaurar dependências verificadas em ambiente limpo.
- [x] Compilar exclusivamente a partir dos fontes revisados.
- [x] Confirmar que nenhum artefato recebido de `bin` ou `obj` foi utilizado.
- [x] Registrar os comandos, as versões das ferramentas e o resultado da compilação.

### Fase 7 — Realizar execução controlada

- [x] Executar inicialmente com acesso de rede restrito, quando possível.
- [x] Observar processos filhos, conexões, portas e arquivos modificados.
- [x] Observar especificamente as alterações feitas no banco SQLite pelo bootstrap.
- [x] Validar logs e encerramento da aplicação.
- [x] Registrar os resultados e concluir formalmente o marco de segurança.

### Ponto de parada obrigatório após a Fase 7

- [x] Apresentar ao usuário o relatório consolidado das sete fases.
- [x] Confirmar que nenhuma pendência funcional foi iniciada.
- [x] Manter TODO-001 e TODO-002 no estado **Bloqueado**.
- [x] Aguardar os detalhes adicionais e uma nova autorização expressa do usuário.

> **Regra de parada:** após concluir a Fase 7, não alterar fontes funcionais, não iniciar o TODO-001 e não iniciar o TODO-002. A próxima ação deverá ser somente reportar os resultados e aguardar instruções.

---

## Pendências funcionais bloqueadas pelo marco de segurança

### TODO-001 — Corrigir a validação do bootstrap do SQLite

- **Estado:** Concluído em 1º de outubro de 2026
- **Prioridade:** Alta
- **Bloqueadores:** resolvidos por autorização expressa do usuário após a conclusão e validação das fases 1 a 7
- **Arquivo identificado:** `Questao5/Infrastructure/Sqlite/DatabaseBootstrap.cs`
- **Evidência de conclusão:** o bootstrap passou a usar transação imediata, versão de esquema, criação idempotente por tabela, validação estrita da estrutura, seed parametrizado e idempotente, verificações de integridade e rollback implícito quando qualquer etapa falha. A suíte cobre banco vazio, parcial, completo, incompatível, versão futura, repetição, rollback e inicializações concorrentes.

#### Problema

O bootstrap consulta as tabelas `contacorrente`, `movimento` e `idempotencia`, mas usa `FirstOrDefault()` e encerra a execução ao encontrar qualquer uma delas. Se o banco contiver somente uma ou duas tabelas, o método pode retornar sem criar o restante do esquema.

Essa falha pode afetar diretamente a disponibilidade da tabela `idempotencia`, além de deixar o banco em um estado parcial e inconsistente.

#### Direção planejada

- Verificar cada tabela individualmente ou utilizar criação idempotente de esquema.
- Avaliar `CREATE TABLE IF NOT EXISTS` de acordo com a versão suportada do SQLite.
- Executar a preparação do esquema dentro de uma transação.
- Preservar dados existentes em execuções repetidas.
- Remover a supressão de nulabilidade em `Program.cs` por meio de resolução obrigatória e segura do serviço, quando a correção for implementada.

#### Critérios de aceite

- [x] As três tabelas esperadas são verificadas individualmente.
- [x] Um banco vazio recebe todo o esquema e os dados iniciais esperados.
- [x] Um banco com esquema parcial recebe somente os elementos ausentes.
- [x] Um banco com esquema completo não recebe alterações destrutivas.
- [x] O bootstrap pode ser executado repetidas vezes sem duplicar dados.
- [x] A criação ou atualização do esquema é atômica.
- [x] Existem testes para banco vazio, parcial e completo.

---

### TODO-002 — Implementar idempotência no serviço de movimentação

- **Estado:** Concluído em 1º de outubro de 2026 — entregas C1, C2 e C3 implementadas e validadas
- **Prioridade:** Alta
- **Próxima etapa:** manutenção evolutiva e integração futura com identidade autenticada, fora do escopo atual
- **Dependência técnica:** TODO-001
- **Estrutura de banco existente:** tabela `idempotencia`
- **Especificação:** `ESPECIFICACAO_MOVIMENTACAO.md`
- **Evidência C1:** comando e handler MediatR, normalização determinística, store SQLite com transação imediata, relógio e gerador de ID injetáveis e testes reais de atomicidade, repetição, conflito, rollback e concorrência.
- **Evidência C2:** `POST /api/v1/movimentos`, DTO HTTP fechado, JSON estrito, validação automática, limite de corpo de 4 KiB, respostas 200/400/409/413 correlacionadas e testes de integração sobre banco descartável.
- **Evidência C3:** timeout de 5 segundos com cancelamento, limites fixos global e específico por IP, concorrência máxima de 8 sem fila, respostas 429/504 correlacionadas, `Retry-After` quando calculável e eventos estruturados 5100–5105 com fingerprint SHA-256 truncado e sem dados bancários.

#### Problema

O esquema possui a tabela `idempotencia`, com as colunas `chave_idempotencia`, `requisicao` e `resultado`, mas não existe código da aplicação que utilize essa estrutura.

O enunciado determina que o cliente pode repetir uma movimentação quando perder a conexão antes de receber a resposta. A identificação da requisição deve impedir que essas tentativas criem movimentos duplicados.

Portanto, a existência da tabela representa somente uma estrutura inicial; o comportamento idempotente ainda não está implementado.

#### Direção planejada

- Expor `POST /api/v1/movimentos` com DTO explícito contendo identificação da requisição, identificação da conta, valor decimal e tipo `C` ou `D`.
- Usar UUID canônico como chave, representação interna versionada e determinística para a requisição e resultado mínimo versionado.
- Devolver HTTP 200 com o mesmo ID original em repetições idênticas e HTTP 409 `IDEMPOTENCY_CONFLICT` quando a chave for reutilizada com payload diferente.
- Persistir movimento e registro idempotente na mesma transação SQLite imediata.
- Serializar escritores concorrentes pelo banco, sem lock em memória, mantendo a chave primária como defesa final.
- Não reservar chaves para falhas de transporte, regra de negócio ou transações revertidas.
- Usar `decimal`, escala máxima 2 e rejeição de arredondamento implícito, preservando temporariamente o `REAL` legado no limite de persistência.
- Aplicar limites, logs e matriz de testes definidos em `ESPECIFICACAO_MOVIMENTACAO.md`.

#### Critérios de aceite

- [x] Toda movimentação no núcleo C1 possui uma chave de idempotência obrigatória e validada.
- [x] A primeira requisição válida gera exatamente um movimento.
- [x] A repetição da mesma requisição devolve o resultado originalmente armazenado.
- [x] A repetição não cria um segundo movimento.
- [x] A mesma chave não aceita silenciosamente uma requisição diferente.
- [x] O movimento e o resultado idempotente são persistidos atomicamente.
- [x] Requisições concorrentes com a mesma chave não geram duplicidade.
- [x] Falhas e rollbacks não deixam registros parciais.
- [x] Existem testes unitários e de integração para sucesso, repetição, conflito, concorrência, rollback e contrato HTTP.
- [x] Timeout, frequência, concorrência e logs estruturados estão implementados e cobertos por testes.
- [x] Logs não registram chave integral, conta, valor, payload, SQL, caminho do banco ou mensagem bruta de exceção.

---

## Backlog de segurança identificado em 30 de setembro de 2026

Esta seção registra os achados da análise estática realizada no branch `20260930` com base em OWASP API Security Top 10 2023, CWE Top 25 2025, NIST SSDF 1.1, OpenSSF, CIS Benchmarks e MITRE ATT&CK v19.2.

Os itens abaixo não autorizam automaticamente alterações funcionais. Sua execução deverá respeitar dependências, decisões de arquitetura e autorização específica. A ausência de infraestrutura ou pipeline foi registrada como condição de avaliação, sem alegação de conformidade.

### SEC-001 — Definir e implementar autenticação da API

- **Estado:** Em andamento — reaberto em 1º de outubro de 2026; planejado na Entrega F0, implementação na Entrega F1
- **Prioridade:** Alta
- **Referenciais:** OWASP API2 e API5; CWE-306 e CWE-862; NIST SSDF PW.4
- **Evidência:** `Program.cs` usa autorização, mas não registra autenticação, não executa `UseAuthentication` e os endpoints atuais não exigem identidade.
- **Decisão de 30 de setembro de 2026 (superada):** a API permaneceria sem autenticação neste desafio, como risco aceito.
- **Decisão de 1º de outubro de 2026:** o usuário revogou a exceção. A API validará JWT de um emissor externo configurável, conforme `ESPECIFICACAO_AUTENTICACAO.md`. O emissor do ambiente do desafio é uma ferramenta de teste que não autentica pessoas; essa limitação é aceita somente para o exercício.

#### Critérios de aceite

- [x] A decisão anterior de ausência de autenticação e seu risco residual foram documentados.
- [x] A autenticação por JWT, a validação do token e a limitação do emissor de teste estão especificadas.
- [ ] Todo endpoint bancário exige token válido; token ausente ou inválido retorna 401.
- [ ] Assinatura, algoritmo, emissor, audiência, validade e sujeito são validados e cobertos por testes negativos.
- [ ] Controles complementares: rate limiting, limites de entrada, logs e correlação estão implementados na movimentação; TLS de produção depende do ambiente.
- [ ] Substituir o emissor de teste e reavaliar este item antes de qualquer implantação real.

### SEC-002 — Implementar autorização em nível de conta e função

- **Estado:** Em andamento — reaberto em 1º de outubro de 2026; planejado na Entrega F0, implementação nas Entregas F2 (movimentação) e E (saldo)
- **Prioridade:** Alta
- **Referenciais:** OWASP API1, API3 e API5; CWE-284, CWE-639, CWE-862 e CWE-863
- **Evidência:** não existe vínculo entre identidade, conta corrente e permissão para consultar saldo ou realizar movimentação.
- **Decisão de 30 de setembro de 2026 (superada):** seriam mantidas apenas validações de existência e estado, sem autorização por titular.
- **Decisão de 1º de outubro de 2026:** a conta só poderá ser movimentada ou consultada pelo correntista registrado em `titularidade_conta`, identificado pela claim `sub`. Autorização por função ou papel continua fora do escopo.

#### Critérios de aceite

- [x] Está documentado que conta existente/ativa não equivale a conta autorizada.
- [x] O DTO da movimentação expõe apenas propriedades necessárias e impede overposting.
- [x] O vínculo conta–correntista e a regra de titularidade estão especificados.
- [ ] A movimentação só é aceita para conta do correntista autenticado.
- [ ] A consulta de saldo só é aceita para conta do correntista autenticado.
- [ ] Conta de outro correntista é indistinguível de conta não cadastrada na resposta, reduzindo enumeração.
- [ ] A idempotência não permite que um correntista recupere o resultado de outro.
- [ ] Reavaliar este item antes de qualquer implantação real.

### SEC-003 — Corrigir a validação parcial do bootstrap

- **Estado:** Concluído em 1º de outubro de 2026
- **Prioridade:** Alta
- **Referenciais:** OWASP API8; CWE-703 e CWE-754; NIST SSDF PW.7 e PW.8
- **Evidência de origem:** `DatabaseBootstrap.Setup` encerrava quando encontrava qualquer uma das três tabelas esperadas.
- **Evidência de conclusão:** `SqliteSchemaValidator` valida individualmente colunas, tipos declarados, nulabilidade, chaves primárias, defaults, índice único de número de conta, constraints `CHECK` e chave estrangeira. `PRAGMA user_version` rejeita versões futuras; estruturas incompatíveis produzem `InvalidDatabaseSchemaException` antes do commit.

#### Critérios de aceite

- [x] Cada tabela, coluna, constraint e versão esperada é validada individualmente.
- [x] Banco parcial não é aceito como banco completo.
- [x] Estrutura incompatível produz falha segura e diagnóstico interno útil.
- [x] Existem testes para banco vazio, parcial, completo e incompatível.

### SEC-004 — Tornar bootstrap e seed atômicos e concorrentes com segurança

- **Estado:** Concluído em 1º de outubro de 2026
- **Prioridade:** Alta
- **Referenciais:** CWE-362, CWE-367 e CWE-703; NIST SSDF PW.5, PW.7 e PW.8
- **Evidência de origem:** tabelas e contas iniciais eram criadas por comandos separados, sem transação ou proteção para inicializações simultâneas.
- **Evidência de conclusão:** toda preparação usa uma transação SQLite imediata (`BeginTransaction(deferred: false)`), serializando escritores concorrentes. O seed usa parâmetros e `ON CONFLICT(idcontacorrente) DO NOTHING`; versão e commit são gravados somente após `foreign_key_check` e `integrity_check`. Testes comprovam rollback, repetição sem sobrescrita e seis inicializações simultâneas sobre o mesmo arquivo.

#### Critérios de aceite

- [x] Criação do esquema e seed são executados atomicamente.
- [x] Falhas provocam rollback completo.
- [x] Inicializações concorrentes não deixam esquema ou dados parciais.
- [x] Execuções repetidas preservam dados e não duplicam contas.
- [x] Existem testes de falha intermediária, rollback e concorrência.

### SEC-005 — Definir representação monetária determinística

- **Estado:** Em andamento — contrato `decimal` aplicado em C1, estratégia do saldo definida na Entrega D e projeção persistida definida na revisão documental; implementação e testes permanecem pendentes
- **Prioridade:** Alta
- **Referenciais:** CWE-682 e CWE-1339; NIST SSDF PW.4 e PW.5
- **Evidência:** a coluna `movimento.valor` está declarada como `REAL`, tipo de ponto flutuante binário inadequado para precisão financeira sem estratégia adicional.
- **Decisão da Entrega B:** contratos e cálculos usarão `decimal`, entradas aceitarão no máximo duas casas sem arredondamento implícito e o limite por movimento será `9999999999.99`. O `REAL` será preservado temporariamente apenas por compatibilidade; a migração permanece fora de TODO-002.
- **Evidência C1:** `CreateMovementCommand` e a requisição normalizada usam `decimal`; escala maior que 2, valor não positivo e valor acima do limite são rejeitados; a representação canônica usa cultura invariável e duas casas; somente o adaptador SQLite converte para `REAL`.
- **Decisão da Entrega D:** a consulta lerá movimentos em snapshot consistente, converterá cada `REAL` no adaptador e acumulará em `decimal` verificado, sem `SUM(valor)` em ponto flutuante nem arredondamento silencioso. A limitação de precisão já perdida no armazenamento permanece risco residual.
- **Revisão da Entrega D:** o saldo será mantido em `saldo_conta` com centavos inteiros e atualizado na mesma transação do movimento. A leitura converte centavos para `decimal`; a reconstrução inicial converte cada `REAL` legado em `decimal` verificado, sem `SUM(valor)` em ponto flutuante. A precisão perdida no armazenamento legado permanece risco residual.

#### Critérios de aceite

- [x] A unidade de armazenamento, escala e regra de arredondamento estão documentadas, incluindo a limitação do `REAL` legado.
- [x] A API de movimentação usa `decimal` nos contratos e cálculos monetários.
- [x] O uso de centavos em `INTEGER` foi avaliado; decidiu-se por mitigação temporária compatível com o esquema legado e migração futura separada.
- [ ] Saldo e movimentações mantêm precisão em casos limítrofes e repetidos.
- [ ] Existem testes para centavos, arredondamento, limites e soma de muitos movimentos.
- [ ] A projeção `saldo_conta` mantém o saldo em centavos inteiros e reconcilia com os movimentos.

### SEC-006 — Habilitar e testar integridade referencial SQLite

- **Estado:** Concluído em 1º de outubro de 2026 — núcleo e cobertura HTTP validados
- **Prioridade:** Média
- **Referenciais:** OWASP API8; CWE-20 e CWE-703
- **Evidência de origem:** a string de conexão não habilitava explicitamente `Foreign Keys=True`; o comportamento efetivo dependia da conexão e da compilação da biblioteca nativa.
- **Evidência atual:** `DatabaseConfig` força `Foreign Keys=True`; `SqliteConnectionFactory` centraliza a abertura, cria o diretório operacional e valida `PRAGMA foreign_keys`; o bootstrap usa a factory; testes confirmam o pragma e a rejeição de movimento órfão pelo banco.

#### Critérios de aceite

- [x] Foreign keys são habilitadas explicitamente em toda conexão aplicável.
- [x] O bootstrap valida o estado de `PRAGMA foreign_keys`.
- [x] Movimento associado a conta inexistente é rejeitado pelo banco e pela aplicação.
- [x] Existem testes de integração para registro órfão no banco e rejeição nas camadas de aplicação e HTTP.

### SEC-007 — Uniformizar o tipo da chave estrangeira de conta

- **Estado:** Bloqueado — exige reconstrução da tabela `movimento` em migração própria, ainda não autorizada; SEC-003 e SEC-004, que a antecediam, estão concluídos
- **Prioridade:** Média
- **Referenciais:** CWE-20 e CWE-704; NIST SSDF PW.5
- **Evidência:** `contacorrente.idcontacorrente` é `TEXT(37)`, enquanto `movimento.idcontacorrente` está declarado como `INTEGER(10)`.

#### Critérios de aceite

- [ ] Chave primária e chave estrangeira usam o mesmo tipo lógico.
- [ ] Migração ou reconstrução preserva a integridade dos dados existentes.
- [ ] Consultas e índices usam a representação uniforme.
- [ ] Há teste de integração para relacionamento e consulta por conta.

### SEC-008 — Implementar limites de recursos e proteção contra abuso

- **Estado:** Concluído em 1º de outubro de 2026 no escopo anônimo do exercício
- **Prioridade:** Média; elevar para alta antes de disponibilizar os endpoints bancários
- **Referenciais:** OWASP API4 e API6; CWE-770
- **Evidência de origem:** não havia rate limiting, limites específicos de corpo, frequência, concorrência ou crescimento do fluxo de movimentação/idempotência.
- **Decisão da Entrega B:** corpo de 4 KiB, timeout de 5 segundos, 30 requisições por minuto por IP no endpoint, limite global de 120 por minuto por IP, até 8 operações concorrentes sem fila e HTTP 429 para excesso. O uso de IP é controle compensatório restrito ao exercício anônimo.
- **Evidência C3:** os controles foram implementados com middleware nativo do ASP.NET Core, configuração externalizável, filas desabilitadas, Problem Details correlacionado, `Retry-After` para janelas fixas e testes de frequência, concorrência e timeout. Limites por identidade, conta ou chave dependem de autenticação futura e não são alegados como atendidos.
- **Decisão da Entrega F0:** com a autenticação, o limite específico passará a ser contado por correntista na Entrega F2; o limite global por IP permanece e é aplicado antes da autenticação.

#### Critérios de aceite

- [x] Limites globais e por endpoint estão definidos e documentados.
- [ ] Limites por cliente, conta ou chave são aplicados conforme o modelo de identidade.
- [x] Excesso retorna HTTP 429 de forma consistente.
- [x] Timeouts, tamanho de entrada e concorrência são limitados.
- [x] Existem testes de abuso, rajada, repetição e exaustão de recursos aplicáveis ao escopo atual.

### SEC-009 — Centralizar tratamento seguro de erros

- **Estado:** Concluído em 30 de setembro de 2026
- **Prioridade:** Média
- **Referenciais:** OWASP API8; CWE-200 e CWE-209
- **Evidência de origem:** não havia tratamento global explícito, `ProblemDetails` customizado ou contrato uniforme para exceções inesperadas.
- **Evidência de conclusão:** `GlobalExceptionHandler` centraliza erros de negócio e exceções inesperadas; validação automática, erros 400 e erros 500 usam `ProblemDetails` correlacionado; respostas inesperadas não expõem mensagem, stack trace, caminho, SQL ou segredo; o evento interno 9000 registra somente correlation ID e tipo da exceção. Testes de integração cobrem validação, regra de negócio, exceção inesperada e conteúdo seguro do log.
- **Evidência C2:** conflito idempotente passou a ser tratado centralmente como HTTP 409 com `IDEMPOTENCY_CONFLICT`; validações do DTO, JSON inválido, propriedade desconhecida e corpo acima do limite retornam Problem Details correlacionado.

#### Critérios de aceite

- [x] Exceções são tratadas centralmente.
- [x] Respostas usam contrato consistente e identificador de correlação.
- [x] Stack traces, caminhos, SQL, headers, segredos e detalhes internos não são enviados ao cliente.
- [x] Logs internos preservam diagnóstico suficiente sem dados sensíveis.
- [x] Existem testes de validação, erro de negócio e exceção inesperada.

### SEC-010 — Remover supressões inseguras de nulabilidade

- **Estado:** Concluído em 30 de setembro de 2026
- **Prioridade:** Média
- **Referenciais:** CWE-476; NIST SSDF PW.7
- **Evidência de origem:** `Program.cs` e `DatabaseConfig.cs` desabilitavam warnings de nulabilidade em vez de garantir valores obrigatórios por construção.
- **Evidência de conclusão:** `DatabaseConfig` agora exige valor não vazio por construção; `IDatabaseBootstrap` é resolvido com `GetRequiredService`; os testes cobrem valor válido, nulo, vazio e composto apenas por espaços; build Release concluído com zero warnings e zero erros.

#### Critérios de aceite

- [x] Serviços obrigatórios são resolvidos com falha explícita quando ausentes.
- [x] Configurações obrigatórias são validadas durante a inicialização.
- [x] Não há `#pragma` de nulabilidade nos pontos corrigidos.
- [x] Build e testes cobrem configuração ausente ou inválida.

### SEC-011 — Restringir hosts permitidos por ambiente

- **Estado:** Bloqueado — depende da topologia de deploy
- **Prioridade:** Média-baixa
- **Referenciais:** OWASP API8; CIS Benchmark aplicável ao ambiente
- **Evidência:** `AllowedHosts` está configurado como `*`.

#### Critérios de aceite

- [ ] Hosts esperados são definidos por ambiente.
- [ ] Reverse proxy e forwarded headers são configurados e testados quando aplicáveis.
- [ ] Requisições com Host indevido são rejeitadas.
- [ ] A configuração de produção não depende de wildcard sem risco aceito documentado.

### SEC-012 — Definir política TLS/HTTPS de produção

- **Estado:** Bloqueado — depende da infraestrutura de deploy
- **Prioridade:** Média
- **Referenciais:** OWASP API8; CIS Benchmark aplicável; NIST SSDF PS.1
- **Evidência:** a aplicação usa redirecionamento HTTPS, mas também escuta HTTP no perfil local e não existe topologia de produção definida.

#### Critérios de aceite

- [ ] O ponto de terminação TLS e a fronteira de confiança estão documentados.
- [ ] A API de produção não aceita dados sensíveis por HTTP inseguro.
- [ ] Certificados, protocolos e cifras seguem o benchmark da plataforma.
- [ ] Forwarded headers e redirecionamentos não permitem spoofing ou loops.
- [ ] A postura TLS é validada no ambiente de implantação.

### SEC-013 — Isolar o banco SQLite versionado

- **Estado:** Em andamento — isolamento local concluído; backup e permissões dependem do deploy
- **Prioridade:** Média
- **Referenciais:** NIST SSDF PS.1; OpenSSF; proteção de dados e artefatos
- **Evidência de origem:** `Questao5/database.sqlite` era usado como banco operacional e podia receber acidentalmente movimentos, chaves idempotentes ou respostas.
- **Evidência atual:** o banco operacional padrão passou para `.data/database.sqlite`, ignorado pelo Git; testes usam bancos exclusivos em `%TEMP%`; a fixture versionada é copiada como somente referência nos testes e possui SHA-256 validado automaticamente.

#### Critérios de aceite

- [x] O banco de referência permanece imutável ou é substituído por criação determinística.
- [x] Desenvolvimento e testes usam cópias descartáveis fora dos arquivos rastreados.
- [x] Hash e conteúdo esperado da fixture são verificáveis.
- [x] Dados operacionais não são commitados.
- [ ] A estratégia de backup e permissões é definida no deploy.

### SEC-014 — Ampliar prevenção contra commit de segredos e configurações locais

- **Estado:** Em andamento — prevenção e scanner local concluídos; integração ao pipeline depende de SEC-021
- **Prioridade:** Média
- **Referenciais:** NIST SSDF PS.1; OpenSSF; CWE-798
- **Evidência de origem:** `.env`, `secrets.json`, `appsettings.Local.json` e outros arquivos locais sensíveis não eram ignorados.
- **Evidência atual:** `.gitignore` cobre configurações locais, bancos operacionais, certificados e chaves privadas sem ocultar templates seguros; `scripts/Test-TrackedSecrets.ps1` verifica arquivos versionados e candidatos ao commit; o gate local passou sem achados.

#### Critérios de aceite

- [x] Padrões locais sensíveis são ignorados sem ocultar templates seguros necessários.
- [x] A aplicação aceita configuração externa por providers nativos do ASP.NET Core; o mecanismo definitivo de produção será definido com o deploy.
- [ ] Secret scanning é executado no repositório e no pipeline; a execução local está implementada e validada, mas ainda não existe pipeline.
- [x] A revisão e o scanner local não encontraram segredo em código, configuração versionada, logs ou artefatos.

### SEC-015 — Implementar logs estruturados de segurança

- **Estado:** Em andamento — eventos da movimentação implementados e testados; cobertura global e operação dependem das próximas entregas e do ambiente
- **Prioridade:** Média
- **Referenciais:** MITRE ATT&CK v19.2; NIST SSDF RV.1; CWE-117
- **Evidência de origem:** o logger injetado não era utilizado e não havia eventos explícitos para bootstrap, validação, autenticação, autorização, abuso ou idempotência.
- **Decisão da Entrega B:** Event IDs 5100 a 5105 distinguirão primeira execução, repetição, conflito, rejeição de negócio, rollback e limite excedido. Payload, conta, valor, chave integral, SQL e connection string são proibidos nos logs.
- **Evidência C3:** `MovementLogger` implementa e testa os Event IDs 5100 a 5105, fingerprint truncado da chave, campos estruturados e ausência de conta, valor, payload, chave integral, SQL, caminho do banco e mensagem bruta de exceção.

#### Critérios de aceite

- [x] Os eventos da movimentação possuem IDs estáveis, nível, componente, resultado e campos estruturados; timestamp é fornecido pelo pipeline de logging.
- [ ] São registrados bootstrap, falhas de validação, autenticação, autorização, rate limiting, idempotência, rollback e exceções.
- [x] Os campos controlados usados nos eventos da movimentação são validados, normalizados ou derivados, sem interpolação livre suscetível a log forging.
- [x] Os testes da movimentação confirmam ausência de senhas, tokens, connection strings, payloads bancários completos e demais dados proibidos.
- [ ] Eventos são associados a Detection Strategies/Analytics aplicáveis e testados.

### SEC-016 — Implementar correlação e rastreabilidade de requisições

- **Estado:** Concluído em 30 de setembro de 2026
- **Prioridade:** Média-baixa
- **Referenciais:** MITRE ATT&CK v19.2; NIST SSDF RV.1
- **Evidência de origem:** não havia uso explícito de `TraceIdentifier`, `Activity` ou correlation ID.
- **Evidência de conclusão:** `CorrelationIdMiddleware` define `HttpContext.TraceIdentifier`, adiciona tag à `Activity`, devolve `X-Correlation-ID` em toda resposta e aceita somente um identificador externo de até 64 caracteres formado por allowlist ASCII. Valores ausentes, múltiplos ou inválidos são substituídos. O identificador aparece em respostas normais, validações, erros de negócio, exceções e logs; não é usado como chave de idempotência.

#### Critérios de aceite

- [x] Toda resposta e evento relevante possui identificador de correlação.
- [x] Correlation ID e chave de idempotência têm semânticas distintas.
- [x] IDs externos são validados e normalizados antes do uso em logs.
- [x] É possível rastrear a requisição entre entrada, resposta, logs e resultado da movimentação, preservando a distinção entre correlação e idempotência.

### SEC-017 — Padronizar datas e horários

- **Estado:** Em andamento — uso residual corrigido e contrato externo do saldo definido; implementação e testes temporais permanecem pendentes
- **Prioridade:** Baixa
- **Referenciais:** CWE-682; qualidade e rastreabilidade operacional
- **Evidência de origem:** o endpoint de exemplo usava `DateTime.Now`; o contrato bancário ainda não define UTC, fuso ou formato.
- **Evidência atual:** o uso residual foi substituído por `DateTime.UtcNow`; testes e novas persistências de teste usam `DateTimeOffset.UtcNow` e formato round-trip `O`.
- **Decisão da Entrega B:** a movimentação obterá a data por relógio UTC injetável e persistirá `datamovimento` no formato legado `dd/MM/yyyy`, com cultura invariável. O formato externo e o fuso da futura consulta de saldo continuam pendentes.
- **Evidência C1:** `MovementStore` usa `TimeProvider.GetUtcNow()` e cultura invariável; teste com relógio fixo comprova persistência determinística no formato legado.
- **Decisão da Entrega D:** `dataHoraConsulta` será obtida por `TimeProvider.GetUtcNow()` após a leitura e serializada em UTC pelo formato round-trip `O`, com offset `+00:00` e cultura invariável.

#### Critérios de aceite

- [x] Datas internas existentes usam UTC ou `DateTimeOffset`.
- [x] Formato externo e fuso da consulta de saldo estão documentados.
- [ ] Persistência e serialização são determinísticas e independentes da cultura do servidor.
- [ ] Existem testes para fuso e transição de data.

### SEC-018 — Remover endpoint residual de exemplo

- **Estado:** Concluído na Entrega C4
- **Prioridade:** Baixa
- **Referenciais:** OWASP API9
- **Evidência:** `WeatherForecastController`, o modelo `WeatherForecast` e comentários residuais do template foram removidos. Um teste de integração consulta o documento OpenAPI em Development e comprova que somente `POST /api/v1/movimentos` está exposto.

#### Critérios de aceite

- [x] Controller e modelo de exemplo são removidos.
- [x] Swagger e inventário contêm apenas endpoints intencionais.
- [x] Não permanecem rotas, modelos ou documentação de template sem uso.

### SEC-019 — Criar suíte de testes de segurança e regressão

- **Estado:** Em andamento — fundação criada; cobertura funcional crescerá com cada item
- **Prioridade:** Alta
- **Referenciais:** NIST SSDF PW.7, PW.8 e RV.1; OpenSSF
- **Evidência de origem:** não existia projeto ou suíte de testes no repositório.
- **Evidência atual:** `Questao5.Tests` usa lock file próprio, xUnit v3, Microsoft Testing Platform, NSubstitute, `WebApplicationFactory` e `coverlet.MTP`; 80 testes passam em Release. Há cobertura para configuração, banco temporário, bootstrap versionado e transacional, integridade referencial, hash da fixture, movimentação, contrato HTTP, inventário OpenAPI, idempotência, concorrência, rollback, timeout, abuso operacional, correlação, erros e logs sem dados sensíveis. A coleta em formato Cobertura permanece validada pelo gate local.
- **Planejamento da Entrega B:** `ESPECIFICACAO_MOVIMENTACAO.md` define testes HTTP, monetários, transacionais, idempotentes, concorrentes, de conflito, rollback, abuso, cancelamento e conteúdo seguro dos logs que serão obrigatórios na implementação de TODO-002.
- **Evidência C1:** a suíte passou a ter 55 testes, incluindo 28 casos focados no núcleo da movimentação para canonicalização, limites monetários, tipos, contas, persistência, repetição, conflito, rollback, cancelamento e concorrência idêntica ou conflitante.
- **Evidência C2:** a suíte passou a ter 72 testes, incluindo 17 casos HTTP para crédito, débito, repetição, conflito, contas inválidas, JSON malformado, campos ausentes, propriedade desconhecida, UUID, valores, tipo, media type e corpo de 4 KiB.
- **Evidência C3:** a suíte passou a ter 79 testes, incluindo frequência global e específica, concorrência sem fila, timeout com cancelamento observado, eventos 5100–5105, fingerprint estável e ausência de dados sensíveis nos logs.
- **Evidência C4:** a suíte passou a ter 80 testes com uma regressão que valida o documento OpenAPI e exige inventário restrito a `POST /api/v1/movimentos`.

#### Critérios de aceite

- [x] Existe projeto de testes separado da aplicação web.
- [x] A stack de testes adota xUnit v3 com Microsoft Testing Platform e NSubstitute, sem duplicar frameworks de mocking.
- [x] Testes de integração usam infraestrutura de banco descartável e determinístico.
- [x] São cobertos fluxo feliz HTTP, validações, idempotência, concorrência, rollback, timeout, abuso operacional e logs específicos; autenticação/autorização permanecem riscos aceitos para o exercício.
- [ ] Testes são executados automaticamente no pipeline e impedem regressões conhecidas.
- [x] Coleta de cobertura compatível com Microsoft Testing Platform v2 foi selecionada e validada com `coverlet.MTP 10.1.0`; `coverlet.collector` não é usado.

### SEC-020 — Fortalecer gates de build e analisadores

- **Estado:** Em andamento — gate local concluído; execução automática depende de SEC-021
- **Prioridade:** Média
- **Referenciais:** NIST SSDF PW.7 e PW.8; OpenSSF
- **Evidência de origem:** não estavam configurados explicitamente `TreatWarningsAsErrors`, nível de análise, restore locked no gate ou build contínuo determinístico.
- **Evidência atual:** `Directory.Build.props` habilita analisadores .NET 10 recomendados, code style em build, warnings como erros e build determinístico; `scripts/Invoke-SecurityGate.ps1` executa restore bloqueado, auditoria direta/transitiva, secret scanning, `dotnet format`, build CI, testes e cobertura. A única supressão é `CA1707` nos arquivos de teste, justificada pela convenção `Metodo_Cenario_Resultado`.

#### Critérios de aceite

- [x] Analisadores .NET estão habilitados em nível acordado.
- [x] Warnings relevantes falham o build, com exceções justificadas.
- [ ] CI usa restore bloqueado e build determinístico; o gate reproduzível está pronto, mas ainda não existe CI.
- [ ] SAST e auditoria de dependências são executados automaticamente; analisadores e auditoria rodam no gate local, mas ainda não existe CI.
- [x] Supressões possuem justificativa, escopo mínimo e revisão.

### SEC-021 — Criar pipeline seguro e controles OpenSSF

- **Estado:** Bloqueado — pipeline/plataforma ainda não definidos
- **Prioridade:** Alta antes de release ou deploy
- **Referenciais:** OpenSSF, SLSA, Sigstore; NIST SSDF PO, PS, PW e RV
- **Evidência:** não há CI/CD, `SECURITY.md`, `CODEOWNERS`, automação de atualização, SBOM, assinatura ou proveniência.

#### Critérios de aceite

- [ ] Branch principal possui proteção e revisão obrigatória quando suportado.
- [ ] Tokens, runners e jobs usam menor privilégio e isolamento de segredos.
- [ ] Ações, ferramentas e imagens são fixadas por versão imutável ou digest.
- [ ] Restore, build, testes, auditoria, SAST e secret scanning são gates obrigatórios.
- [ ] Release gera SBOM, hashes e proveniência; assinatura é aplicada quando compatível.
- [ ] `SECURITY.md`, ownership e processo de resposta a vulnerabilidades estão definidos.

### SEC-022 — Aplicar CIS Benchmark ao ambiente de deploy

- **Estado:** Não avaliável — infraestrutura ainda não definida
- **Prioridade:** Alta antes de produção
- **Referenciais:** CIS Benchmarks e CIS Software Supply Chain Security Benchmarks
- **Evidência:** não há Dockerfile, infraestrutura como código, configuração de servidor, reverse proxy ou ambiente de implantação no repositório.

#### Critérios de aceite

- [ ] Plataforma, sistema operacional, runtime, proxy, contêiner e CI/CD estão inventariados.
- [ ] Benchmarks e versões aplicáveis são identificados.
- [ ] Level 1 é avaliado como baseline; Level 2 é avaliado conforme criticidade.
- [ ] Hardening é testado antes da adoção e mantido como código quando possível.
- [ ] Exceções e riscos residuais são documentados.

### SEC-023 — Definir monitoramento e detecções orientados pelo MITRE ATT&CK

- **Estado:** Bloqueado — depende do ambiente de observabilidade e dos eventos do SEC-015
- **Prioridade:** Média; alta antes de produção
- **Referenciais:** MITRE ATT&CK v19.2; NIST SSDF RV.1
- **Evidência:** não existem retenção, alertas, métricas, dashboards, proteção de integridade ou validação de utilidade investigativa dos logs.

#### Critérios de aceite

- [ ] Ameaças e técnicas aplicáveis são mapeadas a Detection Strategies e Analytics atuais.
- [ ] Alertas cobrem enumeração, abuso de fluxo, falhas em massa, repetição e adulteração.
- [ ] Retenção, acesso, integridade, disponibilidade e privacidade dos logs estão definidos.
- [ ] Cenários de detecção são testados e produzem evidência útil para investigação.
- [ ] Há procedimento de triagem, resposta e melhoria após incidentes.

### Controles positivos preservados

- [x] Fonte NuGet restrita ao `nuget.org` com package source mapping.
- [x] Lock file com hashes de conteúdo presente.
- [x] Auditoria NuGet direta e transitiva habilitada no projeto.
- [x] SDK fixado por `global.json`, sem versões preview.
- [x] Dependências diretas usam versões explícitas.
- [x] Swagger está condicionado ao ambiente Development no pipeline atual.
- [x] Não foram encontrados segredos hardcoded, execução de comandos, SSRF, upload de arquivos ou CORS permissivo no código atual.
- [x] Restore bloqueado validado em 30 de setembro de 2026 para a solução e para o novo lock file de testes.
- [x] Auditoria direta e transitiva validada em 30 de setembro de 2026 sem pacotes vulneráveis conhecidos nas fontes configuradas.
- [x] Build Release validado com zero warnings e zero erros; seis testes executados com sucesso.
- [x] Hash SHA-256 de `Questao5/database.sqlite` permaneceu `E355A3EDB7D73E7784A7467513248D530C27CC989C624A35D13A188B2AA8DD3C` após restore, build e testes.

### Limitações da análise

- [x] Vulnerabilidades diretas e transitivas foram revalidadas após restore bloqueado; nenhuma vulnerabilidade conhecida foi reportada.
- [x] O valor efetivo de `PRAGMA foreign_keys` foi confirmado em teste, incluindo rejeição de registro órfão.
- [ ] Migrar `MediatR.Extensions.Microsoft.DependencyInjection` 11.1.0, pacote legado preexistente, em mudança separada e com testes de regressão do bootstrap.
- [ ] Avaliar CIS somente quando a infraestrutura e o deploy forem definidos.
- [ ] Reavaliar OWASP API1, API2, API3, API5 e API6 antes de qualquer implantação real; SEC-001 e SEC-002 foram reabertos em 1º de outubro de 2026, e o emissor de teste é limitação aceita apenas para este exercício.

---

## Procedimento após a conclusão dos sete marcos

1. Confirmar documentalmente a conclusão das sete fases de segurança.
2. Apresentar ao usuário o relatório consolidado, os riscos residuais e as recomendações.
3. Confirmar que nenhum código funcional foi alterado como consequência automática da conclusão dos marcos.
4. Manter cada pendência funcional como **Bloqueada** até seu planejamento e sua autorização específicos.
5. Interromper a execução e aguardar os detalhes adicionais do usuário.
6. Somente após uma nova autorização expressa, elaborar um plano específico para as pendências funcionais.

Este procedimento foi cumprido. TODO-001 recebeu autorização própria e foi concluído em 1º de outubro de 2026. TODO-002 foi planejado na Entrega B, implementado nas entregas C1 a C3 e concluído em 1º de outubro de 2026. C4 foi concluída e a Entrega D formalizou o planejamento da consulta de saldo. Em 1º de outubro de 2026 o usuário autorizou a sequência F0, F1, F2, E e G descrita no roadmap.

Não existe, neste documento, autorização antecipada para implementar outras pendências funcionais; TODO-003 a TODO-006 aguardam decisão.

## Guardrails permanentes de segurança

- [ ] Confirmar o branch `20260930` antes de cada nova etapa de implementação.
- [ ] Mapear cada endpoint e fluxo aos riscos aplicáveis do OWASP API Security Top 10 2023.
- [ ] Revisar cada mudança contra as fraquezas aplicáveis do CWE Top 25 2025.
- [ ] Aplicar as práticas PO, PS, PW e RV do NIST SSDF 1.1 durante o ciclo de desenvolvimento.
- [ ] Avaliar novas dependências antes da inclusão e manter restore NuGet bloqueado, auditável e reproduzível.
- [ ] Definir pipeline com menor privilégio, componentes fixados, isolamento de segredos e proteção de artefatos.
- [ ] Gerar SBOM, hashes e proveniência/assinatura quando houver processo de release.
- [ ] Identificar e validar o CIS Benchmark específico quando infraestrutura ou deploy forem definidos.
- [ ] Definir logs estruturados, sem dados sensíveis, e casos de detecção orientados pelo MITRE ATT&CK.
- [ ] Executar testes positivos, negativos, de abuso, concorrência e regressão proporcionais à mudança.
- [ ] Documentar exceções, controles não aplicáveis e riscos residuais.
- [ ] Consultar `DIRETRIZES_SEGURANCA.md` como gate obrigatório de toda tarefa futura.

Estes itens são controles contínuos e não devem ser marcados globalmente como concluídos; sua aplicação deve ser comprovada em cada mudança relevante.

## Histórico de atualização deste TODO

- **30 de setembro de 2026:** documento criado após análise estática Zero Trust. TODO-001 e TODO-002 registrados como bloqueados até a conclusão das sete fases de segurança.
- **30 de setembro de 2026:** acrescentada barreira de aprovação manual. A conclusão das sete fases não desbloqueia automaticamente TODO-001 ou TODO-002; após a Fase 7, o trabalho deve parar e aguardar detalhes adicionais e autorização expressa do usuário.
- **30 de setembro de 2026:** sete marcos de segurança concluídos. Relatório consolidado criado em `RELATORIO_SEGURANCA.md`. TODO-001 e TODO-002 permanecem bloqueados.
- **30 de setembro de 2026:** adotados como guardrails permanentes OWASP API Security Top 10, CWE Top 25, CIS Benchmarks, NIST SSDF, OpenSSF e MITRE ATT&CK. Criado `DIRETRIZES_SEGURANCA.md`; o branch de trabalho definido é `20260930`.
- **30 de setembro de 2026:** análise estática do código atual convertida no backlog SEC-001 a SEC-023, com prioridades, referenciais, evidências, dependências, critérios de aceite, controles positivos e limitações de avaliação.
- **1º de outubro de 2026:** TODO-001, SEC-003 e SEC-004 concluídos após autorização específica. O bootstrap SQLite tornou-se versionado, estrito, idempotente, atômico e seguro para inicializações concorrentes. O gate completo passou com 31 testes, zero warnings, zero erros, sem vulnerabilidades conhecidas ou segredos detectados; a fixture manteve o SHA-256 esperado.
- **1º de outubro de 2026:** Entrega B concluída após autorização específica. O contrato HTTP, a regra monetária, a canonicalização idempotente, a transação concorrente, os limites, os eventos de log e a matriz de testes de TODO-002 foram definidos em `ESPECIFICACAO_MOVIMENTACAO.md`. TODO-002 passou de Bloqueado para Planejado, sem implementação funcional.
- **1º de outubro de 2026:** Entrega C1 implementou o núcleo transacional e idempotente da movimentação com MediatR, Dapper e SQLite, sem endpoint HTTP. Testes cobrem regras, atomicidade, repetição, conflito, rollback, cancelamento e concorrência; TODO-002 passou a Em andamento, aguardando C2 e C3.
- **1º de outubro de 2026:** Entrega C2 implementou `POST /api/v1/movimentos`, DTO fechado, JSON estrito, validação automática, Problem Details para conflito e limite de corpo de 4 KiB. A suíte atingiu 72 testes; C3 permanece pendente para limites operacionais e logs.
- **1º de outubro de 2026:** Entrega C3 implementou timeout de 5 segundos, rate limits de 30 e 120 requisições por minuto por IP, concorrência máxima de 8 sem fila, respostas 429/504 correlacionadas e logs 5100–5105 sem dados bancários. A suíte atingiu 79 testes e TODO-002 foi concluído.
- **1º de outubro de 2026:** pausa de organização concluída. `TODO.md` foi confirmado como fonte canônica de escopo e andamento; documentos especializados e históricos foram classificados sem criar backlog paralelo. O roadmap foi consolidado com C4 para limpeza, documentação, gate e commit funcional, seguida da Entrega D para planejar a consulta de saldo. Estados evidentemente desatualizados de segurança foram reconciliados com as evidências de C1 a C3.
- **1º de outubro de 2026:** Entrega C4 removeu o endpoint e o modelo `WeatherForecast`, eliminou comentários residuais do template e adicionou regressão do inventário OpenAPI. A especificação foi reconciliada com o estado implementado. O gate completo passou com 80 testes, zero avisos, zero erros, sem vulnerabilidades conhecidas ou segredos detectados; a fixture manteve o SHA-256 esperado. SEC-018 foi concluído e D tornou-se a próxima entrega planejável.
- **1º de outubro de 2026:** Entrega D consolidou em `ESPECIFICACAO_SALDO.md` os requisitos, contrato HTTP, cálculo monetário, tempo UTC, snapshot de leitura, limites, logs, arquitetura, testes e riscos residuais da consulta de saldo. O gate passou com 80 testes, zero avisos, zero erros, sem vulnerabilidades conhecidas ou segredos detectados; a fixture manteve o SHA-256 esperado. Nenhum código funcional foi implementado; a Entrega E permanece bloqueada até autorização específica.
- **1º de outubro de 2026:** revisão documental da Entrega D substituiu o cálculo em tempo real pela projeção persistida de saldo (`saldo_conta`), com centavos inteiros, atualização transacional com movimento e idempotência, preenchimento inicial e reconciliação. O cache em memória foi avaliado e descartado nesta etapa, e o cenário foi reconfirmado como centrado em conta, sem identidade de titular. Nenhum código funcional, schema, fixture ou dado operacional foi alterado; a Entrega E permanece bloqueada.
- **1º de outubro de 2026:** levantamento de fechamento de escopo registrou TODO-003 a TODO-006, ainda sem autorização de execução. A Entrega F0 revogou a exceção de autenticação, reabriu SEC-001 e SEC-002 e consolidou em `ESPECIFICACAO_AUTENTICACAO.md` a autenticação JWT, o identificador do correntista, a tabela `titularidade_conta`, a regra de titularidade para movimentação e saldo, a idempotência `v2` e o emissor `mock-oauth2-server` em Docker Compose. As especificações de movimentação e saldo e as diretrizes foram atualizadas; a tabela de erros da movimentação passou a listar 413, 415 e 504, e o estado de SEC-007 foi corrigido. O usuário autorizou a sequência F0, F1, F2, E e G; a etapa E1 foi absorvida pela F2. Nenhum código, schema, fixture ou dado operacional foi alterado na F0.
