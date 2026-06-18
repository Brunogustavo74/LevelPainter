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


        private float _tamanhoGrade = 1f;
        private float _camadaPinturaY = 0f;
        private bool _mostrarSettings = false;


        private Vector2 _scrollPrincipal;
        private bool _mostrarSalvarCarregar = false;
        private string _nomeDoMapa = "MeuNivel";
        private string _statusMsg = "Pronto";

        private static readonly Color CorPincel = new Color(0.20f, 0.80f, 0.30f);
        private static readonly Color CorBorracha = new Color(0.90f, 0.30f, 0.20f);
        private static readonly Color CorSubstituir = new Color(0.90f, 0.70f, 0.10f);
        private static readonly Color CorPainelEsc = new Color(0.15f, 0.15f, 0.15f);
        private static readonly Color CorDestaque = new Color(0.25f, 0.55f, 0.95f);



        [MenuItem("Tools/Level Painter %#l", priority = 200)]
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



        private void OnGUI()
        {
            DesenharBarraSuperior();
            DesenharModoAtivo();

            _scrollPrincipal = EditorGUILayout.BeginScrollView(_scrollPrincipal);
            DesenharSecaoPaleta();
            DesenharSecaoCamada();
            DesenharSecaoGrade();
            DesenharInfoSelecao();
            if (_mostrarSalvarCarregar) DesenharSecaoSalvarCarregar();
            if (_mostrarSettings) DesenharSecaoSettings();
            EditorGUILayout.EndScrollView();

            DesenharBarraStatus();
        }


        private void DesenharBarraSuperior()
        {
            Color prev = GUI.backgroundColor;
            GUI.backgroundColor = CorPainelEsc;
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUI.backgroundColor = prev;

            GUILayout.Label("🗺 Level Painter", EditorStyles.boldLabel, GUILayout.Width(120f));
            GUILayout.FlexibleSpace();

            Color corBotao = _pintando ? new Color(0.2f, 0.7f, 0.3f) : new Color(0.4f, 0.4f, 0.4f);
            GUI.backgroundColor = corBotao;
            string labelPintura = _pintando ? "● Pintando" : "○ Iniciar Pintura";
            if (GUILayout.Button(labelPintura, EditorStyles.toolbarButton, GUILayout.Width(115f)))
                AlternarPintura();
            GUI.backgroundColor = prev;

            GUILayout.Space(4f);
            _mostrarSalvarCarregar = GUILayout.Toggle(
                _mostrarSalvarCarregar, "💾 Salvar/Carregar",
                EditorStyles.toolbarButton, GUILayout.Width(105f));
            _mostrarSettings = GUILayout.Toggle(
                _mostrarSettings, "⚙", EditorStyles.toolbarButton, GUILayout.Width(24f));

            EditorGUILayout.EndHorizontal();
        }

        private void DesenharModoAtivo()
        {
            if (!_pintando)
            {
                EditorGUILayout.HelpBox(
                    "Clique em 'Iniciar Pintura' para ativar o pintor na Scene View.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Ferramenta:", GUILayout.Width(72f));

            DesenharBotaoFerramenta(PainterTool.Pincel, "✏ Pincel", CorPincel, "B");
            DesenharBotaoFerramenta(PainterTool.Borracha, "✕ Borracha", CorBorracha, "E");
            DesenharBotaoFerramenta(PainterTool.Substituir, "↺ Substituir", CorSubstituir, "T");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(76f);
            DesenharBotaoFerramenta(PainterTool.Retangulo, "🔲 Retângulo", CorPincel, "U");
            DesenharBotaoFerramenta(PainterTool.Linha, "➖ Linha", CorPincel, "I");
            GUILayout.FlexibleSpace();

            GUILayout.Label($"Rot: {_scene.CurrentRotationY:F0}°", GUILayout.Width(64f));
            if (GUILayout.Button("↩", GUILayout.Width(24f)))
            {
                _scene.ResetRotation();
                Repaint();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();

            if (_scene.IsHoveringValid)
            {
                var cell = _scene.HoveredCell;
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"Célula: ({cell.x}, {cell.y}, {cell.z})", EditorStyles.miniLabel);
                GUILayout.Label(
                    _tileSelecionado != null ? $"📦 {_tileSelecionado.tileName}" : "— Nenhum —",
                    EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DesenharBotaoFerramenta(
            PainterTool ferramenta, string label, Color cor, string atalho)
        {
            Color prev = GUI.backgroundColor;
            bool selecionado = _scene.CurrentTool == ferramenta;
            GUI.backgroundColor = selecionado ? cor : new Color(0.3f, 0.3f, 0.3f);

            if (GUILayout.Button(
                    new GUIContent(label, $"Atalho: {atalho}"),
                    GUILayout.Height(22f), GUILayout.Width(88f)))
            {
                _scene.CurrentTool = ferramenta;
                DefinirStatus($"Ferramenta: {ferramenta}");
            }
            GUI.backgroundColor = prev;
        }



        private void DesenharSecaoPaleta()
        {
            DesenharCabecalhoSecao("🎨 Paleta de Tiles");

            EditorGUI.BeginChangeCheck();
            _palette = (LevelPalette)EditorGUILayout.ObjectField(
                "Asset da Paleta", _palette, typeof(LevelPalette), false);
            if (EditorGUI.EndChangeCheck() && _palette != null)
                EditorPrefs.SetString(PrefChavePaleta, AssetDatabase.GetAssetPath(_palette));

            if (_palette == null)
            {
                EditorGUILayout.HelpBox(
                    "Arraste um LevelPalette aqui ou crie um novo.\n" +
                    "Botão direito no Projeto → Criar → Level Painter → Level Palette",
                    MessageType.Warning);
                if (GUILayout.Button("Criar Nova Paleta"))
                    CriarNovaPaleta();
                return;
            }


            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("🔍", GUILayout.Width(18f));
            _buscaQuery = EditorGUILayout.TextField(_buscaQuery, EditorStyles.toolbarSearchField);
            if (GUILayout.Button("✕", EditorStyles.toolbarButton, GUILayout.Width(20f)))
                _buscaQuery = "";
            EditorGUILayout.EndHorizontal();

            DesenharAbas();
            DesenharGradeTiles();
        }

        private void DesenharAbas()
        {
            EditorGUILayout.BeginHorizontal();
            foreach (TileCategory cat in Enum.GetValues(typeof(TileCategory)))
            {
                bool ativo = cat == _categoriaAtiva;
                Color prev = GUI.backgroundColor;
                GUI.backgroundColor = ativo ? CorDestaque : new Color(0.25f, 0.25f, 0.25f);

                string icone = cat switch
                {
                    TileCategory.Chao => "⬛",
                    TileCategory.Trilhos => "🛤",
                    TileCategory.Estruturas => "🏗",
                    TileCategory.Aderecos => "📦",
                    TileCategory.Vegetacao => "🌿",
                    _ => "■"
                };
                if (GUILayout.Button($"{icone} {cat}", GUILayout.Height(20f)))
                    _categoriaAtiva = cat;

                GUI.backgroundColor = prev;
            }
            EditorGUILayout.EndHorizontal();
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
            GUI.backgroundColor = selecionado ? CorDestaque : new Color(0.22f, 0.22f, 0.22f);

            Rect r = GUILayoutUtility.GetRect(
                TamanhItemPaleta, TamanhItemPaleta + 16f,
                GUILayout.Width(TamanhItemPaleta));


            EditorGUI.DrawRect(r, selecionado
                ? new Color(0.20f, 0.45f, 0.85f, 0.40f)
                : new Color(0.20f, 0.20f, 0.20f, 0.60f));


            if (selecionado)
            {
                Handles.BeginGUI();
                Handles.color = CorDestaque;
                Handles.DrawSolidRectangleWithOutline(r, Color.clear, CorDestaque);
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
            GUI.Label(labelRect, tile.tileName, new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = selecionado ? Color.white : Color.gray }
            });


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
            Repaint();
        }



        private void DesenharSecaoCamada()
        {
            DesenharCabecalhoSecao("🔧 Superfície e Camada");

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Raycast:", GUILayout.Width(68f));
            _scene.CurrentRaycastMode = (RaycastMode)EditorGUILayout.EnumPopup(_scene.CurrentRaycastMode);
            EditorGUILayout.EndHorizontal();

            GUI.enabled = _scene.CurrentRaycastMode == RaycastMode.PlanoFixo;
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Camada Y:", GUILayout.Width(68f));
            float novoY = EditorGUILayout.Slider(_camadaPinturaY, -10f, 20f);
            if (!Mathf.Approximately(novoY, _camadaPinturaY))
            {
                _camadaPinturaY = novoY;
                _scene.PaintLayerY = _camadaPinturaY;
            }
            if (GUILayout.Button("0", GUILayout.Width(22f)))
            {
                _camadaPinturaY = 0f;
                _scene.PaintLayerY = 0f;
            }
            EditorGUILayout.EndHorizontal();
            GUI.enabled = true;
        }



        private void DesenharSecaoGrade()
        {
            DesenharCabecalhoSecao("📐 Configuração da Grade");
            EditorGUI.BeginChangeCheck();
            _tamanhoGrade = EditorGUILayout.FloatField("Tamanho da Célula", _tamanhoGrade);
            if (EditorGUI.EndChangeCheck())
            {
                _tamanhoGrade = Mathf.Max(0.1f, _tamanhoGrade);
                DefinirStatus($"Tamanho da grade: {_tamanhoGrade}");
            }
        }



        private void DesenharInfoSelecao()
        {
            if (_scene?.SelectedPlacedTile == null) return;

            DesenharCabecalhoSecao("ℹ️ Tile Selecionado");
            var t = _scene.SelectedPlacedTile;
            EditorGUILayout.LabelField("Nome", t.TileName);
            EditorGUILayout.LabelField("Categoria", t.Category.ToString());
            EditorGUILayout.LabelField("Posição", _scene.SelectedCell.ToString());
            EditorGUILayout.LabelField("Rotação Y", $"{t.RotationY:F0}°");
        }



        private void DesenharSecaoSalvarCarregar()
        {
            DesenharCabecalhoSecao("💾 Salvar / Carregar");
            _nomeDoMapa = EditorGUILayout.TextField("Nome do Mapa", _nomeDoMapa);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("💾 Salvar Mapa"))
                SalvarMapa();
            if (GUILayout.Button("📂 Carregar Mapa"))
                CarregarMapa();
            EditorGUILayout.EndHorizontal();
        }

        private void SalvarMapa()
        {
            string caminho = EditorUtility.SaveFilePanel(
                "Salvar Mapa do Nível", Application.dataPath, _nomeDoMapa, "json");
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
            string caminho = EditorUtility.OpenFilePanel(
                "Carregar Mapa do Nível", Application.dataPath, "json");
            if (string.IsNullOrEmpty(caminho)) return;

            if (_palette == null)
            {
                EditorUtility.DisplayDialog(
                    "Level Painter",
                    "Atribua uma Paleta antes de carregar um mapa.",
                    "OK");
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



        private void DesenharSecaoSettings()
        {
            DesenharCabecalhoSecao("⚙ Atalhos de Teclado");
            EditorGUILayout.HelpBox(
                "B — Pincel\n" +
                "E — Borracha\n" +
                "T — Substituir\n" +
                "U — Retângulo (Preencher)\n" +
                "I — Linha\n" +
                "R — Girar 90°\n" +
                "LMB — Pintar\n" +
                "RMB — Apagar",
                MessageType.None);
        }


        private void DesenharBarraStatus()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            string statusPintura = _pintando ? "● Ativo" : "○ Inativo";
            GUILayout.Label(statusPintura, EditorStyles.miniLabel, GUILayout.Width(60f));
            GUILayout.Label(_statusMsg, EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            int total = _db?.Grid.Count ?? 0;
            GUILayout.Label($"Tiles: {total}", EditorStyles.miniLabel, GUILayout.Width(70f));
            EditorGUILayout.EndHorizontal();
        }



        private void DesenharCabecalhoSecao(string titulo)
        {
            GUILayout.Space(4f);
            Rect r = GUILayoutUtility.GetRect(0f, 20f, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(r, new Color(0.13f, 0.13f, 0.13f));
            GUI.Label(new Rect(r.x + 6f, r.y + 2f, r.width, r.height), titulo,
                new GUIStyle(EditorStyles.boldLabel) { fontSize = 11 });
            GUILayout.Space(2f);
        }



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
                    float rotY = filho.eulerAngles.y;

                    string nomeTile = "Desconhecido";
                    foreach (var t in _palette.Tiles)
                    {
                        if (t.prefab == prefab) { nomeTile = t.tileName; break; }
                    }
                    _db.Place(cell, new PlacedTile(filho.gameObject, prefab, cat, nomeTile, rotY));
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
