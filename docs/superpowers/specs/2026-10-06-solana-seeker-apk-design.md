# Especificação Técnica: Plemu Mobile Demo para Solana Seeker (Android APK)

**Data de Publicação:** 2026-10-06  
**Status:** Aprovado para Implementação  
**Autor:** Plemu Labs (Dev 1 / Dev 2)  
**Alvo de Hardware:** Solana Seeker (Android 14+, ARM64, dApp Store 2.0)  
**Track:** Solana Mobile Hackathon — Gaming & Mobile Infrastructure  

---

## 1. Visão Executiva & Objetivos da Demo

O **Plemu** é um emulador e console retrô Web3 com arquitetura *content-trust fail-closed*. Para o **Solana Mobile Hackathon**, entregaremos um APK nativo otimizado especificamente para o smartphone **Solana Seeker**, demonstrando como a propriedade digital de jogos, segurança de chaves em hardware e emulação de alta fidelidade operam em conjunto sem fricção.

### Metas Principais da Demo
1. **Emulação Nativa a 60 FPS:** Execução nativa do core Game Boy / Game Boy Color (SameBoy) via `libretro_host.so` compilado em `arm64-v8a` integrado ao Unity 6.
2. **Integração MWA 2.0 & Seed Vault:** Autenticação e assinatura de transações através do **Mobile Wallet Adapter (MWA)**, acionando o hardware seguro (**Seed Vault**) do Solana Seeker para biometria e assinatura de transações.
3. **Hero Flow — Loja & Compra On-Chain com Receipt PDA:**
   - O APK inicializa com um jogo gratuito para teste imediato (`Libbet`).
   - Um título comercial premium de demonstração é exibido na Loja com preço em Devnet USDC / SOL.
   - O jogador toca em **"Comprar Licença"**, assina a transação via MWA com autorização biométrica no Seed Vault, o programa Anchor `plemu` registra o `Receipt` PDA na Solana Devnet, e o `LaunchGate` criptográfico autoriza e inicializa a ROM local instantaneamente.
4. **Touch Controls Estilo Game Boy (Modo Retrato):** UI customizada com proporção 4:3 na metade superior e uma carcaça retrô com D-Pad e botões táteis virtuais com **feedback háptico (vibração)** na metade inferior.

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
│   │   - Loja / Vitrine Devnet      │  │  - Android Haptic Feedback  │  │
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
│   │  - Anchor buy_license inst.    │  │   - MWA 2.0 Intent Client   │  │
│   │  - Receipt PDA derivation      │  │   - Seed Vault Biometrics   │  │
│   └────────────────┬───────────────┘  └──────────────┬──────────────┘  │
│                    │                                 │                 │
│                    └────────────────►◄───────────────┘                 │
└─────────────────────────────────────┼──────────────────────────────────┘
                                      │
                                      ▼
                        Solana Devnet RPC / Clusters
```

### Componentes de Software

| Componente | Linguagem/Tecnologia | Localização no Repositório | Responsabilidade |
|---|---|---|---|
| **App Shell & UI** | C# (Unity 6 / UGUI) | `apps/device/unity/Assets/_Game/Scripts/Runtime/UI/` | Navegação da Loja, tela de detalhes, renderização do emulador e HUD de status. |
| **Virtual Gamepad** | C# (Unity EventSystem) | `apps/device/unity/Assets/_Game/Scripts/Runtime/Emulation/` | D-Pad e botões A/B/Start/Select com suporte a touch multitouch e haptics Android. |
| **Content Bootstrap** | C# (Unity) | `apps/device/unity/Assets/_Game/Scripts/Runtime/Platform/` | Extração de `StreamingAssets` para `Application.persistentDataPath` na primeira execução. |
| **LaunchGate** | C# (.NET Standard) | `apps/device/unity/Assets/_Game/Scripts/Runtime/Content/` | Validação fail-closed antes de qualquer execução de jogo. |
| **Libretro Host** | C11 (ARM64 JNI) | `apps/device/unity/Assets/Plugins/Android/libs/arm64-v8a/` | Hospedagem nativa do core SameBoy com ring buffer de áudio C11 thread-safe. |
| **MWA Adapter Bridge** | Java/Kotlin + C# | `apps/device/unity/Assets/Plugins/Android/` | Disparo de Intents do Mobile Wallet Adapter para conexão e assinatura com o Seed Vault. |
| **Solana Transaction Engine** | C# | `src/Plemu.Solana/` | Construção de instruções Anchor `buy_license`, derivação de PDAs e queries RPC. |

---

## 3. Integração Mobile Wallet Adapter (MWA 2.0) & Seed Vault

### 3.1. Princípios de Segurança & Protocolo
O Solana Seeker possui o **Seed Vault**, uma partição segura de hardware (Secure Execution Environment) que isola chaves privadas do sistema operacional Android. A comunicação deve ser estritamente mediada pelo protocolo padrão **Solana Mobile Wallet Adapter 2.0**:
- Esquema de Intent: `solana-wallet://` com parâmetros de sessão encriptados via ECDH (Curve25519).
- O aplicativo cliente solicita permissões mínimas: `solana:devnet`.
- A assinatura de transações ocorre dentro do ambiente do Seed Vault, exigindo autenticação biométrica (impressão digital) ou PIN do Seeker.

