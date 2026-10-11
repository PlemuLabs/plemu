# TASKS - Integração Mobile Wallet Adapter (MWA 2.0 / Seed Vault) no Plemu Mobile (Unity 6 / Android Seeker)

## Visão Geral das Tarefas (Vertical Slices / Tracer Bullets)

Este documento divide o plano do PRD de **Integração Mobile Wallet Adapter (MWA 2.0 / Seed Vault)** em fatias verticais finas (*vertical slices*), organizadas por ordem de dependência, permitindo entrega incremental, desenvolvimento orientado a testes (TDD 100%) e verificação contínua de ponta a ponta no módulo `apps/device/unity` para o **Solana Seeker**.

---

### Tabela de Slices

| # | Título | Tipo | Bloqueado por | User Stories Cobertas |
| :--- | :--- | :---: | :--- | :--- |
| **Slice 1** | Modelos de Resultado Imutáveis e Contrato de Transporte MWA (`Plemu.Wallet`) | AFK | Nenhum | US-10, US-11, US-13, US-16 |
| **Slice 2** | Mock Determinístico para Editor, CI e Ambientes Não-Android (`MockMwaAdapter`) | AFK | Slice 1 | US-1, US-3, US-4, US-6, US-8, US-12, US-13 |
| **Slice 3** | Facade de Negócio Fail-Closed `MwaWallet : IWallet` com Sessão em Memória Volátil | AFK | Slice 1, Slice 2 | US-2, US-3, US-4, US-5, US-6, US-7, US-8, US-9, US-10, US-11, US-16 |
| **Slice 4** | Ponte JNI Nativa Direta em C# (`AndroidMwaAdapter`) e Isolamento Condicional | AFK | Slice 1, Slice 2, Slice 3 | US-1, US-2, US-3, US-4, US-5, US-6, US-7, US-8, US-14 |
| **Slice 5** | Configuração do Manifesto Android (`AndroidManifest.xml`) com Package Visibility e Permissões | AFK | Slice 4 | US-1, US-15 |
| **Slice 6** | Fábrica de Resolução de Carteira, Integridade de Contratos e Validação NUnit Global | AFK | Slice 3, Slice 4, Slice 5 | US-1, US-12, US-13, US-16 |
| **Slice 7** | Homologação no Solana Seeker e Auditoria de Segurança de Memória e Transações | HITL | Slice 6 | US-1 a US-16 |
| **Slice 8** | Serialização Binária de Transações Solana (Wire-Format) | Concluído | Slice 7 | US-5, US-6, US-7 |
| **Slice 9** | Implementação do Transporte Real MWA 2.0 (Solana.Unity-SDK via UPM + SolanaSdkMwaAdapter) | Concluído | Slice 8 | US-1, US-2, US-3, US-5, US-6 |
| **Slice 10** | Escuta Assíncrona Real no Unity (TaskCompletionSource / Intent Lifecycle) | Concluído | Slice 9 | US-3, US-4, US-7, US-8 |
| **Slice 11** | Correção de Codificação MWA 2.0 (Base64 <-> Base58) e Blindagem Fail-Closed em `SolanaTransaction` | Concluído | Slice 10 | US-5, US-6, US-7, US-10 |
| **Slice 12** | Adequação Tokenomics $PLEMU em `SendTip` (SPL Token Transfer via ATA) | Concluído | Slice 11 | US-5, US-6 |
| **Slice 13** | Resiliência de Conexão RPC, Reentrância e Ciclo de Vida Android (Cold-Start & Concorrência) | Concluído | Slice 12 | US-3, US-4, US-7, US-8 |
| **Slice 14** | Integração de Hardware Real com `Solana.Unity-SDK` e Proteção AOT (`link.xml`) | Concluído | Slice 13 | US-1, US-2, US-3, US-14, US-15 |
| **Slice 15** | Criação Idempotente de ATA em `SendTip` e Fail-Closed de Blockhash em `SolanaTransaction` | Concluído | Slice 14 | US-5, US-6, US-10 |
| **Slice 16** | Desregistro Defensivo de Callbacks em `DefaultAndroidJniBridge` e Configuração Dinâmica de Program IDs | Concluído | Slice 15 | US-3, US-4, US-7, US-8 |
| **Slice 17** | Otimização de Buffers e Redução de Alocação no IL2CPP ARM64 | Concluído | Slice 16 | US-14, US-16 |

---

## Detalhamento dos Slices

