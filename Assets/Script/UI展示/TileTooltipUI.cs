using UnityEngine;
using TMPro;
using EvolutionLaws.Core;
using EvolutionLaws.Data;
using TileData = EvolutionLaws.Data.TileData;
using UnityEngine.UI;

namespace EvolutionLaws.UI
{
    /// <summary>
    /// 【地块提示UI】
    /// 职责:跟随鼠标显示地块数据
    /// 原则:纯UI显示,不修改数据
    /// </summary>
    public class TileTooltipUI : MonoBehaviour
    {
        public static TileTooltipUI Instance { get; private set; }

        // ==========================================
        // 引用
        // ==========================================
        [Header("References")]
        [Tooltip("环境管理器 (用于查询地块数据)")]
        public EnvironmentManager EnvironmentManager;

        [Tooltip("面板的根对象 (用于显示/隐藏)")]
        public GameObject PanelRoot;

        // ==========================================
        // UI元素引用
        // ==========================================
        [Header("UI Elements")]
        [Tooltip("坐标文本 (例如: X: 45, Y: 32)")]
        public TextMeshProUGUI Coordinates_Text;

        [Tooltip("地形类型文本 (例如: 平地/水域/山脉)")]
        public TextMeshProUGUI TerrainType_Text;

        [Tooltip("移动代价文本")]
        public TextMeshProUGUI MovementCost_Text;

        [Tooltip("温度文本 (例如: 温度: 22.5°C)")]
        public TextMeshProUGUI Temperature_Text;

        [Tooltip("植物生物量文本")]
        public TextMeshProUGUI BiomassPlant_Text;

        [Tooltip("肉类生物量文本")]
        public TextMeshProUGUI BiomassMeat_Text;

        [Tooltip("矿物生物量文本")]
        public TextMeshProUGUI BiomassMineral_Text;

        [Tooltip("土壤肥力文本")]
        public TextMeshProUGUI SoilFertility_Text;

        [Tooltip("隐蔽度文本")]
        public TextMeshProUGUI StealthFactor_Text;

        // ==========================================
        // 配置参数
        // ==========================================
        [Header("Positioning Settings")]
        [Tooltip("UI相对鼠标的偏移 (像素)")]
        public Vector2 CursorOffset = new Vector2(20f, -20f);

        [Tooltip("是否限制在屏幕边界内")]
        public bool ClampToScreen = true;

        [Tooltip("距离屏幕边缘的最小距离 (像素)")]
        public float ScreenEdgePadding = 10f;

        [Header("Update Settings")]
        [Tooltip("刷新间隔 (秒), 0=每帧刷新")]
        public float UpdateInterval = 0.1f;

        // ==========================================
        // 调试选项
        // ==========================================
        [Header("Debug Settings")]
        [Tooltip("是否显示详细调试日志")]
        public bool ShowDebugLogs = true;

        private bool _isFeatureEnabled = true;

        // ==========================================
        // 运行时状态
        // ==========================================
        private Camera _mainCamera;

        private RectTransform _rectTransform;
        private Canvas _parentCanvas;
        private float _lastUpdateTime;
        private int _lastTileX = -1;
        private int _lastTileY = -1;

        // 【新增安全锁】跨场景加载时的安全保护期
        private float _safeStartTimer = 0.5f;

        // ==========================================
        // 初始化
        // ==========================================
        private void Awake()
        {
            Instance = this;
            _mainCamera = Camera.main;
            if (PanelRoot != null)
            {
                _rectTransform = PanelRoot.GetComponent<RectTransform>();
            }
            else
            {
                Debug.LogError("[TileTooltip]  PanelRoot 未赋值！无法获取 RectTransform。");
            }
            if (PanelRoot != null)
            {
                _parentCanvas = PanelRoot.GetComponentInParent<Canvas>();
                if (_parentCanvas == null)
                {
                    Debug.LogError("[TileTooltip]  未找到父 Canvas 组件！");
                }
            }
        }

