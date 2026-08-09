using System.Collections.Generic;
using UnityEngine;
using EvolutionLaws.Data;
using EvolutionLaws.Config;
using EvolutionLaws.Utilities;

namespace EvolutionLaws.Core
{
    /// <summary>
    /// 【繁殖系统】
    /// 职责:
    /// 1. 检测成熟度和繁殖需求
    /// 2. 寻找配偶 (同种 + 距离 + 兼容性)
    /// 3. 生成后代数据 (基因混合)
    /// 原则: 纯逻辑类, 只返回数据, 不创建视图
    /// </summary>
    public class ReproductionSystem
    {
        // ==========================================
        // 配置参数
        // ==========================================
        public float Mate_Search_Range = 10f;       // 寻找配偶的最大距离

        public float Reproduction_Hunger_Threshold = 30f; // 饥饿度低于此值才繁殖
        public float Reproduction_Energy_Cost = 50f;      // 繁殖消耗的能量
        public float Offspring_Spawn_Range = 2f;          // 后代出生距离

        // 防卡死全局最大种群数保险阀
        public int Global_Population_Cap = 300;           // 全局生物总数上限

        public int Species_Population_Cap = 150;          // 单一物种总数上限 (防止某类生物垄断生育权

        // ==========================================
        // 运行时状态
        // ==========================================
        private float _simulationTime = 0f;

        // 存储待生成的后代数据 (由 SimulationManager 读取)
        private List<CreatureData> _pendingOffspring = new List<CreatureData>();

        // ==========================================
        // 核心 Tick 方法
        // ==========================================
        /// <summary>
        /// 对所有生物执行繁殖逻辑
        /// </summary>
        public void Tick(List<CreatureData> creatures, Dictionary<string, SpeciesBlueprint> blueprintMap, float deltaTime)
        {
            _simulationTime += deltaTime;
            _pendingOffspring.Clear(); // 清空上一帧的后代列表

            Dictionary<string, int> speciesPop = new Dictionary<string, int>();
            foreach (var c in creatures)
            {
                if (c.IsDead) continue;
                if (!speciesPop.ContainsKey(c.SpeciesID)) speciesPop[c.SpeciesID] = 0;
                speciesPop[c.SpeciesID]++;
            }
            foreach (var creature in creatures)
            {
                if (creature.IsDead || creature.IsUnconscious) continue;

                // 【防卡死监测】如果种群数即将触及天花板，大自然将剥夺交配权
                if (creatures.Count + _pendingOffspring.Count >= Global_Population_Cap)
                    break;
                // 【反垄断监测】如果这个物种的数量已经达到了该物种的独立上限，不准再生了，把生育机会留给别人
                if (speciesPop.ContainsKey(creature.SpeciesID) && speciesPop[creature.SpeciesID] >= Species_Population_Cap)
                    continue;
                // ──────────────────────────────────
                // 阶段 1: 检查是否成熟
                // ──────────────────────────────────
                float age = _simulationTime - creature.BirthTimestamp;
                if (age < creature.Maturity_Age)
                    continue; // 未成熟,跳过

                // ──────────────────────────────────
                // 阶段 2: 检查繁殖需求
                // ──────────────────────────────────
                if (!ShouldReproduce(creature))
                    continue;

                // ──────────────────────────────────
                // 阶段 3: 寻找配偶
                // ──────────────────────────────────
                CreatureData mate = FindMate(creature, creatures);
                if (mate == null)
                    continue; // 找不到配偶

                // ──────────────────────────────────
                // 阶段 4: 执行交配
                // ──────────────────────────────────
                Mate(creature, mate);
            }

            // ──────────────────────────────────
            // 阶段 5: 检查怀孕生物
            // ──────────────────────────────────
            foreach (var creature in creatures)
            {
                if (!creature.IsPregnant) continue;

                float pregnancyTime = _simulationTime - creature.Pregnancy_Start_Time;
                if (pregnancyTime >= creature.Pregnancy_Duration)//如果怀孕时间到达或超过预定的妊娠期，尝试生育
                {
                    // 获取当前这只生物的物种人数
                    int currentSpeciesCount = speciesPop.ContainsKey(creature.SpeciesID) ? speciesPop[creature.SpeciesID] : 0;

                    // 【防卡死监测】生育前的最后一道坎 (全局容量 + 种族独立容量双重限制)
                    if (creatures.Count + _pendingOffspring.Count < Global_Population_Cap &&
                        currentSpeciesCount < Species_Population_Cap)
                    {
                        GiveBirth(creature, creatures, blueprintMap, _simulationTime);
                        speciesPop[creature.SpeciesID]++; // 生完登记在册，防止同一种族同一帧集体疯狂产仔
                    }
                    else
                    {
                        // 强制流产/放弃生育，恢复普通状态
                        creature.IsPregnant = false;
                        creature.Mate_UID = null;
                        Debug.LogWarning($"[ReproductionSystem] 容量到达极限或种族人口超标，生物被强制终止产仔。");
                    }
                }
            }
        }

