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
  - [ ] `AndroidMwaAdapter` implementa `IMobileWalletAdapter` usando exclusivamente primitivas nativas do Unity (`AndroidJavaClass`, `AndroidJavaObject`).
  - [ ] Verificação de presença do Seed Vault via `PackageManager` implementada.
  - [ ] Disparo de intents com payload de identidade e cluster Devnet.
  - [ ] Compilação condicional segura sem quebrar o build no Unity Editor ou Windows.
  - [ ] Nenhuma dependência externa `.aar` ou alteração no `build.gradle` exigida.
  - [ ] Tratamento resiliente de falhas de JNI retornando modelos tipados com mensagens explicativas.

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
  - [ ] Arquivo `AndroidManifest.xml` criado em `Assets/Plugins/Android/` com formatação XML válida.
  - [ ] Tag `<queries>` com esquema `solana-wallet` presente e configurada.
  - [ ] Permissão `android.permission.INTERNET` declarada.
  - [ ] Sem conflitos com as bibliotecas nativas arm64-v8a existentes no diretório `libs/`.

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
  - [ ] `WalletFactory` implementado com resolução automática baseada na plataforma de execução.
  - [ ] `WalletFactoryTests.cs` com 100% de cobertura NUnit.
  - [ ] Todas as suítes de testes (`MwaResultModelTests`, `MwaAdapterTests`, `MwaWalletFacadeTests`, `WalletFactoryTests`, `WalletMockTests`) passando com sucesso.
  - [ ] Build e testes de `plemu/Plemu.slnx` executando com sucesso (100% pass).
  - [ ] Documento `contracts/wallet-interface.md` atualizado com o contrato MWA.

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
  - [ ] Conexão e autenticação biométrica via Seed Vault funcionando no hardware Seeker.
  - [ ] Transações de compra de licença e gorjetas confirmadas na Solana Devnet.
  - [ ] Retorno gracioso em caso de cancelamento da operação pelo usuário.
  - [ ] Zero vazamento de credenciais em logs ou armazenamento em disco (conformidade fail-closed comprovada).
