using UnityEngine;
using EvolutionLaws.Data;
using System.Collections.Generic;

namespace EvolutionLaws.Config
{
    // 辅助结构：食谱配置
    [System.Serializable]
    public struct DietConfig
    {
        public ResourceType Type;// 资源类型，在数据表中配置好了，要更改在数据表上更改
        [Range(0, 1)] public float Efficiency;
    }

    [CreateAssetMenu(fileName = "Blueprint_NewSpecies", menuName = "Evolution Laws/Species Blueprint")]
    public class SpeciesBlueprint : ScriptableObject
    {
        // ==========================================
        // 1. 基础信息
        // ==========================================
        [Header("--- 身份与外观 ---")]
        public string SpeciesID;

        public string DisplayName;
        public Sprite DefaultSprite;
        public Color SpeciesColor = Color.white;// 物种颜色覆盖

        // ==========================================
        // 2. 生理体征
        // ==========================================
        [Header("--- 生理参数 ---")]
        public float MaxHealth = 100f; // 对应 Structure & Vitality Max

        [Tooltip("结构完整度 (物理血量)。归零=身体粉碎")]
        public float Structure_Max = 100f;

        public float Structure_Current = 100f; // 归零 = 物理粉碎

        [Tooltip("生理健康度 (免疫血量)。归零=病死/毒死")]
        public float Vitality_Max = 100f;

        public float Vitality_Current = 100f;  // 归零 = 生理衰竭

        public float Mass = 1.0f;
        public float Size = 1.0f;

        // ==========================================
        // 3. 新陈代谢
        // ==========================================
        [Header("--- 代谢参数 ---")]
        [Tooltip("最大能量值")]
        public float MaxEnergy = 100f;

        [Tooltip("最大营养值")]
        public float MaxNutrients = 500f;

        [Tooltip("基础代谢率 (每秒消耗的能量)")]
        public float BaseMetabolicRate = 1.0f;

        [Header("--- 食谱 (Diet) ---")]
        [Tooltip("【重要】进食效率表。\n顺序必须对应 ResourceType 枚举：\n0: 无\n1: 植物纤维\n2: 肉\n3: 腐肉")]
        public List<DietConfig> DietPreferences = new List<DietConfig>()
        {
            new DietConfig { Type = ResourceType.Plant_Fiber, Efficiency = 0.0f },
            new DietConfig { Type = ResourceType.Meat, Efficiency = 0.0f }
        };

        // ==========================================
        // 4. 行为能力
        // ==========================================
        [Header("--- 行为参数 ---")]
        [Tooltip("移动速度 (单位: 米/秒)")]
        public float MoveSpeed = 5.0f;

        [Tooltip("攻击伤害")]
        public float AttackDamage = 10.0f;

        [Tooltip("攻击范围 (格子距离)")]
        public float Attack_Range = 1.5f;

        [Tooltip("攻击间隔 (秒)")]
        public float Attack_Cooldown = 2.0f;

        // ==========================================
        // 5. 感知系统
        // ==========================================
        [Header("--- 感知系统 ---")]
        [Tooltip("视觉范围")]
        public float VisionRange = 15.0f;

        [Tooltip("视觉角度")]
        public float VisionAngle = 120f;

        [Tooltip("嗅觉灵敏度")]
        public float ScentSensitivity = 1.0f;

        [Tooltip("听觉阈值 (越低越灵敏)")]
        public float HearingThreshold = 0.2f; // ✅ 补全

        [Tooltip("同时关注目标数")]
        public int AttentionCap = 3;          // ✅ 补全

        // ==========================================
        // 6. 认知与性格
        // ==========================================
        [Header("Brain & Needs")]
        [Tooltip("饥饿需求 (0-100)，100极度饥饿")]
        public float Need_Hunger = 0f;

        public float Need_Safety = 100f;         // 100安全，0恐慌
        public float Need_Reproduction = 0f;

        [Header("--- 认知与性格 ---")]
        [Tooltip("压力恐慌阈值")]
        public float StressPanicThreshold = 80f;

        [Tooltip("压力恢复阈值")]
        public float StressRecoverThreshold = 50f; // ✅ 补全

        [Tooltip("性格特质：攻击性 (0-1)")]
        [Range(0, 1)] public float TraitAggression = 0.5f;

        [Tooltip("性格特质：好奇心 (0-1)")]
        [Range(0, 1)] public float TraitCuriosity = 0.5f;

        [Tooltip("性格特质：韧性 (0-1)")]
        [Range(0, 1)] public float TraitTenacity = 0.5f;

        // ==========================================
        // 7. 遗传与演化
        // ==========================================
        [Header("--- 遗传参数 ---")]
        [Tooltip("初始基因附加词缀列表")]
        public List<string> InitialAffixes = new List<string>();

        [Tooltip("温度适应范围")]
        public MinMaxRange ToleranceTemp = new MinMaxRange { Min = -10, Max = 40 };// 温度适应范围

