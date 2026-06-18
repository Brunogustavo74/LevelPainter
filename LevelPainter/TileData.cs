using System;
using UnityEngine;

namespace LevelPainter
{
    public enum TileCategory
    {
        Chao,
        Trilhos,
        Estruturas,
        Aderecos,
        Vegetacao
    }




    public enum TileSnapMode
    {




        Manual,






        AutoCenter,





        AutoCenterXZOnly,
    }

    [Serializable]
    public class TileItem
    {
        public string tileName = "Novo Tile";
        public Sprite icon;
        public TileCategory category = TileCategory.Chao;
        public GameObject prefab;

        [Tooltip(
            "Manual: usa positionOffset como está.\n" +
            "AutoCenter: centraliza XZ e ajusta Y para assentar na célula.\n" +
            "AutoCenterXZOnly: centraliza só XZ (pivot já está na base).")]
        public TileSnapMode snapMode = TileSnapMode.AutoCenter;

        [Tooltip("Offset adicional aplicado APÓS o snap automático (ou como offset total no modo Manual).")]
        public Vector3 positionOffset = Vector3.zero;
        public Vector3 rotationOffset = Vector3.zero;
        public Vector3 scaleOverride = Vector3.one;

        [NonSerialized]
        public Texture2D cachedPreviewTexture;
    }

    [Serializable]
    public class TilePlacementData
    {
        public int x;
        public int y;
        public int z;
        public float rotationY;
        public string prefabPath;
        public string category;
        public string tileName;

        public Vector3Int ToVector3Int() => new Vector3Int(x, y, z);
    }

    [Serializable]
    public class LevelMapData
    {
        public string mapName = "Mapa Sem Título";
        public string createdAt;
        public float gridSize = 1f;
        public TilePlacementData[] tiles = Array.Empty<TilePlacementData>();
    }
}