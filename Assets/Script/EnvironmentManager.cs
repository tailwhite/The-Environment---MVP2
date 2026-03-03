using UnityEngine;
using UnityEngine.Tilemaps;
using EvolutionLaws.Data;
using TileData = EvolutionLaws.Data.TileData;

namespace EvolutionLaws.Core
{
    /// <summary>
    /// 【环境管理器】
    /// 职责:
    /// 1. 生成地图数据 (使用Perlin Noise)
    /// 2. 渲染地图到Tilemap
    /// 3. 管理环境动态变化 (植物生长/昼夜循环)
    /// 原则:数据与视图分离,EnvironmentData为数据源
    /// </summary>
    public class EnvironmentManager : MonoBehaviour
    {
        // ==========================================
        // 地图数据引用
        // ==========================================
        [Header("Data Reference")]
        [Tooltip("地图数据容器 (由SimulationManager提供)")]
        public EnvironmentData EnvironmentData;

        // ==========================================
        // 地图生成参数
        // ==========================================
        [Header("Map Generation Settings")]
        [Tooltip("地图宽度 (格子数)")]
        public int MapWidth = 100;

        [Tooltip("地图高度 (格子数)")]
        public int MapHeight = 100;

        [Tooltip("Perlin噪声缩放 (越小越平滑)")]
        [Range(1f, 100f)]
        public float NoiseScale = 20f;

        [Tooltip("随机种子 (相同种子生成相同地图)")]
        public int Seed = 12345;

        // ==========================================
        // 地形阈值配置
        // ==========================================
        [Header("Terrain Thresholds")]
        [Tooltip("水域阈值 (低于此值为水)")]
        [Range(0f, 1f)]
        public float WaterThreshold = 0.4f;

        [Tooltip("矿石地形阈值 (在此值和墙体阈值之间为矿石区)")]
        [Range(0f, 1f)]
        public float MineralThreshold = 0.65f;

        [Tooltip("墙体阈值 (高于此值为墙)")]
        [Range(0f, 1f)]
        public float WallThreshold = 0.8f;

        // ==========================================
        // 资源生成参数
        // ==========================================
        [Header("Resource Generation")]
        [Tooltip("植物生物量基础值")]
        public float BasePlantBiomass = 50f;

        public float BaseMineralBiomass = 100f;

        [Tooltip("温度偏移范围 (-X到+X)")]
        public float TemperatureRange = 15f;

        // ==========================================
        // Tilemap渲染引用
        // ==========================================
        [Header("Tilemap References")]
        [Tooltip("地面Tilemap层")]
        public Tilemap GroundTilemap;

        [Tooltip("障碍物Tilemap层 (墙/水)")]
        public Tilemap ObstacleTilemap;

        [Tooltip("草地瓦片")]
        public TileBase GroundTile;

        [Tooltip("矿石地形瓦片 (岩石地)")]
        public TileBase MineralTile;

        [Tooltip("水域瓦片")]
        public TileBase WaterTile;

        [Tooltip("墙体瓦片")]
        public TileBase WallTile;

        [Tooltip("食物丰富区域瓦片 (可选)")]
        public TileBase FoodPatchTile;

        // ==========================================
        // 初始化
        // ==========================================
        private bool _isInitialized = false;

        public bool IsInitialized => _isInitialized;

        private void Awake()
        {
            // 仅创建空容器,不填充数据
            // 这样可以防止空引用错误,但地图内容需要手动初始化
            if (EnvironmentData == null)
            {
                Debug.Log($"[EnvironmentManager] Awake 创建空数据容器: {MapWidth}x{MapHeight}");
                EnvironmentData = new EnvironmentData(MapWidth, MapHeight);
                EnvironmentData.MapName = "Empty_Uninitialized_World";
            }
        }

