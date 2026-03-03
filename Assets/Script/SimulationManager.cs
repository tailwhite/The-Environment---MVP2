using System.Collections.Generic;
using UnityEngine;
using EvolutionLaws.Data;
using EvolutionLaws.Config;
using EvolutionLaws.View;

namespace EvolutionLaws.Core
{
    /// <summary>
    /// 【物种生成配置】
    /// 用于在 Inspector 中配置每个物种的生成数量
    /// </summary>
    [System.Serializable]
    public class SpeciesSpawnConfig//物种生成配置，后续都是对这个对象进行操作
    {
        [Tooltip("物种蓝图")]
        public SpeciesBlueprint Blueprint;

        [Tooltip("生成数量")]
        [Range(0, 100)]
        public int SpawnCount = 2;

        [Tooltip("是否启用这个物种 (取消勾选可临时禁用)")]
        public bool Enabled = true;
    }

    /// <summary>
    /// 【核心仿真管理器】
    /// 职责：持有所有数据，驱动所有系统的Tick循环
    /// 原则：纯调度器，不含具体业务逻辑
    /// </summary>
    public class SimulationManager : MonoBehaviour
    {
        [Header("Spawning Config")]
        [Tooltip("多物种生成配置列表 (在 Inspector 中添加物种)")]
        public List<SpeciesSpawnConfig> SpeciesConfigs = new List<SpeciesSpawnConfig>();

        // ==========================================
        // 数据容器 (Source of Truth)
        // ==========================================
        //包含所有生物数据
        public List<CreatureData> AllCreatures = new List<CreatureData>();

        //环境数据设置
        public EnvironmentData Environment;

        // ==========================================
        // 管理器引用
        // ==========================================
        [Header("Manager References")]
        [Tooltip("环境管理器 (负责地图生成)")]
        public EnvironmentManager EnvironmentManager;

        // ==========================================
        // 视图资源引用
        // ==========================================
        [Header("Prefab References")]
        [Tooltip("通用生物模板预制体 (带 CreatureView 组件)")]
        public GameObject Creature_Template_Prefab;

        //将ID与生物UI关联起来
        private Dictionary<string, CreatureView> _creatureViews = new Dictionary<string, CreatureView>(); // UID -> View

        // ==========================================
        // 仿真参数
        // ==========================================
        [Header("Simulation Settings")]
        [Tooltip("仿真时间缩放 (1.0 = 实时, 2.0 = 2倍速)")]
        public float TimeScale = 1.0f;

        [Tooltip("固定时间步长 (秒)，建议 0.05 - 0.1")]
        public float FixedDeltaTime = 0.1f;

        private float _accumulator = 0f;

        //暂停相关
        private bool _isPaused = false;

        private float _previousTimeScale = 1.0f; // 记录暂停前的速度

        // ==========================================
        // 系统引用 (未来接入)
        // ==========================================
        private MetabolismSystem _metabolismSystem;// 新陈代谢系统

        private MovementSystem _movementSystem; // 生物移动系统
        private InteractionSystem _interactionSystem; //交互系统 (进食)

        // private PerceptionSystem _perceptionSystem;
        // private DecisionSystem _decisionSystem;
        // private EvolutionSystem _evolutionSystem;
        // private EnvironmentSystem _environmentSystem;

