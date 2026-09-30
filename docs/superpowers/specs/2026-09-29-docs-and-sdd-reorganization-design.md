# Design: Reorganização da Documentação, Specs Normativas e SDD

- **Data:** 2026-09-29
- **Status:** Aprovado
- **Escopo:** Reestruturação de arquivos Markdown de configuração de agentes, especificações normativas e framework SDD no repositório Plemu.

---

## 1. Contexto e Motivação

O repositório Plemu acumulou arquivos Markdown na raiz (`AGENTS.md`, `PLAN.md`, `Plemu.md`, `README.md`) e duas pastas com papéis sobrepostos ou ambíguos (`spec/` contendo duas especificações normativas e `docs/superpowers/` contendo o framework de SDD).

Além disso:
- Há redundância entre o `AGENTS.md`, o `README.md` e o `Plemu.md` sobre a tese dos quatro degraus ("four rungs"), tech stack e modelo de negócios.
- O Gemini CLI busca nativamente por `GEMINI.md` no workspace, enquanto as instruções estavam apenas em `AGENTS.md`.
- `PLAN.md` na raiz continha mais de 3.000 linhas de plano de expansão, fora da estrutura de planos do framework Superpowers SDD (`docs/superpowers/plans/`).
- `Plemu.md` (visão de produto em português) estava solto na raiz, competindo com `README.md`.

---

## 2. Nova Arquitetura de Documentação

### 2.1 Estrutura de Diretórios Alvo

```text
plemu/
├── README.md                                    # Portal público (EN): visão técnica geral, 4 rungs, links oficiais
├── AGENTS.md                                    # Instruções canônicas para agentes: invariantes, regras, comandos de dev
├── GEMINI.md                                    # Wrapper nativo Gemini CLI importando AGENTS.md
│
├── docs/
│   ├── vision-pt.md                             # Antigo Plemu.md (visão de produto, pitch, marketplace e IA em PT-BR)
│   ├── specs/                                   # ESPECIFICAÇÕES NORMATIVAS DO PROTOCOLO (RFCs / contratos congelados)
│   │   ├── SPEC.md                              # Content Trust SPEC v1 (FROZEN 2026-07-12)
│   │   └── SPEC-ERRATA.md                       # Erratas oficiais e extensões de implementação
│   │
│   ├── superpowers/                             # FRAMEWORK DE SDD (Superpowers)
│   │   ├── specs/                               # Design specs geradas em tarefas/features
│   │   │   └── 2026-09-29-docs-and-sdd-reorganization-design.md
│   │   └── plans/                               # Planos de implementação executáveis
│   │       ├── platform-expansion.md            # Antigo PLAN.md (plano macro de 43 tarefas)
│   │       └── 2026-09-28-fix-github-actions-workflows.md
│   │
│   ├── runbooks/                                # Runbooks operacionais (já existentes)
│   └── ...                                      # Documentos de apoio (upload-process, ci-and-testing, etc.)
```

---

## 3. Matriz de Responsabilidades & Eliminação de Redundâncias

| Arquivo | Público-Alvo / Finalidade | O que contém | O que NÃO deve conter (removido para evitar redundância) |
|---|---|---|---|
| `README.md` | Desenvolvedores / Colaboradores humanos | Pitch resumido, tabela dos 4 rungs, links de navegação para `docs/`, comandos de início rápido. | Detalhes exaustivos de 43 tarefas de implementação (aponta para `docs/superpowers/plans/platform-expansion.md`). |
| `AGENTS.md` | Agentes de IA autônomos e CLI agents | Invariantes inquebráveis, restrições do compilador, comandos de build/test/lint, convenções de git e erros. | Pitch de vendas, teses de captação de investimento ou textos de marketing. |
| `GEMINI.md` | Gemini CLI runtime | Configuração direta do assistente com importação do `AGENTS.md`. | Duplicação de comandos já presentes no `AGENTS.md`. |
| `docs/vision-pt.md` | Fundadores, Investidores e Comunidade PT | Visão de produto, tese da escada de distribuição, economia dos packs, marketplace e roadmap de IA. | Guias operacionais de CI ou detalhes de linhas de comando de build. |
| `docs/specs/` | Implementadores de clientes e verificadores | Especificações normativas imutáveis do protocolo e formatos de arquivo (`SPEC.md`, `SPEC-ERRATA.md`). | Planos de tarefas ou histórico temporário de desenvolvimento. |
| `docs/superpowers/` | Agentes e devs executando SDD | Design docs (`specs/`) e planos de implementação passo-a-passo (`plans/`). | Especificações de protocolo que devem durar além da conclusão de uma tarefa. |

---

## 4. Atualizações de Scripts e Dependências de Caminhos

Ao mover `spec/` para `docs/specs/`:
1. `tools/scripts/rotate-dev-keys.sh`:
   - Atualizar a referência de `spec/SPEC.md` para `docs/specs/SPEC.md`.
   - Atualizar `ERRATA="${ROOT}/docs/specs/SPEC-ERRATA.md"`.
2. `tools/scripts/rotate_replace.py`:
   - Atualizar `frozen = os.path.join(root, "docs", "specs", "SPEC.md")`.
3. Links em `README.md`, `AGENTS.md`, `docs/superpowers/plans/platform-expansion.md`.

---

## 5. Estratégia de Verificação

1. **Integridade de Links:** Garantir que nenhum link interno de markdown esteja quebrado após a movimentação.
2. **Scripts de Chave:** Executar verificação estática de sintaxe e busca nos scripts `rotate-dev-keys.sh` e `rotate_replace.py` para garantir que apontam para o novo caminho.
3. **Workspace Rust & CI:** Rodar `cargo test --workspace` e `cargo clippy` para garantir que nenhum teste que dependa de caminhos seja afetado.
