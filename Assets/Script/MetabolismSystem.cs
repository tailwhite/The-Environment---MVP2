using System.Collections.Generic;
using UnityEngine;
using EvolutionLaws.Data;

namespace EvolutionLaws.Core
{
    /// <summary>
    /// 【代谢系统】
    /// 职责：
    /// 1. 计算并应用 Energy/Nutrients 消耗
    /// 2. 应用环境压力 (温度/毒性)
    /// 3. 检测死亡条件
    /// 原则：纯逻辑类，不含MonoBehaviour
    /// </summary>
    public class MetabolismSystem
    {
        // ==========================================
        // 配置参数 (可在Inspector中调整，或从ScriptableObject加载)
        // ==========================================
        public float Energy_Regen_Rate = 5.0f;        // 每秒恢复量 (静止时)

        public float Nutrient_To_Energy_Rate = 0.5f;  // Nutrients转Energy的效率
        public float Starvation_Damage_Rate = 2.0f;   // 饥饿时每秒损失Vitality
        public float Temp_Penalty_Multiplier = 0.1f;  // 温度偏离每度的代谢惩罚倍率

        // ==========================================
        // 核心Tick方
        // ==========================================
        /// <summary>
        /// 对所有生物执行代谢计算
        /// </summary>
        public void Tick(List<CreatureData> creatures, EnvironmentData environment, float deltaTime)
        {
            foreach (var creature in creatures)
            {
                // 跳过已死亡或休眠状态
                if (creature.IsDead || creature.IsTorpor) continue;

                // ──────────────────────────────────
                // 阶段 1: 计算总代谢消耗
                // ──────────────────────────────────
                float totalBurn = CalculateMetabolicBurn(creature, environment);
                creature.Current_Metabolic_Burn = totalBurn; // 记录到数据（用于UI显示）

                // ──────────────────────────────────
                // 阶段 2: 消耗 Energy
                // ──────────────────────────────────
                creature.Energy -= totalBurn * deltaTime;// 总消耗 = 代谢消耗 * 时间增量

                // 如果Energy耗尽，从Nutrients转换
                if (creature.Energy < 0)
                {
                    float deficit = -creature.Energy;// 需要补足的能量缺口
                    float nutrientCost = deficit / Nutrient_To_Energy_Rate;
                    creature.Nutrients -= nutrientCost;
                    creature.Energy = 0; // 重置为0（已透支部分从Nutrients扣除）
                }

                // ──────────────────────────────────
                // 阶段 3: Energy通过消耗营养Nutrients恢复 (仅当未昏迷)
                // ──────────────────────────────────
                bool isResting = !creature.IsMoving && !creature.IsFeeding;  // ✅ 判断是否在休息

                if (isResting && creature.Nutrients > 0)// 只有在处于休息状态时才恢复
                {
                    // 仅当能量低于 50% 时才开始转换（避免浪费营养）
                    if (creature.Energy < creature.Energy_Max * 0.5f)
                    {
                        // 昏迷时转换效率降低（模拟昏迷状态下代谢缓慢）
                        float efficiencyMultiplier = creature.IsUnconscious ? 0.5f : 1.0f;
                        float regenAmount = Energy_Regen_Rate * deltaTime * efficiencyMultiplier;
                        float nutrientCost = regenAmount / Nutrient_To_Energy_Rate;

                        // 检查是否有足够营养
                        if (creature.Nutrients >= nutrientCost)
                        {
                            creature.Nutrients -= nutrientCost;
                            creature.Energy += regenAmount;
                            creature.Energy = Mathf.Min(creature.Energy, creature.Energy_Max);
                        }
                        else
                        {
                            // 营养不足时，转换剩余所有营养
                            float possibleRegen = creature.Nutrients * Nutrient_To_Energy_Rate * efficiencyMultiplier;
                            creature.Energy += possibleRegen;
                            creature.Nutrients = 0;
                        }
                    }
                }

                // ──────────────────────────────────
                // 阶段 4: 饥饿惩罚 (Nutrients耗尽)
                // ──────────────────────────────────
                if (creature.Nutrients <= 0)
                {
                    creature.Vitality_Current -= Starvation_Damage_Rate * deltaTime;
                    creature.Nutrients = 0; // 防止负数
                }
                // 当营养低于 80% 时开始产生饥饿感，营养越低，饥饿感越趋近 100
                float nutrientPercent = creature.Nutrients / creature.Nutrients_Max;
                if (nutrientPercent < 0.8f)
                {
                    // 营养剩 80% 饥饿感是 0；营养 0% 时饥饿感是 100
                    creature.Need_Hunger = (0.8f - nutrientPercent) / 0.8f * 100f;
                }
                else
                {
                    creature.Need_Hunger = 0f;
                }
                // ──────────────────────────────────
                // 阶段 5: 环境伤害 (毒性)
                // ──────────────────────────────────
                // TODO: 这里需要获取生物所在格子的毒性
                // 现在用占位逻辑：假设毒性由外部系统写入
                // float toxicity = GetTileAt(creature.Position).Toxicity_Level;
                // creature.Vitality_Current -= toxicity * deltaTime;

                // ──────────────────────────────────
                // 阶段 6: 死亡检测
                // ──────────────────────────────────
                if (creature.Structure_Current <= 0 || creature.Vitality_Current <= 0)
                {
                    creature.IsDead = true;
                }

                // ──────────────────────────────────
                // 阶段 7: 昏迷检测 (Energy归零)
                // ──────────────────────────────────
                if (creature.Energy <= 0 && creature.Nutrients > 0)
                {
                    creature.IsUnconscious = true;
                }
                else if (creature.Energy > creature.Energy_Max * 0.2f) // 恢复20%后苏醒
                {
                    creature.IsUnconscious = false;
                }

                // 边界保护
                creature.Energy = Mathf.Clamp(creature.Energy, 0, creature.Energy_Max);
                creature.Nutrients = Mathf.Clamp(creature.Nutrients, 0, creature.Nutrients_Max);
                creature.Vitality_Current = Mathf.Max(creature.Vitality_Current, 0);
            }
        }

