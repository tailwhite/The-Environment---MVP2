using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using EvolutionLaws.Data;
using EvolutionLaws.Meta;

namespace EvolutionLaws.Core
{
    public class ExtinctionManager
    {
        private bool _isSettling = false;
        private HashSet<string> _discoveredAffixesThisRun = new HashSet<string>();

        // 设定单局最大时间 (例如天灾结束是 1000 秒，或者你需要的任何时长)
        public float MaxSimulationTime = 2000f;

        // 在死亡瞬间被 SimulationManager 调用
        public void RecordDeath(CreatureData dead)
        {
            // 如果死者身上有词缀，提炼为“先祖印记”
            foreach (var affix in dead.ActiveAffixes)
            {
                _discoveredAffixesThisRun.Add(affix);
            }
        }

        public void Tick(List<CreatureData> creatures, float globalTime)
        {
            if (_isSettling) return;

            // 存活时长大于 5 秒（防刚开局判定误杀），且局内所有生物死绝
            if (globalTime > 5f && creatures.Count == 0)
            {
                _isSettling = true;
                TriggerSettlement(globalTime, false, 0);
            }
            // 2. 胜利判定：到达预定的时间上限
            else if (globalTime >= MaxSimulationTime)
            {
                _isSettling = true;

                // 游戏胜利！把最终幸存者身上的词缀全部加入萃取池！
                foreach (var creature in creatures)
                {
                    foreach (var affix in creature.ActiveAffixes)
                    {
                        _discoveredAffixesThisRun.Add(affix);
                    }
                }

                TriggerSettlement(globalTime, true, creatures.Count);
            }
        }

        private void TriggerSettlement(float survivedTime, bool isVictory, int survivedCount)
        {
            // 1. 发放基础奖励与人口额外奖励
            int baseReward = isVictory ? 500 : 100;
            int survivalBonus = survivedCount * 10;
            int totalPoints = baseReward + survivalBonus;
            MetaDataManager.Current.ResearchPoints += totalPoints;

            // 用于显示在 UI 上的文字摘要
            string uiDetails = $"本纪元存活时间: {survivedTime:F1} 秒\n";
            uiDetails += $"最终存活: {survivedCount} 只\n";
            uiDetails += $"获得研究点数: {totalPoints} 点\n\n";

            // 2. 提取先祖印记逻辑
            bool hasNewMarks = false;
            foreach (var affix in _discoveredAffixesThisRun)
            {
                string markToUnlock = null;

                var affixDef = EvolutionLaws.Config.AffixManager.GetAffix(affix);

                if (affixDef != null && !string.IsNullOrEmpty(affixDef.CorrespondingMarkID))
                {
                    markToUnlock = affixDef.CorrespondingMarkID;
                }
                else
                {
                    // 如果在此词缀上没有配置对应的印记，直接跳过，不产出印记
                    continue;
                }

                if (!string.IsNullOrEmpty(markToUnlock))
                {
                    if (!MetaDataManager.Current.UnlockedMarks.Contains(markToUnlock))
                    {
                        MetaDataManager.Current.UnlockedMarks.Add(markToUnlock);
                        uiDetails += $"<color=#00FF00>萃取先祖印记: [{markToUnlock}]</color>\n";
                        hasNewMarks = true;
                    }
                    // 自动穿上新获得的印记
                    if (!MetaDataManager.Current.EquippedMarks.Contains(markToUnlock))
                    {
                        MetaDataManager.Current.EquippedMarks.Add(markToUnlock);
                    }
                }
            }

            if (!hasNewMarks)
            {
                uiDetails += "<color=#888888>未萃取到新的先祖印记...</color>\n";
            }

            // 3. 落地保存数据、导出分析
            MetaDataManager.Save();

            if (SimulationManager.Instance != null)
            {
                SimulationManager.Instance.ExportAnalyticsData();
            }

            // 4. 呼出结算 UI面板 (替代掉原来的直接跳转)
            if (EvolutionLaws.UI.SettlementUI.Instance != null)
            {
                EvolutionLaws.UI.SettlementUI.Instance.Show(isVictory, uiDetails);
            }
            else
            {
                // 无 UI 的后备方案
                Debug.LogWarning("未找到 SettlementUI 实例，直接跳转大厅。");
                SceneManager.LoadScene("MetaScene");
            }
        }
    }
}