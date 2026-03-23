using UnityEngine;
using EvolutionLaws.Data;
using EvolutionLaws.Config;
using System.Collections.Generic;
using EvolutionLaws.Core;

namespace EvolutionLaws.Utilities
{
    /// <summary>
    /// 【基因工具类】
    /// 职责: 处理所有基因相关的计算逻辑
    /// 原则: 纯静态方法, 无状态, 可复用
    /// </summary>
    public static class GeneticsUtility
    {
        public static CreatureData CreateOffspring(
            SpeciesBlueprint blueprint,
            Vector2 birthPosition,
            CreatureData mother,
            CreatureData father,
            float GlobalTime)
        {
            // 1. 从蓝图创建基础数据
            var offspring = blueprint.CreateCreatureData(birthPosition, GlobalTime);

            // 2. 设置继承信息
            offspring.Generation = mother.Generation + 1;
            offspring.ParentUIDs.Clear();
            offspring.ParentUIDs.Add(mother.UID);
            if (father != null)
            {
                offspring.ParentUIDs.Add(father.UID);
            }

            // 3. 应用基因继承
            ApplyInheritance(offspring, mother, father);

            // 4. 应用基因突变
            ApplyMutation(offspring, blueprint.MutationRate);

            return offspring;
        }

        // ==========================================
        // 基因继承
        // ==========================================
        /// <summary>
        /// 应用基因继承 (从父母继承属性)
        /// </summary>
        public static void ApplyInheritance(CreatureData offspring, CreatureData mother, CreatureData father)
        {
            // ━━━━━━━━━━━━━━━━━━━━━━━━
            // 基础属性: 父母平均值 ± 5% 随机
            // ━━━━━━━━━━━━━━━━━━━━━━━━
            if (father != null)
            {
                // 双亲繁殖
                offspring.Size = Random.Range(
                    Mathf.Min(mother.Size, father.Size) * 0.95f,
                    Mathf.Max(mother.Size, father.Size) * 1.05f
                );

                offspring.Move_Speed = (mother.Move_Speed + father.Move_Speed) / 2f * Random.Range(0.95f, 1.05f);
                offspring.Attack_Damage = (mother.Attack_Damage + father.Attack_Damage) / 2f * Random.Range(0.95f, 1.05f);

                // 代谢率继承
                offspring.Base_Metabolic_Rate = (mother.Base_Metabolic_Rate + father.Base_Metabolic_Rate) / 2f * Random.Range(0.95f, 1.05f);

                // 感知能力继承
                offspring.Vision_Range = (mother.Vision_Range + father.Vision_Range) / 2f * Random.Range(0.95f, 1.05f);
                offspring.Scent_Sensitivity = (mother.Scent_Sensitivity + father.Scent_Sensitivity) / 2f * Random.Range(0.95f, 1.05f);
            }
            else
            {
                // 单亲繁殖 (无性繁殖)
                offspring.Size = mother.Size * Random.Range(0.95f, 1.05f);
                offspring.Move_Speed = mother.Move_Speed * Random.Range(0.95f, 1.05f);
                offspring.Attack_Damage = mother.Attack_Damage * Random.Range(0.95f, 1.05f);
                offspring.Base_Metabolic_Rate = mother.Base_Metabolic_Rate * Random.Range(0.95f, 1.05f);
                offspring.Vision_Range = mother.Vision_Range * Random.Range(0.95f, 1.05f);
                offspring.Scent_Sensitivity = mother.Scent_Sensitivity * Random.Range(0.95f, 1.05f);
            }

            // ━━━━━━━━━━━━━━━━━━━━━━━━
            // 词缀继承 (从父母随机抽取)
            // ━━━━━━━━━━━━━━━━━━━━━━━━
            InheritAffixes(offspring, mother, father);

            Debug.Log($"[GeneticsUtility] 基因继承完成 | 体型: {offspring.Size:F2} | 速度: {offspring.Move_Speed:F2}");
        }