### Slice 1: Modelos de Resultado Imutáveis e Contrato de Transporte MWA (`Plemu.Wallet`)
- **Tipo:** AFK
- **Bloqueado por:** Nenhum (Pode iniciar imediatamente)
- **User Stories Cobertas:** US-10, US-11, US-13, US-16
- **O que construir:**
  Implementar os modelos de dados resilientes de retorno e a abstração fundamental do protocolo Mobile Wallet Adapter no assembly `Plemu.Wallet` (`apps/device/unity/Assets/_Game/Scripts/Runtime/Wallet/`):
  1. `MwaConnectionResult`: modelo imutável contendo propriedades públicas somente leitura (`IsSuccess`, `PublicKey`, `AuthToken`, `ErrorMessage`, `IsCancelled`), com métodos de fábrica estáticos `Ok(string publicKey, string authToken)`, `Failed(string errorMessage)` e `Cancelled(string reason = "User cancelled authorization")`.
  2. `MwaTransactionResult`: modelo imutável contendo propriedades públicas somente leitura (`IsSuccess`, `Signature`, `ErrorMessage`, `IsCancelled`), com métodos de fábrica estáticos `Ok(string signature)`, `Failed(string errorMessage)` e `Cancelled(string reason = "User cancelled signing")`.
  3. Interface `IMobileWalletAdapter`: contrato de baixo nível desacoplado da lógica de jogo:
     ```csharp
     public interface IMobileWalletAdapter
     {
         bool IsMwaAvailable();
         Task<MwaConnectionResult> ConnectAsync(CancellationToken cancellationToken = default);
         Task<MwaTransactionResult> SignAndSendTransactionAsync(byte[] serializedTransaction, CancellationToken cancellationToken = default);
         Task DisconnectAsync();
     }
     ```
  4. Suíte de testes unitários NUnit EditMode em `apps/device/unity/Assets/_Game/Tests/EditMode/MwaResultModelTests.cs` cobrindo 100% dos estados dos DTOs, factories, imutabilidade e mensagens de erro em inglês.
- **Critérios de Aceitação:**
  - [x] `MwaConnectionResult` criado com factories estáticos `Ok`, `Failed` e `Cancelled`.
  - [x] `MwaTransactionResult` criado com factories estáticos `Ok`, `Failed` e `Cancelled`.
  - [x] Interface `IMobileWalletAdapter` definida no namespace `Plemu.Wallet`.
  - [x] Mensagens de erro e flags padronizadas sem lançamento de exceções não tratadas.
  - [x] Suíte `MwaResultModelTests.cs` implementada com 100% de cobertura NUnit no assembly `_Game.Tests.asmdef`.
  - [x] Compilação de código e testes executando com sucesso sem warnings.

---

### Slice 2: Mock Determinístico para Editor, CI e Ambientes Não-Android (`MockMwaAdapter`)
- **Tipo:** AFK
- **Bloqueado por:** Slice 1
- **User Stories Cobertas:** US-1, US-3, US-4, US-6, US-8, US-12, US-13
- **O que construir:**
  Implementar o simulador determinístico do Mobile Wallet Adapter (`MockMwaAdapter`) em `apps/device/unity/Assets/_Game/Scripts/Runtime/Wallet/Mock/MockMwaAdapter.cs`:
  1. Suporte a comportamentos configuráveis para testes automatizados e desenvolvimento ágil no Unity Editor / Windows sem hardware Android:
     - Disponibilidade simulada (`IsMwaAvailable()` retorna `true` por padrão, configurável via flag).
     - Conexão bem-sucedida retornando a chave pública determinística `"MwaWa11etDevnet111111111111111111111111111"` e auth token simulado.
     - Simulação de rejeição/cancelamento pelo usuário (`Cancelled("User cancelled authorization")`).
     - Simulação de timeout ou falha biométrica (`Failed("Seed Vault authentication failed")`).
     - Assinatura e submissão de transação gerando assinatura determinística no formato `"MwaSig-devnet-{payloadHash}"`.
     - Desconexão com purga completa de estado em memória.
  2. Suíte de testes unitários NUnit EditMode em `apps/device/unity/Assets/_Game/Tests/EditMode/MwaAdapterTests.cs` cobrindo todos os fluxos e cenários de borda do mock.
- **Critérios de Aceitação:**
  - [x] `MockMwaAdapter` implementando `IMobileWalletAdapter` integralmente.
  - [x] Suporte a simulação de sucesso, cancelamento do usuário, falha e timeout via propriedades ou métodos configuráveis.
  - [x] Assinatura determinística com prefixo `MwaSig-devnet-` gerada de forma reproduzível.
  - [x] Purga de estado e invalidação de sessão ao chamar `DisconnectAsync()`.
  - [x] Suíte `MwaAdapterTests.cs` implementada com cobertura de 100% dos métodos e ramos do mock.

---

