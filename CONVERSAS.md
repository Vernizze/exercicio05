# Histórico de conversas e decisões

Este arquivo é o registro oficial das conversas sobre o desenvolvimento deste projeto.

## Convenção do registro

- Cada interação deve informar a **data**, o **modo de atuação** e o **autor**.
- **Usuário** identifica as solicitações e observações do responsável pelo projeto.
- **Cline (assistente)** identifica minhas respostas, análises e ações.
- **Plan** indica exploração, análise e planejamento, sem alterações no projeto.
- **Act** indica que alterações no projeto estão autorizadas e podem ser executadas.
- Novas conversas devem ser acrescentadas em ordem cronológica, preservando o histórico anterior.
- Toda troca do motor ou modelo de IA deverá ser registrada neste arquivo, informando a data e, quando disponível, o fornecedor, o nome e a versão do modelo anterior e do novo modelo.

---

## Interação 001 — 30 de setembro de 2026

**Modo: Act**

### Usuário

> Hoje temos um desafio diferente. Esta pasta que está aberta é um projeto que recebi nesta fase de desenvolvimento e tenho de fazer uma implementação sobre ela.
>
> Uma característica muito importante: todas as nossas conversas devem ser salvas em um arquivo `.md` específico, para que meus avaliadores entendam o que faremos daqui para frente. Quero que nessas conversas fique muito claro onde eu estou falando, onde você está respondendo e o modo em que estamos atuando (Plan/Act).
>
> Com isso em mente, antes de mais nada, crie esse arquivo já com o que estou lhe passando e aproveite a oportunidade para se apresentar e dar um alô para eles.

### Cline (assistente)

Olá, avaliadores! Sou **Cline**, o assistente de programação que acompanhará o responsável por este projeto durante a análise e a implementação solicitada.

Meu papel será ajudar a compreender a base de código recebida, planejar as mudanças, implementar a solução de acordo com os padrões já adotados no projeto e validar o resultado sempre que o ambiente permitir. Procurarei deixar claros os requisitos considerados, as decisões técnicas, os arquivos alterados e os testes executados.

Para garantir a rastreabilidade pedida, este arquivo, `CONVERSAS.md`, passa a ser o histórico oficial das nossas interações. Cada novo registro identificará claramente as falas do **Usuário** e do **Cline**, além do modo vigente: **Plan** ou **Act**.

Nesta primeira atividade, inspecionei a raiz do projeto, confirmei que não havia outro arquivo destinado a esse histórico e criei este documento sem modificar o código da aplicação.

É um prazer participar deste desafio. Alô e sejam bem-vindos ao nosso histórico de desenvolvimento!

---

## Interação 002 — 30 de setembro de 2026

**Modo: Act**

### Usuário

> Ótimo, Cline. Qual é o motor de IA que estamos utilizando? Já anote que, caso façamos trocas dele, você deve registrar no histórico também, entendido?

### Cline (assistente)

Entendido. Estamos utilizando o **Cline** como agente de programação, conectado a um modelo de IA da **OpenAI**. O identificador técnico exato do modelo subjacente — como seu nome específico e sua versão — não está exposto para mim nesta sessão. Por transparência, não atribuirei um nome de modelo que eu não consiga confirmar.

Também foi acrescentada à convenção deste documento uma regra permanente: toda troca do motor ou modelo de IA deverá ser registrada no histórico, incluindo a data da alteração e, quando essas informações estiverem disponíveis, o fornecedor, o nome e a versão do modelo anterior e do novo modelo.

**Identificação disponível nesta interação:**

- Agente de programação: **Cline**
- Fornecedor do modelo: **OpenAI**
- Nome e versão exatos do modelo: **não expostos nesta sessão**
- Situação: **configuração inicial registrada; nenhuma troca registrada até o momento**

---

## Interação 003 — 30 de setembro de 2026

**Modo: Plan**

### Usuário