        // ==========================================
        // 词缀继承
        // ==========================================
        /// <summary>
        /// 从父母继承词缀 (50% 概率)
        /// </summary>
        private static void InheritAffixes(CreatureData offspring, CreatureData mother, CreatureData father)
        {
            offspring.ActiveAffixes.Clear();

            // 继承母亲词缀 (50% 概率)
            foreach (var affix in mother.ActiveAffixes)
            {
                if (Random.value < 0.5f)
                {
                    offspring.ActiveAffixes.Add(affix);
                }
            }

            // 继承父亲词缀 (50% 概率)
            if (father != null)
            {
                foreach (var affix in father.ActiveAffixes)
                {
                    if (Random.value < 0.5f && !offspring.ActiveAffixes.Contains(affix))
                    {
                        offspring.ActiveAffixes.Add(affix);
                    }
                }
            }

            Debug.Log($"[GeneticsUtility] 词缀继承: {offspring.ActiveAffixes.Count} 个");
        }

        // ==========================================
        // 基因突变
        // ==========================================
        /// <summary>
        /// 应用基因突变 (随机变异)
        /// </summary>
        public static void ApplyMutation(CreatureData offspring, float mutationRate)
        {
            if (Random.value > mutationRate)
                return;

            Debug.Log($"[GeneticsUtility] 🧬 突变触发!");

            int mutationType = Random.Range(0, 4); // 简化为4个方向的特化

            // 【补丁】定义一个最大突变阈值 (避免无限膨胀或缩到看不见)
            // 正常情况下，最好把基础值存在 Blueprint 里进行比较，这里暂用绝对值限制示范
            switch (mutationType)
            {
                case 0: // 巨型化特化 (大体型、高伤害、低速度、高消耗)
                    offspring.Size = Mathf.Clamp(offspring.Size * 1.15f, 0.5f, 3.0f);
                    offspring.Attack_Damage *= 1.1f;
                    offspring.Move_Speed *= 0.9f;
                    offspring.Base_Metabolic_Rate *= 1.15f;
                    Debug.Log("[GeneticsUtility] 变异方向: 巨型化");
                    break;

                case 1: // 敏捷化特化 (高速度、小体型、低伤害、高消耗)
                    offspring.Move_Speed = Mathf.Clamp(offspring.Move_Speed * 1.15f, 1f, 15f);
                    offspring.Size = Mathf.Clamp(offspring.Size * 0.9f, 0.5f, 3.0f);
                    offspring.Attack_Damage *= 0.85f;
                    offspring.Base_Metabolic_Rate *= 1.1f;
                    Debug.Log("[GeneticsUtility] 变异方向: 敏捷化");
                    break;

                case 2: // 感知特化 (寻找食物/猎物极强，但更脆弱)
                    offspring.Vision_Range = Mathf.Clamp(offspring.Vision_Range * 1.2f, 5f, 40f);
                    offspring.Scent_Sensitivity *= 1.2f;
                    offspring.Structure_Max *= 0.9f; // 变得更脆
                    Debug.Log("[GeneticsUtility] 变异方向: 感知强化");
                    break;

                case 3: // 节能特化 (极低消耗，但丧失战斗力和速度)
                    offspring.Base_Metabolic_Rate = Mathf.Clamp(offspring.Base_Metabolic_Rate * 0.8f, 0.2f, 5f);
                    offspring.Move_Speed *= 0.9f;
                    offspring.Vision_Range *= 0.9f;
                    Debug.Log("[GeneticsUtility] 变异方向: 节能化");
                    break;
            }

            // 词缀突变 (建议：定义一个全局或蓝图中的可用词缀池)
            // if (Random.value < 0.05f)
            // {
            //     string newAffix = GetRandomAffixForSpecies(offspring.SpeciesID);
            //     if (!string.IsNullOrEmpty(newAffix) && !offspring.ActiveAffixes.Contains(newAffix))
            //         offspring.ActiveAffixes.Add(newAffix);
            // }
        }
    }
}