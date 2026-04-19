using UnityEngine;
using UnityEngine.EventSystems;
using EvolutionLaws.Data;
using EvolutionLaws.View;
using EvolutionLaws.Core;

namespace EvolutionLaws.UI
{
    /// <summary>
    /// 【选择管理器】
    /// 职责:处理鼠标点击,选中生物,通知UI系统
    /// 原则:单一职责,只负责输入检测和事件分发
    /// </summary>
    public class SelectionManager : MonoBehaviour
    {
        // ==========================================
        // 引用
        // ==========================================
        [Header("References")]
        [Tooltip("检查面板UI脚本")]
        public InspectorPanelUI InspectorPanel;

        [Tooltip("相机管理器 (用于跟随功能)")]
        public Core.CameraManager CameraManager;

        // ==========================================
        // 运行时状态
        // ==========================================
        private CreatureView _currentSelectedCreature;

        private Camera _mainCamera; // 缓存主相机

        // ==========================================
        // 可视化配置
        // ==========================================
        [Header("Visual Feedback")]
        [Tooltip("选中时的高亮颜色")]
        public Color SelectionHighlightColor = Color.yellow;

        [Tooltip("选中时的缩放倍数")]
        public float SelectionScaleMultiplier = 1.2f;

        private Vector3 _originalScale;
        private Color _originalColor;

        private void Start()
        {
            _mainCamera = Camera.main; // 在 Start 时缓存，避免每帧执行底层查找
        }

        // ==========================================
        // 输入检测
        // ==========================================
        private void Update()
        {
            Vector3 mousePos = Input.mousePosition;
            if (mousePos.x < 0 || mousePos.y < 0 || mousePos.x > Screen.width || mousePos.y > Screen.height)
            {
                return;
            }
            // 检测鼠标左键点击
            if (Input.GetMouseButtonDown(0))
            {
                // 1. 防穿透：如果点击落在了UI上，直接放弃本次点选
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    return;

                // 2. 状态互斥：如果正捏着"上帝操作技能"蓄势待发，不应误选生物
                if (SimulationManager.Instance != null &&
                    SimulationManager.Instance.GodPower != null &&
                    SimulationManager.Instance.GodPower.CurrentSkill != PlayerGodPowerSystem.SelectedSkill.None)
                {
                    return;
                }

                HandleClick();
            }

            // 快捷键:取消选择 (ESC)
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                DeselectCreature();
            }
            // 快捷键:相机跟随选中生物 (F)
            if (Input.GetKeyDown(KeyCode.F))
            {
                if (_currentSelectedCreature != null && CameraManager != null)
                {
                    CameraManager.Follow(_currentSelectedCreature.transform);
                    Debug.Log($"[SelectionManager] 相机开始跟随: {_currentSelectedCreature.GetData().SpeciesID}");
                }
                else if (_currentSelectedCreature == null)
                {
                    Debug.LogWarning("[SelectionManager] 没有选中的生物,无法跟随");
                }
                else if (CameraManager == null)
                {
                    Debug.LogError("[SelectionManager] CameraManager 引用未设置!");
                }
            }
        }

        // ==========================================
        // 点击处理
        // ==========================================
        private void HandleClick()
        {
            // 将鼠标屏幕坐标转换为世界坐标
            Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            // 执行2D物理射线检测
            RaycastHit2D hit = Physics2D.Raycast(mouseWorldPos, Vector2.zero);

            if (hit.collider != null)
            {
                // 尝试获取CreatureView组件
                CreatureView creatureView = hit.collider.GetComponent<CreatureView>();

                if (creatureView != null)
                {
                    SelectCreature(creatureView);
                }
                else
                {
                    // 点击到了其他物体,取消选择
                    DeselectCreature();
                }
            }
            else
            {
                // 点击到空白区域,取消选择
                DeselectCreature();
            }
        }

        // ==========================================
        // 选择生物
        // ==========================================
        private void SelectCreature(CreatureView creatureView)
        {
            // 如果已经选中同一个生物,不重复处理
            if (_currentSelectedCreature == creatureView) return;

            // 取消之前的选择
            DeselectCreature();

            // 记录新选择
            _currentSelectedCreature = creatureView;

            // 应用视觉高亮
            ApplySelectionVisuals(creatureView);

            // 通知Inspector面板更新
            if (InspectorPanel != null)
            {
                InspectorPanel.DisplayCreature(creatureView.GetData());
            }

            Debug.Log($"[SelectionManager] 选中生物: {creatureView.GetData().SpeciesID} ({creatureView.GetData().UID})");
        }

        // ==========================================
        // 取消选择
        // ==========================================
        private void DeselectCreature()
        {
            if (_currentSelectedCreature != null)
            {
                // 恢复视觉效果
                RemoveSelectionVisuals(_currentSelectedCreature);

                Debug.Log($"[SelectionManager] 取消选择: {_currentSelectedCreature.GetData().SpeciesID}");
                _currentSelectedCreature = null;
            }

            // 隐藏Inspector面板
            if (InspectorPanel != null)
            {
                InspectorPanel.Hide();
            }
        }

        // ==========================================
        // 视觉高亮效果
        // ==========================================
        private void ApplySelectionVisuals(CreatureView creatureView)
        {
            // 保存原始状态
            _originalScale = creatureView.transform.localScale;

            var renderer = creatureView.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                _originalColor = renderer.color;
                // 应用高亮颜色 (混合模式)
                renderer.color = Color.Lerp(_originalColor, SelectionHighlightColor, 0.5f);
            }

            // 放大
            creatureView.transform.localScale = _originalScale * SelectionScaleMultiplier;
        }

        private void RemoveSelectionVisuals(CreatureView creatureView)
        {
            if (creatureView == null) return;

            // 恢复原始缩放
            creatureView.transform.localScale = _originalScale;

            // 恢复原始颜色
            var renderer = creatureView.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.color = _originalColor;
            }
        }

        // ==========================================
        // 公共接口 (用于外部调用)
        // ==========================================
        public CreatureView GetCurrentSelection() => _currentSelectedCreature;
    }
}