# Especificação Técnica: Plemu Mobile Demo para Solana Seeker (Android APK)
## Com Bolsa de Valores de Jogos (Game-Fi Bonding Curves & MWA)

**Data de Publicação:** 2026-10-06  
**Status:** Aprovado para Implementação  
**Autor:** Plemu Labs (Dev 1 / Dev 2)  
**Alvo de Hardware:** Solana Seeker (Android 14+, ARM64, dApp Store 2.0)  
**Track:** Solana Mobile Hackathon — Gaming, DeFi & Mobile Infrastructure  

---

## 1. Visão Executiva & Objetivos da Demo

O **Plemu** é um ecossistema de emulação retrô Web3 com arquitetura *content-trust fail-closed* e uma camada financeira descentralizada. Para o **Solana Mobile Hackathon**, entregaremos um APK nativo de alto impacto para o **Solana Seeker**, introduzindo o conceito inovador de **"Bolsa de Valores de Jogos" (Game Token Market)**.

No Plemu, **cada jogo é um token negociável na Solana**, cujo valor sobe e desce dinamicamente por meio de uma **Bonding Curve On-Chain**. Quanto mais um jogo é jogado, comprado e apoiado com gorjetas, mais liquidez entra em sua curva, valorizando o token do jogo em tempo real. Jogadores podem não apenas jogar a 60 FPS com controles táteis retrô, mas também investir antecipadamente em jogos promissores e lucrar com sua popularidade diretamente pelo smartphone.

### Metas Principais da Demo
1. **Emulação Nativa a 60 FPS:** Execução nativa do core Game Boy / Game Boy Color (SameBoy) via `libretro_host.so` compilado em `arm64-v8a` integrado ao Unity 6.
2. **Integração MWA 2.0 & Seed Vault:** Autenticação e assinatura de transações através do **Mobile Wallet Adapter (MWA)**, acionando a partição segura de hardware (**Seed Vault**) do Solana Seeker para biometria e assinatura instantânea.
3. **Bolsa de Valores de Jogos (Bonding Curve Unificada):**
   - Cada jogo possui um SPL Token dedicado (ex: `$LIBBET`, `$STAR`).
   - Compras de licenças e gorjetas injetam automaticamente liquidez na Bonding Curve do jogo, elevando o preço do token.
   - Jogadores e investidores podem comprar e vender tokens do jogo em 1 clique na interface do Seeker via swap instantâneo MWA.
4. **Hero Flow Completo (Loja -> Compra de Licença -> Trading de Token -> Gameplay):**
   - Jogo grátis imediato (`Libbet`) com controles virtuais táteis.
   - Jogo comercial premium (`Starforge Quest`) bloqueado pelo `LaunchGate`.
   - Compra da licença via MWA: registra o `Receipt` PDA e injeta fundos na Bonding Curve do `$STAR`.
   - Demonstração da valorização do token ao vivo no gráfico e ticker do jogo!
5. **Touch Controls Estilo Game Boy (Modo Retrato):** UI customizada vertical com proporção 4:3 na metade superior, mini-ticker financeiro e carcaça retrô com D-Pad e botões com **feedback háptico (vibração)** na metade inferior.

---

## 2. Arquitetura do Sistema Mobile

