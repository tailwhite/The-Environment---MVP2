using System.Collections.Generic;
using UnityEngine;
using EvolutionLaws.Data;
using EvolutionLaws.Config;

namespace EvolutionLaws.Core
{
    public class EvolutionSystem
    {
        public float Max_Genetic_Complexity = 2.0f;
        private PlayerGodPowerSystem _godPowerSystem;

        public EvolutionSystem(PlayerGodPowerSystem godPowerSystem)
        {
            _godPowerSystem = godPowerSystem;
        }

        public void Tick(List<CreatureData> creatures, EnvironmentData environment, float deltaTime)
        {
            foreach (var creature in creatures)
            {
                if (creature.IsDead) continue;
                // 处理消息倒计时
                if (creature.EvoMsgTimer > 0)
                {
                    creature.EvoMsgTimer -= deltaTime;
                }
                AccumulatePotentials(creature, environment, deltaTime);
            }
        }

        /*public void Tick(List<CreatureData> creatures, float deltaTime)
        {
            foreach (var creature in creatures)
            {
                if (creature.IsDead) continue;

                // 核心机制 1：基于环境账单刺激潜能积累
                AccumulatePotentials(creature, deltaTime);

                // 【已移除】：CheckPotentialAwakening(creature);
                // 我们不再进行低效的每帧轮询，改为在 AddPotential 中触发觉醒
            }
        }*/

        private void AccumulatePotentials(CreatureData creature, EnvironmentData environment, float deltaTime)
        {
            if (creature.IsMoving)
            {
                AddPotential(creature, "Fast_Legs", deltaTime * 0.5f);
            }

            if (creature.Structure_Current < creature.Structure_Max * 0.5f)
            {
                AddPotential(creature, "Scales", deltaTime * 0.8f);
            }

            // 获取脚下格子
            var tile = environment.GetTile(Mathf.FloorToInt(creature.Position.x), Mathf.FloorToInt(creature.Position.y));
            float actualTemp = environment.Global_Temperature + (tile != null ? tile.Temperature_Offset : 0f);

            // 当它处于零度以下的试炼场挣扎时，疯狂累计抗寒基因（一秒给 5 点，大约20秒不死就能觉醒）
            if (actualTemp <= 0f)
            {
                AddPotential(creature, "Thick_Fur", deltaTime * 1f);
            }
            else if (creature.Lifetime_EnergySpent_Temp > 100f)
            {
                AddPotential(creature, "Thick_Fur", deltaTime * 0.3f);
            }
        }

        // 增加辅助方法：检查生物是否拥有包含该词缀引发权的“潜能”
        private bool HasPermitForAffix(CreatureData creature, string affixID)
        {
            // 遍历生物目前携带的所有潜能密钥 (包括在0代出生时塞入的 "Survival_Potential_Base" 等)
            foreach (var key in creature.Potential_Keys)
            {
                // 如果这个 key 是一个潜能卡包，且卡包里包含目标词缀，则放行
                if (EvolutionLaws.Meta.MetaConfigManager.CanPotentialUnlockAffix(key, affixID))
                {
                    return true;
                }
            }
            return false; // 如果找遍了都没授权，说明这只生物没有点这个科技树分支，不让它涨进度
        }

        //  将“值积累”与“阈值触发”合并
        private void AddPotential(CreatureData creature, string affixID, float amount)
        {
            if (!HasPermitForAffix(creature, affixID))
                return;
            for (int i = 0; i < creature.Potential_Keys.Count; i++)
            {
                if (creature.Potential_Keys[i] == affixID)
                {
                    creature.Potential_Values[i] += amount;

                    // 触发式检测：只有在数值变动的这一瞬间，才检查是否觉醒
                    if (creature.Potential_Values[i] >= 100f)
                    {
                        TriggerAwakening(creature, affixID, i);
                    }
                    return;
                }
            }

            // 新增潜能项
            creature.Potential_Keys.Add(affixID);
            creature.Potential_Values.Add(amount);
        }

        // 专职处理觉醒逻辑 (原 CheckPotentialAwakening 的内核)
        private void TriggerAwakening(CreatureData creature, string affixID, int index)
        {
            // 校验是否允许获取该词缀
            if (CanAcquireAffix(creature, affixID))
            {
                creature.ActiveAffixes.Add(affixID);
                creature.Genetic_Complexity += 0.2f;

                creature.RecalculateStats(AffixManager.GetDatabase());
                _godPowerSystem.AddEnergy(100f, $"基因飞跃：{creature.SpeciesID} 觉醒了 {affixID}！");
                // 1. 【性能最优解】将消息直接注入给生物本尊，由它的已实例化的UI读取！
                creature.EvolutionMsg = $"进化:\n{affixID}!";
                creature.EvoMsgTimer = 5.0f; // 滞留 5 秒

                if (EvolutionLaws.UI.NotificationUIManager.Instance != null)
                {
                    //EvolutionLaws.UI.NotificationUIManager.Instance.ShowFloatingText(creature.Position, $"进化:{affixID}!", Color.red);
                    // 【追踪优化1】加上具体坐标，方便你手动把镜头移过去
                    EvolutionLaws.UI.NotificationUIManager.Instance.AddLogMessage($"【进化】{creature.SpeciesID} 绝境中觉醒: {affixID} 于坐标({creature.Position.x:F0}, {creature.Position.y:F0})", Color.yellow);
                }
                // 【追踪优化2】在 Unity Editor 的 Scene 窗口（不仅是 Game 窗口）画一根直冲云霄的超级射线，保留 5 秒！
                // 这样你切到 Scene 面板一眼就能看到哪里发生了变异！
                Debug.DrawRay(new Vector3(creature.Position.x, creature.Position.y, 0), Vector3.up * 50f, Color.red, 5f);

                // 【追踪优化3】让控制台的日志支持点击对象高亮（传递 UID 方便溯源）
                Debug.Log($"<color=cyan>[EvolutionSystem] 📍 坐标({creature.Position.x:F1},{creature.Position.y:F1}) 发生基因飞跃！{creature.SpeciesID} (UID:{creature.UID.Substring(0, 4)}) 觉醒了: {affixID}</color>");
            }

            // 无论成功还是失败，均从进度列表中抹除
            creature.Potential_Keys.RemoveAt(index);
            creature.Potential_Values.RemoveAt(index);
        }

        // 核心机制 2：觉醒前的校验 (基因复杂度/互斥词缀/有效性)
        private bool CanAcquireAffix(CreatureData creature, string affixID)
        {
            // 限制条件：基因复杂度上限
            if (creature.Genetic_Complexity >= Max_Genetic_Complexity) return false;
            if (creature.ActiveAffixes.Contains(affixID)) return false;// 已拥有同词缀，无需重复获取
            if (AffixManager.GetAffix(affixID) == null) return false;// 无效词缀ID

            // 使用 O(1) 哈希判定互斥 (需确保已应用上一步的 O(1) 优化配置)
            foreach (var existingID in creature.ActiveAffixes)
            {
                if (AffixManager.AreIncompatible(affixID, existingID)) return false;
            }

            return true;
        }
    }
}