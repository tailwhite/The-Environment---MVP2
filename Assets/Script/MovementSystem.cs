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
        public float Wander_Interval = 2.0f;           // 漫游重新索敌决策间隔(秒)

        public float TargetApproachThreshold = 1.0f;   // 与目标之间保持的距离

        [Header("Energy Cost")]
        public float Move_Energy_Base_Cost = 0.2f;     // 基础移动能量消耗 (每移动1米)

        public float Move_Structure_Wear = 0.1f;       // 移动结构磨损 (每移动1米)

        [Header("A* Pathfinding")]
        public float Path_Recalculate_Interval = 1.5f; // 猎物移动时重新寻路的间隔

        // ==========================================
        // 使用仿真累计时间
        // ==========================================
        private float _simulationTime = 0f; // 仿真累计时间

        private Dictionary<string, float> _lastMoveTime = new Dictionary<string, float>(); // UID -> 上次移动的仿真时间

        // A* 导航缓存
        private Dictionary<string, List<Vector2>> _activePaths = new Dictionary<string, List<Vector2>>();

        private Dictionary<string, float> _lastPathCalcTime = new Dictionary<string, float>();

        // ==========================================
        // 核心Tick方法
        // ==========================================
        /// <summary>
        /// 对所有生物执行移动逻辑
        /// </summary>
        public void Tick(List<CreatureData> creatures, EnvironmentData environment, EnvironmentManager envManager, float deltaTime)
        {
            _simulationTime += deltaTime;

            foreach (var creature in creatures)
            {
                creature.IsMoving = false;

                // 1. 基础状态检查
                if (!CanMove(creature))
                    continue;

                // 2. 刷新并获取目标位置
                Vector2? targetPos = GetOrUpdateTargetPosition(creature, environment, envManager, creatures);
                if (!targetPos.HasValue)
                    continue;

                // 3. 检查是否已经抵达目标
                if (CheckAndHandleTargetArrival(creature, targetPos.Value))
                    continue;

                // 4. 获取当前速度倍率 (例如：狩猎/逃跑时爆发)
                float speedMultiplier = GetSpeedMultiplier(creature.CurrentBehavior);

                // 5. 【新增 A* 导航】获取下一个要经过的转折路点，而不是直接指向终点卡在河边
                Vector2 nextWaypoint = GetNextWaypointViaAStar(creature, targetPos.Value, environment);

                // 6. 计算带“沿墙滑动”保护的下一步位置 (微操防跌跤)
                Vector2 nextPos = CalculateNextPositionWithSliding(creature, nextWaypoint, envManager, deltaTime, speedMultiplier);

                // 计算本帧实际产生的位移
                float actualMoveDist = Vector2.Distance(creature.Position, nextPos);
                if (actualMoveDist <= 0.001f)
                    continue;

                // 7. 结算本次移动的账单 (体力不够则强制休息)
                if (!ApplyMovementCost(creature, nextPos, environment, actualMoveDist, speedMultiplier))
                {
                    creature.CurrentBehavior = BehaviorState.Resting;
                    continue;
                }

                // 8. 更新坐标和状态
                creature.Position = nextPos;
                creature.IsMoving = true;
            }
        }

        // ==========================================
        // 子逻辑抽取段
        // ==========================================

        /// <summary>
        /// 检查生物有没有体力/意识支撑行动
        /// </summary>
        private bool CanMove(CreatureData creature)
        {
            if (creature.IsDead || creature.IsUnconscious) return false;
            if (creature.Energy <= 0f) return false;
            if (creature.CurrentBehavior == BehaviorState.Resting) return false;
            return true;
        }

        /// <summary>
        /// 判定是否抵近目标，如果抵达则重置目标点
        /// </summary>
        private bool CheckAndHandleTargetArrival(CreatureData creature, Vector2 targetPos)
        {
            float distanceToTarget = Vector2.Distance(creature.Position, targetPos);
            if (distanceToTarget <= TargetApproachThreshold)
            {
                // 若是寻找死物(矿食/漫游)，到了就清空，等待决策系统派发新任务
                if (string.IsNullOrEmpty(creature.TargetCreatureUID))
                {
                    creature.TargetPosition = null;

                    if (creature.CurrentBehavior == BehaviorState.Idle)
                    {
                        // 漫游到了终点，强制要求重置CD去寻找下一个点
                        _lastMoveTime[creature.UID] = _simulationTime;
                    }
                }
                return true; // 报告已到达，终止本帧后续移动运算
            }
            return false;
        }

        /// <summary>
        /// 获取移动爆发加成
        /// </summary>
        private float GetSpeedMultiplier(BehaviorState state)
        {
            // 生存本能带来的额外爆发速度：狩猎和逃跑跑得更快
            if (state == BehaviorState.Hunting || state == BehaviorState.Fleeing)
            {
                return 1.5f;
            }
            return 1.0f;
        }

        // ==========================================
        // A* 寻路调度器
        // ==========================================
        private Vector2 GetNextWaypointViaAStar(CreatureData creature, Vector2 finalTarget, EnvironmentData envData)
        {
            bool needsNewPath = false;

            // 情景 1: 没路，或者路太老过期了
            if (!_activePaths.ContainsKey(creature.UID) ||
               (_simulationTime - _lastPathCalcTime.GetValueOrDefault(creature.UID, 0f) > Path_Recalculate_Interval))
            {
                needsNewPath = true;
            }
            else
            {
                // 情景 2: 猎物移动距离过远(>2格子)，导致原航线的终点作废，需重新导航
                var path = _activePaths[creature.UID];
                if (path.Count > 0 && Vector2.Distance(path[path.Count - 1], finalTarget) > 2.0f)
                {
                    needsNewPath = true;
                }
            }

            if (needsNewPath)
            {
                // 呼叫底层的 A* 引擎进行烧脑运算
                var newPath = SimpleAStar.FindPath(creature.Position, finalTarget, envData);
                if (newPath != null && newPath.Count > 0)
                {
                    _activePaths[creature.UID] = newPath;
                    _lastPathCalcTime[creature.UID] = _simulationTime;
                }
                else
                {
                    // 彻底没有路 (被墙定死或者是孤岛)，降级为走直线尽力靠近
                    return finalTarget;
                }
            }

            // 路点提取与队列管理
            if (_activePaths.TryGetValue(creature.UID, out var currentPath) && currentPath.Count > 0)
            {
                // 如果极其靠近当前的第一个途径点，弹出节点，前往下一站
                if (Vector2.Distance(creature.Position, currentPath[0]) < 0.4f)
                {
                    currentPath.RemoveAt(0);
                }

                if (currentPath.Count > 0)
                {
                    return currentPath[0]; // 返回前方导航节点
                }
            }

            return finalTarget; // 路径耗尽，直达终点
        }

        /// <summary>
        /// 计算滑墙防卡死位移
        /// </summary>
        private Vector2 CalculateNextPositionWithSliding(CreatureData creature, Vector2 targetPos, EnvironmentManager envManager, float deltaTime, float speedMultiplier)
        {
            Vector2 dir = (targetPos - creature.Position).normalized;
            float distanceToTarget = Vector2.Distance(creature.Position, targetPos);

            // 步进距离 = 速度 × 倍率 × 帧用时
            float stepDist = creature.Move_Speed * speedMultiplier * deltaTime;
            stepDist = Mathf.Min(stepDist, distanceToTarget); // 防抖，不要越过目标

            Vector2 nextPos = creature.Position + dir * stepDist;

            // 遇到路点精度导致的微小碰撞时，尝试侧行贴过去
            if (!envManager.IsWalkable(Mathf.FloorToInt(nextPos.x), Mathf.FloorToInt(nextPos.y)))
            {
                bool canMoveX = envManager.IsWalkable(Mathf.FloorToInt(nextPos.x), Mathf.FloorToInt(creature.Position.y));
                bool canMoveY = envManager.IsWalkable(Mathf.FloorToInt(creature.Position.x), Mathf.FloorToInt(nextPos.y));

                if (canMoveX) nextPos = new Vector2(nextPos.x, creature.Position.y);
                else if (canMoveY) nextPos = new Vector2(creature.Position.x, nextPos.y);
                else return creature.Position; // 死角，待在原地
            }

            return nextPos;
        }

        /// <summary>
        /// 计算并扣除本帧移动的能量与肉体磨损，支持冲刺带来的剧烈消耗
        /// </summary>
        private bool ApplyMovementCost(CreatureData creature, Vector2 targetPosition, EnvironmentData environment, float actualMoveDist, float speedMultiplier)
        {
            float baseCost = Move_Energy_Base_Cost;

            // 获取目标格子的地形数据评估难度
            int targetX = Mathf.FloorToInt(targetPosition.x);
            int targetY = Mathf.FloorToInt(targetPosition.y);
            var tile = environment.GetTile(targetX, targetY);

            if (tile != null)
            {
                baseCost *= tile.Movement_Cost; // 沼泽等困难地形惩罚
            }

            // 基础倍率修正: 体型与最终速度
            baseCost *= Mathf.Sqrt(creature.Size);

            // 【重要】速度越快，单位里程耗能越高！(引入爆发惩罚)
            float finalSpeed = creature.Move_Speed * speedMultiplier;
            baseCost *= Mathf.Sqrt(finalSpeed / 5.0f);

            // 最终体力消耗 = 单位里程造价 × 实际跑了几米
            float moveCost = baseCost * actualMoveDist;

            if (creature.Energy < moveCost)
            {
                return false; // 体力透支，支付失败
            }

            // 执行扣款和记账
            creature.Energy -= moveCost;
            creature.Lifetime_EnergySpent_Move += moveCost;
            creature.Structure_Current -= Move_Structure_Wear * actualMoveDist;

            return true;
        }

        // ==========================================
        // 目标决策与追踪修正
        // ==========================================
        private Vector2? GetOrUpdateTargetPosition(CreatureData creature, EnvironmentData environment, EnvironmentManager envManager, List<CreatureData> creatures)
        {
            // ━━━ 实时解决"瞄准残影"：热更新猎物/敌人的当前坐标 ━━━
            if (!string.IsNullOrEmpty(creature.TargetCreatureUID))
            {
                var targetCreature = creatures.Find(c => c.UID == creature.TargetCreatureUID);
                if (targetCreature != null && !targetCreature.IsDead)
                {
                    creature.TargetPosition = targetCreature.Position; // 覆写为目标的当下一帧坐标
                }
                else
                {
                    // 猎物已被吃掉/逃脱
                    creature.TargetCreatureUID = null;
                    creature.TargetPosition = null;
                }
            }

            // ━━━ 漫游逻辑发呆期处理 ━━━
            if (creature.CurrentBehavior == BehaviorState.Idle)
            {
                // 如果没有设定点，或者逛太久触发了重思CD
                if (!creature.TargetPosition.HasValue || ShouldWander(creature.UID))
                {
                    creature.TargetPosition = DecideWanderTarget(creature, environment, envManager);
                }
            }

            return creature.TargetPosition;
        }

        private bool ShouldWander(string uid)
        {
            if (!_lastMoveTime.ContainsKey(uid))
            {
                _lastMoveTime[uid] = _simulationTime;
                return true;
            }
            float timeSinceLastMove = _simulationTime - _lastMoveTime[uid];
            if (timeSinceLastMove >= Wander_Interval)
            {
                _lastMoveTime[uid] = _simulationTime;
                return true; // CD 好了，换个方向
            }
            return false;
        }

        // ==========================================
        // 漫游目标选择
        // ==========================================
        private Vector2 DecideWanderTarget(CreatureData creature, EnvironmentData environment, EnvironmentManager envManager)
        {
            int currentX = Mathf.FloorToInt(creature.Position.x);
            int currentY = Mathf.FloorToInt(creature.Position.y);

            // 让它看远一点 (往外探索 1~4 格)，而不是永远只能摸一格
            int maxTries = 5;
            for (int i = 0; i < maxTries; i++)
            {
                int targetX = currentX + Random.Range(-4, 5);
                int targetY = currentY + Random.Range(-4, 5);

                if (envManager.IsWalkable(targetX, targetY))
                {
                    return new Vector2(targetX + 0.5f, targetY + 0.5f);
                }
            }

            // 周围走不通就呆在原地
            return creature.Position;
        }

        // ==========================================
        // 移动成本计算
        // ==========================================
        private float CalculateMoveCost(CreatureData creature, Vector2 targetPosition, EnvironmentData environment)
        {
            float baseCost = Move_Energy_Base_Cost;

            int targetX = Mathf.FloorToInt(targetPosition.x);
            int targetY = Mathf.FloorToInt(targetPosition.y);
            var tile = environment.GetTile(targetX, targetY);

            if (tile != null)
            {
                baseCost *= tile.Movement_Cost; // 沼泽地形耗体力
            }

            baseCost *= creature.Size;
            baseCost *= (creature.Move_Speed / 5.0f);

            return baseCost;
        }

        // ==========================================
        // 核心技术：微型 A* (A-Star) 启发式寻路算法引擎
        // ==========================================
        private static class SimpleAStar
        {
            private class Node
            {
                public int X, Y;
                public float GCost, HCost;
                public float FCost => GCost + HCost;
                public Node Parent;
            }

            public static List<Vector2> FindPath(Vector2 startPos, Vector2 targetPos, EnvironmentData envData)
            {
                int startX = Mathf.FloorToInt(startPos.x); int startY = Mathf.FloorToInt(startPos.y);
                int targetX = Mathf.FloorToInt(targetPos.x); int targetY = Mathf.FloorToInt(targetPos.y);

                // 边界与简单合法性校验
                if (startX < 0 || startX >= envData.Width || startY < 0 || startY >= envData.Height) return null;
                if (targetX < 0 || targetX >= envData.Width || targetY < 0 || targetY >= envData.Height) return null;

                List<Node> openList = new List<Node>();
                HashSet<int> closedSet = new HashSet<int>();
                Dictionary<int, Node> nodeDict = new Dictionary<int, Node>();

                int GetIndex(int x, int y) => y * envData.Width + x;

                Node startNode = new Node { X = startX, Y = startY, GCost = 0, HCost = GetHeuristicDist(startX, startY, targetX, targetY) };
                openList.Add(startNode);
                nodeDict.Add(GetIndex(startX, startY), startNode);

                int iterations = 0;
                int maxIterations = 1500; // 防止复杂迷宫卡顿死循环

                while (openList.Count > 0 && iterations < maxIterations)
                {
                    iterations++;

                    // 获取期望代价最低的格子
                    Node current = openList[0];
                    int currentIndex = 0;
                    for (int i = 1; i < openList.Count; i++)
                    {
                        if (openList[i].FCost < current.FCost || (openList[i].FCost == current.FCost && openList[i].HCost < current.HCost))
                        {
                            current = openList[i];
                            currentIndex = i;
                        }
                    }

                    openList.RemoveAt(currentIndex);
                    closedSet.Add(GetIndex(current.X, current.Y));

                    // 如果抵达
                    if (current.X == targetX && current.Y == targetY)
                    {
                        return RetracePath(current);
                    }

                    // 开始探寻周边的八个方向
                    for (int x = -1; x <= 1; x++)
                    {
                        for (int y = -1; y <= 1; y++)
                        {
                            if (x == 0 && y == 0) continue;

                            int neighborX = current.X + x;
                            int neighborY = current.Y + y;

                            if (neighborX < 0 || neighborX >= envData.Width || neighborY < 0 || neighborY >= envData.Height) continue;

                            int neighborIndex = GetIndex(neighborX, neighborY);
                            if (closedSet.Contains(neighborIndex)) continue; // 已探寻不再走

                            var tile = envData.GetTile(neighborX, neighborY);
                            if (tile.Movement_Cost >= 10f) continue; // 核心：绝对不可通行（水、山区墙壁）跳过

                            // 代价核算（斜向走是根号2）
                            float distCost = (x != 0 && y != 0) ? 1.414f : 1.0f;
                            float moveCost = current.GCost + distCost * tile.Movement_Cost;

                            if (!nodeDict.TryGetValue(neighborIndex, out Node neighborNode))
                            {
                                neighborNode = new Node { X = neighborX, Y = neighborY };
                                nodeDict.Add(neighborIndex, neighborNode);
                            }

                            if (moveCost < neighborNode.GCost || !openList.Contains(neighborNode))
                            {
                                neighborNode.GCost = moveCost;
                                neighborNode.HCost = GetHeuristicDist(neighborX, neighborY, targetX, targetY);
                                neighborNode.Parent = current;

                                if (!openList.Contains(neighborNode))
                                    openList.Add(neighborNode);
                            }
                        }
                    }
                }
                return null; // 被水域彻底包死孤岛，无路可达
            }

            // 返回格式化的导航节点列
            private static List<Vector2> RetracePath(Node endNode)
            {
                List<Vector2> path = new List<Vector2>();
                Node current = endNode;
                while (current != null)
                {
                    // 加 0.5f 为了让寻路点正好落在于格子中心以免擦边撞墙
                    path.Add(new Vector2(current.X + 0.5f, current.Y + 0.5f));
                    current = current.Parent;
                }
                path.Reverse(); // 翻转，让离自己最近的路点放列表开头
                return path;
            }

            private static float GetHeuristicDist(int x1, int y1, int x2, int y2)
            {
                int dstX = Mathf.Abs(x1 - x2);
                int dstY = Mathf.Abs(y1 - y2);
                if (dstX > dstY) return 1.414f * dstY + 1.0f * (dstX - dstY);
                return 1.414f * dstX + 1.0f * (dstY - dstX);
            }
        }
    }
}