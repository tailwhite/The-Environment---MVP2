using UnityEngine;
using EvolutionLaws.Data;

namespace EvolutionLaws.Utilities
{
    /// <summary>
    /// 【代谢工具类】
    /// 职责: 处理代谢相关的通用计算
    /// 原则: 纯静态方法, 只包含真正被多处重复使用的逻辑
    /// </summary>
    public static class MetabolismUtility
    {
        // ==========================================
        // 消化效率查询 (核心重复方法)
        // ==========================================
        /// <summary>
        /// 获取生物对指定资源类型的消化效率
        /// 使用位置:
        /// - InteractionSystem (进食计算)
        /// - PerceptionSystem (食物吸引力计算)
        /// - DecisionSystem (狩猎评估)
        /// </summary>
        public static float GetDietEfficiency(CreatureData creature, ResourceType resourceType)
        {
            if (creature == null) return 0f;

            int index = (int)resourceType;

            // 边界检查
            if (creature.Diet_Efficiency_Flat == null ||
                index < 0 ||
                index >= creature.Diet_Efficiency_Flat.Count)
            {
                return 0f;
            }

            return creature.Diet_Efficiency_Flat[index];
        }

        // ==========================================
        // 温度适应性检查
        // ==========================================
        /// <summary>
        /// 检查温度是否在生物的舒适区间内
        /// </summary>
        public static bool IsTemperatureComfortable(CreatureData creature, float temperature)
        {
            if (creature == null) return false;
            return creature.Tolerance_Temp.IsInRange(temperature);
        }

        /// <summary>
        /// 计算温度压力系数 (0-1, 越大越不适)
        /// 使用位置:
        /// - MetabolismSystem (温度惩罚计算)
        /// - DecisionSystem (未来可能用于"寻找遮蔽"行为)
        /// </summary>
        public static float CalculateTemperatureStress(CreatureData creature, float temperature)
        {
            if (creature == null) return 0f;

            var tolerance = creature.Tolerance_Temp;

            // 在舒适区间内
            if (tolerance.IsInRange(temperature))
                return 0f;

            // 低于下限
            if (temperature < tolerance.Min)
            {
                float coldStress = (tolerance.Min - temperature) / 20f; // 假设 20 度为极限
                return Mathf.Clamp01(coldStress);
            }

            // 高于上限
            float heatStress = (temperature - tolerance.Max) / 20f;
            return Mathf.Clamp01(heatStress);
        }

        // ==========================================
        // 健康状态快速查询
        // ==========================================
        /// <summary>
        /// 获取能量百分比 (0-1)
        /// </summary>
        public static float GetEnergyPercent(CreatureData creature)
        {
            if (creature == null || creature.Energy_Max <= 0f) return 0f;
            return creature.Energy / creature.Energy_Max;
        }

        /// <summary>
        /// 获取营养百分比 (0-1)
        /// </summary>
        public static float GetNutrientPercent(CreatureData creature)
        {
            if (creature == null || creature.Nutrients_Max <= 0f) return 0f;
            return creature.Nutrients / creature.Nutrients_Max;
        }

        /// <summary>
        /// 检查生物是否处于饥饿状态
        /// </summary>
        public static bool IsStarving(CreatureData creature)
        {
            return creature != null &&
                   creature.Energy < creature.Energy_Max * 0.1f &&
                   creature.Nutrients < creature.Nutrients_Max * 0.1f;
        }
    }
}