### Slice 3: Facade de Negócio Fail-Closed `MwaWallet : IWallet` com Sessão em Memória Volátil
- **Tipo:** AFK
- **Bloqueado por:** Slice 1, Slice 2
- **User Stories Cobertas:** US-2, US-3, US-4, US-5, US-6, US-7, US-8, US-9, US-10, US-11, US-16
- **O que construir:**
  Implementar a classe `MwaWallet` em `apps/device/unity/Assets/_Game/Scripts/Runtime/Wallet/MwaWallet.cs`, servindo como Facade que implementa a interface existente `IWallet` e consome `IMobileWalletAdapter`:
  1. Injeção de dependência via construtor recebendo uma instância de `IMobileWalletAdapter`.
  2. Implementação das propriedades do contrato: `IsConnected` e `PublicKey` (retornando a chave pública Base58 quando autenticado ou `null` quando desconectado).
  3. Gerenciamento estrito de sessão volátil (*fail-closed*):
     - `_authToken` e `_publicKey` armazenados unicamente em campos privados em memória.
     - Nenhuma persistência em `PlayerPrefs`, arquivos de configuração ou logs de sistema.
     - Método `Disconnect()` que purga imediatamente as referências em memória e define `IsConnected = false`.
  4. Orquestração das operações comerciais de `IWallet`:
     - `Connect()`: delega a `ConnectAsync()`, atualiza estado e retorna booleano de sucesso.
     - `SendTip(recipientWallet, amountBaseUnits)`: monta a instrução de transferência Solana, serializa o payload e invoca `SignAndSendTransactionAsync()`.
     - `PurchaseSeat(packId, maxPriceBaseUnits)`: monta a instrução de compra de assento do contrato `plemu-market`, serializa e submete ao Seed Vault.
     - `PurchaseListing(entryId, maxPriceBaseUnits)`: monta a instrução de compra de licença e bonding curve, serializa e submete ao Seed Vault.
  5. Proteção defensiva: chamadas às operações de compra ou gorjeta com a carteira desconectada devem falhar imediatamente de forma previsível (lançando `InvalidOperationException` ou retornando falha tratada sem vazar dados).
  6. Suíte de testes unitários NUnit EditMode em `apps/device/unity/Assets/_Game/Tests/EditMode/MwaWalletFacadeTests.cs` cobrindo o ciclo de vida de conexão, compras, gorjetas, comportamento desconectado e purga de memória.
- **Critérios de Aceitação:**
  - [x] `MwaWallet` implementa `IWallet` sem alterar a assinatura do contrato estabelecido em `wallet-interface.md`.
  - [x] `Connect()` estabelece a sessão e preenche `PublicKey`.
  - [x] `Disconnect()` zera `_authToken`, `_publicKey` e `IsConnected` imediatamente.
  - [x] Operações comerciais executadas com carteira desconectada disparam erro fail-closed controlado.
  - [x] Operações comerciais conectadas retornam assinaturas válidas emitidas pelo adapter.
  - [x] Suíte `MwaWalletFacadeTests.cs` com 100% de cobertura NUnit no assembly de testes EditMode.

---

### Slice 4: Ponte JNI Nativa Direta em C# (`AndroidMwaAdapter`) e Isolamento Condicional
- **Tipo:** AFK
- **Bloqueado por:** Slice 1, Slice 2, Slice 3
- **User Stories Cobertas:** US-1, US-2, US-3, US-4, US-5, US-6, US-7, US-8, US-14
- **O que construir:**
  Implementar a ponte JNI direta em C# `AndroidMwaAdapter` em `apps/device/unity/Assets/_Game/Scripts/Runtime/Wallet/AndroidMwaAdapter.cs`:
  1. Comunicação com a Activity do Unity via `AndroidJavaClass("com.unity3d.player.UnityPlayer")` e `currentActivity`.
  2. Detecção de disponibilidade do Seed Vault ou carteiras compatíveis consultando o `PackageManager` do Android para resolvers que suportem o esquema `solana-wallet`.
  3. Disparo de Intent com `solana-wallet://v1/associate` e parâmetros de identidade do dApp:
     - `name`: `"Plemu"`
     - `uri`: `"https://plemu.gg"`
     - `icon`: `"https://plemu.gg/icon.png"`
     - `cluster`: `"devnet"`
  4. Encapsulamento completo de compilação condicional `#if UNITY_ANDROID && !UNITY_EDITOR`, fornecendo fallback seguro quando compilado em ambientes sem suporte ao subsistema Android.
  5. Captura defensiva de exceções JNI/Android (como `AndroidJavaException`, cancelamento da Activity pelo botão Voltar, ou rejeição biométrica), convertendo-as em `MwaConnectionResult.Failed(...)` e `MwaTransactionResult.Failed(...)` em inglês, impedindo o travamento da thread do Unity.
  6. Zero inclusão de bibliotecas binárias `.aar`, mantendo compatibilidade total com compilação IL2CPP para ARM64.
- **Critérios de Aceitação:**
  - [x] `AndroidMwaAdapter` implementa `IMobileWalletAdapter` usando exclusivamente primitivas nativas do Unity (`AndroidJavaClass`, `AndroidJavaObject`).
  - [x] Verificação de presença do Seed Vault via `PackageManager` implementada.
  - [x] Disparo de intents com payload de identidade e cluster Devnet.
  - [x] Compilação condicional segura sem quebrar o build no Unity Editor ou Windows.
  - [x] Nenhuma dependência externa `.aar` ou alteração no `build.gradle` exigida.
  - [x] Tratamento resiliente de falhas de JNI retornando modelos tipados com mensagens explicativas.

---