        // ==========================================
        // 公共手动初始化方法
        // ==========================================
        /// <summary>
        /// 【核心初始化方法】由 SimulationManager 调用
        /// 执行完整的地图生成和渲染流程
        /// </summary>
        public void InitializeMapManual()
        {
            // 防止重复初始化
            if (_isInitialized)
            {
                Debug.LogWarning("[EnvironmentManager] 地图已经初始化,跳过重复调用");
                return;
            }

            Debug.Log($"[EnvironmentManager] ========== 开始手动初始化 ==========");

            // ──────────────────────────────────
            // 步骤 1: 检查并创建数据容器
            // ──────────────────────────────────
            if (EnvironmentData == null || EnvironmentData.Grid == null || EnvironmentData.Grid.Length == 0)
            {
                Debug.Log($"[EnvironmentManager] 重新创建数据容器: {MapWidth}x{MapHeight}");
                EnvironmentData = new EnvironmentData(MapWidth, MapHeight);
                EnvironmentData.MapName = $"ProceduralMap_{Seed}";
            }

            // ──────────────────────────────────
            // 步骤 2: 生成地图数据
            // ──────────────────────────────────
            GenerateMap();

            // ──────────────────────────────────
            // 步骤 3: 渲染到 Tilemap
            // ──────────────────────────────────
            RenderMap();

            // ──────────────────────────────────
            // 步骤 4: 标记完成
            // ──────────────────────────────────
            _isInitialized = true;
            Debug.Log($"[EnvironmentManager] ========== 初始化完成 ==========");
            Debug.Log($"[EnvironmentManager] 地图名称: {EnvironmentData.MapName}");
            Debug.Log($"[EnvironmentManager] 地图尺寸: {EnvironmentData.Width}x{EnvironmentData.Height}");
        }

