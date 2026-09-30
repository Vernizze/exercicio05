# Relatório dos sete marcos de segurança

**Data da execução:** 30 de setembro de 2026  
**Abordagem:** Zero Trust  
**Escopo:** material recebido, dependências, reconstrução e primeira execução controlada  
**Resultado geral:** concluído com riscos residuais documentados

## Resumo executivo

Os sete marcos de segurança foram executados antes do início de qualquer implementação funcional.

Não foram encontrados indicadores de código malicioso nos fontes, documentos, banco, binários recebidos, pacotes restaurados ou artefatos reconstruídos. O Microsoft Defender não detectou ameaças em nenhum dos alvos verificados.

A cadeia de dependências recebida não foi considerada segura para execução sem alterações. A auditoria inicial encontrou duas vulnerabilidades conhecidas e confirmou que o projeto utilizava o .NET 6, fora de suporte desde 12 de novembro de 2024. A infraestrutura foi atualizada para .NET 10 LTS e versões estáveis auditadas das dependências, sem alteração das regras funcionais da aplicação.

A aplicação foi reconstruída a partir dos fontes em diretórios isolados, executada somente em interface local e observada durante uma requisição HTTP controlada. Não foram identificados processos, conexões ou alterações de arquivos inesperados.

Conforme decisão registrada, a conclusão destes marcos **não autoriza o início das pendências funcionais**. TODO-001 e TODO-002 permanecem bloqueados.

---

## Fase 1 — Registro da análise

**Estado:** concluída

- A solicitação Zero Trust, o planejamento, os procedimentos e os resultados foram registrados em `CONVERSAS.md`.
- As inspeções estáticas foram diferenciadas das etapas posteriores que envolveram restore, build e execução.
- Antes da execução controlada, os fontes, documentos, banco, binários e cadeia de build foram revisados estaticamente.

---

## Fase 2 — Preservação de evidências

**Estado:** concluída

As evidências foram preservadas fora do workspace em:

`c:\git\exercicio05_evidencias_20260930`

Foram gerados:

- Cópia do workspace recebido, excluindo apenas os metadados internos do Git;
- Manifesto de 192 arquivos com tamanho, horário e SHA-256;
- Inventário de 122 binários recebidos;
- Cópias dos dois ZIPs encontrados no diretório de Downloads;
- Hashes dos ZIPs originais;
- Logs de restore, auditoria, build, varredura e execução controlada.

Os 192 arquivos copiados foram comparados individualmente por SHA-256 com a origem, com zero divergências.

Os dois ZIPs encontrados são idênticos e possuem o SHA-256:

`108B010B356EEA66DD2141D449C38134E9745D8741A8A6A4C570DB6C687FDCC1`

### Limitação registrada

A primeira tentativa de cópia tentou preservar ACLs NTFS e recebeu acesso negado porque a sessão não estava elevada. A cópia foi refeita preservando dados, atributos e timestamps. A integridade foi validada posteriormente por hash individual de todos os arquivos.

---

## Fase 3 — Higienização do workspace

**Estado:** concluída

Foram criados ou configurados:

- `.gitignore` para Visual Studio, .NET, testes, temporários e artefatos locais;
- `NuGet.Config` com limpeza das fontes herdadas e autorização exclusiva do `nuget.org`;
- `global.json` fixando o SDK `10.0.401`;
- Exclusão defensiva explícita de `bin` e `obj` locais no projeto.

Foram removidos do workspace confiável:

- `.vs` recebido;
- `bin` recebido;
- `obj` recebido;
- `Questao5.csproj.user`;
- marcas `Zone.Identifier` da cópia de trabalho.

As marcas da Web e todos os artefatos originais continuam preservados na pasta externa de evidências.

### Observação sobre a IDE

O C# Dev Kit do VS Code recriou automaticamente um `obj` local contendo somente arquivos de avaliação de design-time. Esses arquivos:

- possuem horário posterior à higienização;
- não são os artefatos recebidos;
- estão ignorados pelo Git;
- foram explicitamente excluídos da compilação;
- não participaram do restore ou build validados.

---

## Fase 4 — Verificação antimalware

**Estado:** concluída

### Ferramenta

- Microsoft Defender Antivirus;
- Motor: `1.1.26080.3`;
- Assinaturas: `1.459.486.0`;
- Última atualização registrada: 30 de setembro de 2026 às 06:18:51;
- Proteção em tempo real: ativa;
- Remediação desabilitada nas varreduras de evidência.

### Alvos verificados

- Workspace original preservado;
- Documento `Questão 5.docx`;
- Banco `database.sqlite`;
- Diretórios recebidos `bin` e `obj`;
- Dois ZIPs originais;
- Cache NuGet limpo utilizado na reconstrução;
- Artefatos da reconstrução limpa;
- Workspace higienizado final.

### Resultado

Nenhuma ameaça foi detectada em qualquer alvo.

---

## Fase 5 — Proteção da cadeia de dependências

**Estado:** concluída

### Situação recebida

O projeto utilizava:

- `net6.0`, fora de suporte desde 12 de novembro de 2024;
- `Microsoft.Data.Sqlite 3.1.6`;
- `Swashbuckle.AspNetCore 6.2.3`;
- `Dapper 2.0.35`;
- `MediatR 11.0.0`;
- Pacotes de teste diretamente no projeto web, apesar de não existirem fontes de teste.

A auditoria inicial encontrou:

- Vulnerabilidade de alta severidade em `SQLitePCLRaw.lib.e_sqlite3 2.0.2`;
- Vulnerabilidade moderada em `Swashbuckle.AspNetCore.SwaggerUI 6.2.3`.

### Medidas aplicadas

- Migração de infraestrutura para `net10.0` LTS;
- `Swashbuckle.AspNetCore 10.2.3`;
- `Dapper 2.1.89`;
- `Microsoft.Data.Sqlite 10.0.12`;
- `MediatR 11.1.0`;
- `MediatR.Extensions.Microsoft.DependencyInjection 11.1.0`;
- Remoção dos pacotes de teste do projeto web;
- Geração de `packages.lock.json`;
- Restore em modo bloqueado;
- Auditoria NuGet habilitada para dependências diretas e transitivas;
- Uso exclusivo do `nuget.org`.

### Resultado final

- Restore bloqueado: sucesso;
- Avisos de auditoria `NU19xx`: zero;
- Erros: zero;
- Dependência SQLite nativa resolvida em `2.1.12`, fora da faixa vulnerável reportada pela auditoria inicial;
- Swagger UI resolvido em `10.2.3`, acima da versão mínima corrigida.

---

## Fase 6 — Reconstrução limpa

**Estado:** concluída

Foram usados diretórios externos ao workspace:

- Cache NuGet: `c:\git\exercicio05_seguranca_runtime\nuget-packages`;
- Intermediários: `c:\git\exercicio05_seguranca_runtime\obj`;
- Saída: `c:\git\exercicio05_seguranca_runtime\bin`.

O build foi executado a partir dos fontes higienizados, sem reutilizar `bin` ou `obj` recebidos.

### Resultado

- Configuração: Release;
- Framework: `net10.0`;
- Build: sucesso;
- Avisos: zero;
- Erros: zero;
- Artefato principal: `Questao5.dll`;
- SHA-256 do artefato principal:

`F2559193DD96F1DC1BD279A2F4225CD165DC8715E9C2A19F0E4FAAD784BB2DA6`

### Ocorrências operacionais registradas

- Uma checagem de artefato foi disparada em paralelo e terminou antes do build, produzindo um falso negativo de sincronização; a checagem sequencial confirmou o artefato.
- A primeira tentativa de build isolado incluiu arquivos de design-time recriados pela IDE e gerou atributos duplicados. Foi adicionada exclusão defensiva explícita de `bin`/`obj`, e a repetição do build terminou com sucesso.

---

## Fase 7 — Execução controlada

**Estado:** concluída

A aplicação foi copiada para uma área descartável e recebeu uma cópia do banco original. O banco do workspace não foi utilizado para escrita.

### Restrições e observações

- Ambiente: Production;
- URL: `http://127.0.0.1:51987`;
- Navegador: não iniciado;
- Interface: somente loopback local;
- Endpoint consultado: `/WeatherForecast`;
- Resposta: HTTP 200;
- Processos filhos: somente `conhost.exe`;
- Conexões observadas: uma porta local em escuta e a conexão local do próprio teste;
- Conexões externas: nenhuma observada;
- Saída de erro: vazia;
- Processo e porta remanescentes após encerramento: nenhum.

O log apresentou somente um aviso de redirecionamento HTTPS, porque a execução controlada foi propositalmente vinculada apenas a HTTP local e não recebeu uma porta HTTPS.

### Integridade de arquivos e banco

- Arquivos antes: 43;
- Arquivos depois: 43;
- Arquivos criados, removidos ou modificados: zero;
- Hash do banco temporário antes e depois:

`E355A3EDB7D73E7784A7467513248D530C27CC989C624A35D13A188B2AA8DD3C`

- Banco temporário alterado: não;
- Banco original do workspace alterado: não.

---

## Riscos residuais e observações

1. Uma varredura antimalware sem detecções reduz risco, mas não constitui prova matemática de ausência de malware.
2. O repositório recebido não possui histórico local de commits que permita comprovar procedência.
3. O C# Dev Kit pode recriar diretórios ignorados de design-time enquanto o projeto estiver aberto.
4. A aplicação ainda contém a falha lógica do bootstrap e não possui idempotência funcional; esses itens estão documentados e permanecem bloqueados.
5. Não existe suíte de testes recebida. Os antigos pacotes de teste estavam no projeto web sem fontes correspondentes.
6. A migração de plataforma e pacotes foi validada por restore, build e execução do endpoint existente, mas futuras funcionalidades deverão receber testes próprios.

---

## Ponto de parada obrigatório

Os sete marcos de segurança estão concluídos.

Nenhuma pendência funcional foi iniciada. Permanecem bloqueados:

- TODO-001 — correção da validação do bootstrap;
- TODO-002 — implementação de idempotência.

O próximo passo depende da apresentação e discussão dos detalhes adicionais pelo usuário e de uma nova autorização expressa.