> Ótimo, vamos começar. Antes de mais nada, planeje uma varredura pelos arquivos recebidos buscando por código suspeito ou malicioso. Como recebemos fontes semiprontos, é importante partir do conceito de Zero Trust e garantir antes de executar, ok?

### Cline (assistente)

Foi planejada e realizada uma análise estática Zero Trust, sem executar a aplicação, builds, testes, restores, scripts ou binários do projeto.

A inspeção abrangeu o inventário de arquivos; fontes C#; solução e projeto; configurações; cadeia de build e dependências; documento do exercício; banco SQLite; hooks e configuração Git; binários; assinaturas Authenticode; arquivos ocultos; links; reparse points; fluxos alternativos NTFS; possíveis segredos; indicadores de execução de shell, persistência, download, rede, ofuscação e carregamento dinâmico.

Não foram encontradas evidências diretas de código malicioso nos fontes examinados. A conclusão foi considerada preliminar, pois a ausência de indicadores na análise estática não constitui garantia absoluta de segurança.

Os principais pontos de atenção identificados foram:

1. Presença de artefatos pré-compilados em `bin` e `obj`, que não devem ser reutilizados.
2. Execução automática de `DatabaseBootstrap.Setup()` durante a inicialização da API.
3. Falha lógica no bootstrap, que encerra ao encontrar qualquer uma das três tabelas esperadas e pode deixar o esquema incompleto.
4. Dependências antigas, uma versão preview do SDK de testes e dependências de teste no projeto web.
5. Ausência de lock file e de configuração local explícita para fontes de pacotes.
6. Metadados de ambientes anteriores em `.vs` e `obj`.
7. Presença uniforme de `Zone.Identifier`, compatível com arquivos extraídos de um ZIP obtido externamente.

O documento `.docx` não apresentou macros, objetos OLE, executáveis ou relacionamentos externos. O banco apresentou cabeçalho SQLite válido e conteúdo compatível com o bootstrap. Não havia hooks Git ativos, links simbólicos ou reparse points.

Foi recomendado não executar o projeto naquele momento e seguir sete fases de segurança:

1. Registrar a análise.
2. Preservar evidências.
3. Higienizar o workspace.
4. Executar verificação antimalware.
5. Proteger a cadeia de dependências.
6. Realizar reconstrução limpa.
7. Realizar execução controlada.

Nenhum arquivo foi alterado durante essa interação em modo Plan.

---

## Interação 004 — 30 de setembro de 2026

**Modo: Plan**

### Usuário

> Ótimo. Para esses pontos levantados, vamos criar outro arquivo `.md` com um TODO, pois você encontrou o problema “3. Falha lógica no bootstrap — risco funcional” e algo ligado à idempotência, certo? Iremos tratá-los assim que esses sete passos de limpeza sugeridos estiverem concluídos.

### Cline (assistente)

Foi confirmado que existem duas pendências relacionadas, mas distintas:

1. O bootstrap usa `FirstOrDefault()` e encerra ao encontrar qualquer tabela reconhecida, podendo deixar `contacorrente`, `movimento` ou `idempotencia` ausentes.
2. A tabela `idempotencia` existe no esquema, mas não há comportamento idempotente implementado na aplicação.

O enunciado foi revisado estaticamente e confirma que o cliente pode repetir uma requisição de movimentação após perder a conexão. A identificação da requisição deverá impedir movimentos duplicados e permitir a recuperação do resultado original.

Foi planejada a criação de `TODO.md`, contendo as sete fases de segurança como marco obrigatório e as seguintes pendências bloqueadas:

- TODO-001 — corrigir a validação do bootstrap do SQLite.
- TODO-002 — implementar idempotência transacional no serviço de movimentação.

Ficou estabelecido que essas correções funcionais somente serão iniciadas após a conclusão e validação das sete fases de segurança. Nenhum arquivo foi alterado durante essa interação em modo Plan.

---

## Interação 005 — 30 de setembro de 2026

**Modo: Act**