        // ==========================================
        // 地图数据生成 (核心算法)
        // ==========================================
        /// <summary>
        /// 使用Perlin Noise生成地图数据
        /// </summary>
        public void GenerateMap()
        {
            // 设置随机种子 (用于偏移Perlin噪声)
            Random.InitState(Seed);
            float offsetX = Random.Range(-10000f, 10000f);
            float offsetY = Random.Range(-10000f, 10000f);

            // 温度噪声的独立偏移
            float tempOffsetX = Random.Range(-10000f, 10000f);
            float tempOffsetY = Random.Range(-10000f, 10000f);

            // 食物噪声的独立偏移
            float foodOffsetX = Random.Range(-10000f, 10000f);
            float foodOffsetY = Random.Range(-10000f, 10000f);
            // 矿石噪声的独立偏移
            float mineralOffsetX = Random.Range(-10000f, 10000f);
            float mineralOffsetY = Random.Range(-10000f, 10000f);
            // 遍历所有格子
            for (int y = 0; y < MapHeight; y++)
            {
                for (int x = 0; x < MapWidth; x++)
                {
                    TileData tile = EnvironmentData.GetTile(x, y);

                    // ──────────────────────────────────
                    // 1. 地形分类 (4种地形)
                    // ──────────────────────────────────
                    float terrainNoise = Mathf.PerlinNoise(
                        (x + offsetX) / NoiseScale,
                        (y + offsetY) / NoiseScale
                    );

                    if (terrainNoise < WaterThreshold)
                    {
                        // ━━━ 水域 ━━━
                        tile.Movement_Cost = 100f;
                        tile.Biomass_Plant = 0f;
                        tile.Biomass_Mineral = 0f;
                        tile.Soil_Fertility = 0f;
                    }
                    else if (terrainNoise >= WallThreshold)
                    {
                        // ━━━ 墙体/山脉 ━━━
                        tile.Movement_Cost = float.MaxValue;
                        tile.Hazard_Damage = 0f;
                        tile.Biomass_Plant = 0f;
                        tile.Biomass_Mineral = 0f; // 墙体内部无法开采
                        tile.Soil_Fertility = 0f;
                    }
                    else if (terrainNoise >= MineralThreshold && terrainNoise < WallThreshold)
                    {
                        // ━━━ 矿石地形 (新增) ━━━
                        tile.Movement_Cost = 2.0f; // 崎岖地面,移动较慢

                        // 矿物资源:高密度
                        float mineralNoise = Mathf.PerlinNoise(
                            (x + mineralOffsetX) / (NoiseScale * 0.8f),
                            (y + mineralOffsetY) / (NoiseScale * 0.8f)
                        );
                        tile.Biomass_Mineral = Mathf.Lerp(
                            BaseMineralBiomass * 0.5f,
                            BaseMineralBiomass * 1.5f,
                            mineralNoise
                        ); // 50-150矿石

                        // 植物:无 (岩石地不长草)
                        tile.Biomass_Plant = 0f;
                        tile.Soil_Fertility = 0f;
                    }
                    else
                    {
                        // ━━━ 草地 (可通行区域) ━━━
                        tile.Movement_Cost = Mathf.Lerp(
                            1.0f,
                            1.5f,
                            (terrainNoise - WaterThreshold) / (MineralThreshold - WaterThreshold)
                        );

                        // 植物资源:高密度
                        float foodNoise = Mathf.PerlinNoise(
                            (x + foodOffsetX) / (NoiseScale * 0.5f),
                            (y + foodOffsetY) / (NoiseScale * 0.5f)
                        );
                        tile.Biomass_Plant = foodNoise * BasePlantBiomass * 2f;
                        tile.Soil_Fertility = Mathf.Lerp(0.5f, 1.5f, foodNoise);

                        // 矿物资源:低密度 (仅在特定噪声下生成少量)
                        float mineralNoise = Mathf.PerlinNoise(
                            (x + mineralOffsetX) / (NoiseScale * 0.3f), // 更高频 = 稀疏分布
                            (y + mineralOffsetY) / (NoiseScale * 0.3f)
                        );

                        if (mineralNoise > 0.85f) // 只有15%的草地有矿
                        {
                            tile.Biomass_Mineral = Random.Range(1f, 5f); // 极少量
                        }
                        else
                        {
                            tile.Biomass_Mineral = 0f;
                        }
                    }

                    // ──────────────────────────────────
                    // 2. 温度分布 (Temperature_Offset)
                    // ──────────────────────────────────
                    float tempNoise = Mathf.PerlinNoise(
                        (x + tempOffsetX) / (NoiseScale * 1.5f), // 更大的噪声尺度 = 更平滑的温度带
                        (y + tempOffsetY) / (NoiseScale * 1.5f)
                    );

                    // 映射到 -TemperatureRange 到 +TemperatureRange
                    tile.Temperature_Offset = Mathf.Lerp(-TemperatureRange, TemperatureRange, tempNoise);

                    // ──────────────────────────────────
                    // 3. 食物分布 (Biomass_Plant)
                    // ──────────────────────────────────
                    if (tile.Movement_Cost < 10f) // 只在可通行地块生成食物
                    {
                        float foodNoise = Mathf.PerlinNoise(
                            (x + foodOffsetX) / (NoiseScale * 0.5f), // 更小的尺度 = 更多局部变化
                            (y + foodOffsetY) / (NoiseScale * 0.5f)
                        );

                        // 生成0到BasePlantBiomass*2的植物量
                        tile.Biomass_Plant = foodNoise * BasePlantBiomass * 2f;

                        // 设置土壤肥力 (影响未来植物生长速度)
                        tile.Soil_Fertility = Mathf.Lerp(0.5f, 1.5f, foodNoise);
                    }
                    else
                    {
                        // 水域或墙体无植物
                        tile.Biomass_Plant = 0f;
                        tile.Soil_Fertility = 0f;
                    }
                    if (tile.Movement_Cost > 1.5f && tile.Movement_Cost < 100f)
                    {
                        // 使用独立的噪声，或者复用地形噪声
                        // 这里我们简单点：地形越崎岖，矿石越多
                        float rockiness = (tile.Movement_Cost - 1.5f) * 2.0f; // 0 到 1

                        // 给一点随机波动
                        float mineralNoise = Mathf.PerlinNoise(
                            (x + offsetX) * 2.5f, // 更高频的噪声，让矿石分布更碎
                            (y + offsetY) * 2.5f
                        );

                        // 只有当噪声和地形都合适时才生成
                        if (mineralNoise > 0.6f)
                        {
                            tile.Biomass_Mineral = rockiness * 100f; // 这里的100是基础矿石量
                        }
                    }
                    else
                    {
                        tile.Biomass_Mineral = 0f;
                    }

                    // ──────────────────────────────────
                    // 4. 隐蔽度 (Stealth_Factor)
                    // ──────────────────────────────────
                    // 基于植物量:植物越多越隐蔽
                    tile.Stealth_Factor = Mathf.Clamp01(tile.Biomass_Plant / (BasePlantBiomass * 2f));
                }
            }

            Debug.Log($"[EnvironmentManager] 地图数据生成完成 | 种子: {Seed}");
        }

