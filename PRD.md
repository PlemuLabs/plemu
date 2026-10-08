# PRD - Integração Mobile Wallet Adapter (MWA 2.0 / Seed Vault) no Plemu Mobile (Unity 6 / Android Seeker)

## Problem Statement

Atualmente, a aplicação móvel do Plemu no Unity 6 (`apps/device/unity`), projetada para execução nativa no **Solana Seeker** (Android 14+, ARM64), possui uma camada de abstração de carteira (`Plemu.Wallet` com `IWallet`), mas depende exclusivamente de uma implementação simulada estática (`MockWallet.cs`), sem nenhuma capacidade de comunicação real com a blockchain Solana ou com carteiras mobile:
1. **Ausência de Integração com o Seed Vault:** O Solana Seeker conta com uma partição segura de hardware (**Seed Vault**) para custódia de chaves e autorização biométrica (impressão digital). Sem a implementação do protocolo Mobile Wallet Adapter (MWA 2.0), os jogadores não conseguem conectar suas carteiras nem autorizar transações com segurança de hardware no dispositivo.
2. **Incompatibilidade com o Fluxo Comercial do Hackathon:** O fluxo de compra de licença de jogos (`buy_license`), gorjetas a criadores e negociação de tokens de jogos em bonding curves requer assinatura e envio de transações on-chain no cluster Solana Devnet diretamente pelo aparelho.
3. **Restrições de Visibilidade de Pacotes no Android 11+ / Android 14:** No Android moderno, o sistema operacional restringe a visibilidade de aplicações de terceiros (*Package Visibility Filtering*). Sem a declaração explícita da tag `<queries>` com o esquema de URI `solana-wallet` no `AndroidManifest.xml`, o `PackageManager` falha em detectar o Seed Vault ou carteiras compatíveis (Phantom, Solflare), resultando em falsos negativos na verificação de disponibilidade.
4. **Fragilidade de Dependências e Build IL2CPP:** A adoção de SDKs monolíticos de terceiros ou plugins binários complexos adiciona atrito desnecessário, dependências pesadas de Gradle e riscos de quebra na compilação IL2CPP para ARM64. O projeto necessita de uma ponte JNI direta, leve e idiomática em C# (espelhando a arquitetura já estabelecida em `SafPicker.cs`).
5. **Risco de Travamento por Exceções JNI Não Tratadas:** Interrupções pelo usuário, rejeição de autenticação biométrica ou cancelamentos de intents no Seed Vault podem gerar exceções JNI/Android não gerenciadas, travando a thread principal do Unity e interrompendo a emulação a 60 FPS caso os retornos não sejam encapsulados em resultados tipados e resilientes (`MwaConnectionResult` / `MwaTransactionResult`).
6. **Vazamento e Persistência Insegura de Sessão:** A gravação descuidada de tokens de autorização (`auth_token`) em armazenamento persistente não criptografado (`PlayerPrefs`) ou sua exposição em logs de sistema viola a segurança do usuário. É indispensável um modelo de sessão estritamente volátil em memória (*fail-closed*), purgado imediatamente no encerramento da sessão ou na desconexão.
7. **Necessidade de Testabilidade e Execução no Unity Editor (TDD 100%):** De acordo com a Regra de Ouro do ecossistema Plemu, todo código novo deve ter 100% de cobertura de testes. É imperativo que o subsistema MWA forneça abstrações limpas e um mock determinístico (`MockMwaAdapter`) para que os testes unitários (EditMode / NUnit) e o desenvolvimento no Editor ocorram sem a necessidade de um dispositivo Android físico conectado.

## Solution

Implementar o subsistema de integração com o **Mobile Wallet Adapter (MWA 2.0 / Seed Vault)** no Unity 6 para o Solana Seeker, seguindo arquitetura desacoplada, ponte JNI direta em C#, resiliência fail-closed e cobertura rigorosa de testes:

1. **Contrato de Transporte de Baixo Nível (`IMobileWalletAdapter`):**
   - Criação da interface desacoplada dentro do assembly `Plemu.Wallet`:
     ```csharp
     public interface IMobileWalletAdapter
     {
         bool IsMwaAvailable();
         Task<MwaConnectionResult> ConnectAsync(CancellationToken cancellationToken = default);
         Task<MwaTransactionResult> SignAndSendTransactionAsync(byte[] serializedTransaction, CancellationToken cancellationToken = default);
         Task DisconnectAsync();
     }
     ```
2. **Modelos de Resultado Imutáveis e Resilientes (`MwaConnectionResult` e `MwaTransactionResult`):**
   - Encapsulam status de sucesso, chave pública em Base58, assinatura de transação (tx hash), token de sessão volátil e mensagens de erro descritivas em inglês, eliminando o lançamento de exceções brutas entre JNI e a lógica de jogo.