### Usuário

> Ok, pode seguir.

### Cline (assistente)

Foi criado o arquivo `TODO.md` na raiz do projeto. O documento registra as sete fases de segurança, seus itens de verificação, a regra formal de bloqueio das correções funcionais, o TODO-001 sobre o bootstrap e o TODO-002 sobre idempotência.

Os dois TODOs foram registrados com prioridade alta e estado **Bloqueado**. O desbloqueio somente poderá ocorrer depois que todas as sete fases forem concluídas e validadas. Também foram definidos critérios de aceite, dependências e a ordem futura de implementação.

Nesta interação também foram incorporadas ao histórico as duas conversas anteriores que ocorreram em modo Plan. Nenhum código da aplicação foi alterado e nenhum artefato do projeto foi executado.

---

## Interação 006 — 30 de setembro de 2026

**Modo: Act**

### Usuário

> Excelente. Vamos seguir com os sete marcos de segurança, certo? Mas, quando acabar, não comece a parte das pendências funcionais, pois temos mais detalhes a tratar antes de começarmos essa parte, entendido?

### Cline (assistente)

Entendido. O escopo atual ficou limitado exclusivamente às sete fases de segurança.

O arquivo `TODO.md` foi atualizado para incluir uma barreira de aprovação manual e um ponto de parada obrigatório após a Fase 7. A conclusão dos sete marcos não desbloqueará, não iniciará e não autorizará automaticamente o TODO-001 ou o TODO-002.

Ao finalizar os sete marcos, o Cline deverá:

1. Apresentar o relatório consolidado dos resultados.
2. Confirmar que nenhuma pendência funcional foi iniciada.
3. Manter TODO-001 e TODO-002 no estado **Bloqueado**.
4. Parar a execução e aguardar os detalhes adicionais do usuário.

Qualquer planejamento ou implementação funcional posterior dependerá de uma nova autorização expressa e específica, registrada neste histórico. Nenhum código da aplicação foi alterado nesta interação.

---

## Interação 007 — 30 de setembro de 2026

**Modo: Act**

### Usuário

> Pode executar os sete passos de segurança.

### Cline (assistente)

Os sete marcos de segurança foram executados e documentados em `RELATORIO_SEGURANCA.md`.

As evidências originais foram preservadas fora do workspace, com manifesto de 192 arquivos e inventário de 122 binários. Todos os arquivos copiados foram validados individualmente por SHA-256, sem divergências. Os dois ZIPs originais encontrados também foram preservados e possuem o mesmo hash.

O Microsoft Defender verificou o workspace original, documento, banco SQLite, binários, ZIPs, cache NuGet limpo, artefatos reconstruídos e workspace final. Nenhuma ameaça foi detectada.

O workspace foi higienizado, com remoção dos artefatos recebidos de `.vs`, `bin`, `obj` e do arquivo `*.user`. Foram adicionados `.gitignore`, `NuGet.Config` restrito ao `nuget.org`, `global.json` e lock file de pacotes.

A auditoria inicial identificou uma vulnerabilidade de alta severidade na cadeia do SQLite e uma vulnerabilidade moderada no Swagger UI. Também confirmou que o .NET 6 estava fora de suporte. A infraestrutura foi migrada para .NET 10 LTS e os pacotes diretos foram atualizados para versões estáveis auditadas. Pacotes de teste sem fontes de teste correspondentes foram removidos do projeto web.

O restore bloqueado terminou com zero avisos e zero erros. A reconstrução limpa, feita em cache, intermediários e saída externos ao workspace, terminou com zero avisos e zero erros. O artefato `Questao5.dll` recebeu o SHA-256 `F2559193DD96F1DC1BD279A2F4225CD165DC8715E9C2A19F0E4FAAD784BB2DA6`.

