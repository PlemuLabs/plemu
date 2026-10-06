# Design: Subsistema de Monetização e Licenciamento On-Chain (Plemu Store)

- **Data:** 2026-10-05
- **Status:** Proposto / Em Revisão
- **Escopo:** Especificação técnica e arquitetural completa para suporte às 5 modalidades de monetização de jogos (Gratuito, Compra Individual, Demo + Compra, Pague Quanto Quiser e Gorjetas) no ecossistema Plemu (Console PSG1, Cloudflare Worker Gateway e Contratos Solana).

---

## 1. Contexto e Motivação

O ecossistema Plemu opera sob o princípio de que *"o emulador é o runtime; a escada de distribuição é o negócio"*. Atualmente, o protocolo possui uma robusta infraestrutura de integridade de conteúdo baseada em manifestos assinados por Ed25519 e verificação de assentos de packs privados (`plemu-seats`).

Contudo, para criadores independentes de jogos retrô e desenvolvedores de *homebrew*, é necessário fornecer um trilho completo de monetização descentralizada na loja pública, espelhando os modelos consagrados de mercado (*itch.io* / *Steam*), mas com liquidação imediata e taxas mínimas na rede Solana.

Este documento formaliza a arquitetura das **5 modalidades de monetização** definidas para criadores e jogadores:
1. **Gratuito (`free`):** Abre o jogo e joga.
2. **Compra Individual (`buy_once`):** Paga uma vez e desbloqueia o jogo na biblioteca.
3. **Demo + Compra (`demo_full`):** Experimenta uma versão demo gratuitamente e adquire o jogo completo.
4. **Pague Quanto Quiser (`pwyw`):** O jogador escolhe o valor de pagamento, respeitando um piso mínimo definido pelo criador.
5. **Gorjeta (`tip_only`):** Apoio voluntário e instantâneo ao desenvolvedor de um jogo gratuito.

---

## 2. Decisões Arquiteturais Fundamentais (ADRs)

### 2.1 Modelo Híbrido de Entrega: Licença PDA vs. Cartucho NFT
- **Licença Digital Não-Transferível (`pda_license`):**
  - Conta PDA leve derivada na Solana: `[b"receipt", artifact_sha256, buyer_pubkey]`.
  - Custo de rent exemption único de ~0.00146 SOL.
  - Indicado para criadores casuais ou utilitários que desejam o menor atrito possível.
- **Cartucho Digital Colecionável (`nft_cartridge`):**
  - Emissão de ativo digital via padrão **Metaplex Core** (`mpl-core`).
  - Metadados apontam para o hash SHA-256 exato do binário auditado.
  - Habilita posse real, edições limitadas e revenda em mercado secundário (ex: Magic Eden / Tensor) com *royalties* perpétuos programados para os autores.

### 2.2 Política de Moedas e Taxa de Plataforma (Take Rate)
- **Multi-Moeda Determinística:** Suporte nativo e simultâneo a **USDC** (SPL Token de 6 casas decimais) e **SOL** nativo (9 casas decimais), com preços fixados explicitamente pelo criador no manifesto, eliminando dependência de oráculos externos voláteis (ex: Pyth).
- **Taxa de Venda (5%):** Em compras individuais e *Pague Quanto Quiser*, a tesouraria da Plemu retém exatamente 500 bps (5%) para sustentar a infraestrutura da rede e auditoria de cores.
- **Gorjetas Livres de Taxa (0%):** Gorjetas enviadas diretamente aos criadores têm 0% de comissão retida pela plataforma.
- **Gorjeta Opcional para a Plataforma:** No checkout, o jogador pode opcionalmente marcar uma caixinha para adicionar uma contribuição voluntária à tesouraria da Plemu.

