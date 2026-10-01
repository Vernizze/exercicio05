# TODO do projeto

Este documento registra pendências técnicas e funcionais identificadas durante a análise do projeto recebido.

## Regra de priorização

As correções funcionais identificadas durante a análise Zero Trust não fazem parte da execução atual. Neste momento, o trabalho está limitado às sete fases de segurança descritas abaixo.

Mesmo depois que as sete fases estiverem concluídas e validadas, os itens funcionais permanecerão com o estado **Bloqueado**. A conclusão do marco de segurança não autoriza nem inicia automaticamente qualquer correção funcional.

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

- **Estado:** Bloqueado
- **Prioridade:** Alta
- **Bloqueadores:** conclusão e validação das fases 1 a 7; discussão dos detalhes adicionais; autorização expressa do usuário
- **Dependência técnica:** TODO-001
- **Estrutura de banco existente:** tabela `idempotencia`

#### Problema

O esquema possui a tabela `idempotencia`, com as colunas `chave_idempotencia`, `requisicao` e `resultado`, mas não existe código da aplicação que utilize essa estrutura.

O enunciado determina que o cliente pode repetir uma movimentação quando perder a conexão antes de receber a resposta. A identificação da requisição deve impedir que essas tentativas criem movimentos duplicados.

Portanto, a existência da tabela representa somente uma estrutura inicial; o comportamento idempotente ainda não está implementado.

#### Direção planejada

- Receber uma identificação única em toda requisição de movimentação.
- Normalizar ou serializar deterministicamente os dados relevantes da requisição.
- Consultar a chave de idempotência antes de gerar um movimento.
- Devolver o resultado original quando a mesma requisição for repetida.
- Rejeitar de forma explícita a reutilização da mesma chave com conteúdo diferente.
- Persistir o movimento e o registro idempotente na mesma transação.
- Tratar concorrência para impedir que duas requisições simultâneas gerem movimentos duplicados.

#### Critérios de aceite

- [ ] Toda movimentação possui uma chave de idempotência obrigatória.
- [ ] A primeira requisição válida gera exatamente um movimento.
- [ ] A repetição da mesma requisição devolve o resultado originalmente armazenado.
- [ ] A repetição não cria um segundo movimento.
- [ ] A mesma chave não aceita silenciosamente uma requisição diferente.
- [ ] O movimento e o resultado idempotente são persistidos atomicamente.
- [ ] Requisições concorrentes com a mesma chave não geram duplicidade.
- [ ] Falhas e rollbacks não deixam registros parciais.
- [ ] Existem testes unitários e de integração para sucesso, repetição, conflito, concorrência e rollback.

---

## Backlog de segurança identificado em 30 de setembro de 2026

Esta seção registra os achados da análise estática realizada no branch `20260930` com base em OWASP API Security Top 10 2023, CWE Top 25 2025, NIST SSDF 1.1, OpenSSF, CIS Benchmarks e MITRE ATT&CK v19.2.

Os itens abaixo não autorizam automaticamente alterações funcionais. Sua execução deverá respeitar dependências, decisões de arquitetura e autorização específica. A ausência de infraestrutura ou pipeline foi registrada como condição de avaliação, sem alegação de conformidade.

### SEC-001 — Definir e implementar autenticação da API

- **Estado:** Risco aceito exclusivamente para o escopo do exercício — não implementar
- **Prioridade:** Alta
- **Referenciais:** OWASP API2 e API5; CWE-306 e CWE-862; NIST SSDF PW.4
- **Evidência:** `Program.cs` usa autorização, mas não registra autenticação, não executa `UseAuthentication` e os endpoints atuais não exigem identidade.
- **Decisão:** por solicitação expressa do usuário, a API permanecerá sem autenticação neste desafio. A decisão não representa conformidade e não pode ser reutilizada em produção.

#### Critérios de aceite

- [x] A ausência de autenticação e o risco residual estão documentados.
- [x] Está documentado que a decisão se limita ao exercício e é proibida para produção.
- [ ] Aplicar controles compensatórios: TLS, rate limiting, limites de entrada, logs, correlação e redução de enumeração.
- [ ] Reabrir este item antes de qualquer implantação real.

### SEC-002 — Implementar autorização em nível de conta e função

- **Estado:** Risco aceito exclusivamente para o escopo do exercício — autorização por titular não implementável sem identidade
- **Prioridade:** Alta
- **Referenciais:** OWASP API1, API3 e API5; CWE-284, CWE-639, CWE-862 e CWE-863
- **Evidência:** não existe vínculo entre identidade, conta corrente e permissão para consultar saldo ou realizar movimentação.
- **Decisão:** serão mantidas validações de existência, estado e propriedades mínimas, mas elas não serão tratadas como autorização.

#### Critérios de aceite