        private void Start()
        {
            // 检查 EnvironmentManager
            if (EnvironmentManager == null)
            {
                Debug.LogError("[TileTooltip] ❌ EnvironmentManager 引用未设置!");
            }
            else
            {
                Debug.Log($"[TileTooltip] ✅ EnvironmentManager 已连接: {EnvironmentManager.name}");

                if (EnvironmentManager.EnvironmentData == null)
                {
                    Debug.LogError("[TileTooltip] ❌ EnvironmentData 为空!");
                }
                else
                {
                    Debug.Log($"[TileTooltip] ✅ 地图数据已加载: {EnvironmentManager.EnvironmentData.Width}x{EnvironmentManager.EnvironmentData.Height}");
                }
            }
        }

        // ==========================================
        // 每帧更新
        // ==========================================
        private void Update()
        {
            // 【核心修复】：场景刚加载的 0.2 秒内不执行任何 UI 激活和射线检测，避开 Unity 底层初始化漏洞
            if (_safeStartTimer > 0f)
            {
                _safeStartTimer -= Time.unscaledDeltaTime; // 使用不受暂停影响的时间
                return;
            }
            // ──────────────────────────────────
            // 开关切换
            // ──────────────────────────────────
            if (Input.GetKeyDown(KeyCode.T))
            {
                _isFeatureEnabled = !_isFeatureEnabled; // 切换开关状态

                if (!_isFeatureEnabled)
                {
                    // 如果刚刚关闭，立刻隐藏面板
                    HidePanel();
                    // (可选) 重置记录，确保下次开启时能立刻刷新
                    _lastTileX = -1;
                    _lastTileY = -1;
                }
            }

            // ──────────────────────────────────
            // 总闸拦截
            // ──────────────────────────────────
            // 如果功能被关闭，直接不执行后续任何逻辑（也不更新位置，也不检测格子）
            if (!_isFeatureEnabled) return;
            //防止在游戏窗口外时，鼠标位置异常导致面板乱飞或报错，所以先检查鼠标是否在屏幕范围内
            Vector3 mousePos = Input.mousePosition;
            if (mousePos.x < 0 || mousePos.y < 0 || mousePos.x > Screen.width || mousePos.y > Screen.height)
            {
                HidePanel(); // 鼠标移出屏幕外时自动隐藏面板
                _lastTileX = -1;
                _lastTileY = -1;
                return;
            }
            // 检查是否需要更新
            if (Time.time - _lastUpdateTime < UpdateInterval)
                return;

            _lastUpdateTime = Time.time;

            // ──────────────────────────────────
            // 1. 检测鼠标下的地块
            // ──────────────────────────────────
            bool tileFound = TryGetTileUnderMouse(out TileData tile, out int tileX, out int tileY);
            if (ShowDebugLogs)
            {
                // 每次检测都输出结果 (仅在首次检测到/失去时输出)
                if (tileFound && (tileX != _lastTileX || tileY != _lastTileY))
                {
                    Debug.Log($"[TileTooltip]  检测到地块: ({tileX}, {tileY})");
                }
                else if (!tileFound && _lastTileX != -1)
                {
                    Debug.Log("[TileTooltip]  鼠标移出地图");
                }
            }

            // ──────────────────────────────────
            // 2. 依次执行：显示 -> 改字 -> 强刷画布 -> 移动位置
            // ──────────────────────────────────
            if (tileFound)
            {
                // 先显示面板
                ShowPanel();

                // 只有格子变化时才刷新文本 (优化性能)
                if (tileX != _lastTileX || tileY != _lastTileY)
                {
                    UpdateDisplay(tile, tileX, tileY);
                    _lastTileX = tileX;
                    _lastTileY = tileY;
                }

                // 【关键修复】：在显示UI并修改文本后，强制通知 Unity 重构排版！
                // 防止稍后的 UpdatePosition() 获取 RectTransform 宽/高时触发底层报错。
                //Canvas.ForceUpdateCanvases();

                // 此时画布长宽已确认，可以安全地进行跟随和屏幕边缘约束运算
                UpdatePosition();
            }
            else
            {
                // 隐藏面板
                HidePanel();
                _lastTileX = -1;
                _lastTileY = -1;
            }
        }