A execução controlada utilizou somente `127.0.0.1`, sem navegador, com uma cópia descartável do banco. O endpoint `/WeatherForecast` respondeu HTTP 200. Não foram observadas conexões externas, processos inesperados, alterações de arquivos ou modificações no banco. Nenhum processo ou porta permaneceu ativo após o encerramento.

Ocorrências operacionais, limitações e riscos residuais foram registrados no relatório. Nenhuma pendência funcional foi iniciada. TODO-001 e TODO-002 permanecem no estado **Bloqueado**, e o ponto de parada obrigatório foi aplicado.

---

## Interação 008 — 30 de setembro de 2026

**Modo: Plan**

### Usuário

> Ótimo. Agora, primeiro gere uma mensagem de commit para isso que fizemos. Depois faça um resumo do arquivo `C:\git\exercicio05\Questao5\Questao5.docx`; nele contém o que realmente precisamos fazer.

### Cline (assistente)

Foi observado que o documento existente no projeto se chama `Questão 5.docx`, com espaço e acento. Seu conteúdo foi extraído diretamente do pacote Open XML, sem abrir o Microsoft Word nem executar conteúdo do documento.

Foi sugerida a seguinte mensagem de commit:

```text
chore(security): higieniza e valida o projeto recebido

- preserva hashes e evidências dos arquivos originais
- remove artefatos locais e binários recebidos
- adiciona configurações seguras para Git e NuGet
- restringe a restauração de pacotes ao nuget.org
- adiciona lock file para restauração reproduzível
- migra o projeto de .NET 6 para .NET 10 LTS
- atualiza dependências com vulnerabilidades conhecidas
- remove dependências de teste sem fontes correspondentes
- valida restore, build e execução controlada da API
- documenta os sete marcos de segurança e seus resultados
- registra pendências funcionais sem iniciar sua implementação
```

Como versão resumida, foi sugerido:

```text
chore(security): higieniza, atualiza e valida o projeto recebido
```

O documento define duas funcionalidades principais para a API bancária:

1. Movimentação de uma conta corrente.
2. Consulta do saldo de uma conta corrente.

Para movimentar uma conta, o serviço deverá receber a identificação da requisição, a identificação da conta corrente, um valor e o tipo `C` para crédito ou `D` para débito. Deverá validar conta cadastrada (`INVALID_ACCOUNT`), conta ativa (`INACTIVE_ACCOUNT`), valor positivo (`INVALID_VALUE`) e tipo permitido (`INVALID_TYPE`). Em caso de sucesso, deverá persistir o movimento, retornar HTTP 200 e devolver o ID gerado. Em caso de inconsistência, deverá retornar HTTP 400 com mensagem e tipo da falha.

A identificação da requisição deverá sustentar um comportamento idempotente: se o cliente repetir uma solicitação por ter perdido a resposta anterior, a operação não poderá gerar um segundo movimento. A estrutura fornecida para isso é a tabela `idempotencia`, com chave, requisição e resultado.

Para consultar o saldo, o serviço deverá receber a identificação da conta, validar sua existência e situação ativa e calcular:

```text
SALDO = SOMA DOS CRÉDITOS - SOMA DOS DÉBITOS
```

Quando não existirem movimentos, o saldo deverá ser `0.00`. A resposta HTTP 200 deverá conter número da conta, nome do titular, data e hora da consulta e saldo atual. Contas inexistentes ou inativas deverão produzir HTTP 400 com mensagem e tipo de falha.

O documento informa que as contas já estão cadastradas e que não faz parte do escopo criar um serviço de cadastro. Foram fornecidas três contas ativas, de números 123, 456 e 789, e três contas inativas, de números 741, 852 e 963.

Também são citados Dapper, CQRS, Mediator/MediatR, Swagger, testes unitários e NSubstitute. Seu uso não é obrigatório, mas conta pontos adicionais. Como Dapper, MediatR e Swagger já fazem parte da base recebida, foi recomendado manter coerência com essa estrutura.

O Swagger deverá documentar atributos, requisições, respostas possíveis e exemplos. Também foi destacada a necessidade de testes unitários e de integração, especialmente para persistência, validações, idempotência, repetição e concorrência.