- [x] A impossibilidade de autorização por titular sem identidade está documentada.
- [x] Está documentado que conta existente/ativa não equivale a conta autorizada.
- [ ] DTOs devem expor apenas propriedades necessárias e impedir overposting.
- [ ] Respostas devem reduzir enumeração e exposição desnecessária.
- [ ] Reabrir este item antes de qualquer implantação real.

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

- **Estado:** Bloqueado — requer decisão de compatibilidade com o enunciado e o esquema recebido
- **Prioridade:** Alta
- **Referenciais:** CWE-682 e CWE-1339; NIST SSDF PW.4 e PW.5
- **Evidência:** a coluna `movimento.valor` está declarada como `REAL`, tipo de ponto flutuante binário inadequado para precisão financeira sem estratégia adicional.

#### Critérios de aceite

- [ ] A unidade de armazenamento, escala e regra de arredondamento estão documentadas.
- [ ] A API usa `decimal` nos contratos e cálculos monetários.
- [ ] É avaliado e decidido o uso de centavos em `INTEGER` ou uma mitigação compatível com o esquema exigido.
- [ ] Saldo e movimentações mantêm precisão em casos limítrofes e repetidos.
- [ ] Existem testes para centavos, arredondamento, limites e soma de muitos movimentos.

### SEC-006 — Habilitar e testar integridade referencial SQLite

- **Estado:** Em andamento — controle de conexão e banco concluído; validação da aplicação depende do endpoint de movimentação
- **Prioridade:** Média
- **Referenciais:** OWASP API8; CWE-20 e CWE-703
- **Evidência de origem:** a string de conexão não habilitava explicitamente `Foreign Keys=True`; o comportamento efetivo dependia da conexão e da compilação da biblioteca nativa.
- **Evidência atual:** `DatabaseConfig` força `Foreign Keys=True`; `SqliteConnectionFactory` centraliza a abertura, cria o diretório operacional e valida `PRAGMA foreign_keys`; o bootstrap usa a factory; testes confirmam o pragma e a rejeição de movimento órfão pelo banco.

#### Critérios de aceite

- [x] Foreign keys são habilitadas explicitamente em toda conexão aplicável.
- [x] O bootstrap valida o estado de `PRAGMA foreign_keys`.
- [ ] Movimento associado a conta inexistente é rejeitado pelo banco e pela aplicação.
- [x] Existe teste de integração para registro órfão no banco; a cobertura da camada de aplicação será adicionada com o endpoint.

### SEC-007 — Uniformizar o tipo da chave estrangeira de conta

- **Estado:** Bloqueado — exige reconstrução/migração do esquema vinculada a SEC-003 e SEC-004
- **Prioridade:** Média
- **Referenciais:** CWE-20 e CWE-704; NIST SSDF PW.5
- **Evidência:** `contacorrente.idcontacorrente` é `TEXT(37)`, enquanto `movimento.idcontacorrente` está declarado como `INTEGER(10)`.

#### Critérios de aceite

- [ ] Chave primária e chave estrangeira usam o mesmo tipo lógico.
- [ ] Migração ou reconstrução preserva a integridade dos dados existentes.
- [ ] Consultas e índices usam a representação uniforme.
- [ ] Há teste de integração para relacionamento e consulta por conta.

### SEC-008 — Implementar limites de recursos e proteção contra abuso

- **Estado:** Bloqueado — depende da definição dos endpoints e identidade do cliente
- **Prioridade:** Média; elevar para alta antes de disponibilizar os endpoints bancários
- **Referenciais:** OWASP API4 e API6; CWE-770
- **Evidência:** não há rate limiting, limites específicos de corpo, frequência, concorrência ou crescimento do fluxo de movimentação/idempotência.

#### Critérios de aceite

- [ ] Limites globais e por endpoint estão definidos e documentados.
- [ ] Limites por cliente, conta ou chave são aplicados conforme o modelo de identidade.
- [ ] Excesso retorna HTTP 429 de forma consistente.
- [ ] Timeouts, tamanho de entrada e concorrência são limitados.
- [ ] Existem testes de abuso, rajada, repetição e exaustão de recursos.

### SEC-009 — Centralizar tratamento seguro de erros

- **Estado:** Concluído em 30 de setembro de 2026
- **Prioridade:** Média
- **Referenciais:** OWASP API8; CWE-200 e CWE-209
- **Evidência de origem:** não havia tratamento global explícito, `ProblemDetails` customizado ou contrato uniforme para exceções inesperadas.
- **Evidência de conclusão:** `GlobalExceptionHandler` centraliza erros de negócio e exceções inesperadas; validação automática, erros 400 e erros 500 usam `ProblemDetails` correlacionado; respostas inesperadas não expõem mensagem, stack trace, caminho, SQL ou segredo; o evento interno 9000 registra somente correlation ID e tipo da exceção. Testes de integração cobrem validação, regra de negócio, exceção inesperada e conteúdo seguro do log.

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