### 3.2. Contrato da Ponte MWA C# (`IMobileWalletAdapter`)
Em `apps/device/unity/Assets/_Game/Scripts/Runtime/Wallet/`:
```csharp
public interface IMobileWalletAdapter
{
    bool IsMwaAvailable();
    Task<WalletIdentity> ConnectAsync(CancellationToken cancellationToken = default);
    Task<byte[]> SignAndSendTransactionAsync(byte[] serializedTransaction, CancellationToken cancellationToken = default);
    Task DisconnectAsync();
}
```

### 3.3. Implementação da Ponte Android (`AndroidMwaBridge.cs`)
- Utiliza a classe Android JNI (`AndroidJavaObject` / `AndroidJavaClass`) para interagir com o pacote leve de intents MWA.
- Suporta fallback gracioso para mock local caso a aplicação seja executada dentro do Unity Editor para testes rápidos.

---

## 4. Jornada da Demonstração (The Hero Flow)

A demonstração no estande/palco do hackathon segue uma trilha cronometrada de 90 segundos:

### Passo 1: Inicialização e Instant-Play (0 a 20s)
1. O jurado/apresentador abre o APK no Solana Seeker.
2. A tela inicial em modo retrato apresenta a moldura retrô do Plemu.
3. O catálogo exibe:
   - **Libbet** (Gratuito / Open-Source): Tocar em "Jogar" abre imediatamente a ROM a 60 FPS.
   - O apresentador demonstra os controles táteis na tela com feedback de vibração nos botões.

### Passo 2: Acesso à Loja & Jogo Comercial Protegido (20 a 40s)
1. O usuário toca em "Voltar" e seleciona o segundo título: **Starforge Quest** (Jogo Comercial de Demonstração).
2. O botão inicial indica: **"Comprar Licença — 0.05 SOL (Devnet)"**.
3. Se o usuário tentar forçar a execução sem licença, o `LaunchGate` intercepta a chamada em menos de 1ms e exibe na tela:
   ```
   DENIED: EntitlementMissing
   Title requires purchase or valid license ticket: starforge-demo.
   ```

### Passo 3: Conexão e Assinatura com Seed Vault via MWA (40 a 70s)
1. O usuário clica em **"Comprar Licença"**.
2. A aplicação dispara o MWA. O Seeker abre o modal seguro de autorização com a identidade da carteira.
3. O usuário autoriza com a impressão digital no sensor biométrico do Seeker.
4. A transação Anchor `buy_license` construída por `PlemuMarketTransactionBuilder`:
   - Envia 0.05 SOL do comprador para a Tesouraria e Vault do Criador.
   - Inicializa a conta PDA de Recibo: `[b"receipt", artifact_sha256, buyer_pubkey]`.
5. A transação é transmitida para a Solana Devnet e confirmada em ~1 segundo.

### Passo 4: Desbloqueio e Emulação Imediata (70 a 90s)
1. O `SolanaEntitlementService` consulta o RPC Devnet e valida a existência do `Receipt` PDA assinado pelo programa `plemu`.
2. O `IEntitlementStore` armazena o ticket de licença localmente em cache criptográfico seguro.
3. O botão na tela transiciona para **"Jogar Agora"** com o selo visual **"Licenciado na Solana"**.
4. O `LaunchGate.Authorize` emite o `LaunchTicket` e o jogo comercial inicializa com áudio e vídeo fluidos.