Antes da implementação, foram identificadas decisões que ainda precisam ser alinhadas: rotas e verbos HTTP; formato dos DTOs e erros; identificador de conta usado nas entradas; formato e fuso das datas; precisão monetária; reutilização de chave idempotente com dados diferentes; transações; concorrência; organização dos testes; e profundidade da documentação Swagger.

Nenhum arquivo foi alterado durante essa interação em modo Plan.

---

## Interação 009 — 30 de setembro de 2026

**Modo: Act**

### Usuário

> Autorizou a mudança para o modo Act e a continuidade do plano aprovado.

### Cline (assistente)

A Interação 008 foi registrada neste histórico com a mensagem de commit sugerida, o resumo dos requisitos extraídos de `Questão 5.docx` e os pontos que ainda precisam ser detalhados antes da implementação.

Esta autorização também foi registrada como Interação 009. Nenhum commit foi criado nesta etapa, nenhum código funcional foi alterado e TODO-001 e TODO-002 permanecem bloqueados.

---

## Interação 010 — 30 de setembro de 2026

**Modo: Act**

### Usuário

> Informou a criação do branch `20260930` para as mudanças futuras e determinou que OWASP API Security Top 10, CWE Top 25, CIS Benchmarks, NIST SSDF, diretrizes OpenSSF e MITRE ATT&CK sejam utilizados como guias permanentes de desenvolvimento seguro, infraestrutura, deploy, cadeia NuGet, pipelines, logs e monitoramento.

### Cline (assistente)

Foi confirmado que o branch ativo é `20260930`, criado a partir do commit `a8fc7503b6fbaf02cfc9319dcf50917665c72582`, e que `master` não será utilizado diretamente para as próximas mudanças.

Foram verificadas as referências oficiais atuais em 30 de setembro de 2026: OWASP API Security Top 10 2023, CWE Top 25 2025, NIST SP 800-218 SSDF 1.1, CIS Benchmarks, diretrizes e ferramentas OpenSSF e MITRE ATT&CK v19.2. Também foi registrada a mudança introduzida no ATT&CK v18, que passou a priorizar Detection Strategies e Analytics e descontinuou Data Sources como objeto mantido.

Foi criado `DIRETRIZES_SEGURANCA.md` para transformar esses referenciais em guardrails verificáveis durante requisitos, implementação, testes, dependências, build, pipeline, deploy e operação. O documento deixa explícito que o uso dos guias não equivale automaticamente a certificação ou conformidade formal.

O `TODO.md` foi atualizado com gates contínuos de segurança. Esses gates deverão ser avaliados em cada mudança futura e incluem análise OWASP/CWE, SSDF, restore bloqueado, auditoria NuGet, segurança de pipeline, SBOM e proveniência quando aplicáveis, hardening CIS e telemetria orientada pelo MITRE ATT&CK.

Nenhuma funcionalidade bancária foi implementada nesta interação. TODO-001 e TODO-002 permanecem bloqueados até autorização específica para a etapa funcional.

---

## Interação 011 — 30 de setembro de 2026

**Modo: Plan**

### Usuário

> Solicitou a análise do código atual com base nos frameworks de segurança adotados e a identificação de itens não conformes antes da continuidade do desenvolvimento.

### Cline (assistente)

Foi realizada análise estática e somente leitura no branch `20260930`. Foram identificados achados relacionados a autenticação, autorização por conta, bootstrap parcial e não transacional, precisão monetária, integridade referencial, limites de recursos, tratamento de erros, nulabilidade, hosts, HTTPS, banco versionado, prevenção de segredos, logs, correlação, datas, endpoint residual, testes, gates de build, pipeline, hardening CIS e monitoramento MITRE ATT&CK.

Também foram registrados controles positivos existentes: fonte NuGet restrita, package source mapping, lock file, auditoria NuGet habilitada, SDK fixado, dependências com versões explícitas e Swagger limitado a Development.

