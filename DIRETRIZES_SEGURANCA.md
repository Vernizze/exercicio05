# Diretrizes permanentes de desenvolvimento seguro

## Finalidade

Este documento estabelece os guardrails de segurança que devem acompanhar o projeto durante todo o ciclo de desenvolvimento, revisão, teste, empacotamento, integração contínua, implantação e operação.

As referências abaixo serão usadas como guias de engenharia e critérios de revisão. Sua adoção não representa, por si só, certificação, conformidade formal ou atendimento integral a todos os controles de cada framework. Controles não aplicáveis deverão ser marcados como tal, com justificativa e risco residual documentados.

## Branch de trabalho

- Branch autorizado para as próximas mudanças: `20260930`.
- Antes de qualquer alteração, confirmar o branch ativo e o estado do repositório.
- Não desenvolver diretamente em `master`.
- Mudanças deverão ser pequenas, revisáveis, testáveis e rastreáveis.

## Autenticação e autorização neste desafio

Até 1º de outubro de 2026 vigorou uma exceção de escopo segundo a qual o exercício não teria autenticação nem autorização por titular. Essa exceção foi **revogada** nessa data por decisão do responsável pelo projeto, e a autenticação e a autorização por titular foram implementadas no mesmo dia pelas entregas F1, F2 e E.

O desenho vigente está em `ESPECIFICACAO_AUTENTICACAO.md`:

- todo endpoint bancário exige JWT válido; a API apenas valida tokens de um emissor externo configurável;
- a conta só pode ser movimentada ou consultada pelo correntista registrado como seu titular, identificado pela claim `sub`;
- validação de existência e situação da conta não substitui autenticação ou autorização;
- respostas devem reduzir enumeração: conta de outro correntista recebe a mesma resposta de conta não cadastrada;
- TLS, rate limiting, limites de payload, idempotência, logs e correlação permanecem controles obrigatórios.

Limitação aceita exclusivamente para a avaliação técnica: o emissor usado no ambiente do desafio é uma ferramenta de teste que não autentica pessoas. No ambiente Docker Compose, suas portas e as da API são publicadas somente em `127.0.0.1`, e a busca de chaves sem TLS é restrita à rede interna do Compose. Ele não poderá ser usado em ambiente real, e a adoção de um provedor real deverá passar por nova análise de risco. A existência de autenticação neste desafio não caracteriza, por si só, conformidade com OWASP API1, API2 ou API5, nem com CWE-306, CWE-639 ou CWE-862.

## Referenciais adotados

### 1. OWASP API Security Top 10 — edição 2023

Aplicar análise explícita dos dez riscos às funcionalidades e endpoints:

- API1 — Broken Object Level Authorization;
- API2 — Broken Authentication;
- API3 — Broken Object Property Level Authorization;
- API4 — Unrestricted Resource Consumption;
- API5 — Broken Function Level Authorization;
- API6 — Unrestricted Access to Sensitive Business Flows;
- API7 — Server Side Request Forgery;
- API8 — Security Misconfiguration;
- API9 — Improper Inventory Management;
- API10 — Unsafe Consumption of APIs.

Controles mínimos para esta API:

- validar autorização no servidor para todo recurso identificado pelo cliente;
- não confiar em IDs, tipos, valores ou propriedades enviados pelo cliente;
- usar DTOs explícitos e impedir overposting/mass assignment;
- estabelecer limites de tamanho, tempo, concorrência e frequência das requisições;
- documentar e inventariar endpoints, versões, contratos e respostas;
- desabilitar comportamentos e recursos não necessários;
- evitar exposição de stack traces, detalhes internos e dados sensíveis;
- tratar idempotência como proteção do fluxo de negócio, sem permitir duplicidade ou conflito silencioso;
- validar de forma defensiva qualquer integração externa que venha a ser adicionada.

### 2. CWE Top 25 — edição 2025

Toda mudança deverá ser revisada contra as classes de fraqueza aplicáveis, com atenção especial a:

- validação imprópria de entrada;
- injeção SQL, de comandos ou de logs;
- autenticação e autorização ausentes ou incorretas;
- autorização baseada em identificador controlado pelo usuário;
- exposição de informações sensíveis;
- tratamento inadequado de erros e condições excepcionais;
- consumo de recursos sem limites;
- condições de corrida e falhas de atomicidade;
- uso de componentes vulneráveis ou não confiáveis;
- geração ou armazenamento inadequado de credenciais, chaves e segredos.