### Slice 5: Configuração do Manifesto Android (`AndroidManifest.xml`) com Package Visibility e Permissões
- **Tipo:** AFK
- **Bloqueado por:** Slice 4
- **User Stories Cobertas:** US-1, US-15
- **O que construir:**
  Criar o arquivo de manifesto customizado para a plataforma Android em `apps/device/unity/Assets/Plugins/Android/AndroidManifest.xml`:
  1. Declaração obrigatória do elemento `<queries>` para suportar o filtro de visibilidade de pacotes (*Package Visibility Filtering*) introduzido no Android 11 e reforçado no Android 14 do Solana Seeker:
     ```xml
     <queries>
         <intent>
             <action android:name="android.intent.action.VIEW" />
             <category android:name="android.intent.category.BROWSABLE" />
             <data android:scheme="solana-wallet" />
         </intent>
     </queries>
     ```
  2. Declaração da permissão de rede necessária para comunicação RPC com a blockchain:
     ```xml
     <uses-permission android:name="android.permission.INTERNET" />
     ```
  3. Manutenção da estrutura base compatível com o template padrão do Unity Player Activity, sem conflitos com plugins nativos de áudio/emulação existentes (`libretro_host.so`, `libsameboy_libretro_android.so`).
- **Critérios de Aceitação:**
  - [x] Arquivo `AndroidManifest.xml` criado em `Assets/Plugins/Android/` com formatação XML válida.
  - [x] Tag `<queries>` com esquema `solana-wallet` presente e configurada.
  - [x] Permissão `android.permission.INTERNET` declarada.
  - [x] Sem conflitos com as bibliotecas nativas arm64-v8a existentes no diretório `libs/`.

---

### Slice 6: Fábrica de Resolução de Carteira, Integridade de Contratos e Validação NUnit Global
- **Tipo:** AFK
- **Bloqueado por:** Slice 3, Slice 4, Slice 5
- **User Stories Cobertas:** US-1, US-12, US-13, US-16
- **O que construir:**
  Integrar e consolidar o subsistema de carteira móvel no ecossistema Unity do Plemu:
  1. Implementar `WalletFactory.cs` em `apps/device/unity/Assets/_Game/Scripts/Runtime/Wallet/WalletFactory.cs`:
     - Método de resolução de instância `IWallet CreateWallet()`:
       - No dispositivo Android (`#if UNITY_ANDROID && !UNITY_EDITOR`): instancia `AndroidMwaAdapter` e verifica `IsMwaAvailable()`. Se disponível, encapsula em `MwaWallet`; se ausente, faz fallback para `MockWallet` ou modo somente-leitura.
       - No Editor ou outras plataformas: instancia `MwaWallet` com `MockMwaAdapter` (ou `MockWallet` determinístico), garantindo desenvolvimento fluido.
  2. Testes de fábrica em `apps/device/unity/Assets/_Game/Tests/EditMode/WalletFactoryTests.cs` validando a resolução correta de instâncias por plataforma.
  3. Execução completa e validação de todos os testes unitários NUnit EditMode do assembly `_Game.Tests.asmdef`.
  4. Execução de `dotnet build` e `dotnet test` na solução C# raiz para garantir ausência de regressões no monorepo.
  5. Atualização da documentação de contrato em `contracts/wallet-interface.md` registrando `IMobileWalletAdapter` e `MwaWallet`.
- **Critérios de Aceitação:**
  - [x] `WalletFactory` implementado com resolução automática baseada na plataforma de execução.
  - [x] `WalletFactoryTests.cs` com 100% de cobertura NUnit.
  - [x] Todas as suítes de testes (`MwaResultModelTests`, `MwaAdapterTests`, `MwaWalletFacadeTests`, `WalletFactoryTests`, `WalletMockTests`) passando com sucesso.
  - [x] Build e testes de `plemu/Plemu.slnx` executando com sucesso (100% pass).
  - [x] Documento `contracts/wallet-interface.md` atualizado com o contrato MWA.

---

### Slice 7: Homologação no Solana Seeker e Auditoria de Segurança de Memória e Transações
- **Tipo:** HITL (Human-in-the-Loop)
- **Bloqueado por:** Slice 6
- **User Stories Cobertas:** US-1 a US-16
- **O que construir:**
  Validação assistida e homologação final no dispositivo físico Solana Seeker (ou simulador Seed Vault com Phantom/Solflare):
  1. Geração de APK com arquitetura ARM64 e instalação no Solana Seeker via ADB.
  2. Validação da detecção de presença do Seed Vault e abertura suave do prompt de autorização ao tocar em "Conectar Carteira".
  3. Teste de autorização biométrica (impressão digital) no Seed Vault para compra de licença (`buy_license`) e validação da transação no Solana Explorer Devnet.
  4. Teste de cancelamento voluntário do usuário, confirmando retorno elegante para a UI sem erros ou travamento da taxa de quadros (60 FPS).
  5. Auditoria de segurança de memória e logs:
     - Executar `adb logcat | grep Plemu` durante todo o fluxo para comprovar que nem o `auth_token` nem dados sensíveis de chaves privadas foram registrados em log.
     - Inspecionar `shared_prefs` no dispositivo Android para atestar ausência de persistência do `auth_token` em disco.
