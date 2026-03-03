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

        public float Move_Energy_Base_Cost = 2.0f;     // 基础移动能量消耗
        public float Move_Structure_Wear = 0.1f;       // 移动结构磨损

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

                //使用仿真时间判断
                if (!ShouldMove(creature.UID))// 如果不应该移动,跳过
                    continue;

                Vector2 targetPosition = DecideWanderTarget(creature, environment, envManager);

                if (targetPosition == creature.Position)
                    continue;

                float moveCost = CalculateMoveCost(creature, targetPosition, environment);
                creature.Energy -= moveCost;
                // 移动导致结构磨损
                creature.Structure_Current -= Move_Structure_Wear;

                creature.Position = targetPosition;

                creature.IsMoving = true;
                // 记录仿真时间
                _lastMoveTime[creature.UID] = _simulationTime;

                Debug.Log($"[MovementSystem] {creature.SpeciesID} 移动到 {targetPosition} | 消耗能量: {moveCost:F1}");
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
        /// 简单漫游:随机选择相邻可通行格子
        /// </summary>
        // 未来可以增加更复杂的漫游行为 (如: 徘徊在兴趣点周围,或沿着特定路径漫游)
        private Vector2 DecideWanderTarget(CreatureData creature, EnvironmentData environment, EnvironmentManager envManager)
        {
            // 当前格子坐标
            int currentX = Mathf.FloorToInt(creature.Position.x);
            int currentY = Mathf.FloorToInt(creature.Position.y);

            // 8个相邻格子的偏移
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

            // 打乱顺序,增加随机性
            ShuffleArray(directions);

            // 遍历所有方向,找到第一个可通行的格子
            foreach (var dir in directions)
            {
                int targetX = currentX + dir.x;
                int targetY = currentY + dir.y;

                // 验证可通行性
                if (envManager.IsWalkable(targetX, targetY))
                {
                    // 返回格子中心点
                    return new Vector2(targetX + 0.5f, targetY + 0.5f);
                }
            }

            // 没有找到可通行格子,保持原地
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