        // ==========================================
        // 获取待生成后代列表 (公共接口)
        // ==========================================
        /// <summary>
        /// 【公共接口】获取本帧生成的后代数据
        /// </summary>
        public List<CreatureData> GetPendingOffspring()
        {
            return new List<CreatureData>(_pendingOffspring); // 返回副本
        }

        // ==========================================
        // 繁殖需求评估
        // ==========================================
        /// <summary>
        /// 检查生物是否满足繁殖条件
        /// </summary>
        private bool ShouldReproduce(CreatureData creature)
        {
            // 已怀孕,跳过
            if (creature.IsPregnant)
                return false;

            // 繁殖冷却中
            if (_simulationTime - creature.Last_Reproduction_Time < creature.Reproduction_Cooldown)
                return false;

            // 饥饿度过高 (饿着肚子不繁殖)
            if (creature.Need_Hunger > Reproduction_Hunger_Threshold)
                return false;

            // 能量不足
            if (creature.Energy < Reproduction_Energy_Cost)
                return false;

            // 营养储备不足 (需要至少 50% 营养)
            float nutrientPercent = creature.Nutrients / creature.Nutrients_Max;
            if (nutrientPercent < 0.5f)
                return false;

            return true;
        }

        // ==========================================
        // 寻找配偶
        // ==========================================
        /// <summary>
        /// 在附近寻找合适的配偶
        /// </summary>
        private CreatureData FindMate(CreatureData self, List<CreatureData> allCreatures)
        {
            CreatureData bestMate = null;
            float bestScore = float.MinValue;

            foreach (var other in allCreatures)
            {
                // 对方必须未怀孕且未死亡未失去意识
                if (other.IsPregnant || other.IsDead || other.IsUnconscious) continue;

                // 跳过自己
                if (other.UID == self.UID) continue;

                // 必须是同种
                if (other.SpeciesID != self.SpeciesID) continue;

                // 必须成熟
                float otherAge = _simulationTime - other.BirthTimestamp;
                if (otherAge < other.Maturity_Age) continue;

                // 对方也必须满足繁殖条件
                if (!ShouldReproduce(other)) continue;

                // 距离检查
                float distance = Vector2.Distance(self.Position, other.Position);
                if (distance > Mate_Search_Range) continue;

                // 计算兼容性评分
                float score = CalculateMateScore(self, other, distance);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMate = other;
                }
            }

            return bestMate;
        }

        // ==========================================
        // 配偶评分
        // ==========================================
        /// <summary>
        /// 计算配偶的吸引力评分
        /// </summary>
        private float CalculateMateScore(CreatureData self, CreatureData other, float distance)
        {
            float score = 100f;

            // 距离越近越好
            score -= distance * 2f;

            // 健康度影响
            float healthPercent = other.Structure_Current / other.Structure_Max;
            score += healthPercent * 20f;

            // 营养储备影响
            float nutrientPercent = other.Nutrients / other.Nutrients_Max;
            score += nutrientPercent * 20f;

            // 基因兼容性 (生殖隔离阈值)
            score += other.Reproductive_Compatibility * 10f;

            return score;
        }

