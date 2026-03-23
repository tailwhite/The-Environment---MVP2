using System.Collections.Generic;
using UnityEngine;
using EvolutionLaws.Data;
using EvolutionLaws.Utilities;

namespace EvolutionLaws.Core
{
    /// <summary>
    /// 【决策系统】
    /// 职责:
    /// 1. 读取感知结果 (PerceivedTargets) 和需求状态 (Needs)
    /// 2. 评估优先级 (威胁 > 饥饿 > 疲劳)
    /// 3. 设置行为状态和目标位置
    /// 原则: 纯逻辑类, 不含 MonoBehaviour
    /// </summary>
    public class DecisionSystem
    {
        // ==========================================
        // 配置参数
        // ==========================================
        public float HungerThreshold = 50f;       // 饥饿度超过此值触发觅食

        public float EnergyRestThreshold = 0.3f;  // 能量低于30%触发休息
        public float ThreatThreshold = 0.5f;      // 威胁度超过此值触发逃跑
        public float FleeDistance = 20f;          // 逃跑目标距离

        // ==========================================
        // 运行时状态
        // ==========================================
        private float _simulationTime = 0f;

        private Dictionary<string, float> _lastDecisionTime = new Dictionary<string, float>();

        // ==========================================
        // 核心 Tick 方法
        // ==========================================
        /// <summary>
        /// 对所有生物执行决策更新
        /// </summary>
        public void Tick(List<CreatureData> creatures, float deltaTime)
        {
            _simulationTime += deltaTime;

            foreach (var creature in creatures)
            {
                if (creature.IsDead || creature.IsUnconscious)
                {
                    // 死亡或昏迷时,强制进入休息状态
                    creature.CurrentBehavior = BehaviorState.Resting;
                    creature.TargetPosition = null;
                    continue;
                }

                // 检查决策冷却 (避免每帧切换目标)
                if (!ShouldMakeDecision(creature))
                    continue;

                // ──────────────────────────────────
                // 优先级 1: 检查威胁 (生存第一)
                // ──────────────────────────────────
                if (EvaluateThreat(creature))
                {
                    HandleThreatBehavior(creature);
                    RecordDecisionTime(creature);
                    continue;
                }

                // ──────────────────────────────────
                // 优先级 2: 检查疲劳 (低能量时休息)
                // ──────────────────────────────────
                if (EvaluateFatigue(creature))
                {
                    SetRestingBehavior(creature);
                    RecordDecisionTime(creature);
                    continue;
                }

                // ──────────────────────────────────
                // 优先级 2.5: 检查狩猎机会 (肉食动物)
                // ──────────────────────────────────
                if (EvaluateHunting(creature))
                {
                    SetHuntingBehavior(creature);
                    RecordDecisionTime(creature);
                    continue;
                }

                // ──────────────────────────────────
                // 优先级 3: 检查饥饿 (寻找食物)
                // ──────────────────────────────────
                if (EvaluateHunger(creature))
                {
                    SetForagingBehavior(creature);
                    RecordDecisionTime(creature);
                    continue;
                }
                // ──────────────────────────────────
                // 优先级 3.5: 检查繁殖需求 (成熟且饱食)
                // ──────────────────────────────────
                if (EvaluateReproduction(creature))
                {
                    SetReproductionBehavior(creature);
                    RecordDecisionTime(creature);
                    continue;
                }
                // ──────────────────────────────────
                // 默认: 闲逛状态
                // ──────────────────────────────────
                SetIdleBehavior(creature);
                RecordDecisionTime(creature);
            }
        }

        // ==========================================
        // 决策冷却检查
        // ==========================================
        /// <summary>
        /// 检查是否应该重新决策 (防止抖动)
        /// </summary>
        private bool ShouldMakeDecision(CreatureData creature)
        {
            if (!_lastDecisionTime.ContainsKey(creature.UID))
                return true;

            float timeSinceLastDecision = _simulationTime - _lastDecisionTime[creature.UID];
            return timeSinceLastDecision >= creature.TargetSwitchCooldown;
        }

        private void RecordDecisionTime(CreatureData creature)// 记录决策时间
        {
            _lastDecisionTime[creature.UID] = _simulationTime;// 记录当前时间为最后决策时间
        }