        // ==========================================
        // 地图渲染 (Tilemap可视化)
        // ==========================================
        /// <summary>
        /// 将EnvironmentData渲染到Unity Tilemap
        /// </summary>
        public void RenderMap()
        {
            // --- [侦探] 第一关：检查尺寸 ---
            Debug.Log($"[侦探] 1. 准备渲染。读取到尺寸: 宽={MapWidth}, 高={MapHeight}");

            if (MapWidth <= 0 || MapHeight <= 0)
            {
                Debug.LogError("🔴 [破案了] 地图宽或高是 0！\n解决方法：请在 Unity Inspector 里找到 EnvironmentManager，把 Map Width 和 Map Height 改成 100。");
                return;
            }

            // --- [侦探] 第二关：检查引用 ---
            if (GroundTilemap == null || ObstacleTilemap == null)
            {
                Debug.LogError("🔴 [破案了] Tilemap 组件没拖！\n解决方法：请把 Hierarchy 里的 Layer_Ground 和 Layer_Obstacles 拖给脚本。");
                return;
            }

            if (GroundTile == null || WaterTile == null)
            {
                Debug.LogError("🔴 [破案了] Tile 瓦片资源没拖！\n解决方法：请把 Project 窗口里的 Grass.asset 和 Water.asset 拖给脚本。");
                return;
            }

            if (EnvironmentData == null)
            {
                Debug.LogError("🔴 [破案了] 数据容器是空的！GenerateMap 可能没运行，或者 Awake 初始化失败。");
                return;
            }

            // 清空现有瓦片
            GroundTilemap.ClearAllTiles();
            ObstacleTilemap.ClearAllTiles();

            int drawCount = 0; // 记录画了多少个
            int mineralCount = 0;
            // --- [侦探] 第三关：开始循环 ---
            for (int y = 0; y < MapHeight; y++)
            {
                for (int x = 0; x < MapWidth; x++)
                {
                    TileData tile = EnvironmentData.GetTile(x, y);

                    // 数据防空检查
                    if (tile == null)
                    {
                        Debug.LogError($"🔴 [异常] 坐标 ({x},{y}) 没有数据！可能是 GenerateMap 里的循环写错了。");
                        return;
                    }

                    Vector3Int tilePos = new Vector3Int(x, y, 0);

                    // 绘制逻辑
                    if (tile.Movement_Cost >= 100f)
                    {
                        // 水域
                        ObstacleTilemap.SetTile(tilePos, WaterTile);
                    }
                    else if (tile.Movement_Cost >= 2.0f && tile.Biomass_Mineral > 10f)
                    {
                        // 矿石地形 (新增)
                        // 判断条件: 移动代价高 + 矿物量丰富
                        if (MineralTile != null)
                        {
                            GroundTilemap.SetTile(tilePos, MineralTile);
                            mineralCount++;
                        }
                        else
                        {
                            // 后备方案:如果没设置矿石瓦片,用草地代替
                            GroundTilemap.SetTile(tilePos, GroundTile);
                        }
                    }
                    else
                    {
                        // 草地
                        GroundTilemap.SetTile(tilePos, GroundTile);
                    }
                    drawCount++;
                }
            }

            // 强制刷新显示
            GroundTilemap.RefreshAllTiles();
            ObstacleTilemap.RefreshAllTiles();

            Debug.Log($"✅ [EnvironmentManager] 渲染完成!");
            Debug.Log($"   总格子数: {drawCount}");
            Debug.Log($"   矿石地块: {mineralCount} ({(float)mineralCount / drawCount * 100:F1}%)");
        }

        // ==========================================
        // 公共接口:地图查询
        // ==========================================
        /// <summary>
        /// 检查指定位置是否可通行
        /// </summary>
        public bool IsWalkable(int x, int y)
        {
            var tile = EnvironmentData.GetTile(x, y);
            return tile != null && tile.Movement_Cost < 10f; // 代价<10视为可通行
        }

        /// <summary>
        /// 获取指定世界坐标的格子数据
        /// </summary>
        public TileData GetTileAtWorldPos(Vector2 worldPos)
        {
            int x = Mathf.FloorToInt(worldPos.x);
            int y = Mathf.FloorToInt(worldPos.y);
            return EnvironmentData.GetTile(x, y);
        }

        // ==========================================
        // 调试可视化
        // ==========================================
        private void OnDrawGizmosSelected()
        {
            if (EnvironmentData == null) return;

            // 绘制温度热力图 (可选)
            for (int y = 0; y < MapHeight; y += 5) // 每5格采样一次,避免性能问题
            {
                for (int x = 0; x < MapWidth; x += 5)
                {
                    var tile = EnvironmentData.GetTile(x, y);
                    if (tile == null) continue;

                    // 温度颜色映射:冷=蓝,热=红
                    float tempNormalized = (tile.Temperature_Offset + TemperatureRange) / (TemperatureRange * 2f);
                    Color tempColor = Color.Lerp(Color.blue, Color.red, tempNormalized);
                    tempColor.a = 0.3f;

                    Gizmos.color = tempColor;
                    Gizmos.DrawCube(new Vector3(x + 0.5f, y + 0.5f, 0), Vector3.one * 0.8f);
                }
            }
        }
    }
}