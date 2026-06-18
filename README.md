# 🗺 Level Painter

Ferramenta de editor para Unity que permite pintar tiles 3D diretamente na Scene View, com suporte a empilhamento, snap automático por bounds, paleta de tiles por categoria, e salvamento/carregamento de mapas em JSON.

---

## Sumário

- [Visão Geral](#visão-geral)
- [Estrutura de Arquivos](#estrutura-de-arquivos)
- [Como Instalar](#como-instalar)
- [Como Usar](#como-usar)
- [Arquitetura Interna](#arquitetura-interna)
- [Sistema de Snap Automático](#sistema-de-snap-automático)
- [Sistema de Empilhamento](#sistema-de-empilhamento)
- [Salvar e Carregar Mapas](#salvar-e-carregar-mapas)
- [Referência dos Dados](#referência-dos-dados)

---

## Visão Geral

O Level Painter é um **Editor Window** do Unity que funciona como um pincel de tiles 3D. Você monta uma paleta de prefabs, abre a ferramenta, e pinta diretamente na cena arrastando o mouse — igual a pintar num tilemap 2D, mas em 3D com prefabs reais.

Funcionalidades principais:

- Pincel, Borracha, Substituir, Retângulo e Linha
- Rotação de tiles em 90° com preview em tempo real (ghost)
- Paleta de tiles organizada por categoria com busca
- Snap automático que centraliza o tile pelos bounds reais do mesh, independente do pivot do prefab
- Empilhamento de tiles: pintar em cima de um tile existente encaixa o novo exatamente no topo do anterior
- Hierarquia automática na cena organizada por categoria
- Salvamento e carregamento de mapas em JSON
- Suporte a desfazer/refazer (Undo/Redo) nativo do Unity

---

## Estrutura de Arquivos

```
Assets/Editor/
├── LevelPainterWindow.cs   # A janela do editor (EditorWindow) — UI e controle geral
├── LevelPainterScene.cs    # Lógica da Scene View — raycast, pintura, ghost, snap
└── LevelPalette.cs         # ScriptableObject que armazena a lista de tiles

Assets/Scripts/LevelPainter/
├── TileData.cs             # Definições de dados: TileItem, TileCategory, TileSnapMode,
│                           # TilePlacementData e LevelMapData
└── GridDatabase.cs         # Dicionário de célula → tile instanciado; serialização JSON
```

Os arquivos de `Editor/` só existem no contexto do editor do Unity (dentro de uma pasta `Editor` ou com `#if UNITY_EDITOR`). Nenhum código do Level Painter roda em build de jogo.

---

## Como Instalar

1. Copie os cinco arquivos acima para o seu projeto Unity, respeitando as pastas (`Editor/` para os três primeiros, qualquer pasta fora de `Editor/` para os dois últimos).
2. O Unity vai compilar automaticamente. Nenhuma dependência externa é necessária — só a URP ou pipeline padrão.
3. Crie uma **Level Palette** indo em `Assets → botão direito → Criar → Level Painter → Level Palette`.
4. No asset criado, popule a lista de tiles no Inspector (nome, prefab, categoria, ícone opcional).
5. Abra a ferramenta pelo menu `Tools → Level Painter` (atalho: `Ctrl+Shift+L`).

---

## Como Usar

### Abrindo a ferramenta

`Tools → Level Painter` ou `Ctrl+Shift+L`. Uma janela flutuante abre. Ela pode ser encaixada em qualquer lugar do layout do Unity.

### Configurando a paleta

No campo **"Asset da Paleta"**, arraste o seu asset `LevelPalette`. A paleta aparece como uma grade de ícones separada por abas de categoria (Chão, Trilhos, Estruturas, Adereços, Vegetação). Clique em um tile para selecioná-lo.

### Iniciando a pintura

Clique no botão **"○ Iniciar Pintura"** (ou `Ctrl+Shift+L` de novo). O botão fica verde e mostra **"● Pintando"**. A partir daqui, a Scene View responde aos cliques do Level Painter.

### Ferramentas disponíveis

| Ferramenta | Atalho | Comportamento |
|---|---|---|
| ✏ Pincel | `B` | Pinta um tile por célula. Arrastar pinta em células diferentes. |
| ✕ Borracha | `E` | Remove o tile da célula clicada. |
| ↺ Substituir | `T` | Remove o tile existente e coloca o selecionado no mesmo lugar. |
| 🔲 Retângulo | — | Clica e arrasta para preencher uma área retangular. |
| ➖ Linha | — | Clica e arrasta para preencher uma linha reta. |

**Rotação:** pressione `R` para girar o tile selecionado em 90°. O ghost (preview azul transparente) atualiza instantaneamente. O botão `↩` ao lado da rotação reseta para 0°.

**Botão direito** na cena apaga o tile da célula, independente da ferramenta ativa.

### Empilhamento

Se você clicar com o Pincel em cima de um tile que já existe, o novo tile é colocado **sobre** o anterior — encostado no topo real do mesh de baixo, sem gap e sem overlap. Para pintar ao lado (mesma camada), mova o mouse para outra célula XZ. Segurar e arrastar **não** empilha na mesma coluna; o empilhamento exige um novo clique na mesma célula.

### Superfície e Camada Y

Na seção **"Superfície e Camada"** você escolhe como o raycast detecta onde pintar:

- **Plano Fixo** — pinta num plano horizontal na altura Y configurada pelo slider "Camada Y". Use isso para pintar camadas de chão, teto etc.
- **Colisores Físicos** — o raycast bate nos colliders da cena, útil para pintar sobre superfícies inclinadas ou irregulares.

### Atalhos resumidos

```
B       Pincel
E       Borracha
T       Substituir
R       Girar tile 90°
LMB     Pintar / ação da ferramenta ativa
RMB     Apagar
```

---

## Arquitetura Interna

O sistema é dividido em três camadas com responsabilidades separadas:

```
┌─────────────────────────────────────────────┐
│           LevelPainterWindow                │  ← UI (EditorWindow)
│  Desenha a janela, paleta, botões, status   │
│  Gerencia estado: paleta ativa, ferramenta, │
│  camada Y, nome do mapa                     │
└────────────────┬────────────────────────────┘
                 │ chama
┌────────────────▼────────────────────────────┐
│           LevelPainterScene                 │  ← Lógica da Scene View
│  Recebe eventos do Unity (mouse, teclado)   │
│  Faz raycast, calcula célula hovered        │
│  Chama PlaceTile / EraseTile / ReplaceTile  │
│  Gerencia o ghost (preview transparente)    │
│  Calcula snap offset e empilhamento         │
└────────────────┬────────────────────────────┘
                 │ lê/escreve
┌────────────────▼────────────────────────────┐
│           GridDatabase                      │  ← Dados
│  Dictionary<Vector3Int, PlacedTile>         │
│  Converte world ↔ cell                      │
│  Serializa para JSON (LevelMapData)         │
└─────────────────────────────────────────────┘
```

### Fluxo de um clique de pintura

1. O Unity dispara `SceneView.duringSceneGui` → `LevelPainterScene.OnSceneGUI` é chamado.
2. Um `Ray` é calculado a partir da posição do mouse na tela.
3. `PerformRaycast` intercepta o ray contra o plano fixo (ou colisores) e retorna o ponto 3D no mundo.
4. `GridDatabase.WorldToCell` converte esse ponto em `Vector3Int` usando `RoundToInt(pos / cellSize)`.
5. No `MouseDown`, `PlaceTile(hoveredCell)` é chamado.
6. `FindStackCell` verifica se a célula está ocupada. Se sim, sobe em Y usando o topo real do tile de baixo até achar uma célula livre.
7. `ComputeSnapOffset` instancia o prefab temporariamente, lê os `Renderer.bounds` e calcula o offset XYZ necessário para centralizar o tile e assentar sua base na célula.
8. O prefab é instanciado via `PrefabUtility.InstantiatePrefab` (mantendo link com o asset original), posicionado e registrado no Undo.
9. `GridDatabase.Place` registra o `PlacedTile` no dicionário.

### Hierarquia gerada na cena

Ao iniciar a pintura, `EnsureHierarchy` cria (se não existir) a seguinte estrutura:

```
Nível
├── Chao
├── Trilhos
├── Estruturas
├── Aderecos
└── Vegetacao
```

Cada tile instanciado é filho do GameObject de sua categoria, mantendo a hierarquia organizada.

### Ghost (preview)

Enquanto o mouse paira sobre a cena, um clone do prefab selecionado é mantido vivo com `HideFlags.HideAndDontSave` (não aparece na hierarquia, não é salvo). Todos os seus materiais são substituídos por um material transparente azul. A posição do ghost é atualizada a cada frame usando o mesmo `ComputeSnapOffset` que será usado ao pintar de verdade — o que você vê é exatamente o que será colocado.

O ghost é reconstruído (`RebuildGhost`) apenas quando o tile selecionado ou a rotação mudam, para não causar instâncias desnecessárias.

---

## Sistema de Snap Automático

O problema clássico de tile painters 3D: prefabs raramente têm o pivot no centro geométrico do mesh. Um trilho com pivot no canto esquerdo, posicionado no centro da célula, fica deslocado e não conecta com o próximo.

O Level Painter resolve isso com `ComputeSnapOffset`, que roda para cada tile na hora de pintar:

```
1. Instancia o prefab temporariamente na origem (HideAndDontSave)
2. Aplica a rotação exata que será usada (incluindo rotação atual do pintor)
3. Lê os Bounds combinados de todos os Renderers filhos
4. Calcula:
   offsetX = -bounds.center.x   → centraliza em X
   offsetZ = -bounds.center.z   → centraliza em Z
   offsetY = -bounds.min.y      → assenta a base do mesh em Y=0 da célula
5. Soma o positionOffset manual do TileItem como ajuste fino
6. Destrói a instância temporária
```

A rotação é considerada no cálculo porque os bounds de uma curva girada 90° são diferentes dos bounds da mesma curva sem rotação. Sem isso, curvas giradas teriam snap errado.

### Modos de snap (TileSnapMode)

Configurado por tile no asset `LevelPalette`:

| Modo | Comportamento |
|---|---|
| `AutoCenter` *(padrão)* | Centraliza XZ e assenta base em Y. Ideal para a maioria dos tiles. |
| `AutoCenterXZOnly` | Centraliza só XZ, não mexe em Y. Para tiles cujo pivot já está na base. |
| `Manual` | Usa `positionOffset` diretamente, sem calcular bounds. Comportamento legado. |

O campo `positionOffset` no `TileItem` funciona como ajuste fino somado por cima do snap automático (nos modos Auto), ou como offset total (no modo Manual).

---

## Sistema de Empilhamento

Quando o Pincel é usado sobre uma célula já ocupada, o sistema não bloqueia nem substitui — ele empilha:

### Encontrando a célula correta (`FindStackCell`)

```
célula base (onde o mouse está)
    ↓ está ocupada?
    ├── NÃO → usa a célula, snap normal pela grid
    └── SIM → lê o topo real do tile instanciado (Renderer.bounds.max.y)
               converte esse Y em índice de célula (CeilToInt)
               tenta a próxima célula acima
               repete até achar uma livre (máx 32 tentativas)
```

### Posicionamento do tile empilhado

Para o tile que vai por cima, a posição Y **não** é calculada pelo centro da célula da grid (que estaria errado). Em vez disso:

```
posY = topY_do_tile_de_baixo + (-bounds.min.y_do_novo_tile)
```

Onde `-bounds.min.y` é a distância do pivot à base do mesh do novo tile. Somando os dois, a base do novo tile encosta exatamente no topo do anterior.

### Prevenção de empilhamento acidental ao arrastar

Arrastar o mouse com o botão pressionado pinta em células XZ diferentes, mas **não** empilha na mesma coluna. O sistema rastreia `_lastPaintedXZ` e só pinta quando o mouse se move para uma nova posição XZ. Para empilhar intencionalmente, solte e clique novamente na mesma célula.

---

## Salvar e Carregar Mapas

Clique em **"💾 Salvar/Carregar"** na barra superior para expandir a seção.

### Salvando

O mapa é serializado como JSON pelo `GridDatabase.ToJson()`. Para cada tile no dicionário, são salvos:

- Coordenadas da célula (x, y, z)
- Rotação Y
- Caminho do asset do prefab (via `AssetDatabase.GetAssetPath`)
- Nome do tile e categoria (para reidentificação na palette)

### Carregando

`RebuildFromMapData` percorre os dados do JSON, busca o `TileItem` correspondente na palette ativa pelo nome e categoria, e reinstancia cada prefab com a mesma posição e rotação original — usando o mesmo `ComputeSnapOffset` para garantir consistência.

> **Atenção:** o mapa salvo referencia tiles pelo nome e categoria. Se você renomear um tile na palette ou mudar sua categoria, os tiles desse tipo não serão reconhecidos ao carregar um mapa antigo.

### Formato do JSON

```json
{
  "mapName": "MeuNivel",
  "createdAt": "2026-06-18T00:00:00Z",
  "gridSize": 1.0,
  "tiles": [
    {
      "x": 0, "y": 0, "z": 0,
      "rotationY": 0.0,
      "prefabPath": "Assets/Prefabs/ChaoHorizontal.prefab",
      "category": "Chao",
      "tileName": "ChaoHorizontal"
    }
  ]
}
```

---

## Referência dos Dados

### TileItem

Campo | Tipo | Descrição
--- | --- | ---
`tileName` | `string` | Nome exibido na paleta e salvo no JSON
`icon` | `Sprite` | Ícone opcional na paleta (usa preview do prefab se vazio)
`category` | `TileCategory` | Categoria para organizar na paleta e na hierarquia da cena
`prefab` | `GameObject` | Prefab que será instanciado
`snapMode` | `TileSnapMode` | Como o snap automático trata esse tile
`positionOffset` | `Vector3` | Offset fino aplicado após o snap (ou offset total no modo Manual)
`rotationOffset` | `Vector3` | Rotação base adicionada à rotação do pintor
`scaleOverride` | `Vector3` | Se diferente de zero, sobrescreve o scale padrão do prefab

### TileCategory

`Chao` · `Trilhos` · `Estruturas` · `Aderecos` · `Vegetacao`

Adicionar novas categorias requer incluir o valor no enum `TileCategory` em `TileData.cs`. A hierarquia na cena e as abas da paleta são geradas automaticamente a partir do enum.

### PlacedTile

Estrutura em memória (não serializada para JSON) que representa um tile instanciado:

Campo | Descrição
--- | ---
`Instance` | O `GameObject` vivo na cena
`SourcePrefab` | Referência ao prefab original
`Category` | Categoria do tile
`TileName` | Nome do tile
`RotationY` | Rotação Y aplicada no momento da pintura

---

## Dicas

**Tamanho da célula** — configure em "Configuração da Grade" para corresponder ao tamanho dos seus prefabs. Se seus tiles têm 1 unidade de largura, use `1.0`. O snap automático não depende disso para posicionar corretamente, mas a navegação pela grid e o highlight de célula ficam mais intuitivos quando o valor bate com o tamanho visual dos tiles.

**Pivot dos prefabs** — com o `snapMode = AutoCenter`, o pivot do prefab não importa para o posicionamento. Mas para **conexão entre peças** (trilhos, estradas), o que importa é que os prefabs tenham o mesmo tamanho que o `cellSize` da grid. Um trilho reto de 1 unidade e uma curva de 1 unidade, com `cellSize = 1`, sempre vão conectar nas bordas das células.

**Performance do snap** — `ComputeSnapOffset` instancia e destrói um GameObject a cada tile pintado para ler os bounds. Em pintura normal isso é imperceptível, mas ao usar Retângulo ou Linha em áreas grandes (centenas de tiles), pode haver uma pequena pausa. Para tiles muito usados, considere usar `snapMode = Manual` com o `positionOffset` calculado manualmente uma vez.
