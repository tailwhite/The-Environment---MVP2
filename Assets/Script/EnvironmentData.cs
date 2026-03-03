using UnityEngine;

namespace EvolutionLaws.Data
{
    // 整个地图的数据容器
    [System.Serializable]
    public class EnvironmentData
    {
        public string MapName;
        public int Width;
        public int Height;

        // 全局场数据
        public float Global_Temperature = 20f;// 全场温度 (°C)

        public float Global_LightLevel = 1.0f; // 0=夜, 1=昼
        public Vector2 Global_WindDirection = Vector2.right;

        // 网格数据 (一维数组存储二维数据，性能更好)
        // 索引方式: index = y * Width + x
        public TileData[] Grid;

        public EnvironmentData(int w, int h)
        {
            Width = w;
            Height = h;
            Grid = new TileData[w * h];
            for (int i = 0; i < Grid.Length; i++) Grid[i] = new TileData();// 初始化每个格子
        }

        public TileData GetTile(int x, int y)
        {
            if (x < 0 || x >= Width || y < 0 || y >= Height) return null;
            return Grid[y * Width + x];
        }
    }

    // 单个格子的数据
    [System.Serializable]
    public class TileData
    {
        // 1. 物理层
        public float Movement_Cost = 1.0f;    // 1=平地, 2=沼泽

        public float Hazard_Damage = 0f;      // 荆棘/岩浆伤害

        // 2. 生化层
        public float Temperature_Offset = 0f; // 局部温差

        public float Toxicity_Level = 0f;     // 毒性

        // 3. 感知层
        public float Stealth_Factor = 0f;     // 0=空地, 1=完全隐蔽

        public float Scent_Accumulation = 0f; // 气味浓度 (运行时计算)

        // 4. 资源层 (动态消长)
        public float Biomass_Plant = 0f;      // 植物存量

        public float Biomass_Meat = 0f;       // 尸体/肉存量
        public float Biomass_Mineral = 0f;     // 矿物存量
        public float Soil_Fertility = 1.0f;   // 土壤肥力
    }
}