```
┌────────────────────────────────────────────────────────────────────────┐
│                          Solana Seeker (Android)                       │
├────────────────────────────────────────────────────────────────────────┤
│                           Plemu Unity App                              │
│                                                                        │
│   ┌────────────────────────────────┐  ┌─────────────────────────────┐  │
│   │   UGUI Retrô (Modo Retrato)    │  │  Virtual Touch RetroPad     │  │
│   │   - Tela do Jogo 4:3 (Topo)    │  │  - D-Pad + A/B/Select/Start │  │
│   │   - Mini-Ticker ($STAR ▲ +12%) │  │  - Android Haptic Feedback  │  │
│   │   - Painel da Bolsa / Swap     │  │                             │  │
│   └─────────────────┬──────────────┘  └──────────────┬──────────────┘  │
│                     │                                │                 │
│                     ▼                                ▼                 │
│   ┌─────────────────────────────────────────────────────────────────┐  │
│   │             LaunchGate (Fail-Closed Content Trust)              │  │
│   │  - Validação de Assinatura Ed25519 do Manifesto                 │  │
│   │  - Streaming SHA-256 da ROM em persistentDataPath               │  │
│   │  - Verificação On-Chain de Receipt PDA / IEntitlementStore      │  │
│   └─────────────────┬───────────────────────────────────────────────┘  │
│                     │ (Se Autorizado)                                  │
│                     ▼                                                  │
│   ┌─────────────────────────────────────────────────────────────────┐  │
│   │                   EmulationSession (C#)                         │  │
│   │  - VideoSurface (Texture2D) / AudioSurface (AudioClip stream)   │  │
│   └─────────────────┬───────────────────────────────────────────────┘  │
│                     │ P/Invoke                                         │
│                     ▼                                                  │
│   ┌─────────────────────────────────────────────────────────────────┐  │
│   │           libretro_host.so (C11, ARM64-v8a)                     │  │
│   │  - Núcleo SameBoy (libsameboy_libretro_android.so)              │  │
│   └─────────────────────────────────────────────────────────────────┘  │
├────────────────────────────────────────────────────────────────────────┤
│                       Camada de Integração Web3                        │
│                                                                        │
│   ┌────────────────────────────────┐  ┌─────────────────────────────┐  │
│   │ PlemuMarketTransactionBuilder  │  │   MwaBridge (Android AAR)   │  │
│   │  - Anchor buy_license + split  │  │   - MWA 2.0 Intent Client   │  │
│   │  - SwapBuy / SwapSell instruc. │  │   - Seed Vault Biometrics   │  │
│   │  - Bonding Curve Math          │  │                             │  │
│   └────────────────┬───────────────┘  └──────────────┬──────────────┘  │
│                    │                                 │                 │
│                    └────────────────►◄───────────────┘                 │
└─────────────────────────────────────┼──────────────────────────────────┘
                                      │
                                      ▼
                        Solana Devnet RPC / Clusters
```

---

## 3. Mecânica Econômica: Games como Tokens Negociáveis (Bonding Curve)

### 3.1. Princípio da "Bolsa de Valores de Jogos"
Cada jogo indexado no catálogo do Plemu é associado a um **SPL Token Mint** e uma conta PDA de **Bonding Curve**:
- **Exemplo:** Jogo *Starforge Quest* -> Ticker `$STAR`.
- **Preço Inicial ($P_0$):** 0.001 SOL.
- **Mecanismo de Curva:** Curva polinomial com reserva proporcional:
  $$P(S) = P_0 + k \cdot S^2$$
  onde $S$ é o supply circulante e $k$ é o fator de escala de liquidez da curva.

### 3.2. Fluxo de Injeção de Liquidez (Jogadores -> Preço Sobe)
Quando um jogador adquire uma licença de jogo ou envia uma gorjeta:
1. **Split Automático:**
   - 70% do valor vai diretamente para o criador do jogo (Creator Splits Vault).
   - 5% vai para a Tesouraria da Plataforma Plemu.
   - **25% do valor é automaticamente roteado para a Bonding Curve do Token do Jogo** (compra direta de tokens para o pool de reserva).
2. **Impacto Econômico:** Cada novo jogador que compra a licença gera pressão compradora direta no token do jogo, fazendo o gráfico de preço subir. Jogos com excelente retenção e volume de jogadores têm tokens que se valorizam exponencialmente.

### 3.3. Painel de Trading no Solana Seeker (Investir / Especular)
A interface móvel disponibiliza a aba **"Mercado / Bolsa"**:
- **Gráfico de Preço:** Gráfico sparkline/linha renderizado com os últimos ticks de negociação (24h high/low, volume e variação percentual).
- **Swap em 1 Toque (Buy / Sell):**
  - O usuário digita a quantidade de SOL que deseja investir no jogo (ex: 0.1 SOL) ou tokens `$STAR` que deseja realizar lucro.
  - Toca em **"Swap via Seed Vault"**.
  - O MWA abre o sensor de impressão digital do Seeker, assina e confirma a transação em ~1 segundo.
- **Cashback de Jogador:** Quem compra a licença recebe automaticamente um lote inicial de boas-vindas do token do jogo, tornando todo jogador um co-proprietário do jogo.

---

## 4. Integração Mobile Wallet Adapter (MWA 2.0) & Seed Vault

### 4.1. Comunicação Segura com o Hardware Seeker
O Solana Seeker isola as chaves mestras no ambiente seguro **Seed Vault**:
- O Plemu APK comunica-se via protocolo de intents `solana-wallet://`.
- Sessão criptografada via ECDH (Diffie-Hellman na Curve25519).
- Suporte a transações atômicas contendo múltiplas instruções (ex: `buy_license` + split de liquidez na bonding curve).

