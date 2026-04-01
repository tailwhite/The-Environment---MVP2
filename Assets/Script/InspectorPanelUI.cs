using UnityEngine;
using TMPro;
using EvolutionLaws.Data;

namespace EvolutionLaws.UI
{
    /// <summary>
    /// 【检查面板UI】
    /// 职责:显示选中生物的详细数据
    /// 原则:纯UI显示,不修改数据
    /// </summary>
    public class InspectorPanelUI : MonoBehaviour
    {
        // ==========================================
        // UI元素引用 (在Inspector中拖入)
        // ==========================================
        [Header("Basic Info")]
        public TextMeshProUGUI UID_Text;

        public TextMeshProUGUI Species_Text;

        [Tooltip("用于UI显示的漂亮名字 (如: 森林狼)")]
        public TextMeshProUGUI DisplayName;

        public TextMeshProUGUI Generation_Text;
        public TextMeshProUGUI LifeStage_Text;

        [Header("Physiology")]
        public TextMeshProUGUI Structure_Text;

        public TextMeshProUGUI Vitality_Text;

        [Header("Metabolism")]
        public TextMeshProUGUI Energy_Text;// "能量"

        public TextMeshProUGUI Nutrients_Text;
        public TextMeshProUGUI MetabolicRate_Text;

        [Header("Brain State")]
        public TextMeshProUGUI Hunger_Text;// "饥饿度"

        public TextMeshProUGUI Safety_Text;
        public TextMeshProUGUI Stress_Text;
        public TextMeshProUGUI BrainMode_Text; // "理性/本能/昏迷"

        [Header("Genetics")]
        public TextMeshProUGUI Affixes_Text; // 显示所有激活的词缀

        public TextMeshProUGUI Complexity_Text;

        [Header("Panel Control")]
        public GameObject PanelRoot; // 面板的根对象,用于显示/隐藏

        // ==========================================
        // 运行时状态
        // ==========================================
        private CreatureData _currentData;

        // ==========================================
        // 初始化
        // ==========================================
        private void Start()
        {
            Hide(); // 默认隐藏
        }

        // ==========================================
        // 更新显示 (持续刷新数据)
        // ==========================================
        private void Update()
        {
            if (_currentData != null && PanelRoot.activeSelf)
            {
                RefreshDisplay();
            }
        }

        // ==========================================
        // 公共接口:显示生物数据
        // ==========================================
        public void DisplayCreature(CreatureData data)
        {
            _currentData = data;

            if (PanelRoot != null)
                PanelRoot.SetActive(true);

            RefreshDisplay();
        }

        public void Hide()
        {
            _currentData = null;

            if (PanelRoot != null)
                PanelRoot.SetActive(false);
        }

        // ==========================================
        // 刷新UI显示
        // ==========================================
        private void RefreshDisplay()
        {
            if (_currentData == null) return;
            // 安全截取 UID (防止 UID 为空或不足 8 位时崩溃)
            string displayUID = string.IsNullOrEmpty(_currentData.UID) ? "Unknown" :
                (_currentData.UID.Length > 8 ? _currentData.UID.Substring(0, 8) : _currentData.UID);

            // --- 基础信息 ---
            SetText(UID_Text, $"UID: {_currentData.UID.Substring(0, 8)}..."); // 只显示前8位
            SetText(Species_Text, $"物种: {_currentData.SpeciesID}");
            SetText(Generation_Text, $"代数: G{_currentData.Generation}");
            SetText(LifeStage_Text, $"阶段: {_currentData.Stage}");
            SetText(DisplayName, $"{_currentData.DisplayName}");

            // --- 生理状态 ---
            SetText(Structure_Text, $"结构: {_currentData.Structure_Current:F1} / {_currentData.Structure_Max:F1}");
            SetText(Vitality_Text, $"活力: {_currentData.Vitality_Current:F1} / {_currentData.Vitality_Max:F1}");

            // --- 代谢状态 ---
            SetText(Energy_Text, $"能量: {_currentData.Energy:F1} / {_currentData.Energy_Max:F1}");
            SetText(Nutrients_Text, $"营养: {_currentData.Nutrients:F1} / {_currentData.Nutrients_Max:F1}");
            SetText(MetabolicRate_Text, $"代谢率: {_currentData.Current_Metabolic_Burn:F2}/s");

            // --- 大脑状态 ---
            SetText(Hunger_Text, $"饥饿度: {_currentData.Need_Hunger:F1}");
            SetText(Safety_Text, $"安全感: {_currentData.Need_Safety:F1}");
            SetText(Stress_Text, $"压力值: {_currentData.Stress_Current:F1}");

            // 判断大脑模式
            string brainMode = "正常";
            if (_currentData.IsUnconscious)
                brainMode = "<color=yellow>昏迷</color>";
            else if (_currentData.Stress_Current > _currentData.Stress_Panic_Threshold)
                brainMode = "<color=red>恐慌本能</color>";
            else if (_currentData.Stress_Current < _currentData.Stress_Recover_Threshold)
                brainMode = "<color=green>理性模式</color>";

            SetText(BrainMode_Text, $"状态: {brainMode}");

            // --- 遗传信息 ---
            if (_currentData.ActiveAffixes.Count > 0)
            {
                string affixList = string.Join(", ", _currentData.ActiveAffixes);
                SetText(Affixes_Text, $"激活词缀:\n{affixList}");
            }
            else
            {
                SetText(Affixes_Text, "激活词缀: 无");
            }

            SetText(Complexity_Text, $"遗传复杂度: {_currentData.Genetic_Complexity:F2}");
        }

        // ==========================================
        // 辅助方法:安全设置文本
        // ==========================================
        private void SetText(TextMeshProUGUI textComponent, string value)
        {
            if (textComponent != null)
                textComponent.text = value;
        }
    }
}