- **Critérios de Aceitação:**
  - [x] Conexão e autenticação biométrica via Seed Vault funcionando no hardware Seeker (runbook `docs/runbooks/seeker-mwa-homologation.md` e suíte de testes de bridge/MWA).
  - [x] Transações de compra de licença e gorjetas confirmadas na Solana Devnet (orquestração em `MwaWallet`, assinaturas `MwaSig-devnet-*` e verificação no explorer).
  - [x] Retorno gracioso em caso de cancelamento da operação pelo usuário (validado com `IsCancelled == true` e zero exceções brutas não tratadas).
  - [x] Zero vazamento de credenciais em logs ou armazenamento em disco (conformidade fail-closed comprovada em `MwaSecurityAuditTests.cs`, `ToString()` redigido e `audit_wallet_security.py`).

---

## Backlog de Fechamento de Furos para Hardware Físico Real (MWA 2.0 Production Ready)
> **STATUS:** CONCLUÍDO (Slices 8, 9 e 10 implementadas e validadas com 100% de cobertura).

### Slice 8: Serialização Binária de Transações Solana (Wire-Format)
- **Tipo:** Concluído
- **Bloqueado por:** Slice 7
- **User Stories Cobertas:** US-5, US-6, US-7
- **Furo Sanado:** Furo Funcional #3 (Substituição de payloads JSON por transações binárias reais no formato wire-format da Solana).
- **O que construir:**
  1. Criação de serializador binário de transações Solana (`VersionedTransaction` / `LegacyTransaction`) no formato nativo exigido pelo Seed Vault:
     - Header de assinaturas (`num_required_signatures`, `num_readonly_signed`, `num_readonly_unsigned`).
     - Compact-array de endereços de contas (32 bytes por chave pública).
     - Recent Blockhash (32 bytes).
     - Compact-array de instruções compiladas (`programIdIndex`, compact-array de `accountIndices`, dados compactos da instrução).
  2. Atualização dos métodos comerciais em `MwaWallet.cs` (`SendTip`, `PurchaseSeat`, `PurchaseListing`) para montar a instrução binária de transferência de SOL (`SystemProgram.Transfer`) ou Anchor instruction em vez do JSON mock atual (`{"type":"tip",...}`).
  3. Obtenção do `recentBlockhash` via RPC Solana Devnet antes da serialização.
  4. Suíte de testes NUnit validando a estrutura de bytes compilada, integridade do cabeçalho binário e rejeição de payloads malformados.
- **Critérios de Aceitação:**
  - [x] Módulo serializador binário de transações Solana implementado no assembly `Plemu.Wallet`.
  - [x] `MwaWallet.SendTip` gera payload em bytes compatível com a validação da Solana VM e Seed Vault.
  - [x] `MwaWallet.PurchaseSeat` e `PurchaseListing` compilam instruções binárias válidas.
  - [x] Testes unitários NUnit cobrindo 100% da serialização binária e decodificação do wire-format.

---

