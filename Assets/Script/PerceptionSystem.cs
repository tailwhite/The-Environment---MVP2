using EvolutionLaws.Data;
using EvolutionLaws.Utilities;
using System.Collections.Generic;
using UnityEngine;

namespace EvolutionLaws.Core
{
    /// <summary>
    /// 【感知系统】(优化版)
    /// 引入 Spatial Partitioning (空间划分) 和 Time-Slicing (分帧调度)
    /// 测试结果复杂度由 O(n²) 降至 O(n) 或 O(n * k)
    /// </summary>
    public class PerceptionSystem
    {
        // ==========================================
        // 性能与配置参数
        // ==========================================
        public float FoodAttractivenessMultiplier = 1.0f; // 食物吸引力系数

        [Header("性能优化设置")]
        public int DistributionFrames = 5;   // 将所有生物的感知运算平摊到 N 帧里完成 (替代原有的暴噪降频)

        public float SpatialChunkSize = 10f; // 空间划分每个网格的尺寸 (10x10米)

        // ==========================================
        // 运行时状态
        // ==========================================
        private float _simulationTime = 0f;

        private int _currentFrame = 0; // 分帧游标

        // 核心：空间划分网格 Hash (键为二维区块索引，值为该区块内的生物列表)
        private Dictionary<Vector2Int, List<CreatureData>> _spatialGrid = new Dictionary<Vector2Int, List<CreatureData>>();

        // ==========================================
        // 核心 Tick 方法
        // ==========================================
        public void Tick(List<CreatureData> creatures, EnvironmentData environment, float deltaTime)
        {
            _simulationTime += deltaTime;

            // 步骤 1：每帧重建空间网格 (极速 O(N) 操作，比查距快几百倍)
            BuildSpatialGrid(creatures);

            // 步骤 2：游标推进，决定本帧该哪一批生物“睁开眼睛”
            _currentFrame = (_currentFrame + 1) % DistributionFrames;// 如果 DistributionFrames=5，则 _currentFrame 在0-4循环，平均每5帧更新一次同一批生物

            for (int i = 0; i < creatures.Count; i++)
            {
                // 【Time-Slicing】：本帧不归你算，继续保留上次的残影，直接跳过
                if (i % DistributionFrames != _currentFrame)
                    continue;

                var creature = creatures[i];
                if (creature.IsDead || creature.IsUnconscious) continue;

                // 清空旧数据
                creature.PerceivedTargets.Clear();

                // 阶段 1: 嗅觉感知 (食物资源) - 查底层的Environment grid
                PerceiveFoodResources(creature, environment);

                // 阶段 2: 视觉感知 (其他生物) - 查空间哈希桶 _spatialGrid
                PerceiveCreaturesFast(creature, environment);

                // 阶段 3: 优先级排序
                SortByPriority(creature);

                // 阶段 4: 视力过载限制
                if (creature.PerceivedTargets.Count > creature.Attention_Cap)
                {
                    creature.PerceivedTargets.RemoveRange(
                        creature.Attention_Cap,
                        creature.PerceivedTargets.Count - creature.Attention_Cap
                    );
                }
            }
        }

        // ==========================================
        // 构建空间划分网格
        // ==========================================
        private void BuildSpatialGrid(List<CreatureData> creatures)
        {
            _spatialGrid.Clear(); // 释放引用，不用从头new
            foreach (var c in creatures)
            {
                if (c.IsDead) continue;
                // 计算当前所处的 Chunk 坐标
                Vector2Int chunkID = new Vector2Int(
                    Mathf.FloorToInt(c.Position.x / SpatialChunkSize),
                    Mathf.FloorToInt(c.Position.y / SpatialChunkSize)
                );

                if (!_spatialGrid.TryGetValue(chunkID, out var list))//如果这个区块还没有生物列表，创建一个新的列表并加入字典
                {
                    list = new List<CreatureData>();
                    _spatialGrid[chunkID] = list;
                }
                list.Add(c);
            }
        }

        // ==========================================
        // 视觉感知: 检测其他生物 (急速版)
        // ==========================================
        private void PerceiveCreaturesFast(CreatureData self, EnvironmentData environment)
        {
            float maxVisionRadius = self.Vision_Range;
            // 计算自己处在哪个 Chunk
            Vector2Int centerChunk = new Vector2Int(
                Mathf.FloorToInt(self.Position.x / SpatialChunkSize),
                Mathf.FloorToInt(self.Position.y / SpatialChunkSize)
            );

            // 我能看多远？折算成我需要检索旁边几个 Chunk (比如视距15米 / Chunk 10米 -> 则检索周边1格即可)
            int searchChunkRadius = Mathf.CeilToInt(maxVisionRadius / SpatialChunkSize);

            // 【Spatial Partitioning】：只遍历中心及周边少量区块，跳过茫茫多的无关远方生物
            for (int x = -searchChunkRadius; x <= searchChunkRadius; x++)
            {
                for (int y = -searchChunkRadius; y <= searchChunkRadius; y++)
                {
                    Vector2Int checkChunk = new Vector2Int(centerChunk.x + x, centerChunk.y + y);

                    if (_spatialGrid.TryGetValue(checkChunk, out var potentialTargets))
                    {
                        foreach (var other in potentialTargets)
                        {
                            if (other.UID == self.UID) continue;

                            float distance = Vector2.Distance(self.Position, other.Position);

                            // 隐蔽计算
                            float effectiveVision = self.Vision_Range;
                            var tile = environment.GetTile(Mathf.FloorToInt(other.Position.x), Mathf.FloorToInt(other.Position.y));
                            if (tile != null && tile.Stealth_Factor > 0f)
                            {
                                float sizeFactor = Mathf.Clamp01(1.0f / (other.Size + 0.1f));
                                float hideReduction = self.Vision_Range * (tile.Stealth_Factor * 0.95f * sizeFactor);
                                effectiveVision -= hideReduction;
                            }

                            // 视距剔除
                            if (distance > effectiveVision) continue;

                            TargetType type = ClassifyCreature(self, other);
                            float threat = CalculateThreat(self, other, type);
                            float attractiveness = 0f;

                            if (type == TargetType.Prey)
                            {
                                float selfMeatEfficiency = MetabolismUtility.GetDietEfficiency(self, ResourceType.Meat);
                                attractiveness = CalculateFoodAttractiveness(self, other.Mass * 50f, selfMeatEfficiency, distance);
                                attractiveness += self.Trait_Aggression;
                            }

                            self.PerceivedTargets.Add(new PerceivedTarget
                            {
                                Type = type,
                                Position = other.Position,
                                UID = other.UID,
                                ResourceType = ResourceType.None,
                                Distance = distance,
                                Attractiveness = attractiveness,
                                Threat = threat,
                                LastSeenTime = _simulationTime
                            });
                        }
                    }
                }
            }
        }

