using UnityEngine;
using EvolutionLaws.Data;

namespace EvolutionLaws.Core
{
    /// <summary>
    /// 【环境动态系统】
    /// 职责:
    /// 1. 植物生长/衰减
    /// 2. 尸体腐烂
    /// 3. 气味扩散 (未来)
    /// 4. 昼夜/季节循环 (未来)
    /// 原则: 纯逻辑类, 只修改 EnvironmentData
    /// </summary>
    [System.Serializable]
    public class EnvironmentDynamicsSystem
    {
        // ==========================================
        // 配置参数 (植物生长)
        // ==========================================
        [Header("Plant Growth Settings")]
        [Tooltip("植物基础生长速率 (单位/秒)")]
        public float Base_Plant_Growth_Rate = 0.5f;

        [Tooltip("植物最大生物量上限")]
        public float Max_Plant_Biomass = 100f;

        [Tooltip("最低光照阈值 (低于此值不生长)")]
        public float Min_Light_For_Growth = 0.3f;

        [Tooltip("最佳生长温度范围")]
        public float Optimal_Temp_Min = 15f;

        public float Optimal_Temp_Max = 30f;

        // ==========================================
        // 配置参数 (矿物结晶)
        // ==========================================
        [Header("Mineral Growth Settings")]
        [Tooltip("矿物基础结晶速率 (单位/秒)")]
        public float Base_Mineral_Growth_Rate = 0.05f; // 很慢

        [Tooltip("单个格子最大矿物容量")]
        public float Max_Mineral_Biomass = 200f;

        // ==========================================
        // 配置参数 (尸体腐烂)
        // ==========================================
        [Header("Decay Settings")]
        [Tooltip("肉类腐烂速率 (单位/秒)")]
        public float Meat_Decay_Rate = 0.1f;

        [Tooltip("腐烂残留比例 (0.1 = 10% 转化为土壤肥力)")]
        public float Decay_To_Fertility = 0.1f;

        // ==========================================
        // 运行时累计时间 (用于昼夜循环)
        // ==========================================
        private float _simulationTime = 0f;

        [Header("Day-Night Cycle (未来扩展)")]
        [Tooltip("昼夜周期长度 (秒)")]
        public float Day_Night_Cycle_Length = 120f; // 2 分钟 = 1 天

        // ==========================================
        // 核心 Tick 方法
        // ==========================================
        /// <summary>
        /// 【核心方法】驱动所有环境动态变化
        /// </summary>
        public void Tick(EnvironmentData environment, float deltaTime)
        {
            if (environment == null || environment.Grid == null)
                return;

            _simulationTime += deltaTime;

            // ──────────────────────────────────
            // 阶段 1: 更新全局环境参数 (昼夜/季节)
            // ──────────────────────────────────
            UpdateGlobalEnvironment(environment);

            // ──────────────────────────────────
            // 阶段 2: 遍历所有格子,更新动态数据
            // ──────────────────────────────────
            for (int y = 0; y < environment.Height; y++)
            {
                for (int x = 0; x < environment.Width; x++)
                {
                    var tile = environment.GetTile(x, y);
                    if (tile == null) continue;

                    // 2.1 植物生长
                    ProcessPlantGrowth(tile, environment, deltaTime);

                    // 2.2 尸体腐烂
                    ProcessMeatDecay(tile, deltaTime);

                    // 2.3 矿物结晶 (缓慢再生)
                    ProcessMineralGrowth(tile, deltaTime);

                    // 2.4 气味扩散 (未来)
                    // ProcessScentDiffusion(tile, environment);
                }
            }
        }

        // ==========================================
        // 全局环境更新 (昼夜循环)
        // ==========================================
        /// <summary>
        /// 更新全局光照、温度等参数
        /// </summary>
        private void UpdateGlobalEnvironment(EnvironmentData environment)
        {
            // ━━━ 昼夜循环 (简化版:正弦波模拟) ━━━
            // 光照: 0.0 (午夜) → 1.0 (正午) → 0.0 (午夜)
            float dayProgress = (_simulationTime % Day_Night_Cycle_Length) / Day_Night_Cycle_Length;
            environment.Global_LightLevel = Mathf.Sin(dayProgress * Mathf.PI * 2f) * 0.5f + 0.5f;

            // ━━━ 温度变化 (随光照波动) ━━━
            // 白天温度高,夜晚温度低
            float tempVariation = (environment.Global_LightLevel - 0.5f) * 10f; // ±5°C
            // environment.Global_Temperature = BaseTemperature + tempVariation; // 如果需要全局温度变化

            // Debug.Log($"[EnvironmentDynamics] 时间: {_simulationTime:F1}s | 光照: {environment.Global_LightLevel:P0}");
        }

