using EvolutionLaws.Config;
using EvolutionLaws.Core;
using EvolutionLaws.Data;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

namespace EvolutionLaws.Utilities
{
    /// <summary>
    /// 【基因工具类】
    /// 职责: 处理所有基因相关的计算逻辑
    /// 原则: 纯静态方法, 无状态, 可复用
    /// </summary>
    public static class GeneticsUtility
    {
        // ==========================================
        // 生成子代数据的主方法
        // ==========================================
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

            // 4. 应用基因突变 (👉使用子代继承来的属于自己的突变率，而不是蓝图硬编码)
            ApplyMutation(offspring, offspring.Mutation_Rate);
            offspring.RecalculateStats(AffixManager.GetDatabase());
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
                offspring.Size = Random.Range(Mathf.Min(mother.Size, father.Size) * 0.95f, Mathf.Max(mother.Size, father.Size) * 1.05f);
                offspring.Base_Move_Speed = (mother.Base_Move_Speed + father.Base_Move_Speed) / 2f * Random.Range(0.95f, 1.05f);
                offspring.Attack_Damage = (mother.Attack_Damage + father.Attack_Damage) / 2f * Random.Range(0.95f, 1.05f);
                offspring.Base_Metabolic_Rate = (mother.Base_Metabolic_Rate + father.Base_Metabolic_Rate) / 2f * Random.Range(0.95f, 1.05f);
                offspring.Vision_Range = (mother.Vision_Range + father.Vision_Range) / 2f * Random.Range(0.95f, 1.05f);
                offspring.Scent_Sensitivity = (mother.Scent_Sensitivity + father.Scent_Sensitivity) / 2f * Random.Range(0.95f, 1.05f);
                offspring.Mutation_Rate = (mother.Mutation_Rate + father.Mutation_Rate) / 2f * Random.Range(0.95f, 1.05f);
                offspring.Offspring_Count.Min = (mother.Offspring_Count.Min + father.Offspring_Count.Min) / 2f;
                offspring.Offspring_Count.Max = (mother.Offspring_Count.Max + father.Offspring_Count.Max) / 2f;
            }
            else
            {
                offspring.Size = mother.Size * Random.Range(0.95f, 1.05f);
                offspring.Base_Move_Speed = mother.Base_Move_Speed * Random.Range(0.95f, 1.05f);
                offspring.Attack_Damage = mother.Attack_Damage * Random.Range(0.95f, 1.05f);
                offspring.Base_Metabolic_Rate = mother.Base_Metabolic_Rate * Random.Range(0.95f, 1.05f);
                offspring.Vision_Range = mother.Vision_Range * Random.Range(0.95f, 1.05f);
                offspring.Scent_Sensitivity = mother.Scent_Sensitivity * Random.Range(0.95f, 1.05f);
                offspring.Mutation_Rate = mother.Mutation_Rate * Random.Range(0.95f, 1.05f);
                offspring.Offspring_Count.Min = mother.Offspring_Count.Min;
                offspring.Offspring_Count.Max = mother.Offspring_Count.Max;
            }

