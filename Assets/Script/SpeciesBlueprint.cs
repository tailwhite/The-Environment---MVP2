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
        [Header("--- 身份与外观 ---")]
        public string SpeciesID;

        public string DisplayName;
        public Sprite DefaultSprite;
        public Color SpeciesColor = Color.white;

        [Header("--- 核心模块配置 ---")]
        [Tooltip("生理与基础属性配置，决定生物的身板厚度与代谢下限")]
        public PhysiologyConfig Physiology = new PhysiologyConfig
        {
            MaxHealth = 100f,
            Mass = 1.0f,
            Size = 1.0f,
            MaxEnergy = 100f,
            MaxNutrients = 1500f,
            BaseMetabolicRate = 1.0f
        };

        [Tooltip("运动与攻击属性配置，决定生物的输出与跑路能力")]
        public ActionConfig Action = new ActionConfig
        {
            MoveSpeed = 5.0f,//这是实际值，基础值会在工厂方法中赋值给数据实体，后续可以通过词缀修改
            AttackDamage = 10f,
            AttackRange = 1.5f,
            AttackCooldown = 2.0f
        };

        public SenseConfig Senses = new SenseConfig
        {
            VisionRange = 15.0f,
            VisionAngle = 120.0f,
            ScentSensitivity = 1.0f,
            HearingThreshold = 0.2f,
            AttentionCap = 3
        };

        public BrainConfig Brain = new BrainConfig
        {
            StressPanicThreshold = 80f,
            StressRecoverThreshold = 50f,
            TraitAggression = 0.5f,
            TraitCuriosity = 0.5f,
            TraitTenacity = 0.5f
        };

        public ReproductionConfig Reproduction = new ReproductionConfig
        {
            MaturityAge = 50f,
            MaxLifespan = 300f,
            PregnancyDuration = 30f,
            ReproductionCooldown = 60f,
            OffspringCount = new MinMaxRange { Min = 1, Max = 3 },
            MutationRate = 0.1f//突变率，0.1f表示10%的基因有可能发生突变
        };

        [Header("--- 遗传与食谱 ---")]
        public List<DietConfig> DietPreferences = new List<DietConfig>();

        public List<string> InitialAffixes = new List<string>();
        public MinMaxRange ToleranceTemp = new MinMaxRange { Min = -10, Max = 40 };
        public float BaseGeneticStability = 100f;
        public float BaseReproductiveCompatibility = 0.7f;
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
        public CreatureData CreateCreatureData(Vector2 spawnPosition, float currentSimulationTime)
        {
            var data = new CreatureData
            {
                UID = System.Guid.NewGuid().ToString(),
                Position = spawnPosition,
                SpeciesID = this.SpeciesID,
                DisplayName = this.DisplayName,
                Generation = 0,
                BirthTimestamp = currentSimulationTime,
                Stage = LifeStage.Adult,

                // Physiology
                Base_Structure_Max = Physiology.MaxHealth,
                Structure_Max = Physiology.MaxHealth,
                Structure_Current = Physiology.MaxHealth,
                Vitality_Max = Physiology.MaxHealth,
                Vitality_Current = Physiology.MaxHealth,
                Mass = Physiology.Mass,
                Size = Physiology.Size,
                Energy = Physiology.MaxEnergy,
                Energy_Max = Physiology.MaxEnergy,
                Nutrients = Physiology.MaxNutrients * 0.8f,
                Nutrients_Max = Physiology.MaxNutrients,
                Base_Metabolic_Rate = Physiology.BaseMetabolicRate,

                // Action
                Base_Move_Speed = Action.MoveSpeed,
                Move_Speed = Action.MoveSpeed,
                Attack_Damage = Action.AttackDamage,
                Attack_Range = Action.AttackRange,
                Attack_Cooldown = Action.AttackCooldown,

                // Senses
                Vision_Range = Senses.VisionRange,
                Vision_Angle = Senses.VisionAngle,
                Scent_Sensitivity = Senses.ScentSensitivity,
                Hearing_Threshold = Senses.HearingThreshold,
                Attention_Cap = Senses.AttentionCap,

                // Brain
                Stress_Panic_Threshold = Brain.StressPanicThreshold,
                Stress_Recover_Threshold = Brain.StressRecoverThreshold,
                Trait_Aggression = Brain.TraitAggression,
                Trait_Curiosity = Brain.TraitCuriosity,
                Trait_Tenacity = Brain.TraitTenacity,

                // Genetics & Reproduction
                ActiveAffixes = new List<string>(this.InitialAffixes),
                Tolerance_Temp = this.ToleranceTemp,
                Genetic_Stability = this.BaseGeneticStability,
                Reproductive_Compatibility = this.BaseReproductiveCompatibility,
                Maturity_Age = Reproduction.MaturityAge,
                Max_Lifespan = Reproduction.MaxLifespan,
                Pregnancy_Duration = Reproduction.PregnancyDuration,
                Reproduction_Cooldown = Reproduction.ReproductionCooldown,
                Mutation_Rate = Reproduction.MutationRate,
                Offspring_Count = Reproduction.OffspringCount
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