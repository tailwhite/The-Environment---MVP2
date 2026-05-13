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
        public float Energy_Regen_Rate = 5.0f;        // 每秒营养转化能量值 (静止时)

        public float Nutrient_To_Energy_Rate = 1f;  // Nutrients转Energy的效率
        public float Starvation_Damage_Rate = 2.0f;   // 饥饿时每秒损失Vitality
        public float Temp_Penalty_Multiplier = 0.1f;  // 温度偏离每度的代谢惩罚倍率

        // ==========================================
        // 核心Tick方
        // ==========================================
        /// <summary>
        /// 对所有生物执行代谢计算
        /// </summary>
        public void Tick(List<CreatureData> creatures, EnvironmentData environment, float deltaTime, float globalTime)
        {
            foreach (var creature in creatures)
            {
                // 跳过已死亡或休眠状态
                if (creature.IsDead || creature.IsTorpor) continue;

                // ──────────────────────────────────
                // 阶段 1: 计算总代谢消耗
                // ──────────────────────────────────
                ApplyAndRecordMetabolism(creature, environment, deltaTime);

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
                bool isResting = !creature.IsMoving && !creature.IsFeeding;  // 判断是否在休息

                if (isResting && creature.Nutrients > 0)// 只有在处于休息状态时才恢复
                {
                    // 仅当能量低于 50% 时才开始转换（避免浪费营养）
                    if (creature.Energy < creature.Energy_Max)
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
                // 阶段5.5生命周期与成长检测
                // ──────────────────────────────────
                float age = globalTime - creature.BirthTimestamp;

                // 幼年期：疯狂长身体
                if (creature.Stage == LifeStage.Larva)
                {
                    // 当年龄达标时，宣告成年
                    if (age >= creature.Maturity_Age)
                    {
                        creature.Stage = LifeStage.Adult;
                    }
                }
                // 成年期跑向老年期
                else if (creature.Stage == LifeStage.Adult)
                {
                    // 假设达到最大寿命的 80% 算作老年
                    if (age >= creature.Max_Lifespan * 0.8f)
                    {
                        creature.Stage = LifeStage.Elder;
                    }
                }
                // ──────────────────────────────────
                // 阶段 6: 死亡检测
                // ──────────────────────────────────
                if (!creature.IsDead)
                {
                    // 检测老化
                    if (globalTime - creature.BirthTimestamp >= creature.Max_Lifespan)
                    {
                        creature.IsDead = true;
                        creature.CauseOfDeath = DeathCause.OldAge;
                    }
                    else if (creature.Structure_Current <= 0)
                    {
                        creature.IsDead = true;

                        // 1. 如果在 CombatSystem 里已经定性为他杀，则严格保留，不可篡改！
                        if (creature.CauseOfDeath != DeathCause.Killed)
                        {
                            // 2. 如果没有外力介入，但身体结构却耗尽散架了（例如没能量还强行挪动透支），属于活活累死/饿死
                            creature.CauseOfDeath = DeathCause.Starvation;
                        }
                    }
                    // 检测器官衰竭 (饥饿/毒素)
                    else if (creature.Vitality_Current <= 0)
                    {
                        creature.IsDead = true;
                        // 判定如果是脂肪亏空导致的体力归零，就是饿死，否则是环境温度致死
                        creature.CauseOfDeath = creature.Nutrients <= 0 ? DeathCause.Starvation : DeathCause.Environment;
                    }
                    if (creature.IsDead && creature.Stage == LifeStage.Larva)
                    {
                        Debug.LogWarning($"[验尸] 幼崽夭折! 生物：{creature.SpeciesID}|死因:{creature.CauseOfDeath} | 存活时间:{(globalTime - creature.BirthTimestamp):F1}s | 临终状态: 昏迷={creature.IsUnconscious}, 体力={creature.Energy:F1}, 营养={creature.Nutrients:F1}");
                    }
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
        // ==========================================
        // 辅助计算方法
        // ==========================================
        /// <summary>
        /// 计算、扣除并记录代谢消耗 (兼顾记账)
        /// </summary>
        private void ApplyAndRecordMetabolism(CreatureData creature, EnvironmentData environment, float deltaTime)
        {
            // 1. 获取基础与环境速率
            float baseBurn = creature.Base_Metabolic_Rate;

            // 【核心】：算上高贵词缀的“基因持有税”
            foreach (var affixID in creature.ActiveAffixes)
            {
                var def = Config.AffixManager.GetAffix(affixID);
                if (def != null)
                {
                    baseBurn += def.Upkeep_Cost;
                }
            }

            float tempPenalty = CalculateTemperaturePenalty(creature, environment.Global_Temperature);

            // 2. 【生态机制】：休息奖励修正系数
            float multiplier = 1.0f;
            if (creature.CurrentBehavior == BehaviorState.Resting || creature.IsUnconscious)
                multiplier = 0.3f; // 深度休息/昏迷时代谢降低
            else if (!creature.IsMoving)
                multiplier = 0.7f; // 站着不动也能省体力

            // 3. 计算本帧具体消耗量
            float costMetab = baseBurn * multiplier * deltaTime;
            float costTemp = tempPenalty * multiplier * deltaTime;
            float totalCost = costMetab + costTemp;

            // 4. 更新面板上的实时速率 (一秒正常流逝扣多少)
            creature.Current_Metabolic_Burn = (baseBurn + tempPenalty) * multiplier;

            // 5. 【账单记账】
            creature.Lifetime_EnergySpent_Metabolism += costMetab;
            creature.Lifetime_EnergySpent_Temp += costTemp;

            // 6. 【扣除体力】
            creature.Energy -= totalCost;
        }

        /// <summary>
        /// 计算总代谢消耗 (基础 + 词缀 + 环境)
        /// </summary>
        private float CalculateMetabolicBurn(CreatureData creature, EnvironmentData environment)
        {
            float baseBurn = creature.Base_Metabolic_Rate;

            foreach (var affixID in creature.ActiveAffixes)
            {
                var def = Config.AffixManager.GetAffix(affixID);
                if (def != null) baseBurn += def.Upkeep_Cost;
            }

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