        // ==========================================
        // 威胁评估
        // ==========================================
        /// <summary>
        /// 检查是否有高威胁目标
        /// </summary>
        private bool EvaluateThreat(CreatureData creature)
        {
            if (creature.PerceivedTargets == null || creature.PerceivedTargets.Count == 0)
                return false;

            foreach (var target in creature.PerceivedTargets)
            {
                if (target.Type == TargetType.Predator && target.Threat > ThreatThreshold)// 如果感知到的目标是捕食者且威胁度超过阈值
                {
                    return true;
                }
            }

            return false;
        }

        // ==========================================
        // 疲劳评估
        // ==========================================
        /// <summary>
        /// 检查是否需要休息
        /// </summary>
        private bool EvaluateFatigue(CreatureData creature)
        {
            // 【生态机制】：绝境求生本能 (肾上腺素机制)
            // 如果体内长期营养/脂肪 (Nutrients) 已经耗尽，休息将无法恢复体力，纯属等死。
            // 此时强制抑制疲劳感，允许进行更低优先级的行动（寻找食物）
            if (creature.Nutrients <= 0f)
            {
                return false;
            }
            // 【行为惯性】：如果已经在休息，那就一口气休息到大部分体力恢复 (80%) 再起来；否则等低于阈值 (30%) 才触发躺下。
            float threshold = creature.CurrentBehavior == BehaviorState.Resting ? 0.8f : EnergyRestThreshold;

            float energyPercent = creature.Energy / creature.Energy_Max;
            return energyPercent < threshold;
        }

        // ==========================================
        // 饥饿评估
        // ==========================================
        /// <summary>
        /// 检查是否需要觅食
        /// </summary>
        private bool EvaluateHunger(CreatureData creature)
        {
            // 【行为惯性】：一旦开始觅食，不吃饱(饥饿感归0)就不会停下脚步；平时则等达到 HungerThreshold(被饿急了)才出动
            float threshold = (creature.CurrentBehavior == BehaviorState.Foraging) ? 0f : HungerThreshold;

            // 饥饿度未达到当前阈值
            if (creature.Need_Hunger <= threshold)
                return false;

            // 检查是否有可用食物
            if (creature.PerceivedTargets == null || creature.PerceivedTargets.Count == 0)
                return false;

            foreach (var target in creature.PerceivedTargets)
            {
                if (target.Type == TargetType.FoodResource && target.Attractiveness > 0f)
                {
                    return true;
                }
            }

            return false;
        }

        // ==========================================
        // 狩猎评估
        // ==========================================
        /// <summary>
        /// 检查是否应该狩猎 (肉食动物逻辑)
        /// </summary>
        private bool EvaluateHunting(CreatureData creature)
        {
            // 只有肉食动物才狩猎 (Meat 效率 > 0.5)
            float meatEfficiency = MetabolismUtility.GetDietEfficiency(creature, ResourceType.Meat);
            if (meatEfficiency < 0.5f)
                return false;

            // 【行为惯性】：狩猎也是进食，一旦杀红了眼，必须吃到肚子撑下(Need_Hunger<=0)才复归平静
            float huntingHungerThreshold = (creature.CurrentBehavior == BehaviorState.Hunting) ? 0f : HungerThreshold;

            // 必须饥饿 或者 极具攻击性(嗜血本能)
            if (creature.Need_Hunger <= huntingHungerThreshold && creature.Trait_Aggression < 0.8f)
                return false;

            // 检查是否有猎物目标
            if (creature.PerceivedTargets == null || creature.PerceivedTargets.Count == 0)
                return false;

            foreach (var target in creature.PerceivedTargets)
            {
                if (target.Type == TargetType.Prey)
                {
                    return true;
                }
            }

            return false;
        }

        // ==========================================
        // 繁殖评估
        // ==========================================
        /// <summary>
        /// 检查是否应该寻找配偶繁殖
        /// </summary>
        private bool EvaluateReproduction(CreatureData creature)
        {
            // 已怀孕,跳过
            if (creature.IsPregnant)
                return false;

            // 未成熟
            float age = _simulationTime - creature.BirthTimestamp;
            if (age < creature.Maturity_Age)
                return false;

            // 营养储备不足 (需要至少 70% 营养)
            float nutrientPercent = creature.Nutrients / creature.Nutrients_Max;
            if (nutrientPercent < 0.7f)
                return false;

            // 能量不足
            float energyPercent = creature.Energy / creature.Energy_Max;
            if (energyPercent < 0.5f)
                return false;

            // 有饥饿需求时优先觅食
            if (creature.Need_Hunger > 30f)
                return false;

            return true;
        }