No contexto .NET/Dapper/SQLite:

- comandos SQL deverão ser parametrizados;
- valores monetários deverão usar representação e regras de arredondamento definidas, nunca aritmética binária de ponto flutuante sem decisão explícita;
- operações compostas deverão usar transações e tratamento correto de concorrência;
- mensagens externas deverão ser seguras e mensagens internas deverão preservar diagnóstico sem vazar segredos;
- analisadores do compilador e avisos relevantes não deverão ser silenciados sem justificativa.

### 3. CIS Benchmarks

Quando infraestrutura, contêiner, sistema operacional, servidor, serviço de CI/CD ou ambiente de implantação forem definidos, deverão ser identificados os CIS Benchmarks específicos e suas versões.

Diretrizes mínimas:

- adotar inicialmente recomendações Level 1 compatíveis com o ambiente;
- avaliar Level 2 conforme criticidade e impacto operacional;
- aplicar hardening primeiro em ambiente de teste;
- registrar exceções, justificativas, impacto e risco residual;
- manter configurações como código, versionadas e revisáveis;
- executar com menor privilégio, superfície mínima, serviços mínimos e portas estritamente necessárias;
- proteger segredos fora do código, da imagem e dos logs;
- definir TLS, headers, permissões, identidade, rede, armazenamento, backup e retenção de logs antes de produção;
- reavaliar o benchmark quando a plataforma ou sua versão mudar.

### 4. NIST SP 800-218 — SSDF 1.1

As atividades serão organizadas nas quatro práticas do SSDF:

- **PO — Prepare the Organization:** requisitos de segurança, responsabilidades, ferramentas, critérios e treinamento;
- **PS — Protect the Software:** proteção do código, credenciais, ambientes, artefatos e acessos;
- **PW — Produce Well-Secured Software:** threat modeling, design seguro, revisão, análise, testes e configuração segura;
- **RV — Respond to Vulnerabilities:** identificação, triagem, correção, divulgação, análise de causa raiz e prevenção de recorrência.

Cada entrega relevante deverá produzir evidências proporcionais ao risco: revisão, testes, auditorias, decisões, hashes, relatórios ou resultados automatizados.

### 5. OpenSSF — dependências NuGet e pipelines

Para dependências:

- manter fontes NuGet explicitamente autorizadas;
- manter lock file e usar restauração bloqueada em validações e pipeline;
- executar auditoria de vulnerabilidades em dependências diretas e transitivas;
- avaliar manutenção, procedência, licença, popularidade, histórico de incidentes e postura do projeto antes de adicionar pacote;
- minimizar dependências e remover pacotes sem uso;
- impedir versões flutuantes e alterações silenciosas de resolução;
- gerar e preservar SBOM quando houver processo de build/release;
- avaliar OpenSSF Scorecard e sinais equivalentes para dependências críticas quando aplicável.

Para configuração e dados locais:

- manter segredos fora de `appsettings*.json` versionados; usar providers externos do ASP.NET Core apropriados ao ambiente;
- manter bancos operacionais em diretórios ignorados, separados de fixtures versionadas;
- tratar `Questao5/database.sqlite` como fixture de referência imutável, verificada pelo SHA-256 `E355A3EDB7D73E7784A7467513248D530C27CC989C624A35D13A188B2AA8DD3C`;
- usar `.data/database.sqlite` somente para desenvolvimento local; backup, permissões e armazenamento de produção deverão ser definidos com a infraestrutura;
- nunca reutilizar a fixture versionada para dados operacionais.

Para repositório e CI/CD:

- exigir revisão de mudanças e proteção do branch principal quando a plataforma permitir;
- aplicar menor privilégio a tokens, identidades e runners;
- fixar ações, ferramentas e imagens por versão imutável ou digest quando possível;
- separar build, teste, publicação e implantação;
- evitar execução de código não confiável com segredos disponíveis;
- proteger caches e artefatos contra envenenamento e substituição;
- gerar hashes e, quando houver publicação, assinatura e proveniência verificável alinhada a SLSA/Sigstore;
- registrar quem produziu o artefato, a partir de qual commit, dependências e ambiente;
- impedir publicação a partir de workspace sujo ou build não reproduzível;
- manter trilha de auditoria e política de resposta a dependências comprometidas.

