# Design: Plemu Web Platform & Game Hub Architecture

- **Data:** 2026-10-06
- **Status:** Proposto (Aprovado em Brainstorming)
- **Escopo:** Expansão da plataforma Plemu para a Web (`apps/web`), englobando Vitrine Unificada (Loja e Packs), Página do Jogo com Modo Cinema, Biblioteca com Persistência Híbrida, Creator Studio com Submissão Criptográfica, Runtime WebAssembly (WASM) e Sincronização com o console PlaySolana PSG1.

---

## 1. Contexto e Motivação

O Plemu nasceu como um ecossistema focado no console portátil **PlaySolana PSG1**, com uma tese jurídica e econômica clara: **o rigor legal escala com o tamanho da audiência** (Local -> Packs Privados -> Loja Beta Pública -> Loja Main Curada).

No entanto, restringir a experiência de jogo e a descoberta de títulos exclusivamente ao hardware físico do PSG1 cria barreiras de adoção:
1. **Fricção de Aquisição:** Entusiastas de jogos retrô e usuários da Web3 que não possuem o PSG1 precisam de uma porta de entrada para descobrir jogos, comprar títulos e apoiar criadores.
2. **Escala para Criadores:** Desenvolvedores de jogos homebrew e romhacks dependem de volume de audiência para viabilizar financeiramente suas criações em USDC e $PLEMU.
3. **Hub Central de Gestão:** Operadores de packs, criadores e jogadores necessitam de uma interface web rica, responsiva e moderna no desktop/mobile para gerenciar coleções, configurar assentos de packs, acompanhar vendas e sincronizar saves.

A solução é o **Modelo Dual**: transformar a Web em um destino de jogos de primeira classe com emulação **WebAssembly (WASM)** de 60 FPS no navegador, ao mesmo tempo em que atua como o companheiro oficial do PSG1 com sincronização de biblioteca e saves.

---

## 2. Invariantes Fundamentais do Sistema

A expansão para a Web herda rigorosamente todas as regras invioláveis de segurança e integridade do ecossistema Plemu:

1. **Content Trust Fail-Closed no Cliente:**
   - O navegador nunca confia cegamente no edge, CDN ou servidor de arquivos.
   - Qualquer ROM ou binário de jogo baixado deve ter seu hash SHA-256 verificado em streaming antes de ser entregue ao core WebAssembly. Se houver divergência de 1 bit, a execução é abortada no ato com erro tipado.
   - Manifestos de loja e de packs devem ser validados contra a chave pública Ed25519 pinada antes da leitura de metadados.

2. **Zero-Egress para Conteúdos Privados (Gated):**
   - Conteúdos de Packs Privados com assentos são servidos exclusivamente através de URLs pré-assinadas com TTL curto (≤ 60s) geradas pelo Cloudflare Worker "The Seat Gate".
   - O Worker valida on-chain na Solana a posse do `Seat` PDA pelo endereço da carteira conectada antes de emitir a URL de download da ROM.

3. **Cálculo Monetário Exato em Inteiros e Alinhamento com a Spec de Monetização:**
   - A plataforma Web adota estritamente o modelo formalizado em `docs/superpowers/specs/2026-10-05-monetization-design.md`:
     - Modelos de Monetização: `free`, `buy_once`, `demo_full`, `pwyw` e `tip_only`.
     - Tipos de Entrega: `pda_license` (recibo PDA leve `[b"receipt", artifact_sha256, buyer_pubkey]`) e `nft_cartridge` (Metaplex Core).
     - Preços em USDC são expressos em inteiros com 6 casas decimais (1 USDC = 1.000.000 unidades) e SOL em lamports (9 decimais). Ponto flutuante (`float`/`double`) é expressamente proibido para cálculos de preços, splits e gorjetas.
     - Taxa de plataforma de 500 bps (5%) para compras e 0% para gorjetas diretas aos criadores.

4. **Isolamento de Execução Web:**
   - A emulação roda isolada em um **Web Worker** dedicado com WebAssembly, impedindo que tarefas de renderização de interface React bloqueiem a taxa de quadros (60 FPS estáveis) e garantindo segurança do sandbox do navegador.

---

## 3. Arquitetura de Rotas e Telas (`apps/web`)

A aplicação Web será estruturada com base no Next.js (App Router), dividida nas seguintes rotas canônicas:

```text
apps/web/
├── app/
│   ├── page.tsx                      # Vitrine Unificada (Loja, Destaques, Catálogo e Packs em Alta)
│   ├── game/
│   │   └── [id]/
│   │       ├── page.tsx              # Página do Jogo (Ficha Técnica, Compra, Tips, Modo Cinema)
│   │       └── embed/page.tsx        # Player embutível leve (opcional para parcerias e iframes)
│   ├── packs/
│   │   ├── page.tsx                  # Diretório de Packs Comunitários e Oficiais
│   │   └── [id]/page.tsx             # Detalhes do Pack (Membros, Jogos Inclusos, Assentos)
│   ├── library/
│   │   └── page.tsx                  # Minha Biblioteca (Local IndexedDB + Carteira Solana + PSG1 Sync)
│   ├── creator/
│   │   ├── page.tsx                  # Creator Studio (Dashboard, Meus Jogos, Vendas, Payouts)
│   │   └── publish/page.tsx          # Assistente de Publicação em 5 Passos
│   └── device/
│       └── page.tsx                  # Emparelhamento e Sincronização com Console PSG1
```

---

## 4. Detalhamento dos Módulos Principais

### 4.1 Vitrine Unificada & Loja (`/`)
- **Hero & Destaques:** Carrossel imersivo com títulos da Tier Main (curadoria oficial) e os Packs de comunidade com maior atividade.
- **Navegação em Abas:**
  - *Destaques:* Jogos em alta, lançamentos e seleções editoriais.
  - *Explorar Catálogo:* Filtros combinados por Sistema/Console (GB, GBA, NES, SNES, Genesis, WASM-4, TIC-80), Gênero (Ação, RPG, Puzzle, Arcade) e Modalidade de Preço (Grátis, Compra, Demo, PWYW).
  - *Packs da Comunidade:* Lista de clubes organizados por criadores com contadores de assentos disponíveis (ex: "42/50 assentos ocupados").
- **Busca Global:** Busca em tempo real com auto-complete por nome do jogo, autor ou tag.

### 4.2 Página do Jogo & Modo Cinema (`/game/[id]`)
- **Ficha Técnica & Atributos Criptográficos:**
  - Galeria de mídia (screenshots em alta resolução, gameplay GIFs).
  - Badges informativos: Console/Sistema, Gênero, Classificação e Selo de Confiança Criptográfica (Signatário Ed25519 oficial, SHA-256 verificado, Licença aberta comprovada).
  - Caixa de Compra / Acesso:
    - Botão de Compra em USDC ou $PLEMU via carteira Solana.
    - Seletor "Pague quanto quiser" (PWYW) com validação de piso mínimo.
    - Botão "Jogar Demo" se disponível.
- **Player Modo Cinema (UX de Jogo):**
  - Ao clicar em "Jogar Agora", a página escurece e o player se expande em uma janela de cinema centralizada (proporção original 4:3 ou pixel ratio do sistema).
  - Captura imediata de teclado e gamepads via Gamepad API com `preventDefault` em teclas de navegação para impedir scroll acidental da página.
  - **Retro HUD Flutuante:**
    - Botão de Tela Cheia (`F`).
    - Quick Save (`F5`) e Quick Load (`F8`) gravados no IndexedDB com timestamp.
    - Toggle de Shaders Retrô (CRT Scanlines / Smooth / Crisp Pixels).
    - Controle de Volume e Mute.
    - **Botão de Gorjeta em 1 Clique:** Modal discreto para enviar 1, 2 ou 5 USDC de gorjeta direta para o criador sem precisar pausar a partida.
    - Tecla `Esc` restaura suavemente a página de detalhes.

### 4.3 Diretório e Detalhes de Packs (`/packs` e `/packs/[id]`)
- **Visualização do Pack:** Capa personalizada, descrição da curadoria do pack, lista de jogos incluídos e indicador de lotação de assentos.
- **Entrada no Pack:**
  - Se gratuito/aberto: Adicionar à biblioteca e gerar permissão local.
  - Se pago: Executar transação Solana para adquirir assento (`Seat` PDA) com o valor definido pelo operador.
  - Se privado por convite: Entrada de chave/código de acesso.
- **Painel do Operador do Pack:**
  - Gestão de membros e revogação de assentos.
  - Aquisição de lotes de assentos adicionais junto ao smart contract Plemu.
  - Adição/Remoção de jogos no manifesto do Pack.

### 4.4 Biblioteca do Jogador & Sincronização (`/library` e `/device`)
- **Modo Convidado (Guest):** Jogos jogados gratuitamente e demos salvos localmente no navegador (IndexedDB).
- **Modo Carteira Conectada:**
  - Recuperação dos recibos on-chain (`Receipt` PDAs) e assentos de packs ativos (`Seat` PDAs).
  - Consulta espelho no Supabase para carregamento instantâneo da coleção.
- **Sincronização com o PSG1:**
  - Aba de pareamento que exibe um QR Code ou código numérico de 6 dígitos.
  - O console PSG1 lê o código e sincroniza a lista de títulos da biblioteca e os saves de bateria (.srm / .sav) via API segura do Supabase/R2.