        // ==========================================
        // 行为设置: 威胁处理 (逃避或困兽犹斗)
        // ==========================================
        /// <summary>
        /// 设置面对威胁的行为
        /// </summary>
        private void HandleThreatBehavior(CreatureData creature)
        {
            // 找到最大威胁目标
            PerceivedTarget? maxThreat = null;
            float maxThreatValue = 0f;

            foreach (var target in creature.PerceivedTargets)
            {
                if (target.Type == TargetType.Predator && target.Threat > maxThreatValue)
                {
                    maxThreat = target;
                    maxThreatValue = target.Threat;
                }
            }

            if (maxThreat.HasValue)
            {
                // -- 困兽犹斗验证 (Fight or Flight) --
                float healthPercent = creature.Structure_Current / creature.Structure_Max;
                bool isCornered = healthPercent < 0.3f || creature.Stress_Current > 80f;

                // 如果具有足够反抗特质，且绝望状态下 -> 发起反击
                if (creature.Trait_Aggression > 0.4f && isCornered)
                {
                    creature.CurrentBehavior = BehaviorState.Hunting; // 借用狩猎状态实施反击攻击
                    creature.TargetPosition = maxThreat.Value.Position;
                    creature.TargetCreatureUID = maxThreat.Value.UID;

                    creature.Stress_Current += 5f * Time.deltaTime;
                    Debug.Log($"[DecisionSystem] {creature.SpeciesID} [{creature.UID.Substring(0, 6)}]困兽犹斗！开始反击攻击 {maxThreat.Value.UID}");
                }
                else
                {
                    // 计算逃跑方向 (与威胁相反)
                    creature.CurrentBehavior = BehaviorState.Fleeing;
                    Vector2 fleeDirection = (creature.Position - maxThreat.Value.Position).normalized;
                    creature.TargetPosition = creature.Position + fleeDirection * FleeDistance;

                    // 被追逐途中增加压力值
                    creature.Stress_Current += 10f * Time.deltaTime;
                    creature.Stress_Current = Mathf.Min(creature.Stress_Current, 100f);

                    //Debug.Log($"[DecisionSystem] {creature.SpeciesID} [{creature.UID.Substring(0, 6)}]进入逃跑模式 | 威胁: {maxThreat.Value.Threat:P0}");
                }
            }
            else
            {
                // 后备方案: 清空目标
                creature.TargetPosition = null;
            }
        }

        // ==========================================
        // 行为设置: 逃跑
        // ==========================================
        /// <summary>
        /// 设置逃跑行为 (远离威胁)
        /// </summary>
        private void SetFleeingBehavior(CreatureData creature)
        {
            creature.CurrentBehavior = BehaviorState.Fleeing;

            // 找到最大威胁目标
            PerceivedTarget? maxThreat = null;
            float maxThreatValue = 0f;

            foreach (var target in creature.PerceivedTargets)
            {
                if (target.Type == TargetType.Predator && target.Threat > maxThreatValue)
                {
                    maxThreat = target;
                    maxThreatValue = target.Threat;
                }
            }

            if (maxThreat.HasValue)
            {
                // 计算逃跑方向 (与威胁相反)
                Vector2 fleeDirection = (creature.Position - maxThreat.Value.Position).normalized;
                creature.TargetPosition = creature.Position + fleeDirection * FleeDistance;

                // 增加压力值
                creature.Stress_Current += 10f * Time.deltaTime;
                creature.Stress_Current = Mathf.Min(creature.Stress_Current, 100f);

                Debug.Log($"[DecisionSystem] {creature.SpeciesID} [{creature.UID.Substring(0, 6)}]进入逃跑模式 | 威胁: {maxThreat.Value.Threat:P0}");
            }
            else
            {
                // 后备方案: 清空目标
                creature.TargetPosition = null;
            }
        }

