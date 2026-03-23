using EvolutionLaws.Data;
using EvolutionLaws.Utilities;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EvolutionLaws.Core
{
    /// <summary>
    /// 【感知系统】
    /// 职责:
    /// 1. 扫描视野范围内的食物资源 (基于网格遍历)
    /// 2. 检测附近的其他生物 (同类/捕食者)
    /// 3. 更新 CreatureData.PerceivedTargets
    /// 原则: 纯逻辑类, 零物理引擎依赖, 基于数学计算
    /// </summary>
    public class PerceptionSystem
    {
        // ==========================================
        // 配置参数
        // ==========================================
        public float UpdateInterval = 0.5f;  // 感知刷新间隔 (性能优化)

        public float FoodAttractivenessMultiplier = 1.0f; // 食物吸引力系数

        // ==========================================
        // 运行时状态
        // ==========================================
        private float _simulationTime = 0f; // 累计仿真时间

        private float _lastUpdateTime = 0f; // 上次更新时间

        // ==========================================
        // 核心 Tick 方法
        // ==========================================
        /// <summary>
        /// 对所有生物执行感知更新
        /// </summary>
        public void Tick(List<CreatureData> creatures, EnvironmentData environment, float deltaTime)
        {
            _simulationTime += deltaTime;

            // 性能优化: 不是每帧更新
            if (_simulationTime - _lastUpdateTime < UpdateInterval)
                return;

            _lastUpdateTime = _simulationTime;

            foreach (var creature in creatures)
            {
                if (creature.IsDead || creature.IsUnconscious) continue;//死亡或昏迷的生物不感知

                // 清空旧数据
                creature.PerceivedTargets.Clear();//对所有生物的感知目标列表进行清空，以准备存储新的感知信息？？？

                // ──────────────────────────────────
                // 阶段 1: 嗅觉感知 (食物资源)
                // ──────────────────────────────────
                PerceiveFoodResources(creature, environment);

                // ──────────────────────────────────
                // 阶段 2: 视觉感知 (其他生物)
                // ──────────────────────────────────
                PerceiveCreatures(creature, creatures);

                // ──────────────────────────────────
                // 阶段 3: 优先级排序 (威胁 > 食物 > 同类)
                // ──────────────────────────────────
                SortByPriority(creature);

                // ──────────────────────────────────
                // 阶段 4: 限制数量 (Attention_Cap)
                // ──────────────────────────────────
                if (creature.PerceivedTargets.Count > creature.Attention_Cap)
                {//如果感知到的目标数量超过了生物的注意力上限，那么就移除多余的目标，保留最优先的几个。
                    creature.PerceivedTargets.RemoveRange(
                        creature.Attention_Cap,
                        creature.PerceivedTargets.Count - creature.Attention_Cap
                    );
                }
            }
        }

        // ==========================================
        // 嗅觉感知: 基于网格扫描食物
        // ==========================================
        /// <summary>
        /// 扫描周围格子的食物资源 (植物/矿物/肉块)
        /// </summary>
        private void PerceiveFoodResources(CreatureData creature, EnvironmentData environment)
        {
            // 计算扫描范围 (嗅觉灵敏度影响半径)
            int scanRadius = Mathf.CeilToInt(creature.Vision_Range * creature.Scent_Sensitivity);
            int centerX = Mathf.FloorToInt(creature.Position.x);//生物所在格子的中心坐标
            int centerY = Mathf.FloorToInt(creature.Position.y);

            // 遍历周围格子
            for (int y = centerY - scanRadius; y <= centerY + scanRadius; y++)
            {
                for (int x = centerX - scanRadius; x <= centerX + scanRadius; x++)
                {
                    var tile = environment.GetTile(x, y);
                    if (tile == null) continue;

                    Vector2 tileCenter = new Vector2(x + 0.5f, y + 0.5f);
                    float distance = Vector2.Distance(creature.Position, tileCenter);

                    // 超出感知范围,跳过
                    if (distance > creature.Vision_Range * creature.Scent_Sensitivity)
                        continue;

                    // ━━━ 检测植物资源 ━━━
                    if (tile.Biomass_Plant > 5f)
                    {
                        float efficiency = MetabolismUtility.GetDietEfficiency(creature, ResourceType.Plant_Fiber);
                        if (efficiency > 0f)
                        {
                            creature.PerceivedTargets.Add(new PerceivedTarget
                            {
                                Type = TargetType.FoodResource,
                                Position = tileCenter,
                                ResourceType = ResourceType.Plant_Fiber,
                                Distance = distance,
                                Attractiveness = CalculateFoodAttractiveness(creature, tile.Biomass_Plant, efficiency),
                                Threat = 0f,
                                LastSeenTime = _simulationTime
                            });
                        }
                    }

                    // ━━━ 检测矿物资源 ━━━
                    if (tile.Biomass_Mineral > 5f)
                    {
                        float efficiency = MetabolismUtility.GetDietEfficiency(creature, ResourceType.Mineral);
                        if (efficiency > 0f)
                        {
                            creature.PerceivedTargets.Add(new PerceivedTarget
                            {
                                Type = TargetType.FoodResource,
                                Position = tileCenter,
                                ResourceType = ResourceType.Mineral,
                                Distance = distance,
                                Attractiveness = CalculateFoodAttractiveness(creature, tile.Biomass_Mineral, efficiency),
                                Threat = 0f,
                                LastSeenTime = _simulationTime
                            });
                        }
                    }

                    // ━━━ 检测肉类资源 ━━━
                    if (tile.Biomass_Meat > 2f)
                    {
                        float efficiency = MetabolismUtility.GetDietEfficiency(creature, ResourceType.Meat);
                        if (efficiency > 0f)
                        {
                            creature.PerceivedTargets.Add(new PerceivedTarget
                            {
                                Type = TargetType.FoodResource,
                                Position = tileCenter,
                                ResourceType = ResourceType.Meat,
                                Distance = distance,
                                Attractiveness = CalculateFoodAttractiveness(creature, tile.Biomass_Meat, efficiency),
                                Threat = 0f,
                                LastSeenTime = _simulationTime
                            });
                        }
                    }
                }
            }
        }

        // ==========================================
        // 视觉感知: 检测其他生物
        // ==========================================
        /// <summary>
        /// 检测视野范围内的其他生物 (同类/捕食者)
        /// </summary>
        private void PerceiveCreatures(CreatureData self, List<CreatureData> allCreatures)
        {
            foreach (var other in allCreatures)
            {
                if (other.UID == self.UID) continue; // 跳过自己
                if (other.IsDead) continue;

                float distance = Vector2.Distance(self.Position, other.Position);//计算与其他生物的距离

                // 超出视野范围
                if (distance > self.Vision_Range) continue;

                // TODO: 未来可以加入视野角度检测 (扇形视野)
                // if (!IsInViewCone(self, other)) continue;

                // 判断目标类型
                TargetType type = ClassifyCreature(self, other);
                float threat = CalculateThreat(self, other, type);

                float attractiveness = 0f;
                if (type == TargetType.Prey)
                {
                    // 猎物相当于一块巨大且优质的肉
                    float selfMeatEfficiency = MetabolismUtility.GetDietEfficiency(self, ResourceType.Meat);
                    attractiveness = CalculateFoodAttractiveness(self, other.Mass * 50f, selfMeatEfficiency);

                    // 叠加捕食者的嗜血本能，即使不饿也会眼馋
                    attractiveness += self.Trait_Aggression;
                }
                self.PerceivedTargets.Add(new PerceivedTarget
                {
                    Type = type,
                    Position = other.Position,
                    UID = other.UID,
                    ResourceType = ResourceType.None,
                    Distance = distance,
                    Attractiveness = attractiveness, // 同类暂时无吸引力
                    Threat = threat,
                    LastSeenTime = _simulationTime
                });//将其他生物添加到感知目标列表中，包含其类型、位置、UID、距离和威胁度等信息
            }
        }

        // ==========================================
        // 生物分类
        // ==========================================
        /// <summary>
        /// 判断其他生物是同类还是捕食者
        /// </summary>
        private TargetType ClassifyCreature(CreatureData self, CreatureData other)
        {
            // 判断是否同种
            if (self.SpeciesID == other.SpeciesID)
                return TargetType.Ally;//标记无害，后续可以用于社交，竞争等行为

            // 判断自己是否是肉食动物
            float selfMeatEfficiency = MetabolismUtility.GetDietEfficiency(self, ResourceType.Meat);

            // 如果自己是肉食 + 对方体型较小 = 猎物
            if (selfMeatEfficiency > 0.5f && other.Size <= self.Size * 1.0f)
                return TargetType.Prey;

            // 判断是否是捕食者 (对方是肉食 + 体型大)
            float otherMeatEfficiency = MetabolismUtility.GetDietEfficiency(other, ResourceType.Meat);
            if (otherMeatEfficiency > 0.5f && other.Size > self.Size * 1.0f && other.Trait_Aggression > 0.5f)
                return TargetType.Predator;

            // 默认视为无害生物
            return TargetType.Ally;
        }

        // ==========================================
        // 威胁度计算
        // ==========================================
        /// <summary>
        /// 计算威胁度 (用于逃跑决策)
        /// </summary>
        private float CalculateThreat(CreatureData self, CreatureData other, TargetType type)
        {
            if (type != TargetType.Predator)
                return 0f;

            // 体型差距越大,威胁越高
            float sizeDiff = (other.Size - self.Size) / self.Size;
            float aggressionFactor = other.Trait_Aggression;

            return Mathf.Clamp01(sizeDiff * aggressionFactor);
        }

        // ==========================================
        // 食物吸引力计算
        // ==========================================
        /// <summary>
        /// 计算食物吸引力 (用于觅食决策)
        /// </summary>
        private float CalculateFoodAttractiveness(CreatureData creature, float foodAmount, float efficiency)
        {
            // 吸引力 = 饥饿度 × 食物量 × 消化效率
            float hungerFactor = creature.Need_Hunger / 100f;
            float amountFactor = Mathf.Clamp01(foodAmount / 50f); // 50 为基准值

            return hungerFactor * amountFactor * efficiency * FoodAttractivenessMultiplier;
        }

        // ==========================================
        // 优先级排序
        // ==========================================
        /// <summary>
        /// 按优先级排序: 威胁 > 食物 > 同类
        /// </summary>
        private void SortByPriority(CreatureData creature)
        {
            creature.PerceivedTargets.Sort((a, b) =>
            {
                // 威胁优先级最高
                if (a.Threat > 0.5f && b.Threat <= 0.5f) return -1;
                if (b.Threat > 0.5f && a.Threat <= 0.5f) return 1;
                // 2. 活体猎物优先级必定高于静态环境资源 (优先锁定活物)
                if (a.Type == TargetType.Prey && b.Type != TargetType.Prey) return -1;
                if (b.Type == TargetType.Prey && a.Type != TargetType.Prey) return 1;

                // 其次按吸引力排序
                return b.Attractiveness.CompareTo(a.Attractiveness);
            });
        }
    }
}