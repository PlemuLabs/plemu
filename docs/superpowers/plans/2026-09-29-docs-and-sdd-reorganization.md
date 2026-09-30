# Reorganização de Documentação, Specs Normativas e SDD — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reestruturar os arquivos Markdown do repositório para centralizar documentação e specs sob `docs/`, eliminar redundâncias de conteúdo, padronizar o ciclo SDD em `docs/superpowers/` e criar a configuração nativa do Gemini CLI em `GEMINI.md`.

**Architecture:** Mover `spec/` para `docs/specs/` (preservando o conteúdo congelado de `SPEC.md` e `SPEC-ERRATA.md`), mover `Plemu.md` para `docs/vision-pt.md` e `PLAN.md` para `docs/superpowers/plans/platform-expansion.md`. Atualizar scripts de rotação de chave (`rotate-dev-keys.sh`, `rotate_replace.py`) para apontar para `docs/specs/`. Enxugar `AGENTS.md` para focar puramente em regras técnicas de engenharia e criar `GEMINI.md` como wrapper nativo. Atualizar os links no `README.md`.

**Tech Stack:** Markdown, Bash, Python, Rust.

**Spec:** `docs/superpowers/specs/2026-09-29-docs-and-sdd-reorganization-design.md`

## Global Constraints

- **Normative SPEC v1 is FROZEN:** Nunca alterar o conteúdo de `SPEC.md`. Apenas mover seu caminho de `spec/` para `docs/specs/`.
- **Preservação de Conteúdo:** O conteúdo integral de `PLAN.md` (as 43 tarefas) e de `Plemu.md` (visão em PT) deve ser preservado intacto em seus novos destinos.
- **Não commitar sem ordem explícita:** Não executar `git commit` ou `git add .` unprompted.

---

### Task 1: Movimentação de Arquivos para `docs/`

**Files:**
- Create/Move: `docs/specs/SPEC.md` (de `spec/SPEC.md`)
- Create/Move: `docs/specs/SPEC-ERRATA.md` (de `spec/SPEC-ERRATA.md`)
- Create/Move: `docs/vision-pt.md` (de `Plemu.md`)
- Create/Move: `docs/superpowers/plans/platform-expansion.md` (de `PLAN.md`)
- Delete: `spec/` (pasta vazia/antiga), `Plemu.md`, `PLAN.md`

- [ ] **Step 1: Criar diretório `docs/specs` se não existir e mover `spec/SPEC.md` e `spec/SPEC-ERRATA.md`**
- [ ] **Step 2: Mover `Plemu.md` para `docs/vision-pt.md`**
- [ ] **Step 3: Mover `PLAN.md` para `docs/superpowers/plans/platform-expansion.md`**
- [ ] **Step 4: Verificar se os arquivos antigos na raiz e a pasta `spec/` foram removidos**

---

### Task 2: Atualização de Scripts de Rotação de Chaves

**Files:**
- Modify: `tools/scripts/rotate-dev-keys.sh`
- Modify: `tools/scripts/rotate_replace.py`

- [ ] **Step 1: Atualizar caminhos em `tools/scripts/rotate-dev-keys.sh`**
  Substituir referências a `spec/SPEC.md` por `docs/specs/SPEC.md` e `spec/SPEC-ERRATA.md` por `docs/specs/SPEC-ERRATA.md`.

- [ ] **Step 2: Atualizar caminhos em `tools/scripts/rotate_replace.py`**
  Substituir a referência em comentário e o join `os.path.join(root, "docs", "specs", "SPEC.md")`.

---

### Task 3: Refatoração de `AGENTS.md` e Criação de `GEMINI.md`

**Files:**
- Modify: `AGENTS.md`
- Create: `GEMINI.md`

- [ ] **Step 1: Refatorar `AGENTS.md`**
  Remover textos de pitch de produto redundantes com o README e manter:
  1. Identidade e Entidade (`Plemu Labs`, `plemu.xyz`).
  2. Invariantes do Modelo de Confiança (Fail-Closed, raw Ed25519 byte verification, Pinned keys, anti-rollback, freshness, forbidden cores).
  3. Tech Stack & Ferramental (versões estritas: Unity 6000.3.19f1, NDK r27c, Rust stable 1.93.1, C11).
  4. Estrutura de Diretórios atualizada (apontando `docs/specs/` e `docs/superpowers/`).
  5. Comandos essenciais de desenvolvimento e testes (Rust, Native shim, CI).
  6. Convenções de Código e Tratamento de Erros (typed errors, fail closed, zero leaks).

- [ ] **Step 2: Criar `GEMINI.md`**
  Criar `GEMINI.md` na raiz do projeto atuando como ponto de entrada para o Gemini CLI, contendo as instruções do projeto e importando/referenciando `AGENTS.md`.

---

### Task 4: Atualização de Links em `README.md` e Documentos Correlatos

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Atualizar `README.md`**
  Atualizar os links que apontavam para `PLAN.md` e `spec/SPEC.md` para:
  - `docs/superpowers/plans/platform-expansion.md`
  - `docs/specs/SPEC.md`
  Adicionar referência explícita para `docs/vision-pt.md` e para o framework SDD em `docs/superpowers/`.

---

### Task 5: Validação e Testes

- [ ] **Step 1: Executar suíte de testes Rust do workspace**
  Executar `cargo test --workspace` a partir de `tools/` para confirmar que nenhum teste quebrou.
- [ ] **Step 2: Verificar links e integridade com grep**
  Fazer grep por referências órfãs a `spec/SPEC.md` ou `PLAN.md` que precisem de ajuste.
- [ ] **Step 3: Verificar status do git**
  Executar `git status` para confirmar as alterações limpas e organizadas.
