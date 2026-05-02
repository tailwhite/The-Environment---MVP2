using EvolutionLaws.Meta;
using EvolutionLaws.Config;
using EvolutionLaws.Data;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace EvolutionLaws.UI
{
    [System.Serializable]
    public class SpeciesDeploySlot
    {
        public SpeciesBlueprint Blueprint;
        public TextMeshProUGUI NameText;
        public TextMeshProUGUI CountText;
        public Button AddBtn;
        public Button SubBtn;
        [HideInInspector] public int CurrentCount = 0;
    }

    /// <summary>
    /// 【局外大厅 UI 管理器】
    /// 职责: 读取局外存档数据、显示资源、处理科技升级、场景跳转
    /// </summary>
    public class MetaUIManager : MonoBehaviour
    {
        [Header("UI 文本引用")]
        [Tooltip("显示当前研究点数")]
        public TextMeshProUGUI ResearchPointsText;

        [Tooltip("显示当前起步生物总数和等级")]
        public TextMeshProUGUI StartPopulationText;

        [Tooltip("显示升级种群规模的当前成本")]
        public TextMeshProUGUI UpgradeCostText;

        [Header("下拉菜单 (Dropdown) 引用")]
        [Tooltip("选择变异潜能的下拉菜单")]
        public TMP_Dropdown PotentialsDropdown;

        [Tooltip("选择先祖印记的下拉菜单")]
        public TMP_Dropdown MarksDropdown;

        [Header("配置")]
        public int UpgradeCost = 50;

        // 物种配置面板列表
        [Header("出战物种分配")]
        public List<SpeciesDeploySlot> DeploySlots = new List<SpeciesDeploySlot>();

        private void Start()
        {
            // 0. 重构：直接调用初始化即可，它会自动去加载 MainMetaDatabase SO
            MetaConfigManager.Initialize();

            // 1. 进入大厅第一件事：读取最新存档（比如刚死出来，需要刷新点数）
            MetaDataManager.Load();

            if (PotentialsDropdown != null)
                PotentialsDropdown.onValueChanged.AddListener(OnPotentialSelectionChanged);

            if (MarksDropdown != null)
                MarksDropdown.onValueChanged.AddListener(OnMarkSelectionChanged);

            // 初始化分配槽位的显示
            InitDeploySlots();
            // 2. 刷新界面显示
            RefreshUI();
        }

        // ==========================================
        // 初始化分配按钮事件
        // ==========================================
        private void InitDeploySlots()
        {
            var data = MetaDataManager.Current;
            if (data.DeployedSpecies == null) data.DeployedSpecies = new Dictionary<string, int>();

            for (int i = 0; i < DeploySlots.Count; i++)
            {
                var slot = DeploySlots[i];
                if (slot.Blueprint == null) continue;

                // 显示名字
                if (slot.NameText != null) slot.NameText.text = slot.Blueprint.DisplayName;

                // 还原存档数量
                if (data.DeployedSpecies.TryGetValue(slot.Blueprint.SpeciesID, out int savedCount))
                {
                    slot.CurrentCount = savedCount;
                }
                else
                {
                    slot.CurrentCount = 0; // 默认 0
                }

                // 绑定点击事件，必须缓存 i 给闭包使用
                int index = i;
                if (slot.AddBtn != null)
                {
                    slot.AddBtn.onClick.RemoveAllListeners();
                    slot.AddBtn.onClick.AddListener(() => ChangeSpeciesCount(index, 10));
                }
                if (slot.SubBtn != null)
                {
                    slot.SubBtn.onClick.RemoveAllListeners();
                    slot.SubBtn.onClick.AddListener(() => ChangeSpeciesCount(index, -10));
                }
            }
        }

        // ==========================================
        // 处理加减计算
        // ==========================================
        private void ChangeSpeciesCount(int slotIndex, int delta)
        {
            var slot = DeploySlots[slotIndex];// 先计算总数，看看加减后会不会超过上限
            int maxCapacity = MetaDataManager.Current.GetStartingPopulation();
            int currentTotal = GetTotalAssignedPopulation();

            // 越界保护
            if (delta > 0 && currentTotal + delta > maxCapacity)
            {
                Debug.LogWarning("[MetaUI] 超过了最大开局种群体积上限！");
                return;
            }
            if (delta < 0 && slot.CurrentCount + delta < 0) return;

            slot.CurrentCount += delta;

            // 实时保存进 MetaData
            var data = MetaDataManager.Current;
            data.DeployedSpecies[slot.Blueprint.SpeciesID] = slot.CurrentCount;
            MetaDataManager.Save();

            RefreshUI();
        }

        private int GetTotalAssignedPopulation()
        {
            int total = 0;
            foreach (var slot in DeploySlots) total += slot.CurrentCount;
            return total;
        }

        // ==========================================
        // 核心UI刷新
        // ==========================================
        private void RefreshUI()
        {
            if (MetaDataManager.Current == null) return;

            // 显示点数和开局数量
            if (ResearchPointsText != null)
                ResearchPointsText.text = $"研究点数: {MetaDataManager.Current.ResearchPoints}";

            //刷新开局人口文本显示：已分配 / 最大容量
            if (StartPopulationText != null)
            {
                int totalAssigned = GetTotalAssignedPopulation();
                int maxCapacity = MetaDataManager.Current.GetStartingPopulation();
                StartPopulationText.text = $"起步生物: {totalAssigned} / {maxCapacity}只 (Lv.{MetaDataManager.Current.MaxPopulationLevel})";
            }
            // 显示升级种群规模的当前成本
            if (UpgradeCostText != null)
                UpgradeCostText.text = $"升级种群规模 (当前成本: {UpgradeCost} RP)";
            // 刷新数字与按钮可用状态
            int totalForUI = GetTotalAssignedPopulation();
            int maxCapForUI = MetaDataManager.Current.GetStartingPopulation();
            foreach (var slot in DeploySlots)
            {
                if (slot.CountText != null) slot.CountText.text = slot.CurrentCount.ToString();
                if (slot.AddBtn != null) slot.AddBtn.interactable = (totalForUI < maxCapForUI);
                if (slot.SubBtn != null) slot.SubBtn.interactable = (slot.CurrentCount > 0);
            }
            // 2. 刷新装配下拉菜单选项
            RefreshDropdowns();
        }

        // ==========================================
        // 初始化下拉菜单的选项
        // ==========================================
        private void RefreshDropdowns()
        {
            var data = MetaDataManager.Current;

            // --- 刷新潜能下拉菜单 ---
            if (PotentialsDropdown != null && data.UnlockedPotentials != null)
            {
                PotentialsDropdown.ClearOptions();
                List<string> potentialOptions = new List<string>();
                potentialOptions.Add("无装备"); // 强行加一个“无装备”的保底选项
                int selectedIndex = 0; // 记录应该选中哪一项
                for (int i = 0; i < data.UnlockedPotentials.Count; i++)
                {
                    string pID = data.UnlockedPotentials[i];
                    string displayName = MetaConfigManager.GetPotentialName(pID);
                    potentialOptions.Add(displayName);

                    if (data.EquippedPotentials.Contains(pID))
                    {
                        selectedIndex = i + 1;
                    }
                }
                PotentialsDropdown.AddOptions(potentialOptions);
                PotentialsDropdown.SetValueWithoutNotify(selectedIndex); // 设置当前显示的值，不触发事件
            }

            // --- 刷新先祖印记下拉菜单 ---
            if (MarksDropdown != null)
            {
                MarksDropdown.ClearOptions();
                List<string> markOptions = new List<string>();

                // 给印记强行加一个“无装备”的保底选项，因为开局可能啥也没有
                markOptions.Add("无装备");
                int selectedIndex = 0;

                for (int i = 0; i < data.UnlockedMarks.Count; i++)
                {
                    string mID = data.UnlockedMarks[i];
                    string displayName = MetaConfigManager.GetMarkName(mID);
                    markOptions.Add(displayName);

                    if (data.EquippedMarks.Contains(mID))
                    {
                        selectedIndex = i + 1; // +1 因为第0个是“None”
                    }
                }
                MarksDropdown.AddOptions(markOptions);
                MarksDropdown.SetValueWithoutNotify(selectedIndex);
            }
        }

        // ==========================================
        // 玩家改变下拉菜单选项时的回调功能
        // ==========================================
        private void OnPotentialSelectionChanged(int index)
        {
            var data = MetaDataManager.Current;
            //string selectedID = data.UnlockedPotentials[index];

            data.EquippedPotentials.Clear(); // 清空旧的（只允许单选）

            if (index > 0)
            {
                string selectedID = data.UnlockedPotentials[index - 1]; // -1 因为第0个是“None”
                data.EquippedPotentials.Add(selectedID);
                Debug.Log($"[MetaUI] 潜能已切换装备为: {selectedID}");
            }
            else
            {
                Debug.Log("[MetaUI] 卸下了所有变异潜能。");
            }
            MetaDataManager.Save();
        }

        private void OnMarkSelectionChanged(int index)
        {
            var data = MetaDataManager.Current;
            data.EquippedMarks.Clear();

            // index == 0 代表选了“无装备”(None)
            if (index > 0)
            {
                string selectedID = data.UnlockedMarks[index - 1];
                data.EquippedMarks.Add(selectedID);
                Debug.Log($"[MetaUI] 印记已切换装备为: {selectedID}");
            }
            else
            {
                Debug.Log("[MetaUI] 卸下了所有先祖印记。");
            }

            MetaDataManager.Save();
        }

        // ==========================================
        // 按钮点击事件 (供 Unity Inspector 绑定)
        // ==========================================

        /// <summary>
        /// 【进入游戏】绑定给 "开启新纪元" 按钮
        /// </summary>
        public void OnStartGameClicked()
        {
            int totalAssigned = GetTotalAssignedPopulation();
            if (totalAssigned <= 0)
            {
                Debug.LogWarning("[MetaUI] 没有任何出战生物！请先分配物种数量！");
                // 如果你有飘字系统，可以在这里提示玩家
                return;
            }
            Debug.Log("[MetaUI] 开启新纪元，潜能已装载，跳转至 SimulationScene...");

            // 确保出战配置已存入本地
            MetaDataManager.Save();

            // 加载局内场景
            SceneManager.LoadScene("SimulationScene");
        }

        /// <summary>
        /// 【局外养成】绑定给 "升级种群规模" 按钮
        /// </summary>
        public void OnUpgradePopulationClicked()
        {
            if (MetaDataManager.Current.ResearchPoints >= UpgradeCost)
            {
                MetaDataManager.Current.ResearchPoints -= UpgradeCost;
                MetaDataManager.Current.MaxPopulationLevel++;
                MetaDataManager.Save();
                RefreshUI();
            }
            else
            {
                Debug.LogWarning("[MetaUI] 研究点数不足！");
            }
        }
    }
}