3. **Ponte Nativa JNI Direta em C# (`AndroidMwaAdapter`):**
   - Implementação nativa direta utilizando `AndroidJavaClass` e `AndroidJavaObject` para comunicação com a Activity do Unity (`com.unity3d.player.UnityPlayer`).
   - Disparo de intents com o esquema `solana-wallet://v1/associate` e parâmetros de dApp (Nome: "Plemu", Cluster: "devnet").
   - Suporte condicional de compilação `#if UNITY_ANDROID && !UNITY_EDITOR`.
   - Consulta de pacotes no `PackageManager` para verificação precisa da presença do Seed Vault.
4. **Mock Determinístico para Editor & CI (`MockMwaAdapter`):**
   - Implementação alternativa de `IMobileWalletAdapter` para execução no Unity Editor, Windows e pipelines de CI.
   - Fornece respostas determinísticas e configuráveis para conexão bem-sucedida, cancelamento pelo usuário, timeout e assinaturas simuladas (`MwaSig-devnet-...`).
5. **Facade de Alto Nível de Negócio (`MwaWallet : IWallet`):**
   - Implementa a interface existente `IWallet` consumindo o `IMobileWalletAdapter`.
   - Gerencia o ciclo de vida da sessão conectada, expondo `IsConnected` e `PublicKey`.
   - Orquestra os fluxos comerciais (`SendTip`, `PurchaseSeat`, `PurchaseListing`) convertendo chamadas de negócio em payloads de transação assinados e enviados pelo Seed Vault.
   - Limpa imediatamente qualquer estado de autenticação em memória ao executar `Disconnect()`.
6. **Configuração de Manifesto Android (`Assets/Plugins/Android/AndroidManifest.xml`):**
   - Declaração mandatória de `<queries>` com o esquema `solana-wallet` para garantir conformidade total com as políticas de Package Visibility do Android 11 a 14+.
   - Inclusão da permissão `android.permission.INTERNET`.

## User Stories

1. As a mobile player on Solana Seeker, I want the app to detect if the Seed Vault or a compatible MWA wallet is installed, so that I know hardware authentication is supported.
2. As a mobile player, I want to tap "Conectar Carteira" in the game UI, so that the Seeker launches the wallet authorization prompt smoothly without freezing.
3. As a mobile player, I want to approve the dApp connection request, so that my Base58 public key is linked to the active Plemu gaming session.
4. As a mobile player, I want to cancel an authorization request at any time, so that the game returns safely to the disconnected state without errors or crashes.
5. As a mobile player purchasing a commercial game license (`buy_license`), I want the app to prompt for biometric authorization (fingerprint) via Seed Vault, so that my transaction is securely confirmed with hardware isolation.
6. As a mobile player tipping a creator or executing a bonding curve swap, I want the Seed Vault to sign and submit the transaction directly to Solana Devnet, so that the operation is executed in a single seamless step.
7. As a mobile player, I want my transaction signature (tx hash) returned immediately upon confirmation, so that the on-chain receipt PDA can be validated by the `LaunchGate`.
8. As a mobile player, I want transaction rejections or biometric scan failures to display a clean, human-readable English message in the UI, so that I know why the operation was aborted.
9. As a mobile player, I want to disconnect my wallet with a single tap, so that my session ends immediately and no temporary authorizations remain.
10. As a security auditor, I want the MWA `auth_token` to be retained strictly in volatile memory during the runtime lifecycle, so that no credentials leak to disk or storage.
11. As a security auditor, I want all session credentials and public keys to be wiped immediately upon disconnect or application close, adhering strictly to fail-closed principles.
12. As a Unity developer working in the Editor, I want a deterministic `MockMwaAdapter` that simulates wallet connection and signing, so that I can iterate on UI and gameplay without an Android device.
13. As a QA engineer, I want comprehensive NUnit EditMode unit tests covering 100% of the MWA adapter lifecycle, facade operations, and edge cases, so that regressions are caught automatically.
14. As an Android build engineer, I want the JNI bridge to compile without external `.aar` libraries or Gradle dependencies, ensuring reliable ARM64 IL2CPP builds.
15. As an Android system engineer, I want `AndroidManifest.xml` to declare `<queries>` for the `solana-wallet` intent scheme, ensuring full compliance with Android 11-14 package visibility rules on the Seeker.
16. As a platform developer, I want `MwaWallet` to implement `IWallet` cleanly, so that all existing and future commercial flows consume the wallet uniformly.

## Implementation Decisions