### 4.2. Contratos C# de Integração (`IMobileWalletAdapter.cs` & `IBondingCurveService.cs`)
Em `apps/device/unity/Assets/_Game/Scripts/Runtime/Wallet/`:
```csharp
public interface IMobileWalletAdapter
{
    bool IsMwaAvailable();
    Task<WalletIdentity> ConnectAsync(CancellationToken cancellationToken = default);
    Task<byte[]> SignAndSendTransactionAsync(byte[] serializedTransaction, CancellationToken cancellationToken = default);
    Task DisconnectAsync();
}

public interface IBondingCurveService
{
    Task<GameTokenQuote> GetQuoteAsync(string gameId, ulong solAmountIn);
    Task<GameTokenMarketData> GetMarketDataAsync(string gameId);
    byte[] BuildSwapBuyInstruction(string buyer, string gameId, ulong solAmount, ulong minTokensOut);
    byte[] BuildSwapSellInstruction(string seller, string gameId, ulong tokenAmount, ulong minSolOut);
}
```

---

## 5. Jornada da Demonstração no Hackathon (The Hero Flow)

A apresentação aos jurados do hackathon dura **2 minutos** e demonstra a união entre emulação nativa e finanças de jogos:

### Minuto 0:00 a 0:30 — Emulação e Instant-Play
1. O apresentador abre o app no Solana Seeker no formato vertical.
2. Abre o jogo gratuito **Libbet** instantaneamente a 60 FPS.
3. Demonstra os controles táteis na carcaça retrô com feedback de vibração háptica nos dedos.

### Minuto 0:30 a 1:00 — A Bolsa de Valores de Jogos & Ticker ao Vivo
1. O apresentador volta para a vitrine e entra na aba **"Bolsa de Jogos"**.
2. Mostra o título **Starforge Quest** com seu ticker oficial: **`$STAR: 0.042 SOL (▲ +14.8%)`**.
3. O apresentador destaca: *"No Plemu, cada jogo é um ativo líquido. Conforme o jogo ganha tração, a bonding curve valoriza o token do jogo."*

### Minuto 1:00 a 1:30 — Compra da Licença via MWA & Injeção de Liquidez
1. O apresentador clica em **"Comprar Licença — 0.05 SOL"**.
2. O Seeker aciona o **Seed Vault**: o apresentador toca o polegar no leitor biométrico.
3. A transação é confirmada na Devnet:
   - A conta `Receipt` PDA é criada on-chain.
   - Uma fatia da transação entra na Bonding Curve do `$STAR`.
4. O gráfico de preço na tela do Seeker atualiza em tempo real: o preço do `$STAR` sobe imediatamente para **`0.046 SOL (▲ +22.4%)`** diante dos jurados!

### Minuto 1:30 a 2:00 — Trade de Tokens & Execução do Jogo
1. O apresentador faz um mini-swap direto: investe 0.1 SOL comprando mais tokens `$STAR` com assinatura instantânea pelo Seed Vault.
2. Toca em **"Jogar Agora"**: o `LaunchGate` valida a assinatura do manifesto, o hash SHA-256 e o `Receipt` PDA.
3. O jogo comercial inicializa suavemente a 60 FPS, com um mini-ticker financeiro exibindo a cotação do `$STAR` no topo da tela do Game Boy.

---

## 6. Design da Interface do Usuário (UI & Touch Controls)

### 6.1. Tela Principal (Modo Retrato Game Boy com Ticker)
```
┌──────────────────────────────────────────────┐
│  PLEMU MOBILE   [ Devnet: Connected (0x4P..) ]│
├──────────────────────────────────────────────┤
│  TICKER: $STAR 0.046 SOL ▲ +22.4% | MC: $46k │
├──────────────────────────────────────────────┤
│                                              │
│             TELA DO JOGO (4:3)               │
│                                              │
│         [ Textura de Emulação SameBoy ]      │
│            Resolução Nativa 160x144          │
│            Escala Crisp Pixel-Perfect        │
│                                              │
├──────────────────────────────────────────────┤
│  Status: 60.0 FPS | Audio: Stereo | Sav: OK  │
├──────────────────────────────────────────────┤
│                                              │
│             CONTROLES RETRÔ TÁTEIS           │
│                                              │
│        ▲                   (X)               │
│     ◄  ●  ►             (Y)   (A)            │
│        ▼                   (B)               │
│                                              │
│         [ SELECT ]        [ START ]          │
│                                              │
├──────────────────────────────────────────────┤
│  [ ABA: JOGAR ]     [ ABA: BOLSA / SWAP ]    │
└──────────────────────────────────────────────┘
```

