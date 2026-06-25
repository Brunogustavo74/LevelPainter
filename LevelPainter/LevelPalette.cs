using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LevelPainter
{
    [CreateAssetMenu(fileName = "LevelPalette",
                     menuName = "Level Painter/Level Palette",
                     order = 1)]
    public class LevelPalette : ScriptableObject
    {
        [SerializeField]
        private List<TileItem> _tiles = new();

        public IReadOnlyList<TileItem> Tiles => _tiles;

        public IEnumerable<TileItem> GetByCategory(TileCategory category)
            => _tiles.Where(t => t.category == category);

        public IEnumerable<TileItem> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return _tiles;

            string lower = query.ToLowerInvariant();
            return _tiles.Where(t =>
                t.tileName.ToLowerInvariant().Contains(lower) ||
                t.category.ToString().ToLowerInvariant().Contains(lower));
        }

        public IEnumerable<TileItem> SearchInCategory(string query, TileCategory category)
        {
            var inCategory = GetByCategory(category);
            if (string.IsNullOrWhiteSpace(query)) return inCategory;

            string lower = query.ToLowerInvariant();
            return inCategory.Where(t => t.tileName.ToLowerInvariant().Contains(lower));
        }

        public void AddTile(TileItem tile)
        {
            if (tile == null) return;
            if (_tiles.Any(t => t.tileName == tile.tileName && t.category == tile.category))
            {
                Debug.LogWarning($"[LevelPainter] Tile '{tile.tileName}' already exists in category '{tile.category}'.");
                return;
            }
            _tiles.Add(tile);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        public void RemoveTile(TileItem tile)
        {
            if (tile == null) return;
            _tiles.Remove(tile);
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        public int Count => _tiles.Count;
    }
}