        // ==========================================
        // 嗅觉感知: 找自然资源
        // ==========================================
        private void PerceiveFoodResources(CreatureData creature, EnvironmentData environment)
        {
            // 计算扫描范围 (基于视觉范围和嗅觉敏感度)
            int scanRadius = Mathf.CeilToInt(creature.Vision_Range * creature.Scent_Sensitivity);
            int centerX = Mathf.FloorToInt(creature.Position.x);
            int centerY = Mathf.FloorToInt(creature.Position.y);

            // 限定不能搜出地图外，避免数组越界且优化无效计算
            int minX = Mathf.Max(0, centerX - scanRadius);
            int maxX = Mathf.Min(environment.Width - 1, centerX + scanRadius);
            int minY = Mathf.Max(0, centerY - scanRadius);
            int maxY = Mathf.Min(environment.Height - 1, centerY + scanRadius);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var tile = environment.GetTile(x, y);
                    if (tile == null) continue;

                    Vector2 tileCenter = new Vector2(x + 0.5f, y + 0.5f);
                    float distance = Vector2.Distance(creature.Position, tileCenter);

                    if (distance > creature.Vision_Range * creature.Scent_Sensitivity)
                        continue;

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
                                Attractiveness = CalculateFoodAttractiveness(creature, tile.Biomass_Plant, efficiency, distance),
                                Threat = 0f,
                                LastSeenTime = _simulationTime
                            });
                        }
                    }

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
                                Attractiveness = CalculateFoodAttractiveness(creature, tile.Biomass_Mineral, efficiency, distance),
                                Threat = 0f,
                                LastSeenTime = _simulationTime
                            });
                        }
                    }

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
                                Attractiveness = CalculateFoodAttractiveness(creature, tile.Biomass_Meat, efficiency, distance),
                                Threat = 0f,
                                LastSeenTime = _simulationTime
                            });
                        }
                    }
                }
            }
        }

        private TargetType ClassifyCreature(CreatureData self, CreatureData other)
        {
            if (self.SpeciesID == other.SpeciesID) return TargetType.Ally;
            float selfMeatEfficiency = MetabolismUtility.GetDietEfficiency(self, ResourceType.Meat);
            if (selfMeatEfficiency > 0.1f && other.Size <= self.Size * 1.5f) return TargetType.Prey;
            float otherMeatEfficiency = MetabolismUtility.GetDietEfficiency(other, ResourceType.Meat);
            if (otherMeatEfficiency > 0.1f && other.Size > self.Size * 0.8f && other.Trait_Aggression > 0.6f) return TargetType.Predator;
            return TargetType.Ally;
        }

        private float CalculateThreat(CreatureData self, CreatureData other, TargetType type)
        {
            if (type != TargetType.Predator) return 0f;
            float sizeDiff = (other.Size - self.Size) / self.Size;
            return Mathf.Clamp01(sizeDiff * other.Trait_Aggression);
        }

        private float CalculateFoodAttractiveness(CreatureData creature, float foodAmount, float efficiency, float distance)
        {
            float hungerFactor = creature.Need_Hunger / 100f;
            float amountFactor = Mathf.Clamp01(foodAmount / 50f);

            // 【生态优化】：距离惩罚 (越远吸引力越低，模拟生物的“节能/懒惰本能”)
            // 距离每远1米，吸引力会平滑衰减
            float distanceFactor = 1.0f / (1.0f + distance * 0.1f);

            // 【生态优化】：性格噪音 (利用“好奇心”打乱羊群)
            // 好奇心越高，越有可能不选最优解而去稍微远一点的地方吃，从而自然分散种群
            float noise = 1.0f + UnityEngine.Random.Range(-0.2f, creature.Trait_Curiosity * 0.5f);

            return hungerFactor * amountFactor * efficiency * distanceFactor * noise * FoodAttractivenessMultiplier;
        }

        private void SortByPriority(CreatureData creature)
        {
            creature.PerceivedTargets.Sort((a, b) =>
            {
                if (a.Threat > 0.5f && b.Threat <= 0.5f) return -1;
                if (b.Threat > 0.5f && a.Threat <= 0.5f) return 1;
                if (a.Type == TargetType.Prey && b.Type != TargetType.Prey) return -1;
                if (b.Type == TargetType.Prey && a.Type != TargetType.Prey) return 1;
                return b.Attractiveness.CompareTo(a.Attractiveness);
            });
        }
    }
}