using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace LevelPainter
{
    public enum PainterTool { Pincel, Borracha, Substituir, Retangulo, Linha }
    public enum RaycastMode { PlanoFixo, ColisoresFisicos }

 

 
    public class LevelPainterScene
    {

        public PainterTool CurrentTool { get; set; } = PainterTool.Pincel;
        public TileItem SelectedTile { get; set; }
        public float CurrentRotationY { get; private set; } = 0f;
        public Vector3Int HoveredCell { get; private set; }
        public bool IsHoveringValid { get; private set; }
        public PlacedTile SelectedPlacedTile { get; private set; }
        public Vector3Int SelectedCell { get; private set; }

        public RaycastMode CurrentRaycastMode { get; set; } = RaycastMode.PlanoFixo;


        private bool _isDraggingBoxOrLine = false;
        private Vector3Int _dragStartCell;
        private Vector3Int _dragCurrentCell;


        private Vector2Int _lastPaintedXZ = new Vector2Int(int.MinValue, int.MinValue);
        private bool _isPaintDragging = false;


        private GridDatabase _db;
        private Transform _levelRoot;
        private readonly Dictionary<TileCategory, Transform> _categoryRoots = new();


        private GameObject _ghostObject;
        private Material _ghostMaterial;
        private TileItem _lastGhostTile;
        private float _lastGhostRotation = -9999f;


        private float _paintLayerY = 0f;
        public float PaintLayerY
        {
            get => _paintLayerY;
            set { _paintLayerY = value; }
        }

        private int _controlId = -1;


        public System.Action RepaintRequest;
        public System.Action<PlacedTile> OnEyedropperPick;

        private static Dictionary<(TileItem, float), Vector3> _snapOffsetCache = new();
        private static Dictionary<(TileItem, float), float> _pivotOffsetCache = new();

        public LevelPainterScene()
        {
            BuildGhostMaterial();
        }

        public void SetDatabase(GridDatabase db) => _db = db;



        public void EnsureHierarchy()
        {
            var levelGO = GameObject.Find("Nível");
            if (levelGO == null)
            {
                levelGO = new GameObject("Nível");
                Undo.RegisterCreatedObjectUndo(levelGO, "Criar Raiz do Nível");
            }
            _levelRoot = levelGO.transform;

            foreach (TileCategory cat in System.Enum.GetValues(typeof(TileCategory)))
            {
                string catName = cat.ToString();
                var existing = _levelRoot.Find(catName);
                if (existing == null)
                {
                    var go = new GameObject(catName);
                    Undo.RegisterCreatedObjectUndo(go, $"Criar Container {catName}");
                    go.transform.SetParent(_levelRoot, false);
                    _categoryRoots[cat] = go.transform;
                }
                else
                {
                    _categoryRoots[cat] = existing;
                }
            }
        }

        private Transform GetCategoryRoot(TileCategory cat)
        {
            if (_categoryRoots.TryGetValue(cat, out var t) && t != null) return t;
            EnsureHierarchy();
            return _categoryRoots.TryGetValue(cat, out t) ? t : _levelRoot;
        }



        public void OnSceneGUI(SceneView sceneView)
        {
            if (_db == null) return;

            Event e = Event.current;


            if (_controlId == -1 || e.type == EventType.Layout)
                _controlId = GUIUtility.GetControlID(FocusType.Passive);

            if (e.type == EventType.Layout)
                HandleUtility.AddDefaultControl(_controlId);

            HandleKeyboardShortcuts(e);


            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            bool hitPlane = PerformRaycast(ray, out Vector3 worldHit);

            if (hitPlane)
            {
                HoveredCell = _db.WorldToCell(worldHit);
                IsHoveringValid = true;

                if (_isDraggingBoxOrLine)
                    _dragCurrentCell = HoveredCell;


                ShowGhost();
                UpdateGhostPosition();
                DrawCellHighlight();
                DrawCoordinatesLabel(worldHit);
            }
            else
            {
                IsHoveringValid = false;
                HideGhost();
            }


            if (e.type == EventType.MouseDown && e.button == 0 && e.alt && hitPlane)
            {
                Vector3Int targetCell = FindOccupiedCellInColumn(HoveredCell);
                if (_db.TryGet(targetCell, out var placed))
                {
                    OnEyedropperPick?.Invoke(placed);
                }
                e.Use();
                RepaintRequest?.Invoke();
            }

            if (e.type == EventType.MouseDown && e.button == 0 && !e.alt && hitPlane)
            {
                if (CurrentTool == PainterTool.Retangulo || CurrentTool == PainterTool.Linha)
                {
                    _isDraggingBoxOrLine = true;
                    _dragStartCell = HoveredCell;
                    _dragCurrentCell = HoveredCell;
                    e.Use();
                }
                else
                {

                    _isPaintDragging = true;
                    _lastPaintedXZ = new Vector2Int(int.MinValue, int.MinValue);
                    HandleLeftClick(e);
                }
            }


            if (e.type == EventType.MouseDrag && e.button == 0 && !e.alt && hitPlane)
            {
                if (_isDraggingBoxOrLine)
                {
                    _dragCurrentCell = HoveredCell;
                    e.Use();
                    sceneView.Repaint();
                }
                else if (_isPaintDragging)
                {

                    var xz = new Vector2Int(HoveredCell.x, HoveredCell.z);
                    bool deveBloquear = CurrentTool == PainterTool.Pincel && xz == _lastPaintedXZ;
                    if (!deveBloquear)
                        HandleLeftClick(e);
                    else
                        e.Use();
                }
            }


            if (e.type == EventType.MouseUp && e.button == 0)
            {
                _isPaintDragging = false;
                _lastPaintedXZ = new Vector2Int(int.MinValue, int.MinValue);

                if (_isDraggingBoxOrLine)
                {
                    ApplyDragArea();
                    _isDraggingBoxOrLine = false;
                    e.Use();
                    RepaintRequest?.Invoke();
                }
            }


            if (e.type == EventType.MouseDown && e.button == 1 && !e.alt && hitPlane)
                HandleRightClick(e);

            sceneView.Repaint();
        }



        private void HandleKeyboardShortcuts(Event e)
        {
            if (e.type != EventType.KeyDown) return;

            switch (e.keyCode)
            {
                case KeyCode.B:
                    CurrentTool = PainterTool.Pincel;
                    e.Use();
                    RepaintRequest?.Invoke();
                    break;
                case KeyCode.E:
                    CurrentTool = PainterTool.Borracha;
                    e.Use();
                    RepaintRequest?.Invoke();
                    break;
                case KeyCode.T:
                    CurrentTool = PainterTool.Substituir;
                    e.Use();
                    RepaintRequest?.Invoke();
                    break;
                case KeyCode.R:
                    RotateCurrentTile();
                    e.Use();
                    RepaintRequest?.Invoke();
                    break;
                case KeyCode.U:
                    CurrentTool = PainterTool.Retangulo;
                    e.Use();
                    RepaintRequest?.Invoke();
                    break;
                case KeyCode.I:
                    CurrentTool = PainterTool.Linha;
                    e.Use();
                    RepaintRequest?.Invoke();
                    break;
            }
        }

        private void HandleLeftClick(Event e)
        {
            switch (CurrentTool)
            {
                case PainterTool.Pincel:
                    PlaceTile(HoveredCell);
                    _lastPaintedXZ = new Vector2Int(HoveredCell.x, HoveredCell.z);
                    break;
                case PainterTool.Borracha:
                    EraseTile(FindOccupiedCellInColumn(HoveredCell));
                    _lastPaintedXZ = new Vector2Int(HoveredCell.x, HoveredCell.z);
                    break;
                case PainterTool.Substituir:
                    ReplaceTile(HoveredCell);
                    _lastPaintedXZ = new Vector2Int(HoveredCell.x, HoveredCell.z);
                    break;
            }
            e.Use();
            RepaintRequest?.Invoke();
        }

        private void HandleRightClick(Event e)
        {
            EraseTile(FindOccupiedCellInColumn(HoveredCell));
            e.Use();
            RepaintRequest?.Invoke();
        }



        private static Vector3 ComputeSnapOffset(TileItem tile, float rotationY)
        {
            if (tile?.prefab == null) return tile?.positionOffset ?? Vector3.zero;
            if (tile.snapMode == TileSnapMode.Manual)
                return tile.positionOffset;

            var key = (tile, rotationY);
            if (_snapOffsetCache.TryGetValue(key, out var cached))
                return cached;

            var temp = Object.Instantiate(tile.prefab);
            temp.hideFlags = HideFlags.HideAndDontSave;


            Quaternion rotation = Quaternion.Euler(
                tile.rotationOffset.x,
                rotationY + tile.rotationOffset.y,
                tile.rotationOffset.z);

            temp.transform.SetPositionAndRotation(Vector3.zero, rotation);


            if (tile.scaleOverride.sqrMagnitude > 0.0001f)
                temp.transform.localScale = tile.scaleOverride;


            var renderers = temp.GetComponentsInChildren<Renderer>();
            bool hasBounds = false;
            Bounds combined = default;

            foreach (var r in renderers)
            {
                if (!hasBounds) { combined = r.bounds; hasBounds = true; }
                else combined.Encapsulate(r.bounds);
            }

            Object.DestroyImmediate(temp);

            if (!hasBounds)
                return tile.positionOffset;


            float offsetX = -combined.center.x;
            float offsetZ = -combined.center.z;
            float offsetY = tile.snapMode == TileSnapMode.AutoCenterXZOnly
                ? 0f
                : -combined.min.y;

            Vector3 result = new Vector3(offsetX, offsetY, offsetZ) + tile.positionOffset;
            _snapOffsetCache[key] = result;
            return result;
        }

        private bool FindStackCell(Vector3Int baseCell, out Vector3Int resultCell, out Vector3 resultWorldPos)
        {
            resultCell = baseCell;
            resultWorldPos = Vector3.zero;

            if (SelectedTile?.prefab == null) return false;

            Vector3Int cell = baseCell;
            for (int attempt = 0; attempt < 32; attempt++)
            {
                if (!_db.IsOccupied(cell))
                {
                    if (attempt == 0)
                    {

                        Vector3 snapOffset = ComputeSnapOffset(SelectedTile, CurrentRotationY);
                        resultCell = cell;
                        resultWorldPos = _db.CellToWorld(cell) + snapOffset;
                    }
                    else
                    {

                        _db.TryGet(new Vector3Int(cell.x, cell.y - 1, cell.z), out PlacedTile below);

                        float baseY = _stackTopY;
                        Vector3 xzSnap = ComputeSnapOffset(SelectedTile, CurrentRotationY);

                        float pivotCorrY = ComputePivotBaseOffsetY(SelectedTile, CurrentRotationY);
                        resultCell = cell;
                        resultWorldPos = new Vector3(
                            _db.CellToWorld(cell).x + xzSnap.x,
                            baseY + pivotCorrY,
                            _db.CellToWorld(cell).z + xzSnap.z);
                    }
                    return true;
                }

                _db.TryGet(cell, out PlacedTile existing);
                _stackTopY = GetInstanceTopY(existing);

                int nextCellY = Mathf.CeilToInt(_stackTopY / _db.CellSize);
                if (nextCellY <= cell.y) nextCellY = cell.y + 1;
                cell = new Vector3Int(cell.x, nextCellY, cell.z);
            }

            return false;
        }


        private float _stackTopY;


        private static float ComputePivotBaseOffsetY(TileItem tile, float rotationY)
        {
            if (tile?.prefab == null || tile.snapMode == TileSnapMode.Manual)
                return tile?.positionOffset.y ?? 0f;

            var key = (tile, rotationY);
            if (_pivotOffsetCache.TryGetValue(key, out var cached))
                return cached;

            var temp = Object.Instantiate(tile.prefab);
            temp.hideFlags = HideFlags.HideAndDontSave;

            Quaternion rotation = Quaternion.Euler(
                tile.rotationOffset.x,
                rotationY + tile.rotationOffset.y,
                tile.rotationOffset.z);
            temp.transform.SetPositionAndRotation(Vector3.zero, rotation);

            if (tile.scaleOverride.sqrMagnitude > 0.0001f)
                temp.transform.localScale = tile.scaleOverride;

            var renderers = temp.GetComponentsInChildren<Renderer>();
            bool hasBounds = false;
            Bounds combined = default;
            foreach (var r in renderers)
            {
                if (!hasBounds) { combined = r.bounds; hasBounds = true; }
                else combined.Encapsulate(r.bounds);
            }
            Object.DestroyImmediate(temp);

            if (!hasBounds) {
                _pivotOffsetCache[key] = tile.positionOffset.y;
                return tile.positionOffset.y;
            }

            float result = -combined.min.y + tile.positionOffset.y;
            _pivotOffsetCache[key] = result;
            return result;
        }


        private float GetInstanceTopY(PlacedTile placed)
        {
            if (placed?.Instance == null) return 0f;

            var renderers = placed.Instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return placed.Instance.transform.position.y + _db.CellSize;

            bool hasBounds = false;
            Bounds combined = default;
            foreach (var r in renderers)
            {
                if (!hasBounds) { combined = r.bounds; hasBounds = true; }
                else combined.Encapsulate(r.bounds);
            }
            return combined.max.y;
        }

        private void PlaceTile(Vector3Int cell)
        {
            if (SelectedTile?.prefab == null) return;

            EnsureHierarchy();


            if (!FindStackCell(cell, out Vector3Int targetCell, out Vector3 worldPos))
                return;

            Quaternion rotation = Quaternion.Euler(
                SelectedTile.rotationOffset.x,
                CurrentRotationY + SelectedTile.rotationOffset.y,
                SelectedTile.rotationOffset.z);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(SelectedTile.prefab);
            instance.transform.SetParent(GetCategoryRoot(SelectedTile.category), true);
            instance.transform.position = worldPos;
            instance.transform.rotation = rotation;

            if (SelectedTile.scaleOverride.sqrMagnitude > 0.0001f)
                instance.transform.localScale = SelectedTile.scaleOverride;

            Undo.RegisterCreatedObjectUndo(instance, "Pintar Tile");

            _db.Place(targetCell, new PlacedTile(
                instance, SelectedTile.prefab,
                SelectedTile.category, SelectedTile.tileName,
                SelectedTile.rotationOffset.x, CurrentRotationY, SelectedTile.rotationOffset.z));
        }

        private void EraseTile(Vector3Int cell)
        {

            Vector3Int target = FindOccupiedCellInColumn(cell);

            if (!_db.TryGet(target, out var placed)) return;

            if (placed.Instance != null)
                Undo.DestroyObjectImmediate(placed.Instance);

            _db.Remove(target);

            if (SelectedCell == target)
                SelectedPlacedTile = null;
        }


        private void EraseColumn(Vector3Int cell)
        {

            var toErase = new List<Vector3Int>();
            foreach (var kvp in _db.Grid)
            {
                if (kvp.Key.x == cell.x && kvp.Key.z == cell.z)
                    toErase.Add(kvp.Key);
            }

            foreach (var key in toErase)
            {
                if (!_db.TryGet(key, out var placed)) continue;

                if (placed.Instance != null)
                    Undo.DestroyObjectImmediate(placed.Instance);

                _db.Remove(key);

                if (SelectedCell == key)
                    SelectedPlacedTile = null;
            }
        }


        private Vector3Int FindOccupiedCellInColumn(Vector3Int cell)
        {
            float clickWorldY = _db.CellToWorld(cell).y;
            float bestDist = float.MaxValue;
            Vector3Int best = cell;
            bool found = false;

            foreach (var kvp in _db.Grid)
            {
                if (kvp.Key.x != cell.x || kvp.Key.z != cell.z) continue;


                float tileY = kvp.Value.Instance != null
                    ? kvp.Value.Instance.transform.position.y
                    : _db.CellToWorld(kvp.Key).y;

                float dist = Mathf.Abs(tileY - clickWorldY);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = kvp.Key;
                    found = true;
                }
            }

            return found ? best : cell;
        }

        private void ReplaceTile(Vector3Int cell)
        {
            if (SelectedTile?.prefab == null) return;

            Vector3Int targetCell = FindOccupiedCellInColumn(cell);
            EraseTile(targetCell);

            EnsureHierarchy();
            Vector3 snapOffset = ComputeSnapOffset(SelectedTile, CurrentRotationY);
            Vector3 worldPos = _db.CellToWorld(targetCell) + snapOffset;

            Quaternion rotation = Quaternion.Euler(
                SelectedTile.rotationOffset.x,
                CurrentRotationY + SelectedTile.rotationOffset.y,
                SelectedTile.rotationOffset.z);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(SelectedTile.prefab);
            instance.transform.SetParent(GetCategoryRoot(SelectedTile.category), true);
            instance.transform.position = worldPos;
            instance.transform.rotation = rotation;

            if (SelectedTile.scaleOverride.sqrMagnitude > 0.0001f)
                instance.transform.localScale = SelectedTile.scaleOverride;

            Undo.RegisterCreatedObjectUndo(instance, "Substituir Tile");

            _db.Place(targetCell, new PlacedTile(
                instance, SelectedTile.prefab,
                SelectedTile.category, SelectedTile.tileName,
                SelectedTile.rotationOffset.x, CurrentRotationY, SelectedTile.rotationOffset.z));
        }



        private IEnumerable<Vector3Int> GetDragCells()
        {
            if (CurrentTool == PainterTool.Retangulo)
            {
                int minX = Mathf.Min(_dragStartCell.x, _dragCurrentCell.x);
                int maxX = Mathf.Max(_dragStartCell.x, _dragCurrentCell.x);
                int minY = Mathf.Min(_dragStartCell.y, _dragCurrentCell.y);
                int maxY = Mathf.Max(_dragStartCell.y, _dragCurrentCell.y);
                int minZ = Mathf.Min(_dragStartCell.z, _dragCurrentCell.z);
                int maxZ = Mathf.Max(_dragStartCell.z, _dragCurrentCell.z);

                for (int x = minX; x <= maxX; x++)
                    for (int y = minY; y <= maxY; y++)
                        for (int z = minZ; z <= maxZ; z++)
                            yield return new Vector3Int(x, y, z);
            }
            else if (CurrentTool == PainterTool.Linha)
            {
                int dx = Mathf.Abs(_dragCurrentCell.x - _dragStartCell.x);
                int dy = Mathf.Abs(_dragCurrentCell.y - _dragStartCell.y);
                int dz = Mathf.Abs(_dragCurrentCell.z - _dragStartCell.z);

                if (dx >= dz && dx >= dy)
                {
                    int step = _dragStartCell.x < _dragCurrentCell.x ? 1 : -1;
                    for (int x = _dragStartCell.x; x != _dragCurrentCell.x + step; x += step)
                    {
                        float t = dx == 0 ? 0 : (float)Mathf.Abs(x - _dragStartCell.x) / dx;
                        int y = Mathf.RoundToInt(Mathf.Lerp(_dragStartCell.y, _dragCurrentCell.y, t));
                        int z = Mathf.RoundToInt(Mathf.Lerp(_dragStartCell.z, _dragCurrentCell.z, t));
                        yield return new Vector3Int(x, y, z);
                    }
                }
                else if (dz >= dx && dz >= dy)
                {
                    int step = _dragStartCell.z < _dragCurrentCell.z ? 1 : -1;
                    for (int z = _dragStartCell.z; z != _dragCurrentCell.z + step; z += step)
                    {
                        float t = dz == 0 ? 0 : (float)Mathf.Abs(z - _dragStartCell.z) / dz;
                        int x = Mathf.RoundToInt(Mathf.Lerp(_dragStartCell.x, _dragCurrentCell.x, t));
                        int y = Mathf.RoundToInt(Mathf.Lerp(_dragStartCell.y, _dragCurrentCell.y, t));
                        yield return new Vector3Int(x, y, z);
                    }
                }
                else
                {
                    int step = _dragStartCell.y < _dragCurrentCell.y ? 1 : -1;
                    for (int y = _dragStartCell.y; y != _dragCurrentCell.y + step; y += step)
                    {
                        float t = dy == 0 ? 0 : (float)Mathf.Abs(y - _dragStartCell.y) / dy;
                        int x = Mathf.RoundToInt(Mathf.Lerp(_dragStartCell.x, _dragCurrentCell.x, t));
                        int z = Mathf.RoundToInt(Mathf.Lerp(_dragStartCell.z, _dragCurrentCell.z, t));
                        yield return new Vector3Int(x, y, z);
                    }
                }
            }
        }

        private void ApplyDragArea()
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            foreach (var cell in GetDragCells())
                PlaceTile(cell);

            Undo.CollapseUndoOperations(undoGroup);
        }



        public void RotateCurrentTile()
        {
            CurrentRotationY = (CurrentRotationY + 90f) % 360f;
            _lastGhostRotation = -9999f;
        }

        public void ResetRotation()
        {
            CurrentRotationY = 0f;
            _lastGhostRotation = -9999f;
        }



        private bool PerformRaycast(Ray ray, out Vector3 hit)
        {
            if (CurrentRaycastMode == RaycastMode.ColisoresFisicos)
            {

                if (Physics.Raycast(ray, out RaycastHit physHit, 1000f))
                {

                    if (physHit.collider.transform.root.name != "__GhostPreview__")
                    {
                        hit = physHit.point;
                        return true;
                    }
                }
            }


            var plane = new Plane(Vector3.up, new Vector3(0f, _paintLayerY, 0f));
            if (plane.Raycast(ray, out float dist))
            {
                hit = ray.GetPoint(dist);
                return true;
            }

            hit = Vector3.zero;
            return false;
        }

        private void BuildGhostMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            _ghostMaterial = new Material(shader);
            _ghostMaterial.name = "GhostPreviewMaterial";

            if (shader.name.StartsWith("Universal Render Pipeline"))
            {
                _ghostMaterial.SetFloat("_Surface", 1f);
                _ghostMaterial.SetFloat("_Blend", 0f);
                _ghostMaterial.SetFloat("_ZWrite", 0f);
                _ghostMaterial.SetFloat("_AlphaClip", 0f);
                _ghostMaterial.SetFloat("_Cull", 0f);

                _ghostMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _ghostMaterial.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                _ghostMaterial.DisableKeyword("_ALPHATEST_ON");

                _ghostMaterial.SetOverrideTag("RenderType", "Transparent");
                _ghostMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _ghostMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _ghostMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else
            {
                _ghostMaterial.SetFloat("_Mode", 2f);
                _ghostMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _ghostMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _ghostMaterial.SetInt("_ZWrite", 0);
                _ghostMaterial.EnableKeyword("_ALPHABLEND_ON");
                _ghostMaterial.renderQueue = 3000;
            }

            _ghostMaterial.color = new Color(0.3f, 0.8f, 1f, 0.5f);
        }

        private void UpdateGhostPosition()
        {
            if (_ghostObject == null) return;
            if (SelectedTile == null) return;

            Vector3 snapOffset = ComputeSnapOffset(SelectedTile, CurrentRotationY);
            _ghostObject.transform.position = _db.CellToWorld(HoveredCell) + snapOffset;
        }

        private void RebuildGhost()
        {
            DestroyGhost();
            if (SelectedTile?.prefab == null) return;

            _ghostObject = Object.Instantiate(SelectedTile.prefab);
            _ghostObject.name = "__GhostPreview__";
            _ghostObject.hideFlags = HideFlags.HideAndDontSave;

            foreach (var col in _ghostObject.GetComponentsInChildren<Collider>())
                col.enabled = false;

            foreach (var mono in _ghostObject.GetComponentsInChildren<MonoBehaviour>())
                mono.enabled = false;

            foreach (var rend in _ghostObject.GetComponentsInChildren<Renderer>())
            {
                rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend.receiveShadows = false;
                var mats = new Material[rend.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = _ghostMaterial;
                rend.sharedMaterials = mats;
            }

            Quaternion rot = Quaternion.Euler(
                SelectedTile.rotationOffset.x,
                CurrentRotationY + SelectedTile.rotationOffset.y,
                SelectedTile.rotationOffset.z);

            _ghostObject.transform.rotation = rot;

            if (SelectedTile.scaleOverride.sqrMagnitude > 0.0001f)
                _ghostObject.transform.localScale = SelectedTile.scaleOverride;

            _lastGhostTile = SelectedTile;
            _lastGhostRotation = CurrentRotationY;
        }

        private void ShowGhost()
        {
            if (SelectedTile?.prefab == null) { HideGhost(); return; }

            bool tileChanged = _lastGhostTile != SelectedTile;
            bool rotChanged = !Mathf.Approximately(_lastGhostRotation, CurrentRotationY);

            if (tileChanged || rotChanged || _ghostObject == null)
                RebuildGhost();

            if (_ghostObject != null && !_ghostObject.activeSelf)
                _ghostObject.SetActive(true);
        }

        private void HideGhost()
        {
            if (_ghostObject != null && _ghostObject.activeSelf)
                _ghostObject.SetActive(false);
        }

        private void DestroyGhost()
        {
            if (_ghostObject != null)
            {
                Object.DestroyImmediate(_ghostObject);
                _ghostObject = null;
            }
        }

        public void Cleanup()
        {
            DestroyGhost();
            if (_ghostMaterial != null)
            {
                Object.DestroyImmediate(_ghostMaterial);
                _ghostMaterial = null;
            }
        }



        private void DrawCellHighlight()
        {
            if (_db == null) return;

            if (_isDraggingBoxOrLine)
            {
                Color dragColor = new Color(0.2f, 0.8f, 1f, 0.8f);
                foreach (var cell in GetDragCells())
                    DrawSingleCellHighlight(cell, dragColor);
            }
            else
            {
                bool occupied = _db.IsOccupied(HoveredCell);
                Color wireColor = CurrentTool switch
                {
                    PainterTool.Borracha => new Color(1f, 0.25f, 0.25f, 0.95f),
                    PainterTool.Substituir => new Color(1f, 0.80f, 0.10f, 0.95f),
                    _ => occupied
                                                ? new Color(1f, 0.50f, 0.10f, 0.95f)
                                                : new Color(0.3f, 1f, 0.50f, 0.95f)
                };
                DrawSingleCellHighlight(HoveredCell, wireColor);
            }
        }

        private void DrawSingleCellHighlight(Vector3Int cell, Color wireColor)
        {
            Vector3 center = _db.CellToWorld(cell);
            center.y += 0.01f;
            float s = _db.CellSize * 0.5f;

            Vector3 p0 = center + new Vector3(-s, 0f, -s);
            Vector3 p1 = center + new Vector3(s, 0f, -s);
            Vector3 p2 = center + new Vector3(s, 0f, s);
            Vector3 p3 = center + new Vector3(-s, 0f, s);

            using (new Handles.DrawingScope(wireColor))
            {
                Handles.DrawLine(p0, p1, 2f);
                Handles.DrawLine(p1, p2, 2f);
                Handles.DrawLine(p2, p3, 2f);
                Handles.DrawLine(p3, p0, 2f);

                Handles.DrawSolidRectangleWithOutline(
                    new Vector3[] { p0, p1, p2, p3 },
                    new Color(wireColor.r, wireColor.g, wireColor.b, 0.07f),
                    Color.clear);
            }
        }

        private void DrawCoordinatesLabel(Vector3 worldPos)
        {
            Vector3 labelPos = worldPos + Vector3.up * 0.35f;
            GUIStyle style = new GUIStyle
            {
                normal = { textColor = Color.white },
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            string coord = $"({HoveredCell.x}, {HoveredCell.y}, {HoveredCell.z})";
            Handles.Label(labelPos, coord, style);
        }



        public void RebuildFromMapData(LevelMapData mapData, LevelPalette palette)
        {
            if (mapData == null || palette == null) return;

            EnsureHierarchy();

            foreach (var kvp in _db.Grid)
                if (kvp.Value.Instance != null)
                    Object.DestroyImmediate(kvp.Value.Instance);

            _db.Clear();

            foreach (var tileData in mapData.tiles)
            {
                TileItem found = null;
                foreach (var t in palette.Tiles)
                {
                    if (t.tileName == tileData.tileName &&
                        t.category.ToString() == tileData.category)
                    {
                        found = t;
                        break;
                    }
                }

                if (found?.prefab == null) continue;

                var cell = tileData.ToVector3Int();


                Vector3 snapOffset = ComputeSnapOffset(found, tileData.rotationY);
                Vector3 worldPos = _db.CellToWorld(cell) + snapOffset;
                Quaternion rot = Quaternion.Euler(
                    tileData.rotationX,
                    tileData.rotationY,
                    tileData.rotationZ);

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(found.prefab);
                instance.transform.SetParent(GetCategoryRoot(found.category), true);
                instance.transform.position = worldPos;
                instance.transform.rotation = rot;

                if (found.scaleOverride.sqrMagnitude > 0.0001f)
                    instance.transform.localScale = found.scaleOverride;

                _db.Place(cell, new PlacedTile(
                    instance, found.prefab, found.category,
                    found.tileName, tileData.rotationX, tileData.rotationY, tileData.rotationZ));
            }
        }
    }
}