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

        // === 顶部新增配额设置 ===
        [Header("A* Pathfinding")]
        public float Path_Recalculate_Interval = 1.5f; // 猎物移动时重新寻路的间隔

        // 抗卡顿配额，单帧最多只允许算出 3 条复杂寻路
        public int Max_AStar_Calculations_Per_Frame = 3;

        private int _astarCalculationsThisFrame = 0;

        // 降频排序机制
        public float Sort_Interval = 0.3f; // 每 0.3 秒才重新计算一次优先级

        private float _lastSortTime = -999f;

        // ==========================================
        // 使用仿真累计时间
        // ==========================================
        private float _simulationTime = 0f; // 仿真累计时间

        private Dictionary<string, float> _lastMoveTime = new Dictionary<string, float>(); // UID -> 上次移动的仿真时间

        // A* 导航缓存
        private Dictionary<string, List<Vector2>> _activePaths = new Dictionary<string, List<Vector2>>();

        private Dictionary<string, float> _lastPathCalcTime = new Dictionary<string, float>();

        // 排序缓存 (对象池思想)，避免每 0.3 秒都 new 新的列表/装箱
        private List<KeyValuePair<CreatureData, float>> _sortBuffer = new List<KeyValuePair<CreatureData, float>>();

        // ==========================================
        // 核心Tick方法
        // ==========================================
        /// <summary>
        /// 对所有生物执行移动逻辑
        /// </summary>
        public void Tick(List<CreatureData> creatures, EnvironmentData environment, EnvironmentManager envManager, float deltaTime)
        {
            _simulationTime += deltaTime;

            // 每帧一开始先重置算力配额
            _astarCalculationsThisFrame = 0;

            // 【核心修改 2：降频排序】每 0.3 秒才排一次序，大幅节省 CPU 开销
            if (_simulationTime - _lastSortTime >= Sort_Interval)
            {
                _lastSortTime = _simulationTime;
                // 【性能优化】缓存分数后再排序，避免 O(N log N) 的重复运算浪费
                _sortBuffer.Clear();
                for (int i = 0; i < creatures.Count; i++)
                {
                    _sortBuffer.Add(new KeyValuePair<CreatureData, float>(creatures[i], GetUrgencyScore(creatures[i])));
                }

                _sortBuffer.Sort((a, b) => b.Value.CompareTo(a.Value)); // 分数从大到小降序

                for (int i = 0; i < creatures.Count; i++)
                {
                    creatures[i] = _sortBuffer[i].Key;
                }
            }
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
                // 这个方法在下面被改造了，会使用上面的配额了
                Vector2 nextWaypoint = GetNextWaypointViaAStar(creature, targetPos.Value, environment);

                // 6. 计算带“沿墙滑动”保护的下一步位置 (微操防跌跤)
                Vector2 nextPos = CalculateNextPositionWithSliding(creature, nextWaypoint, envManager, deltaTime, speedMultiplier);

                // 计算本帧实际产生的位移
                float actualMoveDist = Vector2.Distance(creature.Position, nextPos);
                if (actualMoveDist <= 0.001f) continue;

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
        // 行为算力调度机制
        // ==========================================
        /// <summary>
        /// 【核心修改 1：动态紧急度】基于生物自身状态（能量、压力、距离）计算算力获取的顺位
        /// </summary>
        private float GetUrgencyScore(CreatureData creature)
        {
            // 提取关键百分比 (0~100)
            float energyPercent = (creature.Energy / Mathf.Max(1f, creature.Energy_Max)) * 100f;
            float hungerPercent = creature.Need_Hunger; // 饥饿度本身就是 0~100

            switch (creature.CurrentBehavior)
            {
                case BehaviorState.Fleeing:
                    // 逃命最急！分数基础 150，被追的越紧（压力值越高），分数越高
                    return 150f + creature.Stress_Current;

                case BehaviorState.Hunting:
                    float distToPrey = 10f;
                    if (creature.TargetPosition.HasValue)
                    {
                        distToPrey = Vector2.Distance(creature.Position, creature.TargetPosition.Value);
                    }
                    // 捕猎：基础 100，越饿越急，距离越近越急着抢算力做微操 A* 扑咬！
                    return 100f + hungerPercent - distToPrey;

                case BehaviorState.Foraging:
                    // 觅食：基础 50，快饿死了就急于寻路
                    return 50f + hungerPercent;

                case BehaviorState.Socializing:
                    return 30f; // 找对象的顺位往后排排

                case BehaviorState.Idle:
                    // 闲逛：就是瞎溜达，距离远近无所谓
                    return 10f;

                case BehaviorState.Resting:
                    return 0f; // 睡觉不需要算力

                default:
                    return 0f;
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
            // 1. 首先判断是否需要重新计算路径：没有路径，或者路径过旧，或者目标点已经偏移了
            if (!_activePaths.ContainsKey(creature.UID) ||
               (_simulationTime - _lastPathCalcTime.GetValueOrDefault(creature.UID, 0f) > Path_Recalculate_Interval))
            {
                needsNewPath = true;
            }
            else
            {
                var path = _activePaths[creature.UID];
                if (path.Count > 0 && Vector2.Distance(path[path.Count - 1], finalTarget) > 2.0f)
                {
                    needsNewPath = true;
                }
            }
            // 2. 如果需要新路径，先检查配额再计算
            if (needsNewPath)
            {
                // 【Time-Slicing 拦截网】：CPU 保护机制
                if (_astarCalculationsThisFrame >= Max_AStar_Calculations_Per_Frame)
                {
                    // 本帧算力穷尽，暂时朝着直线撞墙走一下以作妥协缓冲，下帧它依旧会被标记需要演算！
                    return finalTarget;
                }

                _astarCalculationsThisFrame++; // 开销+1
                // 执行 A* 寻路
                var newPath = SimpleAStar.FindPath(creature.Position, finalTarget, envData);
                if (newPath != null && newPath.Count > 0)
                {
                    _activePaths[creature.UID] = newPath;
                    _lastPathCalcTime[creature.UID] = _simulationTime;
                }
                else
                {
                    return finalTarget;
                }
            }

            // 路点提取与队列管理 (原样保持不变)
            if (_activePaths.TryGetValue(creature.UID, out var currentPath) && currentPath.Count > 0)
            {
                if (Vector2.Distance(creature.Position, currentPath[0]) < 0.4f)
                {
                    currentPath.RemoveAt(0);
                }

                if (currentPath.Count > 0)
                {
                    return currentPath[0];
                }
            }

            return finalTarget;
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
                    //主动清空它的 A* 缓存路径，防止它依然顺着记忆中的错误旧路点奔跑导致鬼畜
                    _activePaths.Remove(creature.UID);
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

                // 重置数据的方法，供对象池重复使用
                public void Init(int x, int y, float gCost = 0, float hCost = 0, Node parent = null)
                {
                    this.X = x; this.Y = y;
                    this.GCost = gCost; this.HCost = hCost;
                    this.Parent = parent;
                }
            }

            // 【性能优化】：静态容器与对象池，彻底消除单次寻路的 GC 内存消耗！
            private static List<Node> _openList = new List<Node>(1000);

            private static HashSet<int> _closedSet = new HashSet<int>(1000);
            private static Dictionary<int, Node> _nodeDict = new Dictionary<int, Node>(1000);
            private static Queue<Node> _nodePool = new Queue<Node>(2000);

            private static Node GetNodeFromPool(int x, int y, float gCost = 0, float hCost = 0, Node parent = null)
            {
                Node node = _nodePool.Count > 0 ? _nodePool.Dequeue() : new Node();
                node.Init(x, y, gCost, hCost, parent);
                return node;
            }

            public static List<Vector2> FindPath(Vector2 startPos, Vector2 targetPos, EnvironmentData envData)
            {//获取生物的当前位置和目标位置的格子坐标，返回从起点到目标的路径点列表（如果不可达则返回 null）
                int startX = Mathf.FloorToInt(startPos.x); int startY = Mathf.FloorToInt(startPos.y);
                int targetX = Mathf.FloorToInt(targetPos.x); int targetY = Mathf.FloorToInt(targetPos.y);

                // 边界与简单合法性校验
                if (startX < 0 || startX >= envData.Width || startY < 0 || startY >= envData.Height) return null;
                if (targetX < 0 || targetX >= envData.Width || targetY < 0 || targetY >= envData.Height) return null;

                // 重置容器而不是 new 新的
                _openList.Clear();
                _closedSet.Clear();
                _nodeDict.Clear();

                int GetIndex(int x, int y) => y * envData.Width + x;

                Node startNode = GetNodeFromPool(startX, startY, 0, GetHeuristicDist(startX, startY, targetX, targetY));
                _openList.Add(startNode);
                _nodeDict.Add(GetIndex(startX, startY), startNode);

                int iterations = 0;
                int maxIterations = 1500; // 防止复杂迷宫卡顿死循环

                List<Vector2> resultPath = null; // 默认为Null

                while (_openList.Count > 0 && iterations < maxIterations)
                {
                    iterations++;

                    // 获取期望代价最低的格子
                    Node current = _openList[0];
                    int currentIndex = 0;
                    for (int i = 1; i < _openList.Count; i++)
                    {
                        if (_openList[i].FCost < current.FCost || (_openList[i].FCost == current.FCost && _openList[i].HCost < current.HCost))
                        {
                            current = _openList[i];
                            currentIndex = i;
                        }
                    }

                    _openList.RemoveAt(currentIndex);
                    _closedSet.Add(GetIndex(current.X, current.Y));

                    // 如果抵达
                    if (current.X == targetX && current.Y == targetY)
                    {
                        resultPath = RetracePath(current);
                        break; // 找到了不要直接 Return，要先执行最后的回收！
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
                            if (_closedSet.Contains(neighborIndex)) continue; // 已探寻不再走

                            var tile = envData.GetTile(neighborX, neighborY);
                            if (tile.Movement_Cost >= 10f) continue; // 核心：绝对不可通行（水、山区墙壁）跳过

                            // 代价核算（斜向走是根号2）
                            float distCost = (x != 0 && y != 0) ? 1.414f : 1.0f;
                            float moveCost = current.GCost + distCost * tile.Movement_Cost;

                            if (!_nodeDict.TryGetValue(neighborIndex, out Node neighborNode))
                            {
                                neighborNode = GetNodeFromPool(neighborX, neighborY);
                                _nodeDict.Add(neighborIndex, neighborNode);
                            }

                            if (moveCost < neighborNode.GCost || !_openList.Contains(neighborNode))// 发现更优路径或者之前没见过这个格子
                            {
                                neighborNode.GCost = moveCost;
                                neighborNode.HCost = GetHeuristicDist(neighborX, neighborY, targetX, targetY);
                                neighborNode.Parent = current;

                                if (!_openList.Contains(neighborNode))
                                    _openList.Add(neighborNode);
                            }
                        }
                    }
                }

                // 【回收逻辑】：将这趟寻路里创建的所有 Node 放回对象池重复利用
                foreach (var node in _nodeDict.Values)
                {
                    _nodePool.Enqueue(node);
                }

                return resultPath;
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