### 2.3 Divisão de Receita (Splits On-Chain) e Linhagem de Romhacks
- **Divisão Atômica (Push):** No ato do pagamento, os 95% líquidos do criador são encaminhados para o cofre do programa `plemu-splits` da obra.
- **Linhagem Derivada Obrigatória:** Se o jogo for uma modificação/romhack ou tradução, a divisão herda a chave do autor base original. Gorjetas em jogos derivados também alimentam o cofre compartilhado para impedir evasão de direitos autorais.

### 2.4 Experiência "Demo + Compra" e Isolamento de Saves
- **Card Único na Biblioteca:** A vitrine e a biblioteca exibem um único card. Usuários sem compra visualizam o botão primário `[Jogar Demo]` e o secundário `[Comprar Completo]`.
- **Binários Desacoplados:** A Demo e o Jogo Completo são entradas de manifesto separadas (`<slug>-demo` e `<slug>`), garantindo que os cores libretro nunca recebam código híbrido ou flags de DRM intrusivas.
- **Isolamento e Migração Segura de Saves:** Saves residem em pastas isoladas (`/saves/<slug>-demo/` e `/saves/<slug>/`). Se o criador declarar `"compatibleSave": true`, o runtime copia com segurança o save da demo para a pasta completa no primeiro lançamento, desde que a pasta de destino esteja vazia.

---

## 3. Extensão do Schema do Manifesto (`manifest-entry.schema.json`)

