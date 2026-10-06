# Plemu Web

Front inicial do hub de jogos web, em Next.js, React e TypeScript.

```sh
cd apps/web
npm ci
npm run dev
```

Abra http://localhost:3000. Para validar: `npm run build` e `npm run typecheck`.

## Escopo desta versão

- Home responsiva inspirada na referência pixel art.
- Catálogo demonstrativo com busca e filtros por gênero.
- Detalhes e player de um mini game HTML5 original: Neon Orbit.
- Biblioteca salva apenas no navegador, sem representar compra ou entitlement.
- Telas informativas de packs, publicação e carteira, indicando integrações pendentes.

O player de demonstração usa iframe com `sandbox="allow-scripts"`, sem `allow-same-origin`. O jogo não recebe acesso ao armazenamento ou carteira da loja.

Antes de aceitar conteúdo de terceiros, hospedar os jogos em uma origem independente e configurar CSP, validação de pacotes, limites de upload, autenticação e entitlements no backend. O player local não é uma implementação de publicação de jogos.

Não há login, upload, pagamentos, conexão de carteira, catálogo remoto ou compra implementados nesta etapa. Os outros três títulos são conceitos visuais, identificados na interface.

## Monetização aprovada

Pagamento exclusivamente em USDC na Solana, sem saldo interno obrigatório ou token PLEMU. Modalidades: gratuito, compra individual, demo + compra, pague quanto quiser (mínimo do criador), gorjeta opcional em jogos gratuitos.

O front demonstra preços, mínimo e gorjeta. Nenhuma carteira é conectada, transação assinada ou compra liberada nesta versão. A biblioteca local é uma lista de favoritos, nunca uma fonte de autorização. Os preços do catálogo são exemplos. O criador recebe receitas em USDC; comissão e repasse ainda não foram definidos. Packs e assinaturas não fazem parte do escopo inicial.

A implementação de pagamentos deverá criar pedidos no servidor, validar o mint USDC oficial da rede escolhida, recebedor, valor, confirmação e reutilização de transações, antes de registrar o acesso. Gorjetas não desbloqueiam compras. A demo precisa ser um artefato separado do jogo completo; guardar o completo em um URL público não protege conteúdo pago. A rede de lançamento e a política de repasses ainda precisam ser definidas.

## Creator Studio

Abra `/creator`. Sete telas com navegação por hash: painel, rascunhos, publicação, vendas, receitas/carteira, estatísticas e perfil. Publicação tem cinco etapas com validação, modalidades em USDC e salvamento de rascunhos locais. Perfil também fica no navegador.

ZIPs são selecionados apenas nesta sessão, limitados na interface a 100 MB. Seu conteúdo não é inspecionado nem enviado. Os nomes podem ficar no rascunho, mas os arquivos devem ser selecionados novamente. Histórico e métricas são exemplos, sem transações, upload, análise, repasses ou telemetria reais.