### 6.2. Aba "Bolsa / Swap do Jogo"
- **Gráfico:** Linha verde/vermelha com oscilação recente do preço.
- **Estatísticas:** Preço atual em SOL e USD, Market Cap estimado, Volume 24h e Holders.
- **Card de Negociação:**
  - Campo "Você Paga": [ 0.1 SOL ]
  - Campo "Você Recebe": [ ~2.17 $STAR ]
  - Botão de Ação: `[ SWAP COM SEED VAULT ]` (estilizado retrô-cyberpunk).

---

## 7. Estrutura de Arquivos e Componentes

```
apps/device/unity/
├── Assets/
│   ├── Plugins/
│   │   └── Android/
│   │       ├── AndroidManifest.xml              # Permissões MWA, Internet e Haptics
│   │       └── libs/arm64-v8a/
│   │           ├── libretro_host.so             # Host C de emulação (já integrado)
│   │           └── libsameboy_libretro_android.so # Núcleo SameBoy (já integrado)
│   ├── _Game/
│   │   ├── Scripts/
│   │   │   └── Runtime/
│   │   │       ├── Emulation/
│   │   │       │   ├── TouchInputSource.cs      # Entrada multitouch para emulação
│   │   │       │   └── VirtualRetroPadUI.cs     # Componente visual e háptico dos botões
│   │   │       ├── Platform/
│   │   │       │   └── AndroidHaptics.cs        # Wrapper JNI para motor de vibração Android
│   │   │       ├── Wallet/
│   │   │       │   ├── IMobileWalletAdapter.cs  # Contrato MWA
│   │   │       │   ├── AndroidMwaAdapter.cs     # Implementação MWA 2.0 / Seed Vault
│   │   │       │   ├── IBondingCurveService.cs  # Serviço de cotações e swaps da curva
│   │   │       │   └── BondingCurveService.cs   # Implementação C# das fórmulas da curva
│   │   │       └── UI/
│   │   │           ├── PlemuApp.cs              # Shell do app, modo retrato e navegação
│   │   │           ├── GameTokenMarketUI.cs     # Painel de trading, gráfico e cotação
│   │   │           └── MiniTickerHUD.cs         # Ticker flutuante no topo do emulador
│   │   └── Tests/
│   │       └── EditMode/
│   │           ├── TouchInputSourceTests.cs     # Testes de input e deadzone
│   │           ├── BondingCurveMathTests.cs     # Testes de precisão matemática da curva
│   │           └── MobileCommerceFlowTests.cs   # Teste end-to-end do gate e compra on-chain
```

---

## 8. Critérios de Aceite & Validação

- [ ] **Compilação de APK:** Projeto Unity gera `PlemuSeekerDemo.apk` para Android ARM64 (IL2CPP) sem erros de linkagem ou JNI.
- [ ] **Emulação Nativa 60 FPS:** Jogo roda com áudio sincronizado e framerate estável a 60 quadros por segundo no Seeker.
- [ ] **Integração MWA & Seed Vault:** O aplicativo solicita e recebe assinaturas de transação via MWA com prompt biométrico nativo do Seeker.
- [ ] **Content Gate Fail-Closed:** O jogo comercial `Starforge` é rigidamente bloqueado antes da emissão da licença on-chain (`DenialReason.EntitlementMissing`).
- [ ] **Dinâmica da Bonding Curve:**
  - A compra da licença transfere fundos para a curva e registra o `Receipt` PDA.
  - O preço do token `$STAR` atualiza dinamicamente e é refletido no `MiniTickerHUD` e no painel `GameTokenMarketUI`.
  - O swap de tokens (compra e venda direta de cotas do jogo) executa corretamente com autorização da carteira.
- [ ] **Feedback Háptico:** Os botões virtuais ativam o motor háptico do Seeker ao serem pressionados.
- [ ] **Conformidade de Testes:** Todas as suítes de testes C# (`Plemu.Solana.Tests` e testes EditMode no Unity) passam com 100% de sucesso.