A avaliação de CIS foi classificada como não conclusiva por ainda não existir infraestrutura de deploy. A revalidação de vulnerabilidades ficou pendente porque o workspace higienizado não contém `project.assets.json` e um restore produziria arquivos, ação incompatível com o modo Plan. Nenhum arquivo foi alterado durante a análise.

---

## Interação 012 — 30 de setembro de 2026

**Modo: Act**

### Usuário

> Solicitou que os achados da análise fossem adicionados ao `TODO.md`.

### Cline (assistente)

Os achados foram adicionados ao `TODO.md` como backlog SEC-001 a SEC-023. Cada item contém estado, prioridade, referenciais, evidência e critérios de aceite. Também foram registrados controles positivos e limitações que exigem restore controlado, execução controlada ou definição futura de infraestrutura.

Nenhum código funcional foi alterado, nenhum item bloqueado foi iniciado e o staging preexistente foi preservado.

---

## Interação 013 — 30 de setembro de 2026

**Modo: Act**

### Usuário

> Autorizou a execução da primeira entrega de segurança: formalização de SEC-001/SEC-002 como riscos aceitos, fundação de SEC-019 e implementação de SEC-010.

### Cline (assistente)

A branch `20260930` foi confirmada antes das alterações. O staging preexistente foi preservado e continuou contendo somente `CONVERSAS.md`.

SEC-001 e SEC-002 foram formalizados como riscos aceitos exclusivamente para este exercício. Foi registrado que existência ou situação ativa da conta não equivale a autenticação ou autorização, que os endpoints permanecem anônimos e que essa decisão não pode ser reutilizada em produção sem nova avaliação formal. TLS, rate limiting, limites de entrada, respostas seguras, correlação e logs permanecem como controles compensatórios obrigatórios.

Foi criado `Questao5.Tests`, adicionado à solução e configurado para `net10.0`, nullable, lock file e auditoria NuGet. A stack inicialmente planejada com xUnit v2, Microsoft.NET.Test.Sdk e coverlet foi reavaliada durante a auditoria: xUnit v2 foi reportado como legado, e `coverlet.collector` é incompatível com Microsoft Testing Platform v2 por depender de VSTest. A fundação final usa xUnit v3 `4.0.1`, Microsoft Testing Platform e NSubstitute `6.2.0`; a escolha de cobertura compatível com MTP ficou pendente.

Foi criada infraestrutura SQLite temporária com diretório exclusivo por teste, `Foreign Keys=True`, verificação explícita de `PRAGMA foreign_keys` e limpeza ao final. Os testes nunca referenciam `Questao5/database.sqlite`.

SEC-010 foi concluído: `DatabaseConfig` passou a validar `DatabaseName` por construção, as supressões de nulabilidade foram removidas e `IDatabaseBootstrap` passou a ser resolvido com `GetRequiredService`. Foram adicionados testes para configuração válida, nula, vazia e composta por espaços.

O `global.json` passou a selecionar `Microsoft.Testing.Platform` como runner nativo do `dotnet test` no SDK .NET 10. O gate final executou restore bloqueado, auditoria direta e transitiva, build Release e testes. Não foram reportadas vulnerabilidades conhecidas; o build terminou com zero warnings e zero erros; os seis testes passaram. `MediatR.Extensions.Microsoft.DependencyInjection` 11.1.0 foi identificado como pacote legado preexistente e registrado para migração separada.

O hash SHA-256 de `Questao5/database.sqlite` permaneceu `E355A3EDB7D73E7784A7467513248D530C27CC989C624A35D13A188B2AA8DD3C` antes e depois das validações. Nenhum item funcional bloqueado foi iniciado.

---

## Interação 014 — 30 de setembro de 2026

**Modo: Act**

### Usuário

> Solicitou a execução, na ordem sugerida, dos itens de segurança sem pendências bloqueadoras, deixando as alterações prontas para um commit posterior.