        // ==========================================
        // 辅助计算方法
        // ==========================================
        /// <summary>
        /// 计算总代谢消耗 (基础 + 词缀 + 环境)
        /// </summary>
        private float CalculateMetabolicBurn(CreatureData creature, EnvironmentData environment)
        {
            float baseBurn = creature.Base_Metabolic_Rate;// 基础代谢速率

            // TODO: 添加词缀代价 (需要从ConfigManager读取词缀定义)
            // foreach (var affixID in creature.ActiveAffixes)
            // {
            //     var def = ConfigManager.GetAffix(affixID);
            //     baseBurn += def.Upkeep_Cost;
            // }

            // 环境惩罚：温度偏离
            float tempPenalty = CalculateTemperaturePenalty(creature, environment.Global_Temperature);

            float totalBurn = baseBurn + tempPenalty;

            // 【生态机制】：休息奖励
            // 如果生物当前处于休息、闲逛未移动或昏迷状态，大幅降低代谢消耗
            if (creature.CurrentBehavior == BehaviorState.Resting || creature.IsUnconscious)
            {
                totalBurn *= 0.3f; // 深度休息/昏迷时代谢大幅降低 (仅消耗30%)
            }
            else if (!creature.IsMoving)
            {
                totalBurn *= 0.7f; // 站着不动时，也能节省一点体力
            }

            return totalBurn;//代谢总消耗
        }

        /// <summary>
        /// 计算温度偏离惩罚
        /// </summary>
        private float CalculateTemperaturePenalty(CreatureData creature, float currentTemp)
        {
            var tolerance = creature.Tolerance_Temp;

            // 在容忍范围内，无惩罚
            if (tolerance.IsInRange(currentTemp))
                return 0f;

            // 计算偏离距离
            float deviation = 0f;
            if (currentTemp < tolerance.Min)
                deviation = tolerance.Min - currentTemp;
            else if (currentTemp > tolerance.Max)
                deviation = currentTemp - tolerance.Max;

            // 每偏离1度，增加 Temp_Penalty_Multiplier 倍的基础代谢
            return deviation * Temp_Penalty_Multiplier * creature.Base_Metabolic_Rate;
        }
    }
}