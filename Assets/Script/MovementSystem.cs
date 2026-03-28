using System.Collections.Generic;
using UnityEngine;
using EvolutionLaws.Data;

namespace EvolutionLaws.Core
{
    /// <summary>
    /// 【移动系统】
    /// 职责:
    /// 1. 执行生物的移动决策 (漫游/追踪/逃跑)
    /// 2. 验证地形可通行性
    /// 3. 计算移动能量消耗
    /// 原则:纯逻辑类,不含MonoBehaviour
    /// </summary>
    public class MovementSystem
    {
        // ==========================================
        // 配置参数
        // ==========================================
        [Header("Movement Settings")]
        public float Wander_Interval = 2.0f;           // 漫游决策间隔(秒)

        public float Move_Energy_Base_Cost = 0.2f;     // 基础移动能量消耗
        public float Move_Structure_Wear = 0.1f;       // 移动结构磨损

        // 目标导向移动参数，未来可以增加更多参数 (如: 追踪/逃跑的决策间隔和能量成本,不同地形的额外成本等)
        public float TargetApproachThreshold = 1.5f;   // 距离目标多近算"到达"

        // ==========================================
        // 使用仿真累计时间
        // ==========================================
        private float _simulationTime = 0f; // 仿真累计时间

        private Dictionary<string, float> _lastMoveTime = new Dictionary<string, float>(); // UID -> 上次移动的仿真时间

        // ==========================================
        // 核心Tick方法
        // ==========================================
        /// <summary>
        /// 对所有生物执行移动逻辑
        /// </summary>
        public void Tick(List<CreatureData> creatures, EnvironmentData environment, EnvironmentManager envManager, float deltaTime)
        {
            // 累加仿真时间
            _simulationTime += deltaTime;

            foreach (var creature in creatures)
            {
                creature.IsMoving = false;

                if (creature.IsDead || creature.IsUnconscious) continue;// 死亡或昏迷的生物不移动
                if (creature.Energy < Move_Energy_Base_Cost) continue;// 能量不足以移动

                if (creature.CurrentBehavior == BehaviorState.Resting)
                    continue;
                //使用仿真时间判断
                if (!ShouldMove(creature.UID))// 如果不应该移动,跳过
                    continue;

                //Vector2 targetPosition = DecideWanderTarget(creature, environment, envManager);
                Vector2 targetPosition = DecideTargetPosition(creature, environment, envManager, creatures);

                if (targetPosition == creature.Position)
                    continue;
                // 计算移动能量消耗
                float moveCost = CalculateMoveCost(creature, targetPosition, environment);
                if (creature.Energy < moveCost)
                {
                    // 体力枯竭，取消移动并强制变回挂起/休息状态，等待代谢系统判决
                    creature.CurrentBehavior = BehaviorState.Resting;
                    continue;
                }
                creature.Energy -= moveCost;
                //记录移动能量消耗
                creature.Lifetime_EnergySpent_Move += moveCost;
                // 移动导致结构磨损
                creature.Structure_Current -= Move_Structure_Wear;

                creature.Position = targetPosition;//唯一一处更新位置的地方,确保所有移动逻辑都走这里

                creature.IsMoving = true;
                // 记录仿真时间
                _lastMoveTime[creature.UID] = _simulationTime;

                //Debug.Log($"[MovementSystem] {creature.SpeciesID} 移动到 {targetPosition} | 消耗能量: {moveCost:F1}");
            }
        }

        // ==========================================
        // 决策逻辑:是否应该移动 (仿真时间)
        // ==========================================
        private bool ShouldMove(string uid)
        {
            // 如果没有移动记录,立即移动
            if (!_lastMoveTime.ContainsKey(uid))
            {// 记录当前仿真时间
                _lastMoveTime[uid] = _simulationTime;
                return true;
            }
            // 检查是否超过间隔时间
            float timeSinceLastMove = _simulationTime - _lastMoveTime[uid];
            return timeSinceLastMove >= Wander_Interval;
        }

        // ==========================================
        // 漫游目标选择
        // ==========================================
        /// <summary>
        /// 选择目标通行格子
        /// </summary>
        // 未来可以增加更复杂的漫游行为 (如: 徘徊在兴趣点周围,或沿着特定路径漫游)
        private Vector2 DecideTargetPosition(CreatureData creature, EnvironmentData environment, EnvironmentManager envManager, List<CreatureData> creatures)
        {
            // ━━━ 实时解决"瞄准残影"：更新追踪目标当前坐标 ━━━
            if (!string.IsNullOrEmpty(creature.TargetCreatureUID))
            {
                var targetCreature = creatures.Find(c => c.UID == creature.TargetCreatureUID);
                if (targetCreature != null && !targetCreature.IsDead)
                {
                    creature.TargetPosition = targetCreature.Position; // 每帧覆写为目标真实坐标
                }
                else
                {
                    creature.TargetCreatureUID = null; // 目标死亡或不存在则脱战
                    creature.TargetPosition = null;
                }
            }

            // ━━━ 目标导向移动 (Foraging/Fleeing/Hunting) ━━━
            if (creature.TargetPosition.HasValue)
            {
                // 检查是否已到达目标
                float distanceToTarget = Vector2.Distance(creature.Position, creature.TargetPosition.Value);

                if (distanceToTarget <= TargetApproachThreshold)
                {
                    // 已到达,若是定点目标则清空 (锁定实体目标的不能清，否则黏上不咬了)
                    if (string.IsNullOrEmpty(creature.TargetCreatureUID))
                        creature.TargetPosition = null;

                    // 如果是觅食状态,到达后切换为闲逛
                    if (creature.CurrentBehavior == BehaviorState.Foraging)
                    {
                        creature.CurrentBehavior = BehaviorState.Idle;
                    }

                    return creature.Position;
                }

                // 朝目标移动一步
                return MoveTowardsTarget(creature, creature.TargetPosition.Value, envManager);
            }

            // ━━━ 默认随机漫游 (Idle) ━━━
            return DecideWanderTarget(creature, environment, envManager);
        }