Toda a política de monetização é serializada e assinada dentro do manifesto JSON com chave Ed25519 pela plataforma.

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "definitions": {
    "Monetization": {
      "type": "object",
      "required": ["model", "delivery"],
      "properties": {
        "model": {
          "type": "string",
          "enum": ["free", "buy_once", "demo_full", "pwyw", "tip_only"]
        },
        "delivery": {
          "type": "string",
          "enum": ["pda_license", "nft_cartridge", "none"]
        },
        "pricing": {
          "type": "object",
          "properties": {
            "USDC": {
              "type": "object",
              "required": ["basePrice", "mintAddress"],
              "properties": {
                "basePrice": { "type": "number", "minimum": 0 },
                "minPrice": { "type": "number", "minimum": 0 },
                "mintAddress": { "type": "string" }
              }
            },
            "SOL": {
              "type": "object",
              "required": ["basePrice", "lamports"],
              "properties": {
                "basePrice": { "type": "number", "minimum": 0 },
                "minPrice": { "type": "number", "minimum": 0 },
                "lamports": { "type": "integer", "minimum": 0 }
              }
            }
          }
        },
        "splits": {
          "type": "array",
          "items": {
            "type": "object",
            "required": ["recipient", "role", "basisPoints"],
            "properties": {
              "recipient": { "type": "string" },
              "role": { "type": "string" },
              "basisPoints": { "type": "integer", "minimum": 1, "maximum": 10000 }
            }
          }
        },
        "platformFee": {
          "type": "object",
          "required": ["treasury", "basisPoints"],
          "properties": {
            "treasury": { "type": "string" },
            "basisPoints": { "type": "integer", "enum": [500] }
          }
        },
        "collection": {
          "type": "object",
          "required": ["address", "sellerFeeBasisPoints"],
          "properties": {
            "address": { "type": "string" },
            "sellerFeeBasisPoints": { "type": "integer", "minimum": 0, "maximum": 10000 }
          }
        },
        "demoConfig": {
          "type": "object",
          "required": ["demoEntryId", "fullGameEntryId", "compatibleSave"],
          "properties": {
            "demoEntryId": { "type": "string" },
            "fullGameEntryId": { "type": "string" },
            "compatibleSave": { "type": "boolean" }
          }
        }
      }
    }
  }
}
```

### Regras Estáticas de Validação do Schema:
1. Se `model` for `"buy_once"` ou `"pwyw"`: `delivery` não pode ser `"none"`, `pricing` é obrigatório, e $\sum(\text{splits.basisPoints}) + \text{platformFee.basisPoints} == 10000$.
2. Se `model` for `"free"` ou `"tip_only"`: `delivery` deve ser `"none"`.
3. Se `model` for `"pwyw"`: `pricing.<currency>.minPrice` é obrigatório e $\ge 0$.
4. Se `delivery` for `"nft_cartridge"`: o objeto `collection` é obrigatório.

---

## 4. Contratos On-Chain na Solana (`plemu-market` e `plemu-splits`)

### 4.1 Estrutura da Conta `Receipt` PDA (`pda_license`)
A licença do jogador é amarrada aos **bytes exatos do artefato auditado**:
* **Seeds:** `[b"receipt", artifact_sha256_bytes (32), buyer_pubkey (32)]`
* **Programa Proprietário:** `plemu-market`

```rust
#[account]
pub struct Receipt {
    /// 8 bytes Anchor discriminator: sha256("account:Receipt")[0..8]
    pub buyer: Pubkey,              // 32 bytes
    pub artifact_sha256: [u8; 32],  // 32 bytes
    pub purchased_at: i64,          // 8 bytes (Unix timestamp UTC)
    pub amount_paid: u64,           // 8 bytes
    pub currency: u8,               // 1 byte (0 = SOL, 1 = USDC)
    pub is_revoked: bool,           // 1 byte (Takedown ou reembolso emergencial)
    pub bump: u8,                   // 1 byte
} // Tamanho total: 82 bytes (Rent: ~0.00146 SOL)
```

### 4.2 Instruções do Programa `plemu-market`
1. `buy_license(amount: u64, currency: u8, tip_to_platform: u64)`:
   - Valida se `amount >= expected_price`.
   - Transfere 5% de `amount` + `tip_to_platform` para a conta da tesouraria Plemu.
   - Transfere 95% de `amount` para o cofre do `plemu-splits` associado ao jogo.
   - Inicializa a conta `Receipt` PDA para a carteira do comprador.
2. `mint_cartridge(amount: u64, currency: u8, tip_to_platform: u64)`:
   - Executa as mesmas transferências financeiras de `buy_license`.
   - Invoca CPI para `mpl-core:CreateV1` mintando o NFT diretamente na carteira do comprador e associando-o à coleção oficial.
3. `send_tip(amount: u64, currency: u8, tip_to_platform: u64)`:
   - 0% de comissão retida: transfere 100% de `amount` diretamente para a carteira do criador (ou cofre de splits, caso seja derivado).
   - Se `tip_to_platform > 0`, efetua a transferência separada para a tesouraria.

---

## 5. Gateway de Download Seguro (Cloudflare Worker)

O Worker (`services/worker/src/index.ts`) controla o **acesso aos downloads**, sem violar a regra de que o dispositivo re-verifica a autenticidade localmente.

```
                    ┌──────────────────────────────────────────────┐
                    │               Console PSG1                   │
                    └──────────────────────┬───────────────────────┘
                                           │
                           1. GET /challenge
                                           │
                                           ▼
                    ┌──────────────────────────────────────────────┐
                    │          Cloudflare Worker Gateway           │
                    │         (services/worker/src/index.ts)       │
                    └──────────────────────┬───────────────────────┘
                                           │
                 Retorna Nonce + Mensagem de Desafio (TTL 5 min)
                                           │
                                           ▼
                    ┌──────────────────────────────────────────────┐
                    │ O Console assina o desafio com a Wallet      │
                    │ POST /gate/entry/:sha256                     │
                    └──────────────────────┬───────────────────────┘
                                           │
                        ┌──────────────────┴──────────────────┐
                        │ Worker consulta Catálogo / KV       │
                        │ Qual o modelo deste sha256?         │
                        └──────────────────┬──────────────────┘
                                           │
             ┌─────────────────────────────┼─────────────────────────────┐
             ▼                             ▼                             ▼
    [model: "free"]            [delivery: "pda_license"]     [delivery: "nft_cartridge"]
    Gera URL R2 Direta         Consulta RPC Solana           Consulta RPC Solana
    (TTL 60s)                  Busca Receipt PDA             Busca Ativo Metaplex Core
                               Checa Discriminador,          Checa owner == wallet e
                               buyer e is_revoked == false   collection == collectionAddress
                                           │                             │
                                           └──────────────┬──────────────┘
                                                          ▼
                                            [Posse Confirmada On-Chain]
                                                          │
                                           Gera URL Pré-Assinada Cloudflare R2
                                           (GET Object, Expirável em 60s)
