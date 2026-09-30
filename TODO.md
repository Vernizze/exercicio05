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

- **Estado:** Bloqueado
- **Prioridade:** Alta
- **Bloqueadores:** conclusão e validação das fases 1 a 7; discussão dos detalhes adicionais; autorização expressa do usuário
- **Arquivo identificado:** `Questao5/Infrastructure/Sqlite/DatabaseBootstrap.cs`

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

- [ ] As três tabelas esperadas são verificadas individualmente.
- [ ] Um banco vazio recebe todo o esquema e os dados iniciais esperados.
- [ ] Um banco com esquema parcial recebe somente os elementos ausentes.
- [ ] Um banco com esquema completo não recebe alterações destrutivas.
- [ ] O bootstrap pode ser executado repetidas vezes sem duplicar dados.
- [ ] A criação ou atualização do esquema é atômica.
- [ ] Existem testes para banco vazio, parcial e completo.

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

## Procedimento após a conclusão dos sete marcos

1. Confirmar documentalmente a conclusão das sete fases de segurança.
2. Apresentar ao usuário o relatório consolidado, os riscos residuais e as recomendações.
3. Confirmar que nenhum código funcional foi alterado como consequência automática da conclusão dos marcos.
4. Manter TODO-001 e TODO-002 como **Bloqueado**.
5. Interromper a execução e aguardar os detalhes adicionais do usuário.
6. Somente após uma nova autorização expressa, elaborar um plano específico para as pendências funcionais.

Não existe, neste documento, autorização antecipada para alterar o estado das pendências funcionais ou implementá-las.

## Histórico de atualização deste TODO

- **30 de setembro de 2026:** documento criado após análise estática Zero Trust. TODO-001 e TODO-002 registrados como bloqueados até a conclusão das sete fases de segurança.
- **30 de setembro de 2026:** acrescentada barreira de aprovação manual. A conclusão das sete fases não desbloqueia automaticamente TODO-001 ou TODO-002; após a Fase 7, o trabalho deve parar e aguardar detalhes adicionais e autorização expressa do usuário.
- **30 de setembro de 2026:** sete marcos de segurança concluídos. Relatório consolidado criado em `RELATORIO_SEGURANCA.md`. TODO-001 e TODO-002 permanecem bloqueados.