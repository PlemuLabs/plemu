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
| **Slice 8** | Serialização Binária de Transações Solana (Wire-Format) | AFK (Pendente) | Slice 7 | US-5, US-6, US-7 |
| **Slice 9** | Implementação do Transporte Real MWA 2.0 (WebSocket + ECDH X25519 ou Solana.Unity-SDK) | AFK (Pendente) | Slice 8 | US-1, US-2, US-3, US-5, US-6 |
| **Slice 10** | Escuta Assíncrona Real no Unity (TaskCompletionSource / Intent Lifecycle) | AFK (Pendente) | Slice 9 | US-3, US-4, US-7, US-8 |

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
> **STATUS:** PENDENTE / AGUARDANDO ORDEM DO USUÁRIO PARA INÍCIO DA IMPLEMENTAÇÃO.

### Slice 8: Serialização Binária de Transações Solana (Wire-Format)
- **Tipo:** AFK (Aguardando ordem para implementar)
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
  - [ ] Módulo serializador binário de transações Solana implementado no assembly `Plemu.Wallet`.
  - [ ] `MwaWallet.SendTip` gera payload em bytes compatível com a validação da Solana VM e Seed Vault.
  - [ ] `MwaWallet.PurchaseSeat` e `PurchaseListing` compilam instruções binárias válidas.
  - [ ] Testes unitários NUnit cobrindo 100% da serialização binária e decodificação do wire-format.

---

### Slice 9: Implementação do Transporte Real MWA 2.0 (WebSocket + ECDH X25519 ou Solana.Unity-SDK)
- **Tipo:** AFK (Aguardando ordem para implementar)
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
  - [ ] Estratégia de transporte selecionada e aprovada pelo mantenedor.
  - [ ] Handshake MWA 2.0 funcionando com porta local e Intent `solana-wallet:/v1/associate/local` (ou adapter do SDK).
  - [ ] Cifragem de canal estabelecida sem exposição de credenciais em texto claro.
  - [ ] Resposta real do Seed Vault recebida com a chave pública do usuário e assinaturas de transações.

---

### Slice 10: Escuta Assíncrona Real no Unity (TaskCompletionSource / Intent Lifecycle)
- **Tipo:** AFK (Aguardando ordem para implementar)
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
  - [ ] Zero dados fictícios hardcoded em `DefaultAndroidJniBridge`.
  - [ ] `TaskCompletionSource` aguarda a conclusão da atividade externa do Seed Vault.
  - [ ] Cancelamentos e rejeições biométricas resolvem graciosamente sem travar a UI (60 FPS).
  - [ ] Suíte NUnit EditMode atualizada validando a semântica assíncrona.