        // ==========================================
        // 行为设置: 觅食
        // ==========================================
        /// <summary>
        /// 设置觅食行为 (走向最吸引人的食物)
        /// </summary>
        private void SetForagingBehavior(CreatureData creature)
        {
            creature.CurrentBehavior = BehaviorState.Foraging;

            // 找到吸引力最高的食物
            PerceivedTarget? bestFood = null;//可空结构体
            float maxAttractiveness = 0f;

            foreach (var target in creature.PerceivedTargets)
            {
                if (target.Type == TargetType.FoodResource && target.Attractiveness > maxAttractiveness)
                {
                    bestFood = target;
                    maxAttractiveness = target.Attractiveness;
                }
            }

            if (bestFood.HasValue)//如果有值
            {
                creature.TargetPosition = bestFood.Value.Position;

                // 降低压力值 (找到食物会减压)
                creature.Stress_Current -= 5f * Time.deltaTime;
                creature.Stress_Current = Mathf.Max(creature.Stress_Current, 0f);

                //Debug.Log($"[DecisionSystem] {creature.SpeciesID}[{creature.UID.Substring(0, 6)}] 进入觅食模式 | 目标: {bestFood.Value.ResourceType} @ {bestFood.Value.Position}");
            }
            else
            {
                // 没有食物,切换到闲逛
                SetIdleBehavior(creature);
            }
        }

        // ==========================================
        // 行为设置: 休息
        // ==========================================
        /// <summary>
        /// 设置休息行为 (停止移动)
        /// </summary>
        private void SetRestingBehavior(CreatureData creature)
        {
            creature.CurrentBehavior = BehaviorState.Resting;
            creature.TargetPosition = null; // 清空目标,MovementSystem 会跳过移动

            // 降低压力值
            creature.Stress_Current -= 3f * Time.deltaTime;
            creature.Stress_Current = Mathf.Max(creature.Stress_Current, 0f);

            //Debug.Log($"[DecisionSystem] {creature.SpeciesID} [{creature.UID.Substring(0, 6)}]进入休息模式 | 能量: {creature.Energy:F1}/{creature.Energy_Max}");
        }

        // ==========================================
        // 行为设置: 闲逛
        // ==========================================
        /// <summary>
        /// 设置闲逛行为 (随机漫游)
        /// </summary>
        private void SetIdleBehavior(CreatureData creature)
        {
            creature.CurrentBehavior = BehaviorState.Idle;
            creature.TargetPosition = null; // 清空目标,让 MovementSystem 使用随机漫游

            // 缓慢降低压力值
            creature.Stress_Current -= 1f * Time.deltaTime;
            creature.Stress_Current = Mathf.Max(creature.Stress_Current, 0f);
        }

        // ==========================================
        // 行为设置: 狩猎
        // ==========================================
        /// <summary>
        /// 设置狩猎行为 (追捕猎物)
        /// </summary>
        private void SetHuntingBehavior(CreatureData creature)
        {
            creature.CurrentBehavior = BehaviorState.Hunting;

            // 找到最佳猎物 (距离最近 + 体型最小)
            PerceivedTarget? bestPrey = null;
            float bestScore = float.MaxValue;

            foreach (var target in creature.PerceivedTargets)
            {
                if (target.Type != TargetType.Prey) continue;

                // 评分 = 距离 (距离越近越好)
                float score = target.Distance;

                if (score < bestScore)
                {
                    bestScore = score;
                    bestPrey = target;
                }
            }

            if (bestPrey.HasValue)
            {
                creature.TargetPosition = bestPrey.Value.Position;
                creature.TargetCreatureUID = bestPrey.Value.UID;

                //Debug.Log($"[DecisionSystem] {creature.SpeciesID} [{creature.UID.Substring(0, 6)}]进入狩猎模式 | 目标: {bestPrey.Value.UID}");
            }
            else
            {
                SetIdleBehavior(creature);
            }
        }

        // ==========================================
        // 行为设置: 繁殖
        // ==========================================
        /// <summary>
        /// 设置繁殖行为 (寻找配偶)
        /// </summary>
        private void SetReproductionBehavior(CreatureData creature)
        {
            creature.CurrentBehavior = BehaviorState.Socializing; // 使用社交状态代表繁殖

            // 清空移动目标 (让 ReproductionSystem 处理配偶寻找)
            creature.TargetPosition = null;

            //Debug.Log($"[DecisionSystem] {creature.SpeciesID} [{creature.UID.Substring(0, 6)}]进入繁殖模式 | 年龄: {Time.time - creature.BirthTimestamp:F0}s");
        }
    }
}