- **Estado:** Bloqueado — depende dos fluxos funcionais e da estratégia de observabilidade
- **Prioridade:** Média
- **Referenciais:** MITRE ATT&CK v19.2; NIST SSDF RV.1; CWE-117
- **Evidência:** o logger injetado não é utilizado e não há eventos explícitos para bootstrap, validação, autenticação, autorização, abuso ou idempotência.

#### Critérios de aceite

- [ ] Eventos possuem IDs estáveis, timestamp UTC, nível, componente, resultado e campos estruturados.
- [ ] São registrados bootstrap, falhas de validação, autenticação, autorização, rate limiting, idempotência, rollback e exceções.
- [ ] Campos controlados pelo usuário não permitem log forging.
- [ ] Senhas, tokens, connection strings e payloads bancários completos não são registrados.
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
- [x] É possível rastrear a requisição entre entrada, resposta e erro; a associação com movimentações persistidas será mantida quando o fluxo bancário existir.

### SEC-017 — Padronizar datas e horários

- **Estado:** Em andamento — uso residual corrigido; contrato bancário ainda não existe
- **Prioridade:** Baixa
- **Referenciais:** CWE-682; qualidade e rastreabilidade operacional
- **Evidência de origem:** o endpoint de exemplo usava `DateTime.Now`; o contrato bancário ainda não define UTC, fuso ou formato.
- **Evidência atual:** o uso residual foi substituído por `DateTime.UtcNow`; testes e novas persistências de teste usam `DateTimeOffset.UtcNow` e formato round-trip `O`.

#### Critérios de aceite

- [x] Datas internas existentes usam UTC ou `DateTimeOffset`.
- [ ] Formato externo e fuso da consulta de saldo estão documentados.
- [ ] Persistência e serialização são determinísticas e independentes da cultura do servidor.
- [ ] Existem testes para fuso e transição de data.

### SEC-018 — Remover endpoint residual de exemplo

- **Estado:** Pendente — executar quando endpoints reais estiverem disponíveis
- **Prioridade:** Baixa
- **Referenciais:** OWASP API9
- **Evidência:** `/WeatherForecast` permanece exposto e não faz parte do requisito bancário.

#### Critérios de aceite

- [ ] Controller e modelo de exemplo são removidos.
- [ ] Swagger e inventário contêm apenas endpoints intencionais.
- [ ] Não permanecem rotas, modelos ou documentação de template sem uso.

### SEC-019 — Criar suíte de testes de segurança e regressão

- **Estado:** Em andamento — fundação criada; cobertura funcional crescerá com cada item
- **Prioridade:** Alta
- **Referenciais:** NIST SSDF PW.7, PW.8 e RV.1; OpenSSF
- **Evidência de origem:** não existia projeto ou suíte de testes no repositório.
- **Evidência atual:** `Questao5.Tests` usa lock file próprio, xUnit v3, Microsoft Testing Platform, NSubstitute, `WebApplicationFactory` e `coverlet.MTP`; 31 testes passam em Release. Há cobertura para configuração, banco temporário, bootstrap versionado e transacional, integridade referencial, hash da fixture, concorrência, rollback, correlação, validação, erro de negócio, exceção inesperada e logs sem dados sensíveis. A coleta em formato Cobertura permanece validada pelo gate local; os percentuais serão atualizados quando a próxima medição consolidada for registrada.

#### Critérios de aceite

- [x] Existe projeto de testes separado da aplicação web.
- [x] A stack de testes adota xUnit v3 com Microsoft Testing Platform e NSubstitute, sem duplicar frameworks de mocking.
- [x] Testes de integração usam infraestrutura de banco descartável e determinístico.
- [ ] São cobertos fluxo feliz HTTP, validações e logs; autenticação/autorização são riscos aceitos e abuso, idempotência, concorrência e rollback dependem dos fluxos bancários.
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
- [ ] Reavaliar OWASP API1, API2, API3, API5 e API6 antes de qualquer implantação real; SEC-001 e SEC-002 são riscos aceitos apenas para este exercício.

---

## Procedimento após a conclusão dos sete marcos

1. Confirmar documentalmente a conclusão das sete fases de segurança.
2. Apresentar ao usuário o relatório consolidado, os riscos residuais e as recomendações.
3. Confirmar que nenhum código funcional foi alterado como consequência automática da conclusão dos marcos.
4. Manter TODO-001 e TODO-002 como **Bloqueado**.
5. Interromper a execução e aguardar os detalhes adicionais do usuário.
6. Somente após uma nova autorização expressa, elaborar um plano específico para as pendências funcionais.

Não existe, neste documento, autorização antecipada para alterar o estado das pendências funcionais ou implementá-las.

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