### 4.5 Creator Studio (`/creator`)
- **Visão Geral (Overview):**
  - Faturamento bruto em USDC, total de jogadores únicos, gorjetas recebidas e vendas por jogo.
- **Assistente de Publicação em 5 Passos:**
  1. *Metadados:* Título, sinopse, console alvo, controles sugeridos e tags.
  2. *Arquivos:* Upload do arquivo da ROM/WASM via URL pré-assinada do R2; cálculo client-side de hash SHA-256; upload de cover art e screenshots.
  3. *Triagem Automática:* Comparação instantânea do hash contra base de dados de dumps comerciais protegidos para impedir pirataria de títulos protegidos.
  4. *Monetização:* Escolha da modalidade (Grátis, Compra individual, Demo + Compra, PWYW) e definição de preços em USDC com split de co-autores se houver.
  5. *Atestação Criptográfica & Envio:* Geração da Attestation v2; assinatura digital da mensagem pela carteira Solana do criador; persistência na tabela `submissions` do Supabase. O jogo entra no ar no Tier Beta imediatamente com status de verificação ativo.

---

## 5. Arquitetura do Runtime WebAssembly (WASM)

A emulação web será implementada em uma camada isolada de alta performance:

```text
┌────────────────────────────────────────────────────────┐
│                   Main Thread (UI)                     │
│  - Next.js / React UI Components                       │
│  - User Input Capture (Keyboard & Gamepad API)         │
│  - Web Audio Context (AudioWorklet playback)           │
│  - Canvas / WebGL Display Surface                      │
└──────────────────────────┬─────────────────────────────┘
                           │ postMessage / SharedArrayBuffer
┌──────────────────────────▼─────────────────────────────┐
│                 Web Worker (Emulator)                  │
│  - Streaming SHA-256 Verifier (Crypto API)             │
│  - WASM Module Loader (Emscripten / Libretro Core)     │
│  - SameBoy WASM (GB/GBC) / mgba WASM (GBA) / Nestopia  │
│  - WASM-4 Native Web Runner                            │
│  - Frame Generation (60 FPS Loop)                      │
│  - Save State Serialization (Battery RAM / Savestate)  │
└────────────────────────────────────────────────────────┘
```

### Cores Suportados no MVP Web:
1. **WASM-4:** Suporte nativo instantâneo no browser via runtime WebAssembly oficial.
2. **Game Boy & Game Boy Color:** Core SameBoy compilado para WebAssembly.
3. **Game Boy Advance:** Core mGBA / gpSP compilado para WebAssembly.
4. **NES & SNES:** Cores Nestopia / FCEUmm e Snes9x compilados para WebAssembly via Emscripten.

---

## 6. Integração Web3 & Serviços

### 6.1 Carteira Solana (`@solana/react` + `@wallet-standard/react`)
- Conexão com carteiras populares (Phantom, Solflare, Backpack).
- Simulação obrigatória de transação (`simulateTransaction`) com margem de segurança de 20% no limite de Compute Units (CU).
- Transações com tratamento de erro legível ao usuário (mapeando códigos de erro de smart contract para mensagens amigáveis em português).

### 6.2 Supabase & Cloudflare Worker
- O Cloudflare Worker "The Seat Gate" valida a assinatura de solicitação de download contra os `Seat` PDAs na Solana e emite URLs pré-assinadas com TTL de 60s.
- O Supabase atua como espelho indexador de alta velocidade para consulta de perfis de criador, listagens públicas de jogos, submissões e estado de moderação DMCA.

---

## 7. Estratégia de Testes e Validação

1. **Testes Unitários:**
   - Validação dos utilitários de comércio (`commerce.ts`: `parseUsdc`, `formatUsdc`).
   - Verificador de integridade de hash SHA-256 e parsing de manifestos no client.
2. **Testes de Componentes & UI:**
   - Renderização correta dos cards de jogo, badges de sistema e cálculo de preços.
   - Funcionamento do assistente de publicação em 5 etapas no Creator Studio.
3. **Testes E2E (Playwright):**
   - Fluxo completo do jogador: Visualizar vitrine -> Acessar página do jogo -> Abrir Modo Cinema -> Jogar mini-game/WASM-4 -> Executar Quick Save no IndexedDB -> Simular gorjeta.
   - Fluxo do criador: Navegar para `/creator/publish` -> Preencher rascunho -> Validar cálculo de hash -> Confirmar formulário.
4. **Validação de Tipos e Build:**
   - `npm run typecheck` (`tsc --noEmit`) sem nenhum erro de tipagem.
   - `npm run build` do Next.js concluído com sucesso.