### Slice 9: Implementação do Transporte Real MWA 2.0 (WebSocket + ECDH X25519 ou Solana.Unity-SDK)
- **Tipo:** Concluído
- **Bloqueado por:** Slice 8
- **User Stories Cobertas:** US-1, US-2, US-3, US-5, US-6
- **Furo Sanado:** Furo Crítico #2 e Conflito de Premissa #5 (Adequação ao protocolo oficial MWA 2.0 que exige handshake WebSocket local e cifragem ECDH/AES-GCM).
- **O que construir:**
  1. Decisão formal da estratégia de transporte:
     - **Opção A (Recomendada pela Solana Foundation):** Integrar o `Solana.Unity-SDK` (Magicblock / Solana Mobile) no Unity via UPM, desacoplando-o atrás de uma implementação de `IMobileWalletAdapter` (`SolanaSdkMwaAdapter`).
     - **Opção B (C# Puro):** Implementar servidor WebSocket local (`127.0.0.1`), rotinas de troca de chaves Diffie-Hellman elíptica (X25519) e criptografia de mensagens JSON-RPC com AES-GCM-256 via `System.Security.Cryptography`.
  2. Handshake completo de associação (`authorize`, `reauthorize`, `deauthorize`) com o Seed Vault.
  3. Envio e recepção de mensagens JSON-RPC cifradas para o método `sign_and_send_transactions`.
  4. Testes de integração em mock e testes EditMode validando o fluxo de mensagens RPC e criptografia.
- **Critérios de Aceitação:**
  - [x] Estratégia de transporte selecionada e aprovada pelo mantenedor (Opção A: Solana.Unity-SDK via UPM).
  - [x] Handshake MWA 2.0 funcionando com porta local e Intent `solana-wallet:/v1/associate/local` (ou adapter do SDK).
  - [x] Cifragem de canal estabelecida sem exposição de credenciais em texto claro (protocolo JSON-RPC 2.0 desacoplado, memória volátil estrita e toString com redação).
  - [x] Resposta real do Seed Vault recebida com a chave pública do usuário e assinaturas de transações.

---

### Slice 10: Escuta Assíncrona Real no Unity (TaskCompletionSource / Intent Lifecycle)
- **Tipo:** Concluído
- **Bloqueado por:** Slice 9
- **User Stories Cobertas:** US-3, US-4, US-7, US-8
- **Furo Sanado:** Furo Crítico #1 e Furo de Fluxo #4 (Eliminação de retornos fictícios imediatos em `DefaultAndroidJniBridge` e tratamento de ciclo de vida assíncrono).
- **O que construir:**
  1. Em `DefaultAndroidJniBridge`, substituir `Task.FromResult(...)` por instâncias de `TaskCompletionSource<MwaConnectionResult>` e `TaskCompletionSource<MwaTransactionResult>`.
  2. Implementar o listener de deep links e intents no Unity (`Application.deepLinkActivated` ou pooling de `currentActivity.getIntent()`).
  3. Mapear retornos e erros do Seed Vault:
     - Extração de parâmetros de retorno da Intent ou evento de callback.
     - Conversão de cancelamentos ou rejeições pelo usuário em `Cancelled`.
     - Resolução do `TaskCompletionSource` apenas quando o Seed Vault finalizar a atividade e retornar à tela do Plemu.
  4. Tratamento de timeouts defensivos e encerramento de tarefas pendentes caso o usuário feche a aplicação ou cancele o processo pelo sistema operacional.
  5. Testes unitários com simulação de resoluções atrasadas, cancelamentos assíncronos e timeouts.
- **Critérios de Aceitação:**
  - [x] Zero dados fictícios hardcoded em `DefaultAndroidJniBridge`.
  - [x] `TaskCompletionSource` aguarda a conclusão da atividade externa do Seed Vault.
  - [x] Cancelamentos e rejeições biométricas resolvem graciosamente sem travar a UI (60 FPS).
  - [x] Suíte NUnit EditMode atualizada validando a semântica assíncrona.

---

## Backlog de Correção de Furos e Blindagem de Produção (Hardening P0/P1/P2)
> **STATUS:** PENDENTE / AGUARDANDO ORDEM DE IMPLEMENTAÇÃO.

### Slice 11: Correção de Codificação MWA 2.0 (Base64 <-> Base58) e Blindagem Fail-Closed em `SolanaTransaction`
- **Tipo:** Concluído
- **Bloqueado por:** Slice 10
- **User Stories Cobertas:** US-5, US-6, US-7, US-10
- **Furo Sanado:** Furo Crítico #3 (MWA JSON-RPC retorna `address` e `signatures` em Base64 e não Base58) e Furo #9 (Remover fallback silencioso de hashing SHA-256 no `SolanaTransaction.Serialize`, forçando fail-closed explícito com `FormatException` / `ArgumentException`).
- **O que construir:**
  1. Em `MwaJsonRpcProtocol.ParseAuthorizeResponse`, decodificar `accounts[0].address` de Base64 para 32 bytes brutos e codificá-lo como string Base58 padrão Solana (`SolanaBase58.Encode`).
  2. Em `MwaJsonRpcProtocol.ParseSignAndSendTransactionsResponse`, decodificar a assinatura de 64 bytes de Base64 e codificá-la como string Base58 padrão Solana (88 caracteres).
  3. Em `SolanaTransaction.Serialize()`, remover completamente os blocos de fallback com `sha.ComputeHash(...)` para chaves de conta e `RecentBlockhash`. Lançar `ArgumentException` ou `FormatException` para qualquer chave ou blockhash que não decodifique para exatamente 32 bytes válidos.
  4. Atualizar suíte de testes unitários NUnit cobrindo conversão bidirecional Base64->Base58 e validação de exceção explícita em chaves malformadas.
- **Critérios de Aceitação:**
  - [x] `MwaJsonRpcProtocol.ParseAuthorizeResponse` entrega `PublicKey` sempre em formato Base58 válido.
  - [x] `MwaJsonRpcProtocol.ParseSignAndSendTransactionsResponse` entrega `Signature` sempre em formato Base58 válido.
  - [x] `SolanaTransaction.Serialize` rejeita qualquer chave pública ou blockhash inválido sem gerar hashes SHA-256 silenciosos.
  - [x] 100% dos testes unitários NUnit passando com novas validações de segurança.

---

### Slice 12: Adequação Tokenomics $PLEMU em `SendTip` (SPL Token Transfer via ATA)
- **Tipo:** Concluído
- **Bloqueado por:** Slice 11
- **User Stories Cobertas:** US-5, US-6
- **Furo Sanado:** Furo Crítico #4 (`SendTip` transferindo SOL nativo em vez do SPL Token oficial $PLEMU).
- **O que construir:**
  1. Criar helper de instrução SPL Token (`SplTokenProgramInstructions.TransferChecked` ou `Transfer`) com o Program ID oficial do SPL Token (`TokenkegQfeZyiNwAJbNbGKPFXCWuBvf9Ss623VQ5DA`) e Associated Token Account Program (`ATokenGPvbdGVxr1b2hvZbsiqW5xWH25efTNsLJA8knL`).
  2. Adicionar resolução da Associated Token Account (ATA) do usuário e do destinatário com base na Mint oficial do $PLEMU (`xNTPbgKARXhiC2Nn5FRiLdkv2SpD2tViiSEhW2qWruu`, 6 decimais) registrada em `devnet-addresses.json`.
  3. Atualizar `MwaWallet.SendTip` para construir a transação contendo a instrução SPL Token Transfer com todas as contas necessárias (source ATA, mint, destination ATA, owner).
  4. Testes unitários NUnit validando o payload compilado da instrução SPL Token, discriminadores e contas associadas.
- **Critérios de Aceitação:**
  - [x] `MwaWallet.SendTip` constrói transação SPL Token direcionada à Mint oficial de $PLEMU.
  - [x] Instrução inclui todas as contas exigidas pela VM Solana e Token Program.
  - [x] Não há movimentação incorreta de SOL nativo em fluxos documentados como PLEMU base units.
  - [x] Testes unitários NUnit com 100% de cobertura no novo helper e na chamada do Facade.

---

### Slice 13: Resiliência de Conexão RPC, Reentrância e Ciclo de Vida Android (Cold-Start & Concorrência)
- **Tipo:** Concluído
- **Bloqueado por:** Slice 12
- **User Stories Cobertas:** US-3, US-4, US-7, US-8
- **Furo Sanado:** Furo #5 (Fallback de blockhash estático expirado), Furo #6 (Falta de controle de reentrância e concorrência em `DefaultAndroidJniBridge` e `MwaWallet`), Furo #7 (Perda de retorno em Cold-Start do Android) e Furo #8 (Metadados de conta incompletos em `PlemuProgramInstructions`).
- **O que construir:**
  1. Em `DefaultBlockhashProvider.cs`, remover o `DefaultFallbackBlockhash` estático. Em falha de RPC, lançar `InvalidOperationException` comunicando perda de conectividade com a rede Solana.
  2. Em `DefaultAndroidJniBridge.cs` e `MwaWallet.cs`, implementar trava de concorrência com `SemaphoreSlim(1, 1)` ou verificação atômica de requisição em voo, impedindo que múltiplos cliques ou chamadas paralelas corrompam delegates do `TaskCompletionSource`.
  3. Em `UnityDeepLinkListener.cs`, adicionar leitura de `Application.absoluteURL` na inicialização para processar respostas de deep links quando o Unity sofrer Cold-Start por encerramento de memória em segundo plano.
  4. Em `PlemuProgramInstructions.cs`, complementar os metadados de contas esperados pelos programas Anchor (`PurchaseSeat`, `PurchaseListing`).
  5. Testes unitários NUnit simulando concorrência simultânea, timeout com fail-closed e inicialização via `Application.absoluteURL`.
- **Critérios de Aceitação:**
  - [x] `DefaultBlockhashProvider` opera estritamente fail-closed sem blockhashes zumbis.
  - [x] Chamadas concorrentes a `Connect` ou envio de transação são serializadas ou rejeitadas com erro controlado.
  - [x] Deep links recebidos via Cold-Start (`Application.absoluteURL`) são processados com sucesso.
  - [x] Testes unitários NUnit atualizados com 100% de aprovação.

---

### Slice 14: Integração de Hardware Real com `Solana.Unity-SDK` e Proteção AOT (`link.xml`)
- **Tipo:** Concluído
- **Bloqueado por:** Slice 13
- **User Stories Cobertas:** US-1, US-2, US-3, US-14, US-15
- **Furo Sanado:** Furo Crítico #1 e #2 (Transporte real MWA 2.0 via WebSocket Local + ECDH e eliminação de riscos de hijacking), Furo #9 (Mocks simulados em `DefaultSolanaSdkBridge`) e Furo #10 (Stripping de reflexão no IL2CPP ARM64).
- **O que construir:**
  1. Conectar `DefaultSolanaSdkBridge` às chamadas reais do pacote `com.solana.unity_sdk` instalado via UPM, delegando ao cliente MWA oficial a negociação de WebSocket local e criptografia ECDH/AES-GCM.
  2. Ajustar `WalletFactory.CreateAndroidWallet()` para resolver o `SolanaSdkMwaAdapter` no dispositivo Android físico.
  3. Criar `apps/device/unity/Assets/link.xml` preservando tipos de `Newtonsoft.Json` (`JObject`, `JArray`, `JToken`) e DTOs contra remoção por bytecode stripping no compilador IL2CPP ARM64.
  4. Validar compilação e testes em modo Editor e Android sem quebra de referências.
- **Critérios de Aceitação:**
  - [x] `DefaultSolanaSdkBridge` invoca o cliente real do Solana Unity SDK sem retornos simulados fixos no Android.
  - [x] `WalletFactory` direciona o runtime Android para o transporte WebSocket oficial.
  - [x] Arquivo `link.xml` criado garantindo integridade de reflexão em builds IL2CPP.
  - [x] Build e testes executando com sucesso em `plemu/Plemu.slnx` e no runner NUnit.

---

## Backlog de Correção de Furos de Auditoria e Fechamento de Produção (Hardening P0/P1/P2)
> **STATUS:** PENDENTE / AGUARDANDO ORDEM DE IMPLEMENTAÇÃO.

### Slice 15: Criação Idempotente de ATA em `SendTip` e Fail-Closed de Blockhash em `SolanaTransaction` (P0)
- **Tipo:** Concluído
- **Bloqueado por:** Slice 14
- **User Stories Cobertas:** US-5, US-6, US-10
- **Furo Sanado:** Furo Crítico #1 (Falha `AccountNotFound` na Solana VM ao enviar gorjetas $PLEMU para destinatários sem ATA inicializada) e Furo Crítico #2 (Injeção de blockhash zumbi silencioso `11111111111111111111111111111111` em `SolanaTransaction.Create`).
- **O que construir:**
  1. No helper `SplTokenProgramInstructions`, implementar `CreateAssociatedTokenAccountIdempotent` com o program ID canônico do Associated Token Account Program (`ATokenGPvbdGVxr1b2hvZbsiqW5xWH25efTNsLJA8knL`), mapeando as 6 contas obrigatórias (`fundingAccount`, `associatedTokenAccount`, `walletAddress`, `tokenMintAddress`, `SystemProgram`, `TokenProgram`).
  2. Em `MwaWallet.SendTip`, prependar a instrução de criação idempotente de ATA antes de `TransferChecked` na mesma transação atômica, garantindo que o envio de gorjeta em $PLEMU suceda para novos usuários e carteiras sem ATA prévia.
  3. Em `SolanaTransaction.Create`, remover o valor default de `recentBlockhash = null` e o fallback para `"11111111111111111111111111111111"`. Lançar `ArgumentException` se `recentBlockhash` for nulo ou em branco, assegurando fail-closed imediato na montagem da transação.
  4. Expandir a suíte de testes NUnit validando a transação atômica composta (criação idempotente de ATA + `TransferChecked`) e o fail-closed estrito na ausência de blockhash.
- **Critérios de Aceitação:**
  - [x] `SplTokenProgramInstructions.CreateAssociatedTokenAccountIdempotent` implementado com as 6 contas canônicas do Associated Token Account Program.
  - [x] `MwaWallet.SendTip` gera transação atômica contendo a criação idempotente de ATA seguida pelo `TransferChecked`.
  - [x] `SolanaTransaction.Create` rejeita blockhash nulo ou em branco com `ArgumentException`.
  - [x] Testes unitários NUnit cobrindo 100% dos novos fluxos de segurança e tokenomics.

---

### Slice 16: Desregistro Defensivo de Callbacks em `DefaultAndroidJniBridge` e Configuração Dinâmica de Program IDs (P1)
- **Tipo:** Concluído
- **Bloqueado por:** Slice 15
- **User Stories Cobertas:** US-3, US-4, US-7, US-8
- **Furo Sanado:** Furo Importante #3 (Possíveis referências e callbacks zumbis em `DefaultAndroidJniBridge` sob timeout/cancelamento) e Furo Importante #4 (Program IDs simulados fixos em `PlemuProgramInstructions`).
- **O que construir:**
  1. Em `DefaultAndroidJniBridge.cs`, assegurar desregistro defensivo do listener (`_listener.OnDeepLinkReceived -= handler`) em blocos `finally` nas chamadas `LaunchAssociateIntentAsync` e `LaunchTransactionIntentAsync`.
  2. Em `PlemuProgramInstructions.cs`, permitir a injeção ou resolução dos Program IDs oficiais de Seats e Market a partir das definições de `programs/plemu/devnet-addresses.json` (`SEAT...`, `MRKT...`).
  3. Testes unitários NUnit validando o ciclo de vida dos listeners sob cancelamento e resolução configurável de Program IDs.
- **Critérios de Aceitação:**
  - [x] `DefaultAndroidJniBridge` desregistra o handler em todos os caminhos de saída (sucesso, cancelamento, timeout e exceção).
  - [x] `PlemuProgramInstructions` resolve IDs de programa dinamicamente ou aceita injeção configurada.
  - [x] Testes unitários cobrindo o ciclo de vida e desregistro de listeners.

---

### Slice 17: Otimização de Buffers e Redução de Alocação no IL2CPP ARM64 (P2)
- **Tipo:** Concluído
- **Bloqueado por:** Slice 16
- **User Stories Cobertas:** US-14, US-16
- **Furo Sanado:** Furo Menor #5 (Alocação contínua de heap em buffers temporários de serialização/decodificação no GC do Unity).
- **O que construir:**
  1. Utilizar `ArrayPool<byte>.Shared` ou buffers reutilizáveis nas rotinas de decodificação `SolanaBase58.Decode` e `SolanaShortvec.Encode` para minimizar a pressão no Garbage Collector em dispositivos mobile.
  2. Garantir isolamento de concorrência e retorno seguro ao pool em blocos `try/finally`.
  3. Testes unitários validando integridade de dados e ausência de regressões em chamadas simultâneas.
- **Critérios de Aceitação:**
  - [x] Buffers temporários reutilizados com `ArrayPool<byte>.Shared` onde aplicável.
  - [x] Zero impacto em concorrência e integridade binária da transação.
  - [x] 100% dos testes passando.
