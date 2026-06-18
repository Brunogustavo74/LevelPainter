using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LevelPainter
{
    public class GridDatabase
    {
        private readonly Dictionary<Vector3Int, PlacedTile> _grid = new();
        private readonly float _cellSize;

        public float CellSize => _cellSize;
        public IReadOnlyDictionary<Vector3Int, PlacedTile> Grid => _grid;

        public GridDatabase(float cellSize = 1f) { _cellSize = cellSize; }


        public Vector3Int WorldToCell(Vector3 worldPos)
            => new Vector3Int(
                Mathf.RoundToInt(worldPos.x / _cellSize),
                Mathf.RoundToInt(worldPos.y / _cellSize),
                Mathf.RoundToInt(worldPos.z / _cellSize));

        public Vector3 CellToWorld(Vector3Int cell)
            => new Vector3(cell.x * _cellSize,
                           cell.y * _cellSize,
                           cell.z * _cellSize);



        public bool IsOccupied(Vector3Int cell) => _grid.ContainsKey(cell);
        public bool TryGet(Vector3Int cell, out PlacedTile tile) => _grid.TryGetValue(cell, out tile);

        public bool Place(Vector3Int cell, PlacedTile tile)
        {
            if (_grid.ContainsKey(cell)) return false;
            _grid[cell] = tile;
            return true;
        }

        public bool Replace(Vector3Int cell, PlacedTile tile)
        {
            _grid[cell] = tile;
            return true;
        }

        public bool Remove(Vector3Int cell) => _grid.Remove(cell);
        public void Clear() => _grid.Clear();



        public LevelMapData ToMapData(string mapName = "Nível")
        {
            var list = new List<TilePlacementData>(_grid.Count);
            foreach (var kvp in _grid)
            {
                var cell = kvp.Key;
                var placed = kvp.Value;

                string prefabPath = string.Empty;
#if UNITY_EDITOR
                if (placed.SourcePrefab != null)
                    prefabPath = AssetDatabase.GetAssetPath(placed.SourcePrefab);
#endif
                list.Add(new TilePlacementData
                {
                    x = cell.x,
                    y = cell.y,
                    z = cell.z,
                    rotationY = placed.RotationY,
                    prefabPath = prefabPath,
                    category = placed.Category.ToString(),
                    tileName = placed.TileName
                });
            }

            return new LevelMapData
            {
                mapName = mapName,
                createdAt = DateTime.UtcNow.ToString("o"),
                gridSize = _cellSize,
                tiles = list.ToArray()
            };
        }

        public string ToJson(string mapName = "Nível")
            => JsonUtility.ToJson(ToMapData(mapName), true);

        public void SaveToFile(string path, string mapName = "Nível")
            => File.WriteAllText(path, ToJson(mapName));

        public static LevelMapData LoadFromFile(string path)
        {
            if (!File.Exists(path)) return null;
            return JsonUtility.FromJson<LevelMapData>(File.ReadAllText(path));
        }
    }



    public class PlacedTile
    {
        public GameObject Instance { get; set; }
        public GameObject SourcePrefab { get; set; }
        public TileCategory Category { get; set; }
        public string TileName { get; set; }
        public float RotationY { get; set; }

        public PlacedTile() { }

        public PlacedTile(GameObject instance, GameObject sourcePrefab,
                          TileCategory category, string tileName, float rotationY)
        {
            Instance = instance;
            SourcePrefab = sourcePrefab;
            Category = category;
            TileName = tileName;
            RotationY = rotationY;
        }
    }
}
