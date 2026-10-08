# Plemu — revisão de interações

## Resultado

Refinamento incremental da home, preservando fundo, paleta, navegação, catálogo, Create e abertura dos packs. As referências foram usadas como padrões de interação: nenhum componente Aceternity foi importado e nenhum código privado do React Bits Pro foi acessado nesta implementação.

Três momentos principais: o cursor reúne pixels no hero; o PSG1 conecta jogo, token e mercado; a primeira arte editorial ganha luz e deslocamento sutis. O magnetismo de Create é uma microinteração. O rasgo e o foil existentes continuam sendo a assinatura dos packs.

## Curadoria das referências

Avaliação de adequação a partir do [catálogo público Aceternity](https://ui.aceternity.com/components), com leitura das páginas públicas dos padrões selecionados e dos principais concorrentes. Esta tabela distingue novas adaptações, recursos preservados e padrões descartados; não implica instalação ou prototipagem de todo o catálogo.

| Padrões avaliados | Decisão e motivo |
| --- | --- |
| [3D Card Effect](https://ui.aceternity.com/components/3d-card-effect), [Wobble Card](https://ui.aceternity.com/components/wobble-card) | Aproveitar profundidade limitada no PSG1; rejeitar deformação/wobble. O console mantém rotação máxima de 4° e deslocamento de 3px. O movimento para sobre controles e durante foco. Corrigido o plano 3D interno que interceptava cliques. |
| [Card Spotlight](https://ui.aceternity.com/components/card-spotlight), Direction Aware Hover | Implementar luz lilás guiada pelo cursor e deslocamento de até 4px apenas na arte Moss & Ember da seção de descoberta. Não inserir painel direcional: título, preço e plataforma já ficam visíveis. |
| [Glare Card](https://ui.aceternity.com/components/glare-card) | Preservar o foil próprio de superfície inteira somente na carta HIT. Não adicionar uma segunda camada de glare/tilt aos packs, que competiria com rasgo e deslize. |
| Expandable Card | Não implementar: os links existentes já levam à página do jogo. Um modal expansível acrescentaria outra rota de navegação e gestão de foco. |
| [Sticky Scroll Reveal](https://ui.aceternity.com/components/sticky-scroll-reveal), Container Scroll Animation | Preservar a entrada/boot e a curta narrativa de scroll existentes do PSG1. Acrescentar link contextual para a economia do jogo selecionado. Não adicionar outra fixação de scroll. |
| Parallax Scroll, Hero Parallax, Parallax Grid Scroll | Rejeitar nesta rodada. Movimento adicional deslocaria a atenção do console e dos dados e complicaria telas menores. |
| Canvas Reveal Effect, Background Ripple Effect, Background Lines, Scales, Glowing Effect, Spotlight, Dither Shader, Grid and Dot Backgrounds | Preservar a grade atual e escolher apenas o Pixel Magnet local. Rejeitar novos fundos, shaders/WebGL, ondas e glows globais para evitar sobreposição de efeitos e custo contínuo. Spotlight fica restrito à arte editorial. |
| Text Hover Effect, Text Reveal Card, Colourful Text, Typewriter Effect, Text Generate Effect, Canvas Text | Não implementar. As fontes pixeladas já estabelecem identidade; mensagem, nomes e preços devem permanecer imediatamente legíveis. Manter somente a entrada curta de texto já existente. |
| [Magnetic Button](https://ui.aceternity.com/components/magnetic-button) | Adaptar o componente próprio existente: área de referência estável, deslocamento máximo de 4px/3px, retorno suave e neutralização em foco/pressionamento. Usado no CTA principal do hero e no CTA “Make something yours”. |
| Moving Border, Hover Border Gradient, Stateful Button | Rejeitar bordas continuamente animadas; não acrescentar estados artificiais de carregamento/sucesso a links comuns. Rasgo e revelação dos packs já comunicam seus estados reais. |
| Navbar Menu, Floating Navbar, Resizable Navbar, Animated Tabs | Manter estrutura, altura e ordem atuais. Os indicadores e estados ativos existentes bastam; evitar outra navegação flutuante ou redimensionamento durante leitura. |

## Pixel Magnet independente

Referência comportamental: [página pública do React Bits](https://pro.reactbits.dev/docs/components/pixel-magnet).

- Um Canvas local ao hero, com 40 quadrados de 2–4px, predominância de `#8C76DB`, pontos `#B6D935` e `#4516BB`.
- Posição real do ponteiro define o centro do enxame. Distância/tempo entre eventos determina expansão; amortecimento normalizado pelo tempo suaviza acompanhamento, assentamento e retorno à grade de repouso.
- `count`, `spread` e seletor `exclude` configuráveis. Limite de 64 partículas e resolução limitada a DPR 2.
- Canvas sem eventos de ponteiro, oculto da árvore acessível e abaixo do conteúdo. Retângulos de texto, CTAs, cartuchos, legendas e instruções recebem margem de exclusão de 12px. As áreas são atualizadas durante a animação para acompanhar transformações.
- `requestAnimationFrame` só permanece ativo enquanto há movimento. Pausa quando a seção sai da tela ou a aba fica oculta; todos os listeners, observers e frames são removidos no cleanup.
- Desativado para `prefers-reduced-motion`, ausência de hover ou ponteiro grosso. Eventos de toque são ignorados. Nenhuma informação depende do efeito.

## Código e dependências

Código próprio novo: `components/pixel-magnet.tsx` e `components/artwork-light.tsx`. Ajustes nos componentes `motion`, `console-depth`, `psg1-experience`, `create-experience`, `home-experience` e nos estilos locais de `app/play-system.css`.

Nenhuma dependência adicionada. React, Canvas, CSS e GSAP já presente no projeto são suficientes. Sem WebGL, sem estado React atualizado por frame e sem partículas representadas por dezenas de elementos DOM. A arte editorial agenda no máximo uma atualização por frame de ponteiro; os demais cartões e as linhas compactas de mercado não recebem esse efeito.

Não foram introduzidos breakpoints novos. Os layouts responsivos existentes são preservados; a ativação dos efeitos depende da capacidade de hover/ponteiro e da preferência de movimento, não apenas da largura da tela. O link PSG1 → economia usa o identificador do jogo atual e mantém a simulação explicitamente identificada.

## Verificação

- Navegador em 1224×900, 768×1024 e 390×844: composição, legibilidade, cards, console e ausência de overflow horizontal da página nos estados inspecionados. A navegação/galerias mantêm seus trilhos horizontais intencionais.
- Pixels: gestos curtos e longos, mudança de direção, assentamento, saída do hero e proximidade de texto/CTA. Proteção visual e camada sem captura de ponteiro conferidas.
- PSG1: inclinação medida em aproximadamente 3,07°/2,25°, retorno a zero; cliques após correção do plano interno; teclado, troca Starfall → Neon Ronin → Moss & Ember com arte/token/preço/variação correspondentes; link para a economia de Moss & Ember navega ao destino correto.
- Create: deslocamento medido em 4px, foco visível e posição neutra ao usar teclado. Arte editorial: luz/posição seguem cursor; título/preço permanecem fixos e o link conserva foco visível.
- Pack: rasgo real por arrasto, revelação, deslize Void Runner → Pixel Rebels HIT com foil completo; no layout de 390px, deslize seguinte para Redline Zero sem overflow.
- Limites da revisão: a ferramenta não oferece velocidade de arrasto configurável, emulação de toque físico ou mudança de preferência do sistema. A resposta à velocidade e os caminhos de movimento reduzido/toque foram revisados no código; testes em dispositivo físico não foram realizados. Não há benchmark de FPS/dispositivo de entrada baixa.

Compilação de produção (26 páginas), TypeScript, formatação e oito testes existentes de simulação aprovados. Console do navegador sem erros. Preview local mantido ativo, sem stage, commit, push ou publicação.

Evidências visuais locais: `qa/pixel-magnet-hero.png` e `qa/interaction-psg-market.png`. As capturas são estáticas; o preview local permite conferir o movimento.

Revisão criativa: manter os efeitos localizados evita competição com o PSG1 e os packs. Não adicionar parallax, animação de títulos, fundos novos ou glare genérico foi uma decisão deliberada de hierarquia.