        // ==========================================
        // 朝目标移动
        // ==========================================
        /// <summary>
        /// 朝目标位置移动一步 (选择最接近的可通行格子)
        /// </summary>
        private Vector2 MoveTowardsTarget(CreatureData creature, Vector2 target, EnvironmentManager envManager)
        {
            int currentX = Mathf.FloorToInt(creature.Position.x);
            int currentY = Mathf.FloorToInt(creature.Position.y);

            Vector2Int[] directions = new Vector2Int[]
            {
                new Vector2Int(0, 1),   // 上
                new Vector2Int(1, 0),   // 右
                new Vector2Int(0, -1),  // 下
                new Vector2Int(-1, 0),  // 左
                new Vector2Int(1, 1),   // 右上
                new Vector2Int(1, -1),  // 右下
                new Vector2Int(-1, -1), // 左下
                new Vector2Int(-1, 1)   // 左上
            };

            Vector2 bestPosition = creature.Position;
            float bestDistance = float.MaxValue;

            // 遍历所有方向,找到最接近目标的可通行格子
            foreach (var dir in directions)
            {
                int targetX = currentX + dir.x;
                int targetY = currentY + dir.y;

                if (!envManager.IsWalkable(targetX, targetY))
                    continue;

                Vector2 candidatePos = new Vector2(targetX + 0.5f, targetY + 0.5f);
                float distance = Vector2.Distance(candidatePos, target);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestPosition = candidatePos;
                }
            }

            return bestPosition;
        }

        // ==========================================
        // 漫游目标选择
        // ==========================================
        /// <summary>
        /// 简单漫游:随机选择相邻可通行格子
        /// </summary>
        private Vector2 DecideWanderTarget(CreatureData creature, EnvironmentData environment, EnvironmentManager envManager)
        {
            int currentX = Mathf.FloorToInt(creature.Position.x);
            int currentY = Mathf.FloorToInt(creature.Position.y);

            Vector2Int[] directions = new Vector2Int[]
            {
                new Vector2Int(0, 1),
                new Vector2Int(1, 0),
                new Vector2Int(0, -1),
                new Vector2Int(-1, 0),
                new Vector2Int(1, 1),
                new Vector2Int(1, -1),
                new Vector2Int(-1, -1),
                new Vector2Int(-1, 1)
            };

            ShuffleArray(directions);

            foreach (var dir in directions)
            {
                int targetX = currentX + dir.x;
                int targetY = currentY + dir.y;

                if (envManager.IsWalkable(targetX, targetY))
                {
                    return new Vector2(targetX + 0.5f, targetY + 0.5f);
                }
            }

            return creature.Position;
        }

        // ==========================================
        // 移动成本计算
        // ==========================================
        /// <summary>
        /// 计算移动到目标位置的能量消耗
        /// </summary>
        private float CalculateMoveCost(CreatureData creature, Vector2 targetPosition, EnvironmentData environment)
        {
            // 基础消耗
            float baseCost = Move_Energy_Base_Cost;

            // 获取目标格子的地形数据
            int targetX = Mathf.FloorToInt(targetPosition.x);
            int targetY = Mathf.FloorToInt(targetPosition.y);
            var tile = environment.GetTile(targetX, targetY);

            if (tile != null)
            {
                // 地形代价修正 (沼泽/雪地会增加消耗)
                baseCost *= tile.Movement_Cost;
            }

            // 体型修正 (大型生物消耗更多)
            baseCost *= creature.Size;

            // 速度修正 (速度快的生物消耗更多)
            baseCost *= (creature.Move_Speed / 5.0f); // 假设5.0是标准速度

            return baseCost;
        }

        // ==========================================
        // 辅助方法:数组随机打乱
        // ==========================================
        private void ShuffleArray<T>(T[] array)
        {
            for (int i = array.Length - 1; i > 0; i--)
            {
                int randomIndex = Random.Range(0, i + 1);
                T temp = array[i];
                array[i] = array[randomIndex];
                array[randomIndex] = temp;
            }
        }
    }
}