- **Arquitetura JNI Direta em C# (Abordagem 1):**
  - Utilização das APIs nativas do Unity (`AndroidJavaClass`, `AndroidJavaObject`) sem introduzir bibliotecas Java/Kotlin externas (`.aar`) ou wrappers pesados. Mantém o monorepo leve, portável e alinhado com `SafPicker.cs`.
- **Desacoplamento de Camadas (Transporte vs Facade):**
  - `IMobileWalletAdapter` responde exclusivamente pelo transporte criptográfico MWA de baixo nível.
  - `MwaWallet` implementa a interface existente `IWallet`, garantindo retrocompatibilidade total com o restante da base de código do jogo.
- **Ciclo de Vida de Sessão em Memória Volátil (Fail-Closed):**
  - O `auth_token` fornecido na autorização inicial é armazenado exclusivamente em variáveis privadas da instância em memória.
  - Não há gravação em `PlayerPrefs`, arquivos de configuração ou logs de depuração. Ao reiniciar o app ou chamar `Disconnect()`, o estado é completamente destruído.
- **Escopo Criptográfico Focado na Demo:**
  - O contrato foca estritamente nas operações essenciais da jornada do Seeker: `ConnectAsync`, `SignAndSendTransactionAsync`, `DisconnectAsync` e `IsMwaAvailable`.
  - Operações de `SignAndSendTransactionAsync` delegam à carteira/Seed Vault a submissão direta ao cluster Solana Devnet, retornando a assinatura final.
- **Isolamento de Plataforma via Compilação Condicional:**
  - Código JNI encapsulado por `#if UNITY_ANDROID && !UNITY_EDITOR`.
  - Em ambiente de Editor ou testes, utiliza-se a implementação mock, garantindo que o Unity compile e execute sem dependência do subsistema Android.
- **Manifesto Android com Queries Explícitas:**
  - Inclusão de `<queries><intent><action android:name="android.intent.action.VIEW"/><data android:scheme="solana-wallet"/></intent></queries>` e permissão de internet.

## Testing Decisions

- **Estratégia de Teste TDD (100% de Cobertura):**
  - Testes unitários implementados em C# com o framework NUnit no diretório `apps/device/unity/Assets/_Game/Tests/EditMode/`.
  - Inclusão no assembly de testes `_Game.Tests.asmdef`.
- **Suítes de Testes Planejadas:**
  - `MwaAdapterTests.cs`:
    - Validação de disponibilidade de MWA no Editor (`IsMwaAvailable`).
    - Validação de fluxo completo de conexão com retorno de chave pública Base58 válida e token de autorização.
    - Simulação de recusa de conexão pelo usuário com retorno de falha e mensagem explicativa em inglês sem lançamento de exceção.
    - Validação do método `DisconnectAsync`, verificando a purga do token e retorno ao estado inicial desconectado.
    - Validação de envio e assinatura de transação serializada com retorno de hash determinístico.
  - `MwaWalletFacadeTests.cs`:
    - Validação do estado inicial desconectado de `MwaWallet`.
    - Delegação correta de `Connect()` para o adapter subjacente.
    - Execução de `SendTip`, `PurchaseSeat` e `PurchaseListing` quando conectado, verificando a geração de assinatura válida.
    - Proteção fail-closed ao invocar operações comerciais com a carteira desconectada.
    - Propagação correta de erros em caso de rejeição no Seed Vault.
  - `MwaResultModelTests.cs`:
    - Validação dos métodos de fábrica estáticos `MwaConnectionResult.Ok(...)`, `MwaConnectionResult.Failed(...)`, `MwaTransactionResult.Ok(...)` e `MwaTransactionResult.Failed(...)`.
- **Comandos de Validação Contínua:**
  - Testes do monorepo: `dotnet test` na raiz do Plemu.
  - Compilação dos assemblies Unity via validação de tipos e integridade de referências (`.asmdef`).

## Out of Scope

- Implementação dos contratos Anchor em Rust no cluster Solana (`programs/plemu-market`, `programs/plemu-splits`).
- Modelagem de UI visual dos botões táteis retrô e layout gráfico da vitrine (cobertos por especificações dedicadas de UI do Seeker).
- Assinatura off-chain de mensagens arbitrárias SIWS (Sign-In With Solana) na versão mobile inicial (o foco da demo é assinatura on-chain com Seed Vault).
- Persistência em disco de tokens de autorização em cofre local criptografado.

## Further Notes

- **Cluster Alvo Padrão:** `Solana Devnet` (RPC: `https://api.devnet.solana.com`).
- **Metadados de Identidade do dApp:**
  - Nome: `"Plemu"`
  - URI de Identidade: `"https://plemu.gg"`
  - Esquema de Ícone: `"https://plemu.gg/icon.png"`
- **Alvo de Hardware:** Solana Seeker (Android 14+, arquitetura ARM64-v8a).
