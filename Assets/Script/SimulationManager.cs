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

        // 👇 全局仿真计时器
        public float GlobalTime { get; private set; } = 0f;

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

        private PerceptionSystem _perceptionSystem;// 感知系统 (视觉/嗅觉/听觉)
        private DecisionSystem _decisionSystem;// 决策系统 (AI行为状态)
        private CombatSystem _combatSystem;// 战斗系统 (攻击/防御)
        private ReproductionSystem _reproductionSystem;// 繁殖系统 (基因混合/后代生成)

        // 环境动态系统 (暴露给面板调节参数)
        [Header("Environment Dynamics System")]
        public EnvironmentDynamicsSystem _environmentSystem = new EnvironmentDynamicsSystem();

        //数据分析系统 (用于收集统计数据，未来可扩展为独立模块)
        private DataAnalyticsSystem _analyticsSystem;

        //蓝图字典 (用于繁殖系统快速查找)
        private Dictionary<string, SpeciesBlueprint> _blueprintMap = new Dictionary<string, SpeciesBlueprint>();

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
            _perceptionSystem = new PerceptionSystem();
            _decisionSystem = new DecisionSystem();
            _combatSystem = new CombatSystem();
            _reproductionSystem = new ReproductionSystem();
            //_environmentSystem = new EnvironmentDynamicsSystem();由unity编辑器中调用

            //数据分析系统 (用于收集统计数据，未来可扩展为独立模块)
            _analyticsSystem = new DataAnalyticsSystem(); // 👈 新增实例
            _analyticsSystem.Initialize();

            BuildBlueprintMap();// 构建蓝图映射表，供繁殖系统使用

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

        // 构建蓝图映射表
        /// <summary>
        /// 从 SpeciesConfigs 构建 SpeciesID -> Blueprint 的映射表
        /// </summary>
        private void BuildBlueprintMap()
        {
            _blueprintMap.Clear();

            foreach (var config in SpeciesConfigs)
            {
                if (config.Blueprint != null && !string.IsNullOrEmpty(config.Blueprint.SpeciesID))
                {
                    _blueprintMap[config.Blueprint.SpeciesID] = config.Blueprint;
                }
            }

            Debug.Log($"[SimulationManager] 蓝图映射表已构建 | 共 {_blueprintMap.Count} 个物种");
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
            // F10键:导出当前配置 (仅在非暂停状态下)
            if (Input.GetKeyDown(KeyCode.F10))
            {
                if (_analyticsSystem != null)
                {
                    // 注意：这里的 _environmentSystem 根据你之前的代码可能是大写的 EnvironmentDynamics，请根据实际变量名填写
                    _analyticsSystem.ExportConfigurationJSON(EnvironmentManager, _environmentSystem, SpeciesConfigs);
                }
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
            GlobalTime += deltaTime;

            // ───────────────────────────────
            // 阶段 1: 环境系统更新
            // ────────────────────────────────────
            // TODO: 更新全局场参数 (昼夜、季节、气候)
            // TODO: 更新格子动态数据 (植物生长、气味扩散、尸体腐烂)
            _environmentSystem.Tick(Environment, deltaTime);

            // ────────────────────────────────────
            // 阶段 2: 生物感知系统
            // ────────────────────────────────────
            // TODO: 遍历所有生物，更新视觉/嗅觉/听觉感知列表
            _perceptionSystem.Tick(AllCreatures, Environment, deltaTime);

            // ────────────────────────────────────
            // 阶段 3: 生物决策系统 (AI)
            // ────────────────────────────────────
            // TODO: 根据需求(Hunger/Safety/Reproduction)和感知结果，更新行为状态
            _decisionSystem.Tick(AllCreatures, deltaTime);

            // ────────────────────────────────────
            // 阶段 4: 行为执行系统
            // ────────────────────────────────────
            // TODO: 根据决策结果执行移动、攻击、进食等行为
            _movementSystem.Tick(AllCreatures, Environment, EnvironmentManager, deltaTime);
            //阶段 4.4: 战斗系统 (攻击/防御)
            _combatSystem.Tick(AllCreatures, deltaTime);
            // 阶段 4.5: 交互系统 (进食/采集)
            _interactionSystem.Tick(AllCreatures, Environment, deltaTime);
            // ────────────────────────────────────
            // 阶段 5: 新陈代谢系统
            // ────────────────────────────────────
            // TODO: 更新 Energy/Nutrients/Structure/Vitality
            // TODO: 应用环境压力 (温度/毒性)
            // TODO: 检测死亡条件
            _metabolismSystem.Tick(AllCreatures, Environment, deltaTime);

            // 阶段 5.5: 繁殖系统 (配偶寻找/基因混合/后代生成)
            _reproductionSystem.Tick(AllCreatures, _blueprintMap, deltaTime);
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
            ProcessNewOffspring();
            CleanupDeadCreatures();

            //数据分析系统 (收集统计数据，未来可扩展为独立模块)
            _analyticsSystem.Tick(AllCreatures, Environment, GlobalTime);

            if (GlobalTime % 10 < deltaTime) // 每10秒打印一次
            {
                int totalHerbivores = 0;
                int starvingCount = 0;
                int foragingCount = 0;

                foreach (var c in AllCreatures)
                {
                    if (c.SpeciesID == "食草虫")
                    {
                        totalHerbivores++;
                        if (c.Nutrients < c.Nutrients_Max * 0.2f) starvingCount++;
                        if (c.CurrentBehavior == BehaviorState.Foraging) foragingCount++;
                    }
                }

                //Debug.Log($"[体检报告 - 时间 {GlobalTime:F0}] 物种只数: {totalHerbivores} | 濒临饿死占比: {(float)starvingCount / totalHerbivores:P1} | 正在找饭占比: {(float)foragingCount / totalHerbivores:P1}");
            }
        }

        private void OnApplicationQuit()
        {
            if (_analyticsSystem != null)
            {
                _analyticsSystem.ExportToFile();
            }
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
                    // 检查生物的主要食性，判定身体成分
                    float mineralEfficiency = EvolutionLaws.Utilities.MetabolismUtility.GetDietEfficiency(dead, ResourceType.Mineral);

                    if (mineralEfficiency > 0.5f)
                    {
                        // 硅基/食矿生物死亡，主要爆出矿石，附带极少量的肉
                        tile.Biomass_Mineral += dead.Mass * 40f;// 矿石资源等于生物质量的40倍
                        tile.Biomass_Meat += dead.Mass * 10f;//肉资源等于生物质量的10倍
                    }
                    else
                    {
                        // 碳基生物死亡，全部转化为蛋白质 (肉)
                        tile.Biomass_Meat += dead.Mass * 50f;//肉资源等于生物质量的50倍
                    }
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

        // 处理后代生成
        /// <summary>
        /// 处理繁殖系统产生的后代 (统一创建视图)
        /// </summary>
        private void ProcessNewOffspring()
        {
            var offspringList = _reproductionSystem.GetPendingOffspring();

            if (offspringList == null || offspringList.Count == 0)
                return;

            foreach (var offspring in offspringList)
            {
                // 1. 添加到数据列表
                AllCreatures.Add(offspring);

                // 2. 获取蓝图
                if (!_blueprintMap.TryGetValue(offspring.SpeciesID, out var blueprint))
                {
                    Debug.LogError($"[SimulationManager] ❌ 找不到物种蓝图: {offspring.SpeciesID}");
                    continue;
                }

                // 3. 创建视图 (统一流程)
                CreateCreatureView(offspring, blueprint);

                Debug.Log($"[SimulationManager] 🎉 后代诞生 | 物种: {blueprint.SpeciesID} | 代数: G{offspring.Generation} | 位置: {offspring.Position}");
            }
        }

        // 创建生物视图 (解耦视图创建逻辑)
        /// <summary>
        /// 【视图创建】为生物数据创建对应的视图对象
        /// </summary>
        private void CreateCreatureView(CreatureData data, SpeciesBlueprint blueprint)
        {
            if (Creature_Template_Prefab == null)
            {
                Debug.LogError("[SimulationManager] Creature_Template_Prefab 未设置!");
                return;
            }

            // 实例化预制体
            var viewObject = Instantiate(
                Creature_Template_Prefab,
                new Vector3(data.Position.x, data.Position.y, 0),
                Quaternion.identity
            );

            // 设置名称
            viewObject.name = data.Generation > 0
                ? $"{blueprint.SpeciesID}_G{data.Generation}_{data.UID.Substring(0, 8)}"
                : $"{blueprint.SpeciesID}_{data.UID.Substring(0, 8)}";

            // 初始化视图
            var view = viewObject.GetComponent<CreatureView>();
            if (view == null)
            {
                Debug.LogError("[SimulationManager] 预制体缺少 CreatureView 组件!");
                Destroy(viewObject);
                return;
            }

            view.Initialize(data, blueprint.DefaultSprite);

            // 注册到字典
            _creatureViews[data.UID] = view;
        }

        // ==========================================
        // 公共接口
        // ==========================================
        /// <summary>
        /// 【核心生成方法】从蓝图生成生物
        /// </summary>
        public CreatureView SpawnCreature(SpeciesBlueprint blueprint, Vector2 position)
        {
            // 1. 从蓝图创建数据
            var data = blueprint.CreateCreatureData(position, GlobalTime);

            // 2. 添加到数据列表
            AllCreatures.Add(data);

            // 3. 创建视图 (统一流程)
            CreateCreatureView(data, blueprint);

            // 4. 返回视图
            if (_creatureViews.TryGetValue(data.UID, out var view))
            {
                Debug.Log($"[SimulationManager] 生成生物 | 物种: {blueprint.SpeciesID} | 位置: {position}");
                return view;
            }

            return null;
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