### Cline (assistente)

Foram implementados os controles independentes de SEC-020, SEC-013, SEC-014, SEC-016 e SEC-009, ampliada a fundação de SEC-019 e executadas as partes não bloqueadas de SEC-006 e SEC-017. SEC-007 não foi iniciado porque a uniformização da chave estrangeira exige reconstrução/migração de esquema e permanece vinculada às autorizações de SEC-003 e SEC-004.

O build passou a usar `Directory.Build.props` com analisadores .NET 10 no nível recomendado, code style em build, warnings como erros, análise de código como erro, determinismo e `ContinuousIntegrationBuild` quando `CI=true`. A única supressão configurada é `CA1707`, limitada aos testes e justificada pela convenção legível `Metodo_Cenario_Resultado`.

Foi criado `scripts/Invoke-SecurityGate.ps1`, que executa restore bloqueado, auditoria NuGet direta e transitiva, scanner local de segredos, `dotnet format --verify-no-changes`, build Release em modo CI, testes e cobertura. O scanner cobre arquivos versionados e candidatos ao commit. O gate completo passou sem vulnerabilidades conhecidas ou possíveis segredos, com zero warnings, zero erros e dezesseis testes aprovados.

O banco operacional padrão foi movido para `.data/database.sqlite`, ignorado pelo Git, enquanto `Questao5/database.sqlite` foi preservado como fixture de referência. O SHA-256 esperado passou a ser validado automaticamente e permaneceu `E355A3EDB7D73E7784A7467513248D530C27CC989C624A35D13A188B2AA8DD3C`. `DatabaseConfig` força `Foreign Keys=True`; `SqliteConnectionFactory` centraliza a abertura, cria o diretório necessário e falha se `PRAGMA foreign_keys` não estiver ativo. O bootstrap usa essa factory, e um teste no schema real confirma a rejeição de movimento órfão pelo SQLite. A validação equivalente na camada de aplicação permanece dependente do endpoint de movimentação.

SEC-009 foi concluído com `GlobalExceptionHandler`, `ProblemDetails` uniforme e tratamento específico de `BusinessRuleException`. Exceções inesperadas retornam mensagem genérica e não expõem stack trace, caminho, SQL, headers, connection string ou mensagem interna. O log estruturado de evento 9000 registra somente correlation ID e tipo da exceção; o teste confirma a ausência dos valores sensíveis usados no cenário adversarial.

SEC-016 foi concluído com middleware de correlação. Toda resposta recebe `X-Correlation-ID`; IDs externos são aceitos somente quando há um único valor de até 64 caracteres na allowlist ASCII de letras, números, ponto, hífen e sublinhado. Valores ausentes, múltiplos ou inválidos são substituídos, `TraceIdentifier` e `Activity` são atualizados, e o ID aparece em respostas normais, validações, erros de negócio, exceções e logs. A chave de idempotência permanece semanticamente separada.

A suíte passou a usar `Microsoft.AspNetCore.Mvc.Testing 10.0.12` e `coverlet.MTP 10.1.0`. A coleta em formato Cobertura foi validada com Microsoft Testing Platform; a aplicação `Questao5` atingiu 88,72% de linhas e 73,07% de branches nesta etapa. Permanecem pendentes os testes de abuso, idempotência, concorrência e rollback, pois dependem dos endpoints bancários ainda não autorizados.

O uso residual de `DateTime.Now` foi substituído por UTC, mas SEC-017 permanece em andamento porque formato, fuso e testes de transição da consulta de saldo dependem do contrato bancário. SEC-013, SEC-014 e SEC-020 também permanecem em andamento apenas nos critérios que dependem de backup/permissões de deploy ou execução automática em CI. Nenhuma funcionalidade bancária, alteração de representação monetária ou reconstrução de schema foi iniciada. O staging preexistente continuou preservado com somente `CONVERSAS.md` staged.

---