        [Tooltip("初始基因稳定性")]
        public float BaseGeneticStability = 100f; //稳定性越高，突变概率越低

        [Tooltip("生殖隔离阈值")]
        public float BaseReproductiveCompatibility = 0.7f; //阈值越高，越难与其他物种交配

        // 繁殖参数
        [Header("--- 繁殖参数 ---")]
        [Tooltip("成熟年龄 (秒)")]
        public float MaturityAge = 50f;

        [Tooltip("最大寿命 (秒)")]
        public float MaxLifespan = 300f;

        [Tooltip("怀孕时长 (秒)")]
        public float PregnancyDuration = 30f;

        [Tooltip("繁殖冷却时间 (秒)")]
        public float ReproductionCooldown = 60f;

        [Tooltip("每次产仔数量 (随机范围)")]
        public MinMaxRange OffspringCount = new MinMaxRange { Min = 1, Max = 3 };

        [Tooltip("基因突变率 (0-1)")]
        [Range(0f, 1f)]
        public float MutationRate = 0.1f;

        //还有一些没有启用，等到需要启用时再进行，比如下面这些————启用时需要在工厂方法中补全
        /*
            [Header("--- 可选：初始状态 ---")]
            [Tooltip("出生时的生命阶段")]
            public LifeStage InitialStage = LifeStage.Adult;

            [Tooltip("初始基因复杂度")]
            public float InitialGeneticComplexity = 1.0f;

            [Tooltip("初始隐性潜力 (可选)")]
            public List<string> InitialPotentialKeys = new List<string>();
            public List<float> InitialPotentialValues = new List<float>();
        */

        // ==========================================
        // 工厂方法
        // ==========================================
        public CreatureData CreateCreatureData(Vector2 spawnPosition, float currentSimulationTime)//获取当前位置，生成一个CreatureData实例
        {
            var data = new CreatureData// 创建一个新的 CreatureData 实例
            {
                // --- Identity ---
                UID = System.Guid.NewGuid().ToString(),// 全局唯一ID，系统自动生成
                Position = spawnPosition,// 出生位置
                SpeciesID = this.SpeciesID,
                DisplayName = this.DisplayName,
                Generation = 0,
                BirthTimestamp = currentSimulationTime,
                Stage = LifeStage.Adult,

                // --- Physiology ---
                Structure_Max = this.MaxHealth,
                Structure_Current = this.MaxHealth,
                Vitality_Max = this.MaxHealth,
                Vitality_Current = this.MaxHealth,
                Mass = this.Mass,
                Size = this.Size,

                // --- Metabolism ---
                Energy = this.MaxEnergy,// 初始能量为最大值
                Energy_Max = this.MaxEnergy,// 最大能量值
                Nutrients = this.MaxNutrients * 0.8f,
                Nutrients_Max = this.MaxNutrients,
                Base_Metabolic_Rate = this.BaseMetabolicRate,

                // --- Action ---
                Move_Speed = this.MoveSpeed,
                Attack_Damage = this.AttackDamage,

                // --- Senses (全填上了) ---
                Vision_Range = this.VisionRange,
                Vision_Angle = this.VisionAngle,
                Scent_Sensitivity = this.ScentSensitivity,
                Hearing_Threshold = this.HearingThreshold, //听力阈值
                Attention_Cap = this.AttentionCap,         //注意力上限

                // --- Brain (全填上了) ---
                Stress_Panic_Threshold = this.StressPanicThreshold,
                Stress_Recover_Threshold = this.StressRecoverThreshold, //压力恢复阈值
                Trait_Aggression = this.TraitAggression,    //攻击性
                Trait_Curiosity = this.TraitCuriosity,      //好奇心
                Trait_Tenacity = this.TraitTenacity,       // 韧性

                // --- Genetics (全填上了) ---
                ActiveAffixes = new List<string>(this.InitialAffixes),// 复制初始词缀列表
                Tolerance_Temp = this.ToleranceTemp,
                Genetic_Stability = this.BaseGeneticStability,             // 基因稳定性
                Reproductive_Compatibility = this.BaseReproductiveCompatibility, //繁殖兼容性

                //繁殖参数
                Maturity_Age = this.MaturityAge,
                Max_Lifespan = this.MaxLifespan,
                Pregnancy_Duration = this.PregnancyDuration,
                Reproduction_Cooldown = this.ReproductionCooldown
            };

            // --- 食谱转换逻辑 ---
            int enumCount = System.Enum.GetNames(typeof(ResourceType)).Length;
            data.Diet_Efficiency_Flat = new List<float>(new float[enumCount]);

            foreach (var config in DietPreferences)
            {
                int index = (int)config.Type;
                if (index >= 0 && index < data.Diet_Efficiency_Flat.Count)
                {
                    data.Diet_Efficiency_Flat[index] = config.Efficiency;
                }
            }

            return data;
        }
    }
}