            // ━━━━━━━━━━━━━━━━━━━━━━━━
            // 词缀继承 (从父母随机抽取)
            // ━━━━━━━━━━━━━━━━━━━━━━━━
            InheritAffixes(offspring, mother, father);
            InheritPotentials(offspring, mother, father);
            Debug.Log($"[GeneticsUtility] 基因继承完成 | 体型: {offspring.Size:F2} | 速度: {offspring.Base_Move_Speed:F2}");
        }

        // ==========================================
        // 词缀继承 (带排异验证)
        // ==========================================
        private static void InheritAffixes(CreatureData offspring, CreatureData mother, CreatureData father)
        {
            offspring.ActiveAffixes.Clear();
            offspring.Genetic_Complexity = 1.0f; // 初始复杂度

            // 继承母亲词缀
            foreach (var affixID in mother.ActiveAffixes)
            {
                if (Random.value < 0.5f) AddAffixSafe(offspring, affixID);
            }

            // 继承父亲词缀
            if (father != null)
            {
                foreach (var affixID in father.ActiveAffixes)
                {
                    if (Random.value < 0.5f) AddAffixSafe(offspring, affixID);
                }
            }
        }

        // =====================================================
        // 继承潜能 (父母积累的潜能会以折损的形式传给下一代，提供成长线索)
        // =====================================================
        private static void InheritPotentials(CreatureData offspring, CreatureData mother, CreatureData father)
        {
            offspring.Potential_Keys.Clear();
            offspring.Potential_Values.Clear();

            Dictionary<string, float> mergedPotentials = new Dictionary<string, float>();

            // 提取母亲积累的潜能 (折损一半传给下一代)
            for (int i = 0; i < mother.Potential_Keys.Count; i++)
            {
                mergedPotentials[mother.Potential_Keys[i]] = mother.Potential_Values[i] * 0.5f;
            }

            // 如果有父亲，结合父亲的潜能
            if (father != null)
            {
                for (int i = 0; i < father.Potential_Keys.Count; i++)
                {
                    string key = father.Potential_Keys[i];
                    float val = father.Potential_Values[i] * 0.5f;

                    if (mergedPotentials.ContainsKey(key))
                        mergedPotentials[key] += val; // 父母都有类似经历，进度叠加，强强联合！
                    else
                        mergedPotentials[key] = val;
                }
            }

            // 写入孩子的数据中
            foreach (var kvp in mergedPotentials)
            {
                if (kvp.Value > 2f || kvp.Value == 0f || EvolutionLaws.Meta.MetaDataManager.Current.EquippedPotentials.Contains(kvp.Key))
                {
                    offspring.Potential_Keys.Add(kvp.Key);
                    offspring.Potential_Values.Add(kvp.Value);
                }
            }
        }

        /// <summary>
        /// 安全添加词缀 (自带互斥检测与复杂度累计)
        /// </summary>
        private static void AddAffixSafe(CreatureData creature, string affixID)
        {
            if (creature.ActiveAffixes.Contains(affixID)) return;

            var newDef = AffixManager.GetAffix(affixID);// 获取新词缀定义
            if (newDef == null) return;

            // 互斥性检验校验
            foreach (var existingID in creature.ActiveAffixes)
            {
                // A 排斥 B
                if (newDef.Incompatible_IDs.Contains(existingID)) return;

                // B 排斥 A
                var existingDef = AffixManager.GetAffix(existingID);
                if (existingDef != null && existingDef.Incompatible_IDs.Contains(affixID)) return;
            }

            creature.ActiveAffixes.Add(affixID);

            // 基因库每重一层，带来 0.2 的复杂惩罚 (可以给后续的生殖隔离留作运算因子)
            creature.Genetic_Complexity += 0.2f;
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

            // 定义一个最大突变阈值 (避免无限膨胀或缩到看不见)
            // 正常情况下，最好把基础值存在 Blueprint 里进行比较，这里暂用绝对值限制示范
            switch (mutationType)
            {
                case 0: // 巨型化特化 (大体型、高伤害、低速度、高消耗)
                    offspring.Size = Mathf.Clamp(offspring.Size * 1.15f, 0.5f, 3.0f);
                    offspring.Attack_Damage *= 1.1f;
                    offspring.Base_Move_Speed *= 0.9f;
                    offspring.Base_Metabolic_Rate *= 1.15f;
                    Debug.Log("<color=red>[GeneticsUtility] 变异方向: 巨型化</color>");
                    Debug.Log($"[GeneticsUtility] 体型: {offspring.Size:F2} | 伤害: {offspring.Attack_Damage:F2} | 速度: {offspring.Base_Move_Speed:F2} | 代谢率: {offspring.Base_Metabolic_Rate:F2}");
                    break;

                case 1: // 敏捷化特化 (高速度、小体型、低伤害、高消耗)
                    offspring.Base_Move_Speed = Mathf.Clamp(offspring.Base_Move_Speed * 1.15f, 1f, 15f);
                    offspring.Size = Mathf.Clamp(offspring.Size * 0.9f, 0.5f, 3.0f);
                    offspring.Attack_Damage *= 0.85f;
                    offspring.Base_Metabolic_Rate *= 1.1f;
                    Debug.Log("<color = green>[GeneticsUtility] 变异方向: 敏捷化</color>");
                    Debug.Log($"[GeneticsUtility] 速度: {offspring.Base_Move_Speed:F2} | 体型: {offspring.Size:F2} | 伤害: {offspring.Attack_Damage:F2} | 代谢率: {offspring.Base_Metabolic_Rate:F2}");
                    break;

                case 2: // 感知特化 (寻找食物/猎物极强，但更脆弱)
                    offspring.Vision_Range = Mathf.Clamp(offspring.Vision_Range * 1.2f, 5f, 40f);
                    offspring.Scent_Sensitivity *= 1.2f;
                    offspring.Structure_Max *= 0.9f; // 变得更脆
                    Debug.Log("<color= brown>[GeneticsUtility] 变异方向: 感知强化</color>");
                    Debug.Log($"[GeneticsUtility] 视觉范围: {offspring.Vision_Range:F2} | 嗅觉灵敏度: {offspring.Scent_Sensitivity:F2} | 结构值: {offspring.Structure_Max:F2}");
                    break;

                case 3: // 节能特化 (极低消耗，但丧失战斗力和速度)
                    offspring.Base_Metabolic_Rate = Mathf.Clamp(offspring.Base_Metabolic_Rate * 0.8f, 0.2f, 5f);
                    offspring.Base_Move_Speed *= 0.9f;
                    offspring.Vision_Range *= 0.9f;
                    Debug.Log("<color=yellow>[GeneticsUtility] 变异方向: 节能化</color>");
                    Debug.Log($"[GeneticsUtility] 代谢率: {offspring.Base_Metabolic_Rate:F2} | 速度: {offspring.Base_Move_Speed:F2} | 视觉范围: {offspring.Vision_Range:F2}");
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