        // ==========================================
        // 初始化
        // ==========================================
        private void Start()
        {
            Debug.Log("[SimulationManager] ========== 开始初始化序列 ==========");

            // ──────────────────────────────────
            // 步骤 1: 检查 EnvironmentManager 引用
            // ──────────────────────────────────
            if (EnvironmentManager == null)
            {
                Debug.LogError("[SimulationManager] ❌ EnvironmentManager 引用未设置! 无法继续初始化");
                return;
            }

            // ──────────────────────────────────
            // 步骤 2: 手动初始化环境 (地图生成)
            // ──────────────────────────────────
            Debug.Log("[SimulationManager] 第 1 步: 初始化环境管理器...");
            EnvironmentManager.InitializeMapManual();

            // ──────────────────────────────────
            // 步骤 3: 验证地图是否初始化成功
            // ──────────────────────────────────
            if (!EnvironmentManager.IsInitialized)
            {
                Debug.LogError("[SimulationManager] ❌ 地图初始化失败! 无法生成生物");
                return;
            }

            // ──────────────────────────────────
            // 步骤 4: 获取地图数据引用
            // ──────────────────────────────────
            if (EnvironmentManager.EnvironmentData != null)
            {
                Environment = EnvironmentManager.EnvironmentData;
                Debug.Log($"[SimulationManager] ✅ 地图数据已获取: {Environment.MapName} ({Environment.Width}x{Environment.Height})");
            }
            else
            {
                // 后备方案:创建空地图
                Debug.LogWarning("[SimulationManager] ⚠️ 地图数据为空,使用后备方案");
                Environment = new EnvironmentData(100, 100);
                Environment.MapName = "Fallback_Empty_World";
            }

            // ──────────────────────────────────
            // 步骤 5: 初始化所有系统
            // ──────────────────────────────────
            Debug.Log("[SimulationManager] 第 2 步: 初始化仿真系统...");
            _movementSystem = new MovementSystem();
            _metabolismSystem = new MetabolismSystem();
            _interactionSystem = new InteractionSystem();
            Debug.Log("[SimulationManager] ✅ 系统初始化完成");

            // ──────────────────────────────────
            // 步骤 6: 生成初始种群 (只在地图准备好后)
            // ──────────────────────────────────
            Debug.Log("[SimulationManager] 第 3 步: 生成初始种群...");
            SpawnInitialPopulation();

            // ──────────────────────────────────
            // 步骤 7: 完成初始化
            // ──────────────────────────────────
            Debug.Log("[SimulationManager] ========== 初始化完成 ==========");
            Debug.Log($"[SimulationManager] 地图: {Environment.MapName} ({Environment.Width}x{Environment.Height})");
            Debug.Log($"[SimulationManager] 生物数: {AllCreatures.Count}");
        }

        // ==========================================
        // 初始种群生成方法
        // ==========================================
        /// <summary>
        /// 生成初始种群 (在地图初始化完成后调用)
        /// </summary>
        private void SpawnInitialPopulation()
        {
            // 检查配置列表是否为空
            if (SpeciesConfigs == null || SpeciesConfigs.Count == 0)
            {
                Debug.LogWarning("[SimulationManager]  SpeciesConfigs 列表为空,跳过生物生成");
                return;
            }

            int totalSpawned = 0;

            // 遍历所有物种配置
            foreach (var config in SpeciesConfigs)
            {
                // 跳过未启用的配置
                if (!config.Enabled)
                {
                    Debug.Log($"[SimulationManager]  跳过未启用的物种: {config.Blueprint?.SpeciesID ?? "未知"}");
                    continue;
                }

                // 检查蓝图是否有效
                if (config.Blueprint == null)
                {
                    Debug.LogWarning("[SimulationManager]  检测到空蓝图配置,跳过");
                    continue;
                }

                // 检查生成数量
                if (config.SpawnCount <= 0)
                {
                    Debug.Log($"[SimulationManager]  {config.Blueprint.SpeciesID} 生成数量为 0,跳过");
                    continue;
                }

                // 生成指定数量的生物
                Debug.Log($"[SimulationManager]  开始生成物种: {config.Blueprint.SpeciesID} × {config.SpawnCount}");

                for (int i = 0; i < config.SpawnCount; i++)
                {
                    Vector2 spawnPos = FindWalkablePosition();//找到一个可通行的位置来生成生物
                    CreatureView view = SpawnCreature(config.Blueprint, spawnPos);

                    if (view != null)
                    {
                        totalSpawned++;
                        Debug.Log($"[SimulationManager]    第 {i + 1}/{config.SpawnCount} 只 {config.Blueprint.SpeciesID} 已生成");
                    }
                    else
                    {
                        Debug.LogWarning($"[SimulationManager]    第 {i + 1}/{config.SpawnCount} 只生成失败");
                    }
                }
            }

            Debug.Log($"[SimulationManager]  种群生成完成 | 总计: {totalSpawned} 只生物");
        }

        // ==========================================
        // Unity 更新循环
        // ==========================================
        private void Update()
        {
            HandleTimeControlInput();
            // 仿真循环 (暂停时不更新)
            if (!_isPaused)
            {
                _accumulator += Time.deltaTime * TimeScale;

                while (_accumulator >= FixedDeltaTime)
                {
                    Tick(FixedDeltaTime);
                    _accumulator -= FixedDeltaTime;
                }
            }
        }

