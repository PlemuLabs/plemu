# Plemu — Cursor → Pixels / Real World → Pixel World

## Implementação

Esta rodada substitui integralmente o primeiro efeito de cursor. A lógica anterior de pontos com deslocamentos fixos não permanece em uso. A identidade, os fundos, a navegação, o PSG1, os packs e as quatro rotas de Create foram preservados.

### Pixel Magnet no hero de Discover

Foi executado `npx --yes shadcn@latest add @reactbits-starter/pixel-magnet-tw --yes` em um projeto temporário, sem modificar a configuração do Plemu. O instalador retornou `Unknown registry "@reactbits-starter"`. A [instalação oficial](https://pro.reactbits.dev/docs/installation) exige registro e chave de licença, indisponíveis nessa configuração. Não houve tentativa de obter código privado. A alternativa independente foi autorizada pelo pedido.

A [demonstração pública e os parâmetros do Pixel Magnet](https://pro.reactbits.dev/docs/components/pixel-magnet) orientaram uma implementação nova: centro de atração com mola amortecida, inércia individual, quadrados alinhados à grade e ciclos de contração alimentados pela velocidade do ponteiro. O verde aparece nos quadrados mais ativos. Ao parar, o enxame assenta; ao sair, volta ao repouso discreto na parte inferior do hero. Um convite curto torna a interação descobrível.

| Parâmetro | Configuração Plemu |
| --- | --- |
| count / resolution | 32 / 96 |
| speed / spread / sizeSpread | 1 / 0.72 / 1.5 |
| falloff / falloffCurve | 1.05 / 0.5 |
| color / hotColor | `#8C76DB` / `#B6D935` |
| follow / velocityScale | 10.5 / 1.6 |
| idleStrength / decay | 0.12 / 1.2 |
| opacity / composição | 1 / normal, canvas transparente |
| Cursor e velocidade | Ativos para mouse/pen com hover e ponteiro preciso |

Esse é um modelo independente compatível com o vocabulário de configuração público, não o algoritmo privado do React Bits. Os quadrados ficam maiores que os pontos da versão anterior, com grade entre 5 e 9px e tamanhos de até duas células nesta configuração. Não há glow ou rastro acumulado de frames.

O canvas é local, sem eventos de ponteiro e com margem de exclusão de 14px em texto, CTAs, cartuchos, valores e legendas. O efeito desliga com movimento reduzido, ponteiro grosso ou ausência de hover. O loop para ao assentar e pausa fora da tela/aba visível. DPR limitado a 2, população limitada a 64, sem estado React por frame.

### Webcam Pixel Grid no Create

O comando `npx --yes shadcn@latest add @aceternity/webcam-pixel-grid --yes` instalou com sucesso o componente público em uma pasta temporária. Seu código foi integrado em `components/ui/webcam-pixel-grid.tsx`, preservando a leitura de pixels, comparação entre frames, suavização de elevação, faces e sombras da grade. [Referência oficial](https://ui.aceternity.com/components/webcam-pixel-grid).

O **Pixel Mirror** fica em `/create#pixel-mirror`, depois da escolha das quatro rotas e antes das opções de publicação. O usuário explora uma experiência de criação/personalização sem encobrir ferramentas. Um link na seção Create da home leva diretamente ao experimento. A câmera não divide o hero nem a mesma seção com o enxame do cursor.

| Configuração | Desktop / tablet / mobile |
| --- | --- |
| Grade | 64×48 / 56×42 / 48×36 |
| Limites de largura | Acima de 900px / 601–900px / até 600px |
| Elevação máxima | 5 / 5 / 3 |
| FPS máximo | 24 / 24 / 18 |
| Motion sensitivity / smoothing | 0.38 / 0.16 |
| Cor | Monochrome `#8C76DB`, opção `#B6D935` |
| Fundo / gap / border opacity | `#19113E` / 0.12 / 0.09 |
| Mirror / invert / darken | Câmera espelhada / false / 0 |
| Movimento reduzido | Elevação zero, até 12 FPS; amostra estática |

Adaptações ao código Aceternity: stream externo controlado pelo aplicativo, remoção da solicitação automática no mount e dos popups genéricos, estilos locais, amostra de arte existente, renderização limitada por FPS/DPR, buffers redimensionados apenas quando necessário e cleanup ao desmontar. Não foi necessário adicionar Tailwind, Motion, shadcn ou outra dependência de runtime.

## Câmera e privacidade

Somente o clique em **Enable camera** chama `getUserMedia`. A captura pede vídeo frontal em resolução ideal de 640×480, sem áudio. Não há upload, gravação, armazenamento de frames, socket ou endpoint de transmissão. Uma sessão só é identificada como LIVE após iniciar a reprodução.

**Turn camera off**, cancelar, sair da seção, ocultar a aba, sair da página e desmontar o componente encerram os tracks. Autorizações que chegam depois do cancelamento são imediatamente liberadas e não reativam a câmera. O retorno à seção exige novo clique; não há retomada automática.

Sem câmera, a grade usa `/art/echo.webp`, identificada como **SAMPLE WORLD · CAMERA OFF**. **Try a sample** demonstra a elevação com movimento da própria arte local, sem simular que a câmera está ativa. Negação/perda/indisponibilidade mantém a navegação e os controles funcionais, com mensagem contextual e tentativa explícita.

## QA e limites

- Navegador: demonstração estática e animada, troca lilás/verde, teclado, foco, ausência de overflow em 1224×900, 768×1024 e 390×844. Renderer desmontado quando a seção saiu da tela; estado retornou a câmera desligada.
- Cursor: deslocamentos curtos/longos, diagonais, inversão de direção, assentamento e saída do hero. Textos/CTAs continuam legíveis. A ferramenta não controla a velocidade física do gesto; não foi feita medição calibrada de velocidade ou FPS.
- Regressão: PSG1 avança para Neon Ronin; abertura de pack por teclado revela as cartas. Rasgo, foil, plataformas e navegação mantêm seus componentes existentes.
- Quatro testes novos verificam tracks liberados, autorização tardia após cancelamento, concorrência de solicitações e erro de permissão. Oito testes de economia existentes continuam passando.
- Build de produção (26 páginas), TypeScript, formatação e checagem de espaços passam. Sem erros de console na revisão da demonstração.
- **Pendente: ativação de câmera real.** A revisão automática rejeitou o clique por exigir autorização específica de acesso à câmera. O prompt, a captura real, a negação pela interface do navegador e a resposta a movimento humano não foram exercitados. Negação e cancelamento foram testados na lógica de sessão, e os estados da interface foram revisados no código.
- Seeker/toque físico e preferência de movimento reduzido do sistema não foram emulados. O layout móvel e os caminhos correspondentes do código foram revisados. A câmera exige contexto seguro (HTTPS ou localhost) e suporte/permissão do navegador; indisponibilidade usa a demonstração local.

Capturas: `qa/pixel-magnet-replacement.png`, `qa/pixel-mirror-create.png`. Aplicação local mantida ativa. Nenhum stage, commit, push ou deploy.