### 6. MITRE ATT&CK v19.2 — logs, detecção e monitoramento

O ATT&CK será utilizado para orientar cenários de ameaça, telemetria e validação defensiva. A versão corrente verificada em 30 de setembro de 2026 é a v19.2. A partir do ATT&CK v18, deverão ser priorizados **Detection Strategies**, **Analytics** e componentes de dados atuais; referências antigas a Data Sources serão tratadas apenas como material legado.

Os logs gerados deverão, quando aplicável:

- ser estruturados, pesquisáveis e correlacionáveis;
- conter timestamp com fuso/UTC, nível, evento, componente, resultado e identificador de correlação;
- registrar falhas de validação, autenticação, autorização, rate limiting, idempotência, acesso administrativo, alteração de configuração e falhas de dependências;
- permitir distinguir primeira execução, repetição idempotente e conflito de chave;
- evitar senhas, tokens, connection strings, payloads bancários completos e outros dados sensíveis;
- resistir a log forging por normalização/estruturação dos campos controlados pelo usuário;
- possuir retenção, controle de acesso e proteção de integridade definidos no ambiente de implantação;
- apoiar alertas sobre padrões anômalos, abuso de fluxo, enumeração, repetição, falhas em massa e tentativa de adulteração;
- ser testados para confirmar que os eventos esperados são emitidos e úteis à investigação.

## Gates obrigatórios para toda mudança futura

### Antes da implementação

- confirmar branch e workspace;
- definir requisitos funcionais e de segurança;
- identificar dados, ativos, fronteiras de confiança e ameaças;
- mapear riscos OWASP API e CWEs aplicáveis;
- avaliar necessidade de nova dependência;
- definir requisitos de log e monitoramento sem dados sensíveis.

### Durante a implementação

- aplicar validação por allowlist e DTOs mínimos;
- parametrizar acesso a dados;
- aplicar menor privilégio e defaults seguros;
- preservar atomicidade, idempotência e concorrência segura;
- não registrar segredos ou dados excessivos;
- criar testes positivos, negativos, de abuso e de regressão.

### Antes de concluir

- revisar diff e arquivos gerados;
- executar restore bloqueado, auditoria NuGet, build limpo e testes;
- revisar riscos OWASP API/CWE e atualizar a matriz de ameaças;
- validar logs relevantes e ausência de informação sensível;
- validar configurações de infraestrutura contra o CIS Benchmark aplicável, quando houver infraestrutura;
- gerar ou atualizar SBOM, hashes, assinatura e proveniência quando houver artefato de release;
- documentar exceções e riscos residuais;
- registrar evidências e resultado em `CONVERSAS.md` e documentos técnicos pertinentes.

O gate local reproduzível deve ser executado a partir da raiz do repositório com:

```powershell
powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File .\scripts\Invoke-SecurityGate.ps1
```

O `Bypass` vale somente para o processo criado e não altera a política de execução da máquina. O script executa restore bloqueado, auditoria NuGet direta e transitiva, scanner local de segredos, verificação de formatação/analisadores, build Release determinístico em modo CI, testes e cobertura compatível com Microsoft Testing Platform.

## Critério de conclusão

Uma tarefa não será considerada concluída apenas porque compila ou atende ao fluxo feliz. Ela deverá possuir validação proporcional de segurança, testes, rastreabilidade e documentação. Falhas críticas ou altas conhecidas, adulteração não esclarecida, restauração não reproduzível, segredo exposto ou ausência de controle essencial impedem a aprovação da entrega.

## Referências oficiais verificadas em 30 de setembro de 2026

- OWASP API Security Top 10 — edição 2023;
- CWE Top 25 Most Dangerous Software Weaknesses — edição 2025;
- CIS Benchmarks e CIS Software Supply Chain Security Benchmarks;
- NIST SP 800-218, SSDF versão 1.1;
- OpenSSF Guides, Scorecard, Principles for Package Repository Security, SLSA e Sigstore;
- MITRE ATT&CK v19.2, considerando a mudança introduzida na v18 para Detection Strategies e Analytics.