---

## 5. Design da Interface do Usuário (UI & Touch Controls)

### 5.1. Layout da Tela (Modo Retrato 20:9)
A tela do Solana Seeker é aproveitada verticalmente em duas seções ergonômicas:

```
┌──────────────────────────────────────────────┐
│  PLEMU MOBILE   [ Devnet: Connected (0x4P..) ]│
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
└──────────────────────────────────────────────┘
```

### 5.2. Especificação do Virtual RetroPad
1. **D-Pad Virtual:**
   - Detecção contínua de arraste e toque nos 4 eixos (`RetroPad.Button.Up, Down, Left, Right`).
   - Zona morta configurada para evitar acionamentos diagonais acidentais.
2. **Botões de Ação:**
   - Botões A e B posicionados no ângulo natural do polegar direito.
   - Toque inicial aciona `HapticFeedback.Perform(HapticFeedbackType.VirtualKey)`.
3. **Mapeamento de Entrada:**
   - A classe `TouchInputSource.cs` implementa `IEmulationInputSource` e traduz eventos de toque diretamente para as máscaras de bits do `RetroPad` no frame do `libretro_host`.

---

## 6. Estrutura de Arquivos e Modificações

```
apps/device/unity/
├── Assets/
│   ├── Plugins/
│   │   └── Android/
│   │       ├── AndroidManifest.xml              # Permissões de Internet, Haptics e Queries MWA
│   │       ├── libs/arm64-v8a/
│   │       │   ├── libretro_host.so             # Binário C do host (já presente)
│   │       │   └── libsameboy_libretro_android.so # Core SameBoy (já presente)
│   │       └── mwa/
│   │           └── MwaPlugin.aar                # Helper AAR para intent MWA 2.0
│   ├── _Game/
│   │   ├── Scripts/
│   │   │   └── Runtime/
│   │   │       ├── Emulation/
│   │   │       │   ├── TouchInputSource.cs      # Entrada de controles táteis virtuais
│   │   │       │   └── VirtualRetroPadUI.cs     # Componente visual e háptico dos botões
│   │   │       ├── Platform/
│   │   │       │   └── AndroidHaptics.cs        # Wrapper JNI para Android Vibrator
│   │   │       ├── Wallet/
│   │   │       │   ├── IMobileWalletAdapter.cs  # Contrato de conexão e assinatura MWA
│   │   │       │   └── AndroidMwaAdapter.cs     # Implementação MWA com fallback para Editor
│   │   │       └── UI/
│   │   │           └── PlemuApp.cs              # Extensão da UI para Modo Retrato e Loja Devnet
│   │   └── Tests/
│   │       └── EditMode/
│   │           ├── TouchInputSourceTests.cs     # Testes de mapeamento e multitouch
│   │           └── MobileCommerceFlowTests.cs   # Testes de simulação de compra e gate on-chain
```

---

## 7. Critérios de Aceite & Validação

- [ ] **Compilação de APK:** O projeto Unity constrói com sucesso para `Android ARM64 (IL2CPP)` gerando `PlemuSeekerDemo.apk`.
- [ ] **Content Bootstrap:** Na primeira execução, o app extrai com sucesso o catálogo de `StreamingAssets` para `Application.persistentDataPath` sem corrupção.
- [ ] **Execução 60 FPS:** O jogo `Libbet` roda a 60 FPS cravados sem engasgos de áudio no emulador Android ARM64 e no dispositivo físico.
- [ ] **Intercepção do LaunchGate:** O jogo comercial `Starforge` é estritamente bloqueado com `DenialReason.EntitlementMissing` antes de qualquer transação on-chain.
- [ ] **Conexão e Assinatura MWA:** Ao tocar em comprar, o Seeker abre a carteira MWA, autentica e retorna a assinatura da transação.
- [ ] **Desbloqueio On-Chain:** A conta `Receipt` PDA criada na Solana Devnet é identificada por `SolanaEntitlementService`, liberando o botão "Jogar" e iniciando a ROM.
- [ ] **Zero Warning / Zero Erros em Testes:** Toda a suíte de testes existente (`dotnet test tests/Plemu.Solana.Tests` e testes EditMode no Unity) permanece verde.