        // ==========================================
        // 植物生长逻辑
        // ==========================================
        /// <summary>
        /// 【核心】计算并应用植物生长
        /// </summary>
        private void ProcessPlantGrowth(TileData tile, EnvironmentData environment, float deltaTime)
        {
            // ━━━ 前置检查 ━━━
            // 1. 已达到最大值,停止生长
            if (tile.Biomass_Plant >= Max_Plant_Biomass)
                return;

            // 2. 不可通行地块 (水域/墙) 不生长
            if (tile.Movement_Cost >= 10f)
                return;

            // 3. 光照不足,不生长
            if (environment.Global_LightLevel < Min_Light_For_Growth)
                return;

            // ━━━ 计算生长速率 ━━━
            float growthRate = Base_Plant_Growth_Rate;

            // 修正因素 1: 土壤肥力 (0.5 - 1.5 倍)
            growthRate *= tile.Soil_Fertility;

            // 修正因素 2: 光照强度 (0.0 - 1.0)
            growthRate *= environment.Global_LightLevel;

            // 修正因素 3: 温度系数 (最佳温度区间内 = 1.0, 偏离降低)
            float temperature = environment.Global_Temperature + tile.Temperature_Offset;
            float tempFactor = CalculateTemperatureFactor(temperature);
            growthRate *= tempFactor;

            // ━━━ 应用生长 ━━━
            tile.Biomass_Plant += growthRate * deltaTime;
            tile.Biomass_Plant = Mathf.Min(tile.Biomass_Plant, Max_Plant_Biomass); // 上限限制
        }

        // ==========================================
        // 温度因子计算
        // ==========================================
        /// <summary>
        /// 计算温度对生长的影响系数 (0.0 - 1.0)
        /// </summary>
        private float CalculateTemperatureFactor(float temperature)
        {
            // 最佳温度区间内 = 100% 生长
            if (temperature >= Optimal_Temp_Min && temperature <= Optimal_Temp_Max)
                return 1.0f;

            // 低于最佳温度: 线性衰减到 0°C 时停止
            if (temperature < Optimal_Temp_Min)
            {
                float coldPenalty = Mathf.Clamp01(temperature / Optimal_Temp_Min);
                return coldPenalty;
            }

            // 高于最佳温度: 线性衰减到 50°C 时停止
            if (temperature > Optimal_Temp_Max)
            {
                float heatPenalty = Mathf.Clamp01(1f - (temperature - Optimal_Temp_Max) / 20f);
                return heatPenalty;
            }

            return 0f;
        }

        // ==========================================
        // 尸体腐烂逻辑
        // ==========================================
        /// <summary>
        /// 【核心】处理肉类资源的自然腐烂
        /// </summary>
        private void ProcessMeatDecay(TileData tile, float deltaTime)
        {
            // 没有肉类资源,跳过
            if (tile.Biomass_Meat <= 0f)
                return;

            // ━━━ 计算腐烂量 ━━━
            float decayAmount = Meat_Decay_Rate * deltaTime;

            // 高温加速腐烂 (简化逻辑: 温度每高 10°C, 腐烂速度 +50%)
            // float tempAcceleration = 1f + Mathf.Max(0f, (temperature - 20f) / 10f) * 0.5f;
            // decayAmount *= tempAcceleration;

            // ━━━ 应用腐烂 ━━━
            float actualDecay = Mathf.Min(decayAmount, tile.Biomass_Meat);
            tile.Biomass_Meat -= actualDecay;

            // ━━━ 转化为土壤肥力 ━━━
            tile.Soil_Fertility += actualDecay * Decay_To_Fertility;
            tile.Soil_Fertility = Mathf.Clamp(tile.Soil_Fertility, 0.1f, 2.0f); // 限制范围

            // 完全腐烂后清零
            if (tile.Biomass_Meat < 0.1f)
                tile.Biomass_Meat = 0f;
        }

        // ==========================================
        // 矿物结晶逻辑
        // ==========================================
        /// <summary>
        /// 【核心】处理矿物的自然缓慢结晶再生
        /// </summary>
        private void ProcessMineralGrowth(TileData tile, float deltaTime)
        {
            // 只有在岩石/崎岖地形 (Movement_Cost >= 2.0) 才会有矿物结晶
            // 水域或平地不长矿石
            if (tile.Movement_Cost < 2.0f || tile.Movement_Cost >= 100f)
                return;

            if (tile.Biomass_Mineral >= Max_Mineral_Biomass)
                return;

            // 地形越崎岖 (代表岩石岩脉显露越多)，结晶越快
            float rockinessFactor = (tile.Movement_Cost - 1.5f);

            float growthAmount = Base_Mineral_Growth_Rate * rockinessFactor * deltaTime;

            tile.Biomass_Mineral += growthAmount;
            tile.Biomass_Mineral = Mathf.Min(tile.Biomass_Mineral, Max_Mineral_Biomass);
        }
        // ==========================================
        // 调试接口
        // ==========================================
        /// <summary>
        /// 获取当前环境状态摘要 (用于 UI 显示)
        /// </summary>
        public string GetEnvironmentSummary(EnvironmentData environment)
        {
            if (environment == null) return "环境数据为空";

            float dayProgress = (_simulationTime % Day_Night_Cycle_Length) / Day_Night_Cycle_Length;
            string timeOfDay = dayProgress < 0.25f ? "黎明" :
                               dayProgress < 0.5f ? "正午" :
                               dayProgress < 0.75f ? "黄昏" : "午夜";

            return $"时间: {timeOfDay} ({_simulationTime:F0}s)\n" +
                   $"光照: {environment.Global_LightLevel:P0}\n" +
                   $"温度: {environment.Global_Temperature:F1}°C";
        }
    }
}