        // ==========================================
        // UI 的物理位移需要在引擎其他系统处理完毕后进行
        // ==========================================
        private void LateUpdate()
        {
            // 只有当功能开启并且面板真正处于显示状态时，才跟随鼠标移动
            if (_isFeatureEnabled && PanelRoot != null && PanelRoot.activeInHierarchy)
            {
                UpdatePosition();
            }
        }

        // ==========================================
        // 位置更新
        // ==========================================
        private void UpdatePosition()
        {
            if (_rectTransform == null) return;

            // 获取鼠标位置并应用偏移
            Vector2 mousePos = Input.mousePosition;
            Vector2 targetPos = mousePos + CursorOffset;

            // 限制在屏幕边界内
            if (ClampToScreen)
            {
                float halfWidth = _rectTransform.rect.width / 2f;
                float halfHeight = _rectTransform.rect.height / 2f;

                targetPos.x = Mathf.Clamp(targetPos.x,
                    ScreenEdgePadding + halfWidth,
                    Screen.width - ScreenEdgePadding - halfWidth);

                targetPos.y = Mathf.Clamp(targetPos.y,
                    ScreenEdgePadding + halfHeight,
                    Screen.height - ScreenEdgePadding - halfHeight);
            }

            // 应用位置
            _rectTransform.position = targetPos;
        }

        // ==========================================
        // 检测鼠标下的地块
        // ==========================================
        private bool TryGetTileUnderMouse(out TileData tile, out int tileX, out int tileY)
        {
            tile = null;
            tileX = -1;
            tileY = -1;

            // 检查依赖
            if (EnvironmentManager == null || _mainCamera == null)
                return false;

            // 1. 屏幕坐标 -> 世界坐标
            Vector3 mouseWorldPos = _mainCamera.ScreenToWorldPoint(Input.mousePosition);
            Vector2 worldPos = new Vector2(mouseWorldPos.x, mouseWorldPos.y);

            // 2. 世界坐标 -> 格子坐标
            tileX = Mathf.FloorToInt(worldPos.x);
            tileY = Mathf.FloorToInt(worldPos.y);

            // 3. 获取地块数据
            tile = EnvironmentManager.EnvironmentData?.GetTile(tileX, tileY);
            //if (tile == null) Debug.Log($"未找到格子! 坐标: {tileX}, {tileY}");
            return tile != null;
        }

        // ==========================================
        // 更新UI显示
        // ==========================================
        private void UpdateDisplay(TileData tile, int tileX, int tileY)
        {
            // 坐标
            SetText(Coordinates_Text, $"坐标: ({tileX}, {tileY})");

            // 地形类型判断
            string terrainType = GetTerrainTypeName(tile.Movement_Cost, tile.Biomass_Mineral);
            SetText(TerrainType_Text, $"地形: {terrainType}");

            // 移动代价
            if (tile.Movement_Cost >= float.MaxValue)
            {
                SetText(MovementCost_Text, "移动代价: 不可通行");
            }
            else if (tile.Movement_Cost >= 100f)
            {
                SetText(MovementCost_Text, "移动代价: 极高 (水域)");
            }
            else
            {
                SetText(MovementCost_Text, $"移动代价: {tile.Movement_Cost:F1}");
            }

            // 温度 (全局温度 + 局部偏移)
            float globalTemp = EnvironmentManager.EnvironmentData?.Global_Temperature ?? 20f;
            float actualTemp = globalTemp + tile.Temperature_Offset;
            SetText(Temperature_Text, $"温度: {actualTemp:F1}°C ({GetTemperatureDescription(actualTemp)})");

            // 植物生物量
            if (tile.Biomass_Plant > 0f)
            {
                SetText(BiomassPlant_Text, $"植物: {tile.Biomass_Plant:F1} ({GetBiomassLevel(tile.Biomass_Plant)})");
                BiomassPlant_Text.color = Color.green;
            }
            else
            {
                SetText(BiomassPlant_Text, "植物: 无");
                BiomassPlant_Text.color = Color.gray;
            }

            // 肉类生物量
            if (tile.Biomass_Meat > 0f)
            {
                SetText(BiomassMeat_Text, $"肉类: {tile.Biomass_Meat:F1}");
                BiomassMeat_Text.color = new Color(1f, 0.5f, 0.5f); // 淡红色
            }
            else
            {
                SetText(BiomassMeat_Text, "肉类: 无");
                BiomassMeat_Text.color = Color.gray;
            }
            if (tile.Biomass_Mineral > 0f)
            {
                SetText(BiomassMineral_Text, $"矿物: {tile.Biomass_Mineral:F1} ({GetMineralLevel(tile.Biomass_Mineral)})");

                // 颜色:灰褐色 (岩石色)
                BiomassMineral_Text.color = new Color(0.6f, 0.5f, 0.4f);
            }
            else
            {
                SetText(BiomassMineral_Text, "矿物: 无");
                BiomassMineral_Text.color = Color.gray;
            }
            // 土壤肥力
            if (tile.Soil_Fertility > 0f)
            {
                SetText(SoilFertility_Text, $"肥力: {tile.Soil_Fertility:F2} ({GetFertilityLevel(tile.Soil_Fertility)})");
            }
            else
            {
                SetText(SoilFertility_Text, "肥力: 不可耕种");
            }

            // 隐蔽度
            SetText(StealthFactor_Text, $"隐蔽度: {tile.Stealth_Factor:P0}");
        }