```

---

## 6. Runtime do Dispositivo PSG1 (Unity / C# & LaunchGate)

### 6.1 Catálogo e Checkout
- Componente de UI adaptado para controles físicos (D-Pad, A/B).
- Verificação proativa de conexão via `PlaySolanaWalletService`.
- Tratamento explícito de saldos de SOL/USDC antes de submeter a transação.

### 6.2 Extensão do `LaunchGate.cs` (Fail-Closed)
O `LaunchGate.Authorize` permanece **100% síncrono e determinístico**, sem tocar na rede:

```csharp
public static class LaunchGate
{
    public static LaunchDecision Authorize(
        string titleId,
        IManifestSource manifests,
        IContentStore store,
        IEntitlementStore entitlements, // Novo: repositório local de recibos/tickets
        IClock clock)
    {
        // 1. Verificações de integridade de manifesto, freshness e hash (já existentes)
        // ...
        
        // 2. Verificação de Monetização e Acesso:
        if (entry.Monetization != null && entry.Monetization.Model != MonetizationModel.Free)
        {
            // Valida ticket local assinado criptograficamente
            if (!entitlements.HasValidTicket(entry.Sha256, clock.UtcNow))
            {
                return LaunchDecision.Denied(
                    DenialReason.EntitlementMissing,
                    $"Title requires purchase or valid license ticket: {titleId}.");
            }
        }
        
        // 3. Emite LaunchTicket e libera boot
        return LaunchDecision.Granted(ticket);
    }
}
```

### 6.3 Garantia de Jogo Offline (*Offline Play Guarantee*)
- Ao comprar e baixar o jogo, o `SolanaEntitlementService` emite um **Ticket Local** assinado e gravado no armazenamento seguro do dispositivo.
- O ticket possui uma janela de validade sincronizada com o `FreshnessPolicy.GracePeriod` (até 7 dias offline).
- O jogador pode levar o console para viagens sem internet e jogar seus títulos adquiridos sem bloqueios ou interrupções.

---

## 7. Estratégia de Testes e Validação (100% Cobertura)

1. **Testes de Schema (`plemu-schemas` em Rust):**
   - Validação de manifestos com cada um dos 5 modelos.
   - Rejeição de somas de `basisPoints` divergentes de 10.000.
2. **Testes do Gateway Cloudflare Worker (TypeScript / Vitest):**
   - Requisição sem assinatura $\rightarrow$ `401 Unauthorized`.
   - Requisição para item pago sem conta `Receipt` $\rightarrow$ `402 Payment Required` ou `403 Forbidden`.
   - Requisição de item com `Receipt` revogado (`is_revoked == 1`) $\rightarrow$ `410 Gone`.
   - Requisição válida $\rightarrow$ `200 OK` com URL pré-assinada do R2 de 60s.
3. **Testes On-Chain (`plemu-market` em Anchor / TypeScript):**
   - Teste de compra com cálculo exato de 5% de taxa e repasse para o cofre de splits.
   - Teste de rejeição em compras abaixo do `minPrice` no *Pague Quanto Quiser*.
   - Teste de mint de cartucho Metaplex Core com hash SHA-256 nos metadados.
4. **Testes de Runtime C# (`Plemu.Solana.Tests`):**
   - Teste de recusa no `LaunchGate` para títulos pagos sem ticket local (`DenialReason.EntitlementMissing`).
   - Teste de validação em modo offline respeitando o `FreshnessPolicy.GracePeriod`.
   - Teste de cópia de save de demo para jogo completo quando `compatibleSave == true`.