        // ==========================================
        // 交配
        // ==========================================
        /// <summary>
        /// 执行交配行为
        /// </summary>
        private void Mate(CreatureData creature, CreatureData mate)
        {
            // 消耗能量
            creature.Energy -= Reproduction_Energy_Cost;
            mate.Energy -= Reproduction_Energy_Cost;

            //记录交配消耗
            creature.Lifetime_EnergySpent_Action += Reproduction_Energy_Cost;
            mate.Lifetime_EnergySpent_Action += Reproduction_Energy_Cost;

            // 设置怀孕状态 (只有一方怀孕)
            creature.IsPregnant = true;
            creature.Pregnancy_Start_Time = _simulationTime;
            creature.Mate_UID = mate.UID;

            // 记录繁殖时间
            creature.Last_Reproduction_Time = _simulationTime;
            mate.Last_Reproduction_Time = _simulationTime;

            Debug.Log($"[ReproductionSystem] 🧬 {creature.SpeciesID} 与 {mate.SpeciesID} 交配成功 | " +
                      $"预计 {creature.Pregnancy_Duration:F0} 秒后产仔");
        }

        // ==========================================
        // 生育
        // ==========================================
        /// <summary>
        /// 生成后代数据 (不创建视图)
        /// </summary>
        private void GiveBirth(CreatureData mother, List<CreatureData> allCreatures, Dictionary<string, SpeciesBlueprint> blueprintMap, float _simulationTime)
        {
            // 清除怀孕状态
            mother.IsPregnant = false;

            // 获取蓝图
            if (!blueprintMap.TryGetValue(mother.SpeciesID, out var blueprint))
            {
                Debug.LogError($"[ReproductionSystem] ❌ 找不到物种蓝图: {mother.SpeciesID}");
                return;
            }

            // 获取父亲数据 (如果还活着)
            CreatureData father = null;
            if (!string.IsNullOrEmpty(mother.Mate_UID))
            {
                father = allCreatures.Find(c => c.UID == mother.Mate_UID);
            }

            int offspringCount = Mathf.RoundToInt(Random.Range(mother.Offspring_Count.Min, mother.Offspring_Count.Max));

            // 【生态修复】：限制后代属性，并向母亲收取生育的营养税
            // 母亲拿出自身当前 60% 的脂肪储备分配给新生儿（如果母亲自己都快饿死了，孩子们出生就离死不远）
            float totalDonatedNutrients = mother.Nutrients * 0.35f;
            float nutrientsPerBaby = totalDonatedNutrients / offspringCount;
            mother.Nutrients -= totalDonatedNutrients;

            //Debug.Log($"[ReproductionSystem]  {mother.SpeciesID} 产仔 {offspringCount} 只 | 消耗母体总营养: {totalDonatedNutrients:F1}");

            // 生成每只后代
            for (int i = 0; i < offspringCount; i++)
            {
                // 随机出生位置 (母亲周围)
                Vector2 offset = Random.insideUnitCircle * Offspring_Spawn_Range;
                Vector2 birthPosition = mother.Position + offset;

                // 使用工具类创建后代数据
                var offspring = GeneticsUtility.CreateOffspring(blueprint, birthPosition, mother, father, _simulationTime);

                // 【核心限制】：覆盖出生时的默认满负荷属性
                offspring.Nutrients = nutrientsPerBaby + 50f;
                // 刚出生的幼崽体力匮乏 (只有 30% 瞬时体力)
                offspring.Energy = offspring.Energy_Max * 0.5f;
                // 幼崽免疫力未完全发育
                offspring.Vitality_Current = offspring.Vitality_Max * 0.8f;
                offspring.Stage = LifeStage.Larva;
                // 添加到待生成列表 (由 SimulationManager 统一处理)
                _pendingOffspring.Add(offspring);
            }

            // 清空配偶 UID
            mother.Mate_UID = null;
        }
    }
}