        // ==========================================
        // 辅助方法:地形类型名称
        // ==========================================
        private string GetTerrainTypeName(float movementCost, float mineralAmount)
        {
            if (movementCost >= float.MaxValue)
                return "山脉/墙体";
            else if (movementCost >= 100f)
                return "水域";
            else if (movementCost >= 2.0f && mineralAmount > 10f)
                return "岩石地 (矿区)";
            else if (movementCost > 1.5f)
                return "高山地形";
            else if (movementCost > 1.0f)
                return "平地";
            else
                return "未知";
        }

        // ==========================================
        // 辅助方法:温度描述
        // ==========================================
        private string GetTemperatureDescription(float temp)
        {
            if (temp < 0) return "极寒";
            else if (temp < 10) return "寒冷";
            else if (temp < 20) return "凉爽";
            else if (temp < 30) return "温暖";
            else if (temp < 40) return "炎热";
            else return "酷热";
        }

        // ==========================================
        // 辅助方法:生物量等级
        // ==========================================
        private string GetBiomassLevel(float biomass)
        {
            if (biomass < 20f) return "稀疏";
            else if (biomass < 50f) return "中等";
            else if (biomass < 80f) return "丰富";
            else return "茂盛";
        }

        // ==========================================
        // 矿物等级描述
        // ==========================================
        private string GetMineralLevel(float mineral)
        {
            if (mineral < 5f) return "痕迹";
            else if (mineral < 20f) return "贫矿";
            else if (mineral < 60f) return "中等";
            else if (mineral < 100f) return "富矿";
            else return "特富矿";
        }

        // ==========================================
        // 辅助方法:肥力等级
        // ==========================================
        private string GetFertilityLevel(float fertility)
        {
            if (fertility < 0.7f) return "贫乏";
            else if (fertility < 1.0f) return "一般";
            else if (fertility < 1.3f) return "肥沃";
            else return "极肥沃";
        }

        // ==========================================
        // 显示/隐藏面板
        // ==========================================
        private void ShowPanel()
        {
            if (PanelRoot != null && !PanelRoot.activeSelf) // 只有当它是隐藏的时候，才去显示它
            {
                PanelRoot.SetActive(true);
                if (ShowDebugLogs) Debug.Log("[TileTooltip] -> 面板开启");
            }
        }

        private void HidePanel()
        {
            if (PanelRoot != null && PanelRoot.activeSelf) // 只有当它是显示的时候，才去隐藏它
            {
                PanelRoot.SetActive(false);
                if (ShowDebugLogs) Debug.Log("[TileTooltip] <- 面板关闭");
            }
        }

        // ==========================================
        // 辅助方法:安全设置文本
        // ==========================================
        private void SetText(TextMeshProUGUI textComponent, string value)
        {
            if (textComponent != null)
            {
                textComponent.text = value;
            }
        }

        // ==========================================
        // 公共接口:手动切换显示
        // ==========================================
        public void SetEnabled(bool enabled)
        {
            this.enabled = enabled;
            if (!enabled)
            {
                HidePanel();
            }
        }
    }
}