        // ==========================================
        // 时间控制输入处理
        // ==========================================
        private void HandleTimeControlInput()
        {
            // 空格键:暂停/继续
            if (Input.GetKeyDown(KeyCode.Space))
            {
                TogglePause();
            }

            // 数字键:设置速度 (仅在非暂停状态下)
            if (!_isPaused)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1))
                {
                    SetTimeScale(1.0f);
                }
                else if (Input.GetKeyDown(KeyCode.Alpha2))
                {
                    SetTimeScale(5.0f);
                }
                else if (Input.GetKeyDown(KeyCode.Alpha3))
                {
                    SetTimeScale(50.0f);
                }
            }
        }

        // ==========================================
        // 暂停/继续切换
        // ==========================================
        private void TogglePause()
        {
            _isPaused = !_isPaused;

            if (_isPaused)
            {
                _previousTimeScale = TimeScale;
                Time.timeScale = 0f; // 暂停 Unity 全局时间
                Debug.Log("[SimulationManager] 暂停");
            }
            else
            {
                TimeScale = _previousTimeScale;
                Time.timeScale = 1f; // 恢复 Unity 全局时间
                Debug.Log($"[SimulationManager] 继续 | 速度: {TimeScale}x");
            }
        }

        // ==========================================
        // 设置时间缩放
        // ==========================================
        private void SetTimeScale(float scale)
        {
            TimeScale = scale;
            Debug.Log($"[SimulationManager] 时间缩放设置为: {TimeScale}x");
        }

        // ==========================================
        // 公共属性
        // ==========================================
        public bool IsPaused => _isPaused;

        public float CurrentTimeScale => TimeScale;

        // ==========================================
        // 【核心仿真循环】
        // ==========================================
        /// <summary>
        /// 主Tick循环 - 所有系统按顺序执行
        /// </summary>
        private void Tick(float deltaTime)
        {
            // ────────────────────────────────────
            // 阶段 1: 环境系统更新
            // ────────────────────────────────────
            // TODO: 更新全局场参数 (昼夜、季节、气候)
            // TODO: 更新格子动态数据 (植物生长、气味扩散、尸体腐烂)
            // _environmentSystem.Tick(Environment, deltaTime);

            // ────────────────────────────────────
            // 阶段 2: 生物感知系统
            // ────────────────────────────────────
            // TODO: 遍历所有生物，更新视觉/嗅觉/听觉感知列表
            // _perceptionSystem.Tick(AllCreatures, Environment, deltaTime);

            // ────────────────────────────────────
            // 阶段 3: 生物决策系统 (AI)
            // ────────────────────────────────────
            // TODO: 根据需求(Hunger/Safety/Reproduction)和感知结果，更新行为状态
            // _decisionSystem.Tick(AllCreatures, deltaTime);

            // ────────────────────────────────────
            // 阶段 4: 行为执行系统
            // ────────────────────────────────────
            // TODO: 根据决策结果执行移动、攻击、进食等行为
            _movementSystem.Tick(AllCreatures, Environment, EnvironmentManager, deltaTime);
            // 阶段 4.5: 交互系统 (进食/采集)
            _interactionSystem.Tick(AllCreatures, Environment, deltaTime);
            // ────────────────────────────────────
            // 阶段 5: 新陈代谢系统
            // ────────────────────────────────────
            // TODO: 更新 Energy/Nutrients/Structure/Vitality
            // TODO: 应用环境压力 (温度/毒性)
            // TODO: 检测死亡条件
            _metabolismSystem.Tick(AllCreatures, Environment, deltaTime);

            // ────────────────────────────────────
            // 阶段 6: 演化系统 (表观遗传)
            // ────────────────────────────────────
            // TODO: 累积潜力进度 (Potential)
            // TODO: 检测词缀激活条件
            // TODO: 处理繁殖和基因传递
            // _evolutionSystem.Tick(AllCreatures, deltaTime);

            // ────────────────────────────────────
            // 阶段 7: 清理阶段
            // ────────────────────────────────────
            // TODO: 移除死亡生物 (IsDead = true)
            // TODO: 生成尸体资源 (Biomass_Meat)
            CleanupDeadCreatures();
        }

        // ==========================================
        // 辅助方法 (桩实现)
        // ==========================================
        /// <summary>
        /// 清理死亡生物并转化为环境资源
        /// </summary>
        private void CleanupDeadCreatures()
        {
            var deadList = AllCreatures.FindAll(c => c.IsDead);
            foreach (var dead in deadList)
            {
                // 转化为环境资源
                var tile = Environment.GetTile((int)dead.Position.x, (int)dead.Position.y);
                if (tile != null)
                {
                    tile.Biomass_Meat += dead.Mass; // ✅ 尸体转化为肉类
                }

                // 销毁视图
                if (_creatureViews.TryGetValue(dead.UID, out var view))
                {
                    if (view != null)
                        Destroy(view.gameObject);
                    _creatureViews.Remove(dead.UID);
                }
            }
            // TODO: 将死亡生物的 Mass 转化为对应格子的 Biomass_Meat
            AllCreatures.RemoveAll(c => c.IsDead);
        }

        private Vector2 FindWalkablePosition()// 在地图上找到一个可通行的位置
        {
            if (EnvironmentManager == null)
                return new Vector2(Random.Range(0, Environment.Width), Random.Range(0, Environment.Height));

            // 尝试最多100次找到可通行位置
            for (int i = 0; i < 100; i++)
            {
                int x = Random.Range(0, Environment.Width);
                int y = Random.Range(0, Environment.Height);

                if (EnvironmentManager.IsWalkable(x, y))
                {
                    return new Vector2(x + 0.5f, y + 0.5f); // 格子中心
                }
            }

            Debug.LogWarning("[SimulationManager] 未找到可通行位置,使用随机坐标");
            return new Vector2(Environment.Width / 2f, Environment.Height / 2f);
        }

        // ==========================================
        // 公共接口
        // ==========================================
        /// <summary>
        /// 【核心生成方法】从蓝图生成生物
        /// </summary>
        public CreatureView SpawnCreature(SpeciesBlueprint blueprint, Vector2 position)
        {
            //从接收位置生成一个以该蓝图为模板的生物
            // 1. 从蓝图创建数据
            var data = blueprint.CreateCreatureData(position);

            // 2. 添加到数据列表
            AllCreatures.Add(data);

            // 3. 实例化视图预制体
            if (Creature_Template_Prefab == null)
            {
                Debug.LogError("[SimulationManager] Creature_Template_Prefab 未设置!");
                return null;
            }

            var viewObject = Instantiate(Creature_Template_Prefab, new Vector3(position.x, position.y, 0), Quaternion.identity);
            viewObject.name = $"{blueprint.SpeciesID}_{data.UID.Substring(0, 8)}";

            // 4. 初始化 CreatureView
            var view = viewObject.GetComponent<CreatureView>();
            if (view == null)
            {
                Debug.LogError("[SimulationManager] 预制体缺少 CreatureView 组件!");
                Destroy(viewObject);
                return null;
            }

            view.Initialize(data, blueprint.DefaultSprite);

            // 5. 注册到字典
            _creatureViews[data.UID] = view;

            Debug.Log($"[SimulationManager] 生成生物 | 物种: {blueprint.SpeciesID} | 位置: {position}");
            return view;
        }

        /// <summary>
        /// 通过UID查找生物
        /// </summary>
        public CreatureData GetCreatureByUID(string uid)
        {
            return AllCreatures.Find(c => c.UID == uid);
        }

        // ==========================================
        // 运行时动态生成接口
        // ==========================================
        /// <summary>
        /// 【运行时接口】在运行中动态生成指定数量的生物
        /// </summary>
        /// <param name="blueprint">物种蓝图</param>
        /// <param name="count">生成数量</param>
        public void SpawnSpeciesAtRuntime(SpeciesBlueprint blueprint, int count)
        {
            if (blueprint == null)
            {
                Debug.LogError("[SimulationManager] 蓝图为空,无法生成!");
                return;
            }

            Debug.Log($"[SimulationManager] 运行时生成: {blueprint.SpeciesID} × {count}");

            for (int i = 0; i < count; i++)
            {
                Vector2 pos = FindWalkablePosition();
                SpawnCreature(blueprint, pos);
            }
        }
    }
}