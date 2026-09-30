# Plemu — Visão, Arquitetura & Roadmap de Distribuição Retrô

> **Tese Fundamental:** *O rigor legal escala com o tamanho da audiência.*
>
> - **Uso privado:** Livre e irrestrito.
> - **Grupo pequeno:** Barato, leve e monetizado por assentos.
> - **Alcance público:** Exige atestação criptográfica e revisão legal.
> - **Curadoria oficial:** Exige crivo editorial e destaque na vitrine.
>
> *Ninguém é barrado de experimentar; quem busca escala, contribui e se submete ao processo.*

---

## Sumário Executivo
1. [O Que É](#1-o-que-é)
2. [A Escada de Distribuição (A Parte Mais Importante do Produto)](#2-a-escada-de-distribuição-a-parte-mais-importante-do-produto)
3. [Como Funciona Tecnicamente (Cadeia de Confiança)](#3-como-funciona-tecnicamente-cadeia-de-confiança)
4. [O Marketplace — Onde Está o Dinheiro](#4-o-marketplace-onde-está-o-dinheiro)
5. [Roadmap — A Feature de IA (Romhacking Assistido)](#5-roadmap-a-feature-de-ia-romhacking-assistido)
6. [O Que Já Está Construído e Validado](#6-o-que-já-está-construído-e-validado)
7. [Versões Exatas (Pinos de Dependência)](#7-versões-exatas-pinos-de-dependência)
8. [O Que Cada Um Precisa Instalar (Ambiente de Dev)](#8-o-que-cada-um-precisa-instalar)
9. [O Que Falta (Próximos Passos em Ordem de Prioridade)](#9-o-que-falta-próximos-passos-em-ordem-de-prioridade)
10. [Financiamento: Raise Público no Futard (MetaDAO)](#10-financiamento-raise-público-no-futard-metadao)
11. [Foco Imediato: Colosseum Crypto World's Fair 2026](#11-foco-imediato-colosseum-crypto-worlds-fair-2026)

---

## 1. O Que É

**Plemu** é um hub multi-emulador de jogos retrô desenvolvido sob medida para o console portátil **PlaySolana PSG1**. Um único player Unity executa cores libretro de consoles clássicos e consoles de fantasia:
- **Consoles Clássicos:** NES, SNES, Mega Drive, Master System / Game Gear, Game Boy / Game Boy Color (GB/GBC), Game Boy Advance (GBA), Nintendo 64 (N64), PlayStation 1 (PS1), Nintendo DS, PSP, Dreamcast e Atari 2600.
- **Fantasy Consoles:** WASM-4, TIC-80 e PICO-8.
- **Total:** 18 sistemas suportados.

Mas o produto **não é apenas "mais um emulador"**. O produto é uma **escada de distribuição com trilho de pagamento nativo em Solana**, na qual:
1. Qualquer pessoa roda o que quiser no próprio aparelho.
2. Compartilha coleções com um grupo fechado pagando por assentos de distribuição.
3. Se quiser alcançar o público geral, submete o conteúdo a uma esteira de revisão legal e entra na loja pública.

> **O emulador é o runtime; a escada de distribuição é o negócio.**

O princípio de design que sustenta toda essa arquitetura é direto e definitivo: **o rigor legal escala com o tamanho da audiência**.
- O uso privado é livre.
- Grupos pequenos operam com custo baixo e formato leve.
- Alcance público irrestrito exige revisão formal.

Ninguém é impedido de experimentar; quem busca escala, contribui e se submete ao processo.

---

## 2. A Escada de Distribuição (A Parte Mais Importante do Produto)

O modelo é estruturado em **quatro degraus progressivos**:

### Degrau 1 — Local
Qualquer usuário pode carregar e rodar as próprias ROMs no seu aparelho, a qualquer momento.
- **Casos de uso:** Testes rápidos, desenvolvimento, execução de dumps pessoais ou validação imediata de um mod recém-criado.
- **Modelo legal:** O mesmo território de qualquer emulador consagrado (RetroArch, EmulationStation, Dolphin) — a plataforma fornece o runtime, o usuário fornece o seu conteúdo, sem distribuição por parte da Plemu.
- **Isolamento:** Conteúdo local roda em uma faixa visualmente identificada como `[LOCAL / NÃO VERIFICADO]`, não aparece em nenhum diretório público, não habilita gorjetas (tips) e não conta como publicação na rede. É o playground livre do usuário.

### Degrau 2 — Packs Privados (Coleções com Assentos)
O usuário organiza uma coleção curada — um **pack** — e a compartilha com um círculo restrito de membros.
- **Monetização de capacidade:** Packs pequenos são gratuitos ou de valor simbólico. Conforme a audiência se expande, o operador adquire **assentos da plataforma**: quanto maior o alcance, maior o investimento em capacidade.
- **Precificação livre:** O operador tem liberdade total para cobrar a mensalidade ou taxa de entrada que desejar de seus membros — a plataforma não precifica memberships, apenas vende assentos de capacidade.

#### Por Que os Packs São o Coração Comercial do Projeto
1. **Receita recorrente e previsível:** A monetização não fica refém do sucesso de bilheteria de títulos individuais.
2. **Criação de um mercado aberto sustentável:** Curadores, comunidades de romhack, clubes de tradução, servidores de speedrun e criadores com audiência estabelecida tornam-se operadores de pack com economias autossuficientes.
3. **Funil natural de conversão:** O operador inicia com 5 amigos, expande para 50, escala para 500 e, a cada estágio, avalia se continua adquirindo assentos ou se submete a coleção para a vitrine pública.

### Degrau 3 — Loja Pública: Tier Beta
Para transcender o grupo privado e alcançar toda a base de usuários do PSG1, o conteúdo é submetido à loja pública.
- **Exigências de submissão:**
  - Atestação criptográfica assinada pela wallet do criador.
  - Verificação automatizada de licenças open-source ou autorizações de distribuição.
  - Triagem rigorosa contra bases de dados de dumps comerciais protegidos.
  - Evidência documental de titularidade ou direito de distribuição.
- **Benefícios:** Uma vez publicado, o jogo ganha listagem oficial, ferramentas de descoberta, recebimento de tips, venda direta e emissão de colecionáveis digitais.

### Degrau 4 — Loja Pública: Tier Main (Curada)
Títulos promovidos da tier beta através de curadoria editorial da plataforma.
- **Destaque:** Constitui o selo de máxima qualidade do ecossistema, recebendo posicionamento privilegiado na página inicial, prioridade de busca e acesso a edições limitadas.

### Resumo Comparativo da Escada

| Degrau | Escopo de Conteúdo | Assinatura & Confiança | Recursos da Plataforma |
|---|---|---|---|
| **1. Local** | ROMs e dumps próprios do usuário | Nenhuma (faixa isolada/quarentenada) | Nenhum: sem listagem, sem tips, sem mint, sem promoção |
| **2. Pack Privado** | Coleções fechadas com $N$ assentos | Assinatura da Plataforma + Atestação do Operador | Venda de assentos, receita de membership definida pelo operador |
| **3. Loja Beta** | Distribuição pública aberta via submissão | Assinatura da Plataforma após verificação e atestação | Listagem pública, busca, tips na tela, venda direta, colecionáveis |
| **4. Loja Main** | Catálogo oficial com curadoria editorial | Assinatura da Plataforma com selo de curadoria | Todos os recursos da Beta + destaque na home e spotlight |

### Separação Técnica e Mecanismo de Takedown
A distinção técnica elementar entre os degraus reside em **quem assina** e **quem é remunerado**:
- **Conteúdo local não é assinado por ninguém:** Permanece isolado no armazenamento do dispositivo.
- **Packs e loja pública são invariavelmente servidos por manifestos assinados:** Essa garantia criptográfica permite a remoção imediata (*takedown*) de qualquer conteúdo irregular, inclusive dentro de um pack fechado. A assinatura não existe para restringir o usuário, mas sim para fornecer o botão de takedown e direcionar de forma inequívoca o fluxo financeiro ao detentor dos direitos.

---

## 3. Como Funciona Tecnicamente (Cadeia de Confiança)

Para todo o conteúdo distribuído pela plataforma (seja em packs privados ou na loja pública), vigora a regra inviolável:
> **O dispositivo só executa conteúdo cujo hash SHA-256 conste em um manifesto JSON assinado com Ed25519, re-verificando esse hash antes de cada inicialização.**  
> **O que não confere, não executa. Falha fechado, sem atalhos ou bypass.**

### Pipeline de Verificação (Byte a Byte)
A cadeia de verificação foi desenvolvida e testada de forma idêntica em Rust e C#, com validação cruzada byte a byte:
1. **Leitura dos bytes brutos:** Carrega os bytes crus do arquivo de manifesto e do envelope de assinatura destacada (`manifest.sig`).
2. **Resolução de chave pública:** Localiza a chave em um registro local pinado, validando seu status (`active`, `next` ou `emergency`).
3. **Verificação da assinatura Ed25519:** Valida a assinatura diretamente contra os bytes brutos **ANTES** de tentar parsear qualquer JSON — se a assinatura falhar, a execução é abortada no ato sem tocar no parser.
4. **Validação estrutural:** Parseia o JSON e certifica conformidade com o schema (`schemaVersion == 1`) e coerência do signatário.
5. **Anti-rollback de sequência:** Garante que o número de sequência do manifesto seja estritamente maior ou igual ao último número aceito pelo aparelho, impedindo ataques de reinstalação de manifestos antigos com entradas revogadas.
6. **Políticas de Freshness (Atualidade):**
   - *Manifesto fresco (até 72h):* Permite jogar e baixar novos conteúdos normalmente.
   - *Vencido em até 7 dias (período de carência):* Permite apenas a execução do conteúdo já previamente instalado no aparelho.
   - *Acima de 7 dias de atraso:* Bloqueia execuções dinâmicas, restringindo o acesso exclusivamente ao *starter-pack* imutável empacotado no APK assinado.
7. **Gate de Lançamento (Per-Launch Gate):** O `LaunchGate` computa o SHA-256 em streaming do artefato e exige correspondência exata com o manifesto, checando ainda se a entrada não foi revogada via sidecar.

### Isolamento Estrutural: Faixa Local vs. Faixa Distribuída
O conteúdo local trafega por um canal completamente apartado e identificado. Não possui acesso a nenhuma funcionalidade do ecossistema: sem listagem, sem recebimento de tips, sem geração de NFTs e sem mecanismo de promoção automática para packs ou loja sem cumprir o fluxo de revisão.  
Não existe viés de código que permita a um binário local converter-se em item distribuído sem assinatura. A fronteira intransponível entre *"o que você joga"* e *"o que você distribui"* é a blindagem legal de toda a plataforma.

### Tiers de Loja e Remoção Reativa (Takedown)
- **Duas tiers públicas:**
  - `beta`: Publicação ágil respaldada por triagem automática de licença e atestação de wallet, dispensando curadoria editorial prévia.
  - `main`: Seleção curada promovida a partir da beta, com exposição destacada e benefícios adicionais.
- **Fluxo de Takedown Reativo:**
  - Recebimento de DMCA ou ordem de cessação $\rightarrow$ Geração imediata de sidecar de revogação na entrada $\rightarrow$ Recompilação e republicação do manifesto assinado (seja do pack ou da loja) $\rightarrow$ O dispositivo processa o novo manifesto, recusa a execução e expurga o artefato no próximo refresh.
  - **SLA de Resposta:** Menos de 24 horas em todos os dispositivos da rede; rota de emergência documentada em runbook com meta de execução inferior a 30 minutos. Aplica-se integralmente aos packs privados, dado que também utilizam manifestos assinados.

### Atestação Criptográfica do Criador
A carteira do desenvolvedor assina uma estrutura congelada de bytes contendo:
- Título da obra
- Sistema emulado
- Hash SHA-256 do binário
- Licença aplicável
- Declaração irrevogável de direito de distribuição
- Endereço da wallet e timestamp UTC

Existe ainda uma variante para curadores de obras de terceiros munidos de comprovação documental de licença. Esse registro é o alicerce econômico do sistema: **a chave que assina é o endereço que recebe os pagamentos**.

### Camada de Emulação & Shim Nativo em C (Decisão vs. SK.Libretro)
Optamos pelo desenvolvimento de um shim nativo proprietário em C11, expondo uma ABI compacta de aproximadamente 22 funções invocadas via P/Invoke pelo C#. Todos os callbacks da especificação libretro operam inteiramente dentro do espaço nativo.

> **Por Que Não Usamos o SK.Libretro?**  
> Avaliamos a biblioteca pronta SK.Libretro e descartamos seu uso pelas seguintes razões críticas (documentadas no repositório):
> - Renderização por hardware (*hardware rendering*) quebrada no Android há anos sem previsão de correção.
> - Marshalling sob Unity IL2CPP instável e não certificado.
> - Falhas graves e travamentos na reprodução de áudio multi-core.
> - Dependência de mantenedor único sem suporte contínuo.

A ABI proprietária do Plemu já prevê e reserva os entry points de OpenGL ES necessários para a ponte de aceleração gráfica por hardware que sistemas como N64, PSP e Dreamcast exigem.

### Cores de Emulação: Allowlist, Deny-by-Default e Auditoria
- **Allowlist restrita:** 16 cores atendendo os 18 consoles suportados, sob filosofia de bloqueio por padrão (*deny-by-default*).
- **Lista de Bloqueio Rígida (`cores/forbidden.json`):** Cores vetados permanentemente por utilizarem licenças com restrições não-comerciais ou termos ambíguos de relicenciamento (`snes9x`, `genesis_plus_gx`, `picodrive`, `fbneo`, `fbalpha`, `duckstation`, `swanstation`, família `mame`, `mesen`, etc.).
- **Auditoria Estática Contínua (`plemu-core-audit`):** Ferramenta automatizada que inspeciona o arquivo `LICENSE` de cada core em disco e valida o commit contra a trava de licenças (`expected-licenses.lock`), abortando os builds de CI noturnos caso qualquer inconsistência seja detectada.

---

## 4. O Marketplace — Onde Está o Dinheiro

O marketplace é a engrenagem central do Plemu e o principal vetor do pitch para investidores e comunidade.

### O Problema do Ecossistema Retrô vs. A Solução Plemu
- **O gargalo do criador retrô:** Não existe um "Steam para Game Boy". O desenvolvedor independente de homebrew não tem loja, não tem checkout, não tem carteira integrada, não tem certificação de autoria e não tem trilho de royalties.
- **A invisibilidade do modder e do curador:** O modder não existe comercialmente — suas modificações circulam como arquivos desprotegidos em fóruns. Curadores, tradutores e administradores de comunidades que mantêm a cena viva há décadas nunca receberam remuneração direta por seu trabalho.
- **A resposta do Plemu:** Todo conteúdo distribuído é gerado nativamente com hash do artefato, wallet do autor e assinatura digital sobre esses parâmetros. O mesmo trilho de proveniência erguido para segurança jurídica converte-se, sem alterações de contrato, em um **trilho de liquidação financeira imediata**.

### Linhas de Receita (Em Ordem de Importância)

#### 1. Assentos de Pack (Receita Principal e Recorrente)
A plataforma monetiza a infraestrutura vendendo capacidade de distribuição. O operador do pack contrata assentos com base no tamanho da sua audiência. Trata-se de receita contínua e altamente previsível, que escala diretamente com a tração da rede e não depende de apostas em títulos específicos. Garante a sustentabilidade do negócio sem recorrer a taxas invasivas de intermediação ou modelos baseados em publicidade.

#### 2. Mensalidade (Membership) Fixada pelo Criador
O operador do pack estabelece autonomamente o valor de acesso à sua comunidade (mensal ou pontual) e retém 100% da receita de subscrição; a Plemu aufere receita apenas pela venda dos assentos. Isso viabiliza a criação de microempresas lucrativas (clubes de romhacking, curadorias fechadas, grupos de speedrun), incentivando o operador a investir em novos assentos para suportar seu crescimento.

#### 3. Tips Diretos ao Criador
Botão nativo de gorjeta acessível durante o gameplay, remetendo tokens instantaneamente para a carteira atestada do criador, sem intermediação ou custódia da plataforma. É a funcionalidade de entrega mais ágil e a que estabelece a validação imediata do produto junto à comunidade de desenvolvedores.

#### 4. Venda Direta e Pay-What-You-Want
Comercialização de títulos na loja pública com autorização de acesso vinculada à carteira do comprador e downloads protegidos pelo mesmo pipeline de verificação criptográfica.

#### 5. Emissão de Cartuchos Digitais Colecionáveis (NFTs)
Mint do jogo na forma de colecionável digital único (via Metaplex Core na Solana), registrando o SHA-256 do artefato nos metadados. O NFT referencia o byte exato do jogo aprovado, viabilizando negociação secundária com royalties programáveis em código.

#### 6. Divisão Automática de Royalties (Splits On-Chain)
Mecanismo de repasse fracionado on-chain entre desenvolvedor do jogo base, tradutor, modder, artista de sprites e compositor musical. Ao criar um mod a partir de um título existente, a divisão financeira herda obrigatoriamente a carteira do autor original. Um recurso transformador para o cenário de modificações e romhacking, inexistente em qualquer outra plataforma.

#### 7. Curadoria como Vetor Econômico
A promoção editorial da tier beta para a tier main confere prestígio, ampliação substancial de descoberta e exclusividade no lançamento de edições limitadas.

#### 8. Quests e Campanhas de Engajamento
Integração profunda com as camadas de identidade e reputação da PlaySolana (PlayDex e PlayID): jogar, publicar títulos, enviar gorjetas e explorar lançamentos geram pontuação e recompensas on-chain, fomentando retenção contínua e impulsionando criadores emergentes.

### Dinâmica do Efeito de Rede
O acesso gratuito no Degrau Local expande a base do funil. O crescimento em escala demanda contratação de capacidade, financiando a infraestrutura por quem aufere valor econômico. A distribuição aberta requer auditoria de integridade, assegurando um catálogo seguro e defensável. E como as compensações financeiras são liquidadas na mesma carteira que assina as atestações, **toda a economia do ecossistema é publicamente verificável na blockchain**.

---

## 5. Roadmap — A Feature de IA (Romhacking Assistido)

A incorporação de fluxos de inteligência artificial generativa aplicada ao romhacking expande drasticamente a capacidade criativa da plataforma, posicionando-se como a segunda tese central de apresentação do projeto.

### A Visão: Criatividade sem Barreiras Técnicas
Oferecer ao jogador ferramentas intuitivas baseadas em IA para modificar e reimaginar ROMs clássicas diretamente no ecossistema, testar as alterações em segundos no próprio PSG1 e disponibilizar suas criações para outros entusiastas.

### Casos de Uso Práticos
- **Reskin de Sprites e Paletas:** Modificação de personagens, texturas de cenário e esquemas de cores.
- **Localização e Adaptação Narrativa:** Tradução automatizada de títulos para português (pt-BR) ou outros idiomas, reescrita estilizada de diálogos e ramificação de roteiros.
- **Edição e Geração Procedural de Fases:** Criação de novos mapas, labirintos e arquiteturas de níveis respeitando a estrutura binária dos consoles originais.
- **Rebalanceamento de Dificuldade:** Geração sob demanda de modos fáceis, desafios hardcore, modos para speedrun ou ajustes de curvas de física.
- **Composição e Arranjo de Áudio:** Novas trilhas sonoras orquestradas em conformidade estrita com as limitações dos chips sonoros clássicos.
- **Cartuchos Remix e Mashups:** Combinação criativa de recursos e mecânicas entre títulos tecnicamente compatíveis.

### A Sinergia entre IA e a Escada de Distribuição
A escada de distribuição resolve o ciclo completo de lançamento de mods com IA:
1. O usuário gera e roda a modificação no **Degrau Local**, sem atrito e sem pedir licença a ninguém.
2. Compartilha o resultado com seu círculo em um **Pack Privado**.
3. O projeto ganha tração na comunidade $\rightarrow$ o operador contrata novos assentos.
4. O mod atinge maturidade e a licença original autoriza trabalhos derivados $\rightarrow$ é submetido à **Loja Pública**, passando pela revisão legal e sendo publicado com divisão programada de royalties para o autor original da base.

### Resolução do Atrito Criativo & Filtro de Conteúdo Amador
- **Atrito Zero:** Elimina o obstáculo técnico entre idealizar uma alteração e experimentá-la rodando em hardware dedicado.
- **Gestão Eficiente de UGC:** Soluciona o dilema tradicional de plataformas abertas (a sobrecarga de criações preliminares que poluem vitrines). A experimentação em larga escala reside de forma produtiva nos Degraus 1 e 2, mantendo a loja pública com alto padrão de qualidade.
- **Expansão Exponencial do Catálogo:** A biblioteca do Plemu desvincula-se da quantidade limitada de homebrews existentes no mundo, passando a refletir a criatividade da comunidade. Cada jogo transforma-se na raiz de um ecossistema de variantes rastreadas e monetizadas.

### Regra de Ouro Legal: Herança Obrigatória de Licenças
A manipulação com IA no ambiente local é livre. Contudo, **a distribuição de derivados subordina-se rigorosamente à licença da obra base**:
- Para que um mod seja comercializado em packs pagos ou listado na loja pública, a obra original deve autorizar categoricamente a criação de trabalhos derivados.
- O derivado herda todas as cláusulas originais (atribuição, reciprocidade em licenças copyleft como CC-BY-SA, etc.).
- O mesmo rigor do pipeline de auditoria que preserva a segurança jurídica da plataforma é o que assegura a integridade da fábrica de mods.

---

## 6. O Que Já Está Construído e Validado

O vertical slice de emulação está **completamente concluído e homologado ponta a ponta**. Não se trata de protótipo conceitual ou mock de apresentação: o software roda hoje.

### Entregas Homologadas
- **Emulação Completa com Core Real:** O core libretro SameBoy executa com sucesso o jogo homebrew *Libbet and the Magic Floor* (licença zlib), atravessando o shim nativo em C e o `LaunchGate` fail-closed dentro do Unity Editor, com renderização gráfica, processamento de áudio e mapeamento de controles 100% funcionais.
- **Build Funcional em APK arm64 IL2CPP:** Compilação gerada em pacote de aproximadamente 30 MB, inspecionada internamente: agrega o shim nativo, o core SameBoy e a biblioteca `libil2cpp` compilados estritamente para `arm64-v8a` (formato ELF de 64 bits). O maior desafio técnico da arquitetura — o marshalling entre libretro e IL2CPP em Android — foi integralmente superado.
- **Suíte de Testes Rust Impecável:** 95 testes unitários e de integração aprovados em todo o workspace de ferramentas, com conformidade total de linters (`cargo clippy`) e formatação (`cargo fmt`) em CI.
- **Smoke Test Nativo com Core Stub:** 16.750 asserções automatizadas validando vídeo, áudio, envio de comandos e persistência de savestates de forma determinística.
- **Smoke Test Nativo com Core Real:** 42 asserções com SameBoy, submetendo o core a rotinas rigorosas de salvamento e rebobinamento (*rewind*) em gameplay real.
- **Testes Automatizados em Unity:** 34 testes `EditMode` e 5 testes `PlayMode`, cobrindo desde a renderização fiel dos quadros de vídeo até o teste de negação de segurança: a alteração de um único byte no binário acarreta rejeição sumária pelo gate via `HashMismatch`, impedindo a inicialização do jogo.
- **Enforcement E2E Offline em CI:** Rotina completa rodando no GitHub Actions que simula e rejeita dumps comerciais ilegítimos, autoriza entradas auditadas, compila o catálogo JSON, assina criptograficamente e verifica os pacotes.
- **Resolução de Condição de Corrida Crítica:** Diagnóstico e saneamento de um erro de *use-after-free* na thread de áudio durante o descarregamento da emulação (evitando travamentos ou fechamentos abruptos no aparelho), protegido por testes de guarda específicos.
- **Registry em Produção com Entradas Reais:** Dois títulos totalmente homologados no catálogo (um jogo de Game Boy e um de WASM-4), com atestações assinadas na prática e documentação probatória de licença.
- **Runbooks de Operações Críticas:** Manuais operacionais estruturados para publicação expressa em situações emergenciais e rotinas de rotação periódica de chaves.
- **Pipelines de CI/CD em Execução:** Quatro workflows ativos no GitHub Actions: compilação e suíte de testes, validação de pull requests do repositório, auditoria noturna de integridade e esteira de revogação imediata.
- **One-Pager de Parceria Estruturado:** Apresentação executiva finalizada para alinhamento com a diretoria de Developer Relations da PlaySolana.
- **Auditoria de Arquitetura Aprovada:** Revisão final do vertical slice chancelada sem pendências impeditivas, validando o comportamento fail-closed da cadeia de segurança desde as chaves pinadas até o runtime nativo.

### Transparência de Escopo Atual
O que está integralmente construído e testado no código atual é o pipeline da **loja pública assinada**. O degrau local (execução de arquivos próprios via Storage Access Framework) e a gestão de packs com assentos comercializáveis estão concebidos na arquitetura, configurando os itens prioritários de implementação imediata.

---

## 7. Versões Exatas (Pinos de Dependência)

Para assegurar rigor absoluto de compilação e evitar divergências de ambiente entre os membros da equipe:

| Camada / Componente | Versão / Commit Homologado | Especificações & Flags |
|---|---|---|
| **Unity Editor** | `6000.3.19f1` (revisão `7689f4515d75`) | Pino oficial da engine (não utilizar outras versões) |
| **Arquitetura / Scripting** | IL2CPP / arm64-v8a exclusivo | `minSdkVersion: 30`, `targetSdkVersion: 34` |
| **Android NDK** | `r27c` (`27.2.12479018`) | NDK oficial embutido no módulo Android do Unity |
| **Core SameBoy** | `v1.0.3` (commit `208ba4afabffab9edde416f2dbb8ae459e34adb8`) | Licença MIT |
| **ROM de Teste (Libbet)** | `v0.08` (32.768 bytes, licença zlib) | SHA-256: `3607412031c8287cf878299ce96e581e85b852dde703806343b95576fa3ff1a9` |
| **Pacotes Unity** | `Newtonsoft.Json 3.2.1`, `Test Framework 1.4.6`, `uGUI 2.0.0` | Módulo nativo `androidjni` habilitado |
| **Criptografia Ed25519 (C#)** | `Chaos.NaCl` | Vendorizado no repositório com proveniência e integridade registradas |
| **Rust Toolchain** | Rust `stable` (Edition 2021, MSRV 1.93.1) | Componentes obrigatórios: `rustfmt` e `clippy` |
| **Crates Rust do Workspace** | `ed25519-dalek 2`, `sha2`, `sha1`, `md-5`, `crc32fast`, `rusqlite 0.32`, `quick-xml 0.37`, `serde 1`, `clap 4`, `chrono 0.4`, `bs58 0.5`, `strsim 0.11`, `zip 2`, `schemars 0.8` | Gerenciadas em `tools/Cargo.toml` |
| **Shim Nativo C** | Padrão C11, compilador `clang` | Parâmetros estritos: `-Wall -Wextra -Werror -fPIC` |
| **SDKs a Integrar** | PlaySolana Unity SDK (`com.playsolana.psdk`), `Solana.Unity-SDK 3.1.0` | Suporte a hardware, carteira on-device e RPC Solana |

---

## 8. O Que Cada Um Precisa Instalar

### Configuração do Ambiente de Trabalho
Para compilar e testar a solução completa localmente:
1. **Unity Hub & Unity 6000.3.19f1:** Instalar o editor oficial e adicionar o módulo **Android Build Support**, marcando impreterivelmente os subcomponentes **OpenJDK** e **Android SDK & NDK Tools** (isso garante a instalação correta do NDK r27c).
2. **Clang / Compilador C11:**
   - *macOS:* Instalar o Xcode Command Line Tools via terminal: `xcode-select --install`. O shim é compilado diretamente com o clang, sem necessidade de configurar o CMake.
   - *Linux/Windows:* Garantir a presença do `clang` compatível com C11 no PATH.
3. **Rust Toolchain:** Instalar a versão estável via `rustup`, habilitando as ferramentas de formatação e análise:
   ```bash
   rustup component add rustfmt clippy
   ```
4. **rgbds:** Compilador de Game Boy necessário para gerar as boot ROMs do SameBoy durante o build do core (`brew install rgbds` ou gerenciador de pacotes equivalente).
5. **Git & Acesso à Rede:** Necessários para clonar os submódulos e baixar o código-fonte do SameBoy no commit pinado durante a primeira compilação.
6. **Credenciamento PlaySolana:** Registro no portal `developers.playsolana.com` para obtenção das credenciais de acesso ao SDK, imagem do simulador PSG1 e requisição formal do DevKit.
7. **PSG1 DevKit Físico:** Dispositivo de desenvolvimento solicitado via portal de desenvolvedores, essencial para os testes de homologação final em hardware real.
8. **Licença Unity para Integração Contínua (Opcional):** Arquivo `.ulf` configurado no secret `UNITY_LICENSE` do repositório para habilitação automática dos jobs de teste `EditMode` em nuvem.

### Fluxo para Subir a Aplicação do Zero
1. **Executar o Staging:** Com o projeto aberto no Unity, execute pelo menu superior:  
   `Plemu → Stage Native + Content`  
   *(Este comando compila o shim em C, compila o core SameBoy, monta o catálogo de dados e assina o manifesto beta com as chaves de desenvolvimento).*
2. **Rodar no Editor:** Abra a cena `Boot` e navegue pela interface: `Library → Libbet → Play`.
3. **Gerar o Pacote Android:** Execute pelo menu do editor:  
   `Plemu → Build PSG1 APK`.

### Diretrizes de Segurança para Gestão de Chaves
- **Chaves de Desenvolvimento:** Ficam estritamente restritas ao ambiente local de teste e **fora do versionamento do repositório**.
- **Chaves de Produção:** Devem residir com segurança em serviços de gestão de segredos (KMS) e secrets de CI com acesso restrito.
- **Chave de Emergência (Revogação Geral):** Gerada em equipamento completamente desconectado da rede (*air-gapped*); nunca entra em contato com repositórios, servidores de hospedagem ou instâncias de CI.

---

## 9. O Que Falta (Próximos Passos em Ordem de Prioridade)

### 1. Novos Itens da Escada Comercial (Definem o Modelo de Negócio)
- **Faixa Local no LaunchGate:** Implementação de uma segunda via de execução no `LaunchGate`, isolada e com rotulagem explícita para arquivos próprios: desprovida de catálogo público, gorjetas, emissão de colecionáveis ou promoção direta. Demanda revisão da especificação (atualmente orientada a canal único) e criação de testes automatizados comprovando que arquivos da faixa local não vazam para a faixa assinada.
- **Manifestos Independentes para Packs:** Cada pack privado operando com manifesto assinado exclusivo, controle de sequência incremental e sidecars de revogação dedicados, preservando a capacidade de takedown dentro de ambientes restritos.
- **Assentos como Entitlement On-Chain:** O aparelho solicita o manifesto do pack mediante comprovação da posse de assento via assinatura de carteira. O assento constitui o ativo vendido pela infraestrutura e comercializado pelo curador como subscrição.
- **Mecanismo de Aquisição e Upgrade de Assentos:** Interface e esteira contratual para contratação escalonada de capacidade conforme a audiência do pack se expande.
- **Esteira de Submissão para a Loja Pública:** Formulário integrado e esteira automatizada de verificação cadastral e de licenças, superando a triagem manual atual baseada em comandos CLI e pull requests no diretório `registry`.

### 2. Bloqueadores para Execução em Dispositivo Real
- **Tratamento de StreamingAssets no Android:** Em compilações Android, os arquivos do diretório `StreamingAssets` são compactados internamente no APK, tornando inviável o uso de APIs síncronas de `System.IO.File`. Requer transição para leitura assíncrona com `UnityWebRequest` (protocolo `jar:file://`) ou descompressão controlada para `Application.persistentDataPath`.  
  *(Atualmente o sistema responde de modo fail-closed apresentando biblioteca vazia sem quebra de segurança, mas essa alteração é o divisor entre um build que instala e um build que roda jogos).*
- **Validação com o DevKit Físico do PSG1:** Homologação prática de resposta dos botões físicos, métricas de framerate, estabilidade térmica e compatibilidade com as regras de JIT do hardware real.

### 3. Integração com o Ecossistema PlaySolana
- **Mapeamento de Controles PSG1Input:** O mapeamento RetroPad já segue o layout clássico do SNES, perfeitamente aderente ao hardware do PSG1.
- **Integração de Carteira com SvalGuard:** Homologação da carteira do console para viabilizar envio de tips, compra de assentos e mint de colecionáveis.
- **Módulos de Gamificação e Identidade (PlayDex e PlayID):** Acoplamento de quests, badges e progressão da plataforma PlaySolana.
- **Ajustes de Interface no Simulador:** Homologação da biblioteca e visualizadores na proporção oficial de tela do console (1240×1080 em orientação retrato).

### 4. Governança, Produção & Segurança
- **Fixação Definitiva de Cores de Emulação:** Submissão de PR substituindo marcações temporárias (TBD) pelos hashes criptográficos e commits definitivos de licença (o CI noturno acusa erro propositalmente na presença de TBD).
- **Keystore de Assinatura e Chaves de Produção:** Criação do keystore de release para empacotamento do Android e configuração segura de chaves em KMS.
- **Refinamentos de Interface:** Mensagens e avisos contextuais sobre o status experimental da tier beta e motor de busca integrado cobrindo ambas as tiers da loja.

### 5. Roadmap de Produto Expandido
- **Marketplace v1:** Sequenciamento de entrega: Tips $\rightarrow$ Assentos de Pack $\rightarrow$ Venda Direta $\rightarrow$ Mint de NFTs.
- **Módulo de Romhacking com IA:** Ferramenta dedicada para remixagem de sprites, tradução e publicação de mods.
- **Expansão de Emuladores Suportados:** Integração de cores de SNES, Mega Drive, GBA e implementação da ponte gráfica em OpenGL ES para N64, PSP e Dreamcast.
- **Portal Web de Criadores:** Painel administrativo em navegador para envio de arquivos, parametrização de packs e relatórios financeiros.

### 6. Alinhamentos Estratégicos com a PlaySolana (One-Pager Pronto)
Pontos essenciais a serem pactuados em alinhamento de 30 minutos com o time de DevRel:
1. Diretriz formal e documentada da PlayVerse sobre políticas de emulação.
2. Autorização e regras do PlayGate para carregamento de arquivos locais pelo usuário (definição prioritária para o Degrau 1).
3. Diretrizes de execução de código JIT e políticas de memória W^X no sistema operacional EchOS (define se consoles avançados como N64, PSP e Dreamcast contarão com recompilação dinâmica ou se operarão em modo interpretado).
4. Limites de tamanho de arquivo para submissões de APK/AAB no PlayGate.
5. Confirmação sobre capacidades touch no painel do portátil.
6. Versões do ecossistema Unity oficialmente homologadas com o PlaySolana SDK.
7. Cronograma para envio do DevKit e abertura da conta de publicação no PlayGate.

---

## 10. Financiamento: Raise Público no Futard (MetaDAO)

A estratégia de captação pública está ancorada no **[futard.io](https://futard.io)**, o launchpad pioneiro baseado em **futarquia** da MetaDAO — ecossistema no qual a governança é orientada por mercados de predição, a tesouraria permanece sob gestão transparente dos detentores de token e todos os desembolsos requerem aprovação formal por voto.

### Referência de Mercado: O Caso HiveBits
Estudo de caso e referência direta: [HiveBits no Futard](https://www.futard.io/launch/AbTuTsoBJS2jQggvfb7d1LA2SfbHa5NB5HbJqX8C2PnS)
- **Meta inicial fixada:** US$ 140.000.
- **Montante captado:** US$ 242.675 (173% da meta alcançada, rodada finalizada com sucesso).
- **Preço por token:** US$ 0,014.
- **Avaliação Total Implícita (FDV):** Aproximadamente US$ 181.000.
- **Supply total:** 12,9 milhões de tokens.
- **Distribuição de Liquidez:** 77,5% de float inicial, com 7% alocados em pool de liquidez na Meteora e 15,5% direcionados ao AMM de futarquia.
- **Vesting e Alinhamento da Equipe:** Cliff de 18 meses com liberação atrelada a marcos de valorização de mercado em parcelas de 20% (escalonadas de 2x até 32x o preço do lançamento).
- **Limite Operacional:** Teto mensal de US$ 10.000 para despesas correntes sem dependência de consulta pontual de governança.
- **Tração da Plataforma Futard:** 93 campanhas concluídas, US$ 44 milhões transacionados e 2.380 investidores participantes.

### A Estrutura de Captação do Plemu
- **Dimensão Consciente da Rodada:** A proposta prevê um volume de captação inferior ao da HiveBits — estritamente o necessário para cobrir aquisições de hardware (DevKits), custos de licenciamento, desenvolvimento do marketplace e alguns meses de fôlego operacional, evitando valorizações infladas ou metas irreais para a fase atual do projeto.
- **Aderência ao Modelo de Futarquia:** A governança por futarquia alia-se organicamente à proposta: fundos travados em contrato inteligente, desembolsos atrelados ao sentimento do mercado e liberações financeiras vinculadas a marcos técnicos representam a estrutura ideal para um projeto fundamentado em entregas verificáveis de código e hardware.

### Nossos Diferenciais Competitivos
1. **Software Funcional Antes do Raise:** Enquanto a maioria das propostas de captação apoia-se em conceitos visuais e promessas de apresentação, o Plemu dispõe de software operacional, testável e auditável de ponta a ponta antes da abertura da rodada. Qualquer participante pode clonar o repositório, executar os testes e atestar a solidez do sistema fail-closed.
2. **Modelo de Receita Sustentável desde a Origem:** A comercialização de assentos de pack entrega uma dinâmica econômica real, previsível e mensurável, superando o estigma de plataformas Web3 que relegam a monetização a fases indefinidas.
3. **Alinhamento Institucional com a PlaySolana:** A participação ou apoio público da equipe da PlaySolana alavanca a narrativa de ambos os lados: para a PlaySolana, o Plemu representa conteúdo de alto apelo para o catálogo de estreia do PSG1 e impulsiona a economia de desenvolvedores do hardware; para o Plemu, esse endosso consolida a credibilidade da rodada e pacifica simultaneamente as aprovações de política da loja.

---

## 11. Foco Imediato: Colosseum Crypto World's Fair 2026

O alvo de execução imediato do projeto é a participação de destaque no hackathon global **[Colosseum Crypto World's Fair](https://colosseum.com/worldsfair)**.

### Dados da Competição
- **Prazo Limite para Submissão:** **12 de outubro de 2026** (uma janela de aproximadamente 25 dias para execução e polimento).
- **Premiação Total:** US$ 840.000 em prêmios, destacando-se o Grande Prêmio de US$ 30.000 e 20 premiações temáticas de US$ 15.000 cada.
- **Trilha Exclusiva Solana:** US$ 100.000 distribuídos entre 10 projetos selecionados (US$ 10.000 cada).
- **Oportunidade de Venture Capital:** Fundo de investimento de US$ 2,5 milhões; os projetos mais promissores ingressam no programa de aceleração com aporte pré-seed de US$ 250.000 e imersão presencial de 12 semanas em San Francisco.

### As 7 Entregas Obrigatórias para a Submissão (Em Ordem de Prioridade)

1. **APK Operacional em Dispositivo Real:**  
   Implementar a leitura correta de `StreamingAssets` no Android. Esse é o bloqueador técnico primário e o que diferencia uma demonstração confinada ao computador de um produto funcional em um console portátil.
2. **Demonstração Mínima da Escada de Distribuição:**  
   Fluxo ponta a ponta ilustrando: carregamento de uma ROM no modo local $\rightarrow$ criação de um pack fechado $\rightarrow$ aquisição de assentos $\rightarrow$ entrada de um segundo usuário no pack.
3. **Marketplace v1 Funcional:**  
   Carteira conectada e exibição em tempo real de gorjetas (*tips*) enviadas diretamente para a carteira do criador durante o jogo.
4. **Protótipo de IA para Romhacking:**  
   Demonstração prática de ao menos uma frente de edição assistida por inteligência artificial (como substituição visual de sprites ou tradução automática de textos) — o elemento de maior apelo visual e memorabilidade para o vídeo de apresentação.
5. **Vídeo de Demonstração da Jornada Completa:**  
   Produção de vídeo demonstrando sem cortes o fluxo integrado:
   - Criação de uma modificação assistida por IA.
   - Teste imediato no console portátil.
   - Compartilhamento em um pack privado.
   - Crescimento do pack com aquisição de assentos adicionais.
   - Submissão para a loja pública e aprovação pela esteira legal.
   - Recepção de gorjeta on-chain de um jogador real.
   - *Encerramento impactante:* Demonstração da tentativa deliberada de adulteração do arquivo distribuído, com o sistema fail-closed abortando a execução em tempo real na tela.
6. **Repositório Público Auditado e Higienizado:**  
   Manutenção do padrão de integridade do código (434 arquivos rastreados, ausência total de credenciais ou segredos expostos, nenhum binário indevido versionado e apenas duas fixtures oficiais com licenças auditadas).
7. **Narrativa e Posicionamento Estratégico de Pitch:**  
   Apresentação clara da proposta de valor: **não um emulador genérico no PSG1**, mas **"a primeira plataforma na qual criadores de jogos retrô e modders distribuem em escada e são remunerados on-chain, em um console portátil dedicado, com capacidade de remoção de conteúdos ilegais garantida em menos de 24 horas"**.

### Sequência Estratégica de Execução
A ordem recomendada de etapas alinha esforço e tração:
1. **Participação no Hackathon Colosseum:** Validação técnica imediata, premiação e visibilidade global perante a comunidade Solana.
2. **Captação no Futard:** Realização do raise público logo após o evento, aproveitando o momento favorável de exposição e contando com o apoio institucional da PlaySolana.
