using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LevelPainter
{
    public class LevelPainterWindow : EditorWindow
    {

        private const string TituloJanela = "Level Painter";
        private const string PrefChavePaleta = "LevelPainter_UltimaPaleta";
        private const string PrefChaveGrade = "LevelPainter_TamanhoGrade";
        private const float TamanhItemPaleta = 72f;
        private const float LarguraMinima = 320f;


        private GridDatabase _db;
        private LevelPainterScene _scene;
        private bool _pintando = false;


        private LevelPalette _palette;
        private TileCategory _categoriaAtiva = TileCategory.Chao;
        private string _buscaQuery = "";
        private Vector2 _scrollPaleta;
        private TileItem _tileSelecionado;
        private Queue<TileItem> _tilesRecentes = new();

        private float _tamanhoGrade = 1f;
        private float _camadaPinturaY = 0f;
        private bool _mostrarSettings = false;


        private Vector2 _scrollPrincipal;
        private bool _mostrarSalvarCarregar = false;
        private string _nomeDoMapa = "MeuNivel";
        private string _statusMsg = "Pronto";

        private static readonly Color BgColor = new Color(0.10f, 0.09f, 0.12f);
        private static readonly Color PanelColor = new Color(0.13f, 0.12f, 0.16f);
        private static readonly Color BorderColor = new Color(0.18f, 0.17f, 0.22f);
        private static readonly Color PurpleAccent = new Color(0.45f, 0.35f, 0.80f);
        private static readonly Color TextNormal = new Color(0.60f, 0.60f, 0.65f);
        private static readonly Color TextHigh = new Color(0.90f, 0.90f, 0.95f);

        private static readonly Color CorPincel = new Color(0.25f, 0.65f, 0.35f);
        private static readonly Color CorBorracha = new Color(0.85f, 0.25f, 0.25f);
        private static readonly Color CorSubstituir = new Color(0.85f, 0.65f, 0.15f);



        [MenuItem("Tools/Level Painter %#m", priority = 200)]
        public static void MostrarJanela()
        {
            var w = GetWindow<LevelPainterWindow>(TituloJanela);
            w.minSize = new Vector2(LarguraMinima, 480f);
            w.Show();
        }



        private void OnEnable()
        {
            titleContent = new GUIContent(
                TituloJanela,
                EditorGUIUtility.IconContent("d_Grid.Default@2x").image);

            _tamanhoGrade = EditorPrefs.GetFloat(PrefChaveGrade, 1f);
            _db = new GridDatabase(_tamanhoGrade);
            _scene = new LevelPainterScene();
            _scene.SetDatabase(_db);
            _scene.RepaintRequest = Repaint;
            _scene.OnEyedropperPick += AoPegarTileComContaGotas;

            string caminhoSalvo = EditorPrefs.GetString(PrefChavePaleta, "");
            if (!string.IsNullOrEmpty(caminhoSalvo))
                _palette = AssetDatabase.LoadAssetAtPath<LevelPalette>(caminhoSalvo);

            SceneView.duringSceneGui += AoSceneGUI;
            Undo.undoRedoPerformed += AoDesfazerRefazer;
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= AoSceneGUI;
            Undo.undoRedoPerformed -= AoDesfazerRefazer;

            if (_scene != null) _scene.OnEyedropperPick -= AoPegarTileComContaGotas;
            if (_pintando) PararPintura();
            _scene?.Cleanup();

            EditorPrefs.SetFloat(PrefChaveGrade, _tamanhoGrade);
            if (_palette != null)
                EditorPrefs.SetString(PrefChavePaleta, AssetDatabase.GetAssetPath(_palette));
        }

        private void AoDesfazerRefazer()
        {
            ReconstruirDatabaseDaCena();
            Repaint();
        }

        private void AoSceneGUI(SceneView sv)
        {
            if (!_pintando) return;
            _scene.OnSceneGUI(sv);
        }



        private GUIStyle _tituloJanelaStyle;
        private GUIStyle _subtituloStyle;
        private GUIStyle _panelTitleStyle;
        private GUIStyle _panelStyle;
        private GUIStyle _whiteLabelStyle;
        private GUIStyle _whiteMiniLabelStyle;

        private void SetTextColor(GUIStyle style, Color c)
        {
            style.normal.textColor = c;
            style.hover.textColor = c;
            style.active.textColor = c;
            style.focused.textColor = c;
            style.onNormal.textColor = c;
            style.onHover.textColor = c;
            style.onActive.textColor = c;
            style.onFocused.textColor = c;
        }

        private void InitStyles()
        {
            if (_tituloJanelaStyle != null) return;
            
            _tituloJanelaStyle = new GUIStyle(EditorStyles.boldLabel);
            _tituloJanelaStyle.fontSize = 24;
            _tituloJanelaStyle.alignment = TextAnchor.MiddleLeft;
            SetTextColor(_tituloJanelaStyle, new Color(0.65f, 0.55f, 0.95f));

            _subtituloStyle = new GUIStyle(EditorStyles.label);
            _subtituloStyle.fontSize = 11;
            _subtituloStyle.alignment = TextAnchor.MiddleLeft;
            SetTextColor(_subtituloStyle, TextNormal);

            _panelTitleStyle = new GUIStyle(EditorStyles.boldLabel);
            _panelTitleStyle.fontSize = 13;
            SetTextColor(_panelTitleStyle, TextHigh);

            _whiteLabelStyle = new GUIStyle(EditorStyles.label);
            SetTextColor(_whiteLabelStyle, TextHigh);

            _whiteMiniLabelStyle = new GUIStyle(EditorStyles.miniLabel);
            _whiteMiniLabelStyle.alignment = TextAnchor.MiddleCenter;
            SetTextColor(_whiteMiniLabelStyle, TextHigh);

            _panelStyle = new GUIStyle();
            _panelStyle.padding = new RectOffset(16, 16, 16, 16);
            _panelStyle.margin = new RectOffset(10, 10, 10, 10);
        }

        private void OnGUI()
        {
            if (_scene == null || _db == null)
            {
                OnEnable();
                if (_scene == null) return;
            }

            InitStyles();
            
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), BgColor);

            EditorGUILayout.BeginHorizontal();

            // LEFT PANEL
            EditorGUILayout.BeginVertical(GUILayout.Width(280));
            GUILayout.Space(20);
            
            GUILayout.BeginHorizontal();
            GUILayout.Space(15);
            GUILayout.BeginVertical();
            GUILayout.Label("LevelPainter", _tituloJanelaStyle);
            GUILayout.Label("v1.0.0", _subtituloStyle);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            
            GUILayout.Space(15);
            
            DesenharLeftPanel();
            
            EditorGUILayout.EndVertical();

            // RIGHT PANEL
            EditorGUILayout.BeginVertical();
            GUILayout.Space(20);
            DesenharRightPanel();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawPanel(Action content)
        {
            Rect r = EditorGUILayout.BeginVertical(_panelStyle);
            EditorGUI.DrawRect(r, PanelColor);
            
            // Borders
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1), BorderColor);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), BorderColor);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 1, r.height), BorderColor);
            EditorGUI.DrawRect(new Rect(r.xMax - 1, r.y, 1, r.height), BorderColor);

            content();
            EditorGUILayout.EndVertical();
        }


        private void DesenharLeftPanel()
        {
            _scrollPrincipal = EditorGUILayout.BeginScrollView(_scrollPrincipal);
            
            // Big Paint Button
            GUILayout.BeginHorizontal();
            GUILayout.Space(10);
            Color prevColor = GUI.backgroundColor;
            GUI.backgroundColor = _pintando ? new Color(0.8f, 0.3f, 0.3f) : PurpleAccent;
            GUIStyle btnPaint = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold };
            btnPaint.normal.textColor = Color.white;
            string labelPaint = _pintando ? "⏹ Parar Pintura" : "▶ Iniciar Pintura";
            if (GUILayout.Button(labelPaint, btnPaint, GUILayout.Height(36), GUILayout.Width(250)))
                AlternarPintura();
            GUI.backgroundColor = prevColor;
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // Ferramentas
            DrawPanel(() => 
            {
                GUILayout.Label("🛠 Ferramentas", _panelTitleStyle);
                GUILayout.Space(12);

                EditorGUILayout.BeginHorizontal();
                DesenharBotaoFerramenta(PainterTool.Pincel, "Pincel", CorPincel, 75);
                DesenharBotaoFerramenta(PainterTool.Borracha, "Borracha", CorBorracha, 75);
                DesenharBotaoFerramenta(PainterTool.Substituir, "Trocar", CorSubstituir, 75);
                EditorGUILayout.EndHorizontal();
                
                GUILayout.Space(8);

                EditorGUILayout.BeginHorizontal();
                DesenharBotaoFerramenta(PainterTool.Retangulo, "Retângulo", CorPincel, 115);
                DesenharBotaoFerramenta(PainterTool.Linha, "Linha", CorPincel, 115);
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(12);
                
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"Rotação: {_scene.CurrentRotationY:F0}°", _subtituloStyle, GUILayout.Width(90));
                GUI.backgroundColor = new Color(0.25f, 0.25f, 0.28f);
                if (GUILayout.Button("↩ Reset", GUILayout.Height(20f), GUILayout.Width(60f)))
                {
                    _scene.ResetRotation();
                    Repaint();
                }
                GUI.backgroundColor = prevColor;
                EditorGUILayout.EndHorizontal();
            });

            // Camadas e Raycast
            DrawPanel(() => 
            {
                GUILayout.Label("📚 Camadas", _panelTitleStyle);
                GUILayout.Space(12);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Raycast", _subtituloStyle, GUILayout.Width(70));
                _scene.CurrentRaycastMode = (RaycastMode)EditorGUILayout.EnumPopup(_scene.CurrentRaycastMode);
                EditorGUILayout.EndHorizontal();
                
                GUILayout.Space(8);

                GUI.enabled = _scene.CurrentRaycastMode == RaycastMode.PlanoFixo;
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Camada Y", _subtituloStyle, GUILayout.Width(70));
                float novoY = EditorGUILayout.Slider(_camadaPinturaY, -10f, 20f);
                if (!Mathf.Approximately(novoY, _camadaPinturaY))
                {
                    _camadaPinturaY = novoY;
                    _scene.PaintLayerY = _camadaPinturaY;
                }
                EditorGUILayout.EndHorizontal();
                GUI.enabled = true;
            });

            // Grade e Assets
            DrawPanel(() => 
            {
                GUILayout.Label("📐 Configurações", _panelTitleStyle);
                GUILayout.Space(12);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Paleta", _subtituloStyle, GUILayout.Width(50));
                EditorGUI.BeginChangeCheck();
                _palette = (LevelPalette)EditorGUILayout.ObjectField(_palette, typeof(LevelPalette), false);
                if (EditorGUI.EndChangeCheck() && _palette != null)
                    EditorPrefs.SetString(PrefChavePaleta, AssetDatabase.GetAssetPath(_palette));
                EditorGUILayout.EndHorizontal();
                
                GUILayout.Space(8);

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Grade", _subtituloStyle, GUILayout.Width(50));
                EditorGUI.BeginChangeCheck();
                _tamanhoGrade = EditorGUILayout.FloatField(_tamanhoGrade);
                if (EditorGUI.EndChangeCheck())
                    _tamanhoGrade = Mathf.Max(0.1f, _tamanhoGrade);
                EditorGUILayout.EndHorizontal();
            });

            // Ações
            DrawPanel(() => 
            {
                GUILayout.Label("⚡ Ações", _panelTitleStyle);
                GUILayout.Space(12);

                _nomeDoMapa = EditorGUILayout.TextField(_nomeDoMapa);
                GUILayout.Space(6);
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Salvar", GUILayout.Height(24))) SalvarMapa();
                if (GUILayout.Button("Carregar", GUILayout.Height(24))) CarregarMapa();
                EditorGUILayout.EndHorizontal();
            });

            EditorGUILayout.EndScrollView();
        }

        private void DesenharBotaoFerramenta(PainterTool ferramenta, string label, Color cor, float width)
        {
            Color prev = GUI.backgroundColor;
            bool selecionado = _scene.CurrentTool == ferramenta;
            GUI.backgroundColor = selecionado ? cor : new Color(0.22f, 0.20f, 0.26f);

            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.fontSize = 11;
            if (selecionado) style.fontStyle = FontStyle.Bold;
            SetTextColor(style, selecionado ? Color.white : TextHigh);

            if (GUILayout.Button(label, style, GUILayout.Height(26f), GUILayout.Width(width)))
            {
                _scene.CurrentTool = ferramenta;
                DefinirStatus($"Ferramenta: {ferramenta}");
            }
            GUI.backgroundColor = prev;
        }

        private void DesenharRightPanel()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("🎨 Paleta de Tiles", _tituloJanelaStyle);
            
            if (_scene.IsHoveringValid)
            {
                GUILayout.FlexibleSpace();
                GUI.backgroundColor = PanelColor;
                GUIStyle coordStyle = new GUIStyle(GUI.skin.box);
                SetTextColor(coordStyle, TextNormal);
                GUILayout.Label($"X: {_scene.HoveredCell.x}, Y: {_scene.HoveredCell.y}, Z: {_scene.HoveredCell.z}", coordStyle);
                GUI.backgroundColor = Color.white;
            }
            EditorGUILayout.EndHorizontal();
            
            GUILayout.Label("Selecione um tile e pinte seu nível diretamente no canvas (Scene View).", _subtituloStyle);
            GUILayout.Space(10);
            
            DrawPanel(() => 
            {
                if (_palette == null)
                {
                    EditorGUILayout.HelpBox("Nenhuma paleta configurada na aba de configurações.", MessageType.Warning);
                    return;
                }

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("🔍 Buscar:", _subtituloStyle, GUILayout.Width(60));
                _buscaQuery = EditorGUILayout.TextField(_buscaQuery, EditorStyles.toolbarSearchField);
                if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(20f)))
                    _buscaQuery = "";
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(10);

                DesenharTilesRecentes();
                DesenharAbas();
                DesenharGradeTiles();
            });
            
            GUILayout.FlexibleSpace();
            DesenharBarraStatus();
        }



        // Old methods removed/merged

        private void DesenharTilesRecentes()
        {
            if (_tilesRecentes.Count == 0) return;

            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("⏱ Recentes:", _subtituloStyle, GUILayout.Width(70f));
            
            var recentesList = _tilesRecentes.Reverse().ToList();
            foreach (var t in recentesList)
            {
                if (GUILayout.Button(new GUIContent(t.icon != null ? t.icon.texture : null, t.tileName), 
                                     GUILayout.Width(28f), GUILayout.Height(28f)))
                {
                    _categoriaAtiva = t.category;
                    SelecionarTile(t);
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            GUILayout.Space(4f);
        }

        private void DesenharAbas()
        {
            EditorGUILayout.BeginHorizontal();
            foreach (TileCategory cat in Enum.GetValues(typeof(TileCategory)))
            {
                bool ativo = cat == _categoriaAtiva;
                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = ativo ? PurpleAccent : new Color(0.20f, 0.19f, 0.24f);

                string icone = cat switch
                {
                    TileCategory.Chao => "⬛",
                    TileCategory.Trilhos => "🛤",
                    TileCategory.Estruturas => "🏗",
                    TileCategory.Aderecos => "📦",
                    TileCategory.Vegetacao => "🌿",
                    _ => "■"
                };
                
                int count = _palette == null ? 0 : _palette.GetByCategory(cat).Count();
                GUIStyle abaStyle = new GUIStyle(EditorStyles.toolbarButton);
                abaStyle.fontSize = 11;
                abaStyle.fontStyle = ativo ? FontStyle.Bold : FontStyle.Normal;
                abaStyle.fixedHeight = 26f;
                SetTextColor(abaStyle, ativo ? Color.white : TextNormal);
                
                if (GUILayout.Button($"{icone} {cat} ({count})", abaStyle))
                    _categoriaAtiva = cat;

                GUI.backgroundColor = prev;
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(12f);
        }

        private void DesenharGradeTiles()
        {
            IEnumerable<TileItem> itens = string.IsNullOrEmpty(_buscaQuery)
                ? _palette.GetByCategory(_categoriaAtiva)
                : _palette.SearchInCategory(_buscaQuery, _categoriaAtiva);

            var lista = itens.ToList();

            if (lista.Count == 0)
            {
                EditorGUILayout.HelpBox("Nenhum tile nesta categoria.", MessageType.Info);
                return;
            }

            float larguraUtil = position.width - 24f;
            int colunas = Mathf.Max(1, Mathf.FloorToInt(larguraUtil / TamanhItemPaleta));


            int linhas = Mathf.CeilToInt((float)lista.Count / colunas);
            float alturaScroll = Mathf.Clamp(linhas * (TamanhItemPaleta + 16f) + 6f, 0f, 240f);

            _scrollPaleta = EditorGUILayout.BeginScrollView(
                _scrollPaleta, GUILayout.Height(alturaScroll));

            int col = 0;
            for (int i = 0; i < lista.Count; i++)
            {
                if (col == 0) EditorGUILayout.BeginHorizontal();

                DesenharItemTile(lista[i]);
                col++;

                if (col >= colunas)
                {
                    GUILayout.FlexibleSpace();
                    EditorGUILayout.EndHorizontal();
                    col = 0;
                }
            }
            if (col > 0)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DesenharItemTile(TileItem tile)
        {
            bool selecionado = _tileSelecionado == tile;
            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = selecionado ? PurpleAccent : new Color(0.18f, 0.17f, 0.22f);

            Rect r = GUILayoutUtility.GetRect(
                TamanhItemPaleta, TamanhItemPaleta + 16f,
                GUILayout.Width(TamanhItemPaleta));


            EditorGUI.DrawRect(r, selecionado
                ? new Color(0.45f, 0.35f, 0.80f, 0.40f) // Purple tint
                : new Color(0.16f, 0.15f, 0.20f, 0.60f));


            if (selecionado)
            {
                Handles.BeginGUI();
                Handles.color = PurpleAccent;
                Handles.DrawSolidRectangleWithOutline(r, Color.clear, PurpleAccent);
                Handles.EndGUI();
            }


            Rect iconRect = new Rect(r.x + 4f, r.y + 4f, r.width - 8f, r.height - 20f);
            if (tile.icon != null)
            {
                GUI.DrawTexture(iconRect, tile.icon.texture, ScaleMode.ScaleToFit);
            }
            else if (tile.prefab != null)
            {
                Texture2D preview = AssetPreview.GetAssetPreview(tile.prefab);
                if (preview != null)
                    GUI.DrawTexture(iconRect, preview, ScaleMode.ScaleToFit);
                else
                {
                    EditorGUI.DrawRect(iconRect, new Color(0.3f, 0.3f, 0.3f));
                    GUI.Label(iconRect, "📦", new GUIStyle
                    {
                        fontSize = 24,
                        alignment = TextAnchor.MiddleCenter
                    });
                }
            }
            else
            {
                EditorGUI.DrawRect(iconRect, new Color(0.25f, 0.25f, 0.25f));
                GUI.Label(iconRect, "?", new GUIStyle
                {
                    fontSize = 28,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.gray }
                });
            }

            Rect labelRect = new Rect(r.x, r.y + r.height - 18f, r.width, 18f);
            
            GUIStyle tileLabelStyle = new GUIStyle(EditorStyles.miniLabel);
            tileLabelStyle.alignment = TextAnchor.MiddleCenter;
            SetTextColor(tileLabelStyle, selecionado ? Color.white : TextNormal);
            
            GUI.Label(labelRect, tile.tileName, tileLabelStyle);


            if (Event.current.type == EventType.MouseDown &&
                r.Contains(Event.current.mousePosition))
            {
                SelecionarTile(tile);
                Event.current.Use();
            }

            GUI.backgroundColor = prev;
        }

        private void SelecionarTile(TileItem tile)
        {
            _tileSelecionado = tile;
            _scene.SelectedTile = tile;
            DefinirStatus($"Selecionado: {tile.tileName}");

            if (tile != null && !_tilesRecentes.Contains(tile))
            {
                if (_tilesRecentes.Count >= 5) _tilesRecentes.Dequeue();
                _tilesRecentes.Enqueue(tile);
            }

            Repaint();
        }

        private void AoPegarTileComContaGotas(PlacedTile placed)
        {
            if (_palette == null || placed == null) return;
            var tile = _palette.Tiles.FirstOrDefault(t => t.tileName == placed.TileName && t.category == placed.Category);
            if (tile != null)
            {
                _categoriaAtiva = tile.category;
                SelecionarTile(tile);
            }
        }



        // Old sections removed

        private void SalvarMapa()
        {
            string caminho = EditorUtility.SaveFilePanel("Salvar Mapa do Nível", Application.dataPath, _nomeDoMapa, "json");
            if (string.IsNullOrEmpty(caminho)) return;
            try
            {
                _db.SaveToFile(caminho, _nomeDoMapa);
                DefinirStatus($"Mapa salvo: {Path.GetFileName(caminho)}");
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                DefinirStatus($"Erro ao salvar: {ex.Message}");
                Debug.LogError($"[LevelPainter] Erro ao salvar: {ex}");
            }
        }

        private void CarregarMapa()
        {
            string caminho = EditorUtility.OpenFilePanel("Carregar Mapa do Nível", Application.dataPath, "json");
            if (string.IsNullOrEmpty(caminho)) return;
            if (_palette == null)
            {
                EditorUtility.DisplayDialog("Level Painter", "Atribua uma Paleta antes de carregar um mapa.", "OK");
                return;
            }
            try
            {
                var dados = GridDatabase.LoadFromFile(caminho);
                if (dados == null) { DefinirStatus("Falha ao carregar: arquivo inválido."); return; }
                _nomeDoMapa = dados.mapName;
                _scene.EnsureHierarchy();
                _scene.RebuildFromMapData(dados, _palette);
                DefinirStatus($"Mapa carregado: {dados.mapName} ({dados.tiles?.Length ?? 0} tiles)");
            }
            catch (Exception ex)
            {
                DefinirStatus($"Erro ao carregar: {ex.Message}");
                Debug.LogError($"[LevelPainter] Erro ao carregar: {ex}");
            }
        }


        private void DesenharBarraStatus()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            string statusPintura = _pintando ? "● Ativo" : "○ Inativo";
            
            GUIStyle statusStyle = new GUIStyle(EditorStyles.miniLabel);
            SetTextColor(statusStyle, TextHigh);

            GUILayout.Label(statusPintura, statusStyle, GUILayout.Width(60f));
            GUILayout.Label(_statusMsg, statusStyle);
            GUILayout.FlexibleSpace();
            int total = _db?.Grid.Count ?? 0;
            GUILayout.Label($"Tiles: {total}", statusStyle, GUILayout.Width(70f));
            EditorGUILayout.EndHorizontal();
        }



        // Removed DesenharCabecalhoSecao



        private void AlternarPintura()
        {
            if (_pintando) PararPintura();
            else IniciarPintura();
        }

        private void IniciarPintura()
        {
            if (_palette == null)
            {
                EditorUtility.DisplayDialog(
                    "Level Painter",
                    "Atribua uma Level Palette antes de iniciar a pintura.",
                    "OK");
                return;
            }
            _pintando = true;
            _scene.EnsureHierarchy();
            _scene.PaintLayerY = _camadaPinturaY;
            SceneView.RepaintAll();
            DefinirStatus("Pintura iniciada.");
        }

        private void PararPintura()
        {
            _pintando = false;
            SceneView.RepaintAll();
            DefinirStatus("Pintura encerrada.");
        }



        private void DefinirStatus(string msg)
        {
            _statusMsg = msg;
            Repaint();
        }

        private void ReconstruirDatabaseDaCena()
        {
            if (_db == null || _palette == null) return;
            _db.Clear();

            var levelGO = GameObject.Find("Nível");
            if (levelGO == null) return;

            foreach (TileCategory cat in Enum.GetValues(typeof(TileCategory)))
            {
                var catTransform = levelGO.transform.Find(cat.ToString());
                if (catTransform == null) continue;

                foreach (Transform filho in catTransform)
                {
                    if (filho == null) continue;
                    var cell = _db.WorldToCell(filho.position);
                    var prefab = PrefabUtility.GetCorrespondingObjectFromSource(filho.gameObject);
                    float rotX = filho.eulerAngles.x;
                    float rotY = filho.eulerAngles.y;
                    float rotZ = filho.eulerAngles.z;

                    string nomeTile = "Desconhecido";
                    foreach (var t in _palette.Tiles)
                    {
                        if (t.prefab == prefab) { nomeTile = t.tileName; break; }
                    }
                    _db.Place(cell, new PlacedTile(filho.gameObject, prefab, cat, nomeTile, rotX, rotY, rotZ));
                }
            }
        }

        private void CriarNovaPaleta()
        {
            string caminho = EditorUtility.SaveFilePanelInProject(
                "Criar Level Palette", "LevelPalette", "asset", "Salvar asset da paleta");
            if (string.IsNullOrEmpty(caminho)) return;

            var paleta = CreateInstance<LevelPalette>();
            AssetDatabase.CreateAsset(paleta, caminho);
            AssetDatabase.SaveAssets();
            _palette = paleta;
            EditorPrefs.SetString(PrefChavePaleta, caminho);
            DefinirStatus("Nova paleta criada.");
        }
    }
}
