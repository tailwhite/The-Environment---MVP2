using System.Buffers;
using System.Collections.Generic;
using UnityEngine;

namespace EvolutionLaws.Data
{
    [System.Serializable]
    public class CreatureData
    {
        // ==========================================
        // 【维度 0】 身份与状态 (Identity & Flags)
        // ==========================================
        [Header("--- 身份信息 ---")]
        [Tooltip("全局唯一ID，系统自动生成")]
        public string UID;                  // 全局唯一ID

        [Tooltip("用于UI显示的漂亮名字 (如: 森林狼)")]
        public string DisplayName;

        [Tooltip("当前的世界坐标")]
        public Vector2 Position;            // 世界坐标位置

        [Tooltip("物种名称 (例如: Wolf_Gen5)")]
        public string SpeciesID;  // 物种标识 (如 "Wolf_Gen5")

        [Tooltip("第几代")]
        public int Generation;      // 代数

        [Tooltip("双亲ID 列表 (支持多配偶)")]
        public List<string> ParentUIDs = new List<string>(); // 双亲ID

        [Tooltip("出生时间戳 (秒)")]
        public float BirthTimestamp;        // 出生时间

        [Tooltip("当前生长阶段")]
        public LifeStage Stage = LifeStage.Adult;

        //年龄相关
        [Tooltip("成熟年龄 (秒) - 达到此年龄才能繁殖")]
        public float Maturity_Age = 50f;

        [Tooltip("最大寿命 (秒) - 达到此年龄后死亡概率增加")]
        public float Max_Lifespan = 300f;

        //繁殖相关
        [Header("--- 繁殖状态 ---")]
        [Tooltip("是否处于怀孕/孵化状态")]
        public bool IsPregnant = false;

        [Tooltip("怀孕开始时间 (仿真时间)")]
        public float Pregnancy_Start_Time = 0f;

        [Tooltip("怀孕时长 (秒)")]
        public float Pregnancy_Duration = 30f;

        [Tooltip("上次繁殖时间 (仿真时间)")]
        public float Last_Reproduction_Time = 0f;

        [Tooltip("繁殖冷却时间 (秒)")]
        public float Reproduction_Cooldown = 60f;

        [Tooltip("配偶 UID (当前繁殖伙伴)")]
        public string Mate_UID = null;

        [Header("--- 运行状态 ---")]
        [Tooltip("是否死亡")]
        public bool IsDead = false;

        [Tooltip("是否处于假死/休眠状态 (不消耗能量)")]
        public bool IsTorpor = false;       // 假死/休眠状态

        [Tooltip("是否昏迷")]
        public bool IsUnconscious = false;  // 昏迷状态

        [Tooltip("是否正在移动（本帧）")]
        public bool IsMoving = false;  // 由 MovementSystem 设置

        [Tooltip("是否正在进食（本帧）")]
        public bool IsFeeding = false;  // 由 InteractionSystem 设置

        // ==========================================
        // 【维度 1】 生理体征 (Physiology)
        // ==========================================
        [Header("--- 生理体征 ---")]
        [Tooltip("结构完整度 (物理血量)。归零=身体粉碎")]
        public float Structure_Max = 100f;

        public float Structure_Current = 100f; // 归零 = 物理粉碎

        [Tooltip("生理健康度 (免疫血量)。归零=病死/毒死")]
        public float Vitality_Max = 100f;

        public float Vitality_Current = 100f;  // 归零 = 生理衰竭

        [Tooltip("体型系数 (1.0 = 标准)。影响代谢和战斗")]
        public float Mass = 1.0f;              // 质量 (击退/流体计算)

        public float Size = 1.0f;              // 体型系数

        // ==========================================
        // 【维度 2】 新陈代谢 (Metabolism)
        // ==========================================
        [Header("--- 新陈代谢 ---")]
        [Tooltip("瞬时体力 (用于跑跳)。归零=昏迷")]
        public float Energy = 100f;            // 瞬时体力 (跑跳)

        public float Energy_Max = 100f;

        [Tooltip("长期营养储备 (脂肪)。Energy耗尽时扣除这里")]
        public float Nutrients = 200f;         // 长期储备 (生长/修复)

        public float Nutrients_Max = 500f;

        [Tooltip("基础代谢率 (每秒自然消耗多少能量)")]
        public float Base_Metabolic_Rate = 1.0f;     // 基础消耗

        public float Current_Metabolic_Burn = 0f;    // 实时计算值 (含环境惩罚)

        // 饮食转化率表 (不可变配置，建议运行时从配置表加载，此处为序列化占位)
        // Key: ResourceType Enum Index, Value: Efficiency
        [Tooltip("【重要】进食效率表。\n顺序必须对应 ResourceType 枚举：\n0: 无\n1: 植物纤维\n2: 肉\n3: 腐肉")]
        public List<float> Diet_Efficiency_Flat = new List<float>();

        // 👇=== 能量账单统计 ===👇
        [Header("--- 能量账单统计 (Lifetime) ---")]
        public float Lifetime_EnergySpent_Metabolism = 0f; // 累计基础代谢消耗

        public float Lifetime_EnergySpent_Temp = 0f;       // 累计温度环境惩罚消耗
        public float Lifetime_EnergySpent_Move = 0f;       // 累计移动消耗
        public float Lifetime_EnergySpent_Action = 0f;     // 累计行为消耗(进食/繁殖/攻击)

        // ==========================================
        // 【维度 3】 行为能力 (Action Profile)
        // ==========================================
        // 运行时会从 Config 读取并应用词缀修正
        [Header("--- 行为参数 ---")]
        [Tooltip("移动速度")]
        public float Move_Speed = 5.0f;

        [Tooltip("攻击伤害")]
        public float Attack_Damage = 10.0f;

        [Tooltip("攻击范围 (格子距离)")]
        public float Attack_Range = 1.5f;

        [Tooltip("攻击间隔 (秒)")]
        public float Attack_Cooldown = 2.0f;

        [Tooltip("上次攻击时间 (仿真时间)")]
        public float Last_Attack_Time = 0f; // 运行时记录

        // ==========================================
        // 【维度 4】 感知系统 (Senses)
        // ==========================================
        [Header("--- 感知系统 ---")]
        [Tooltip("视觉范围 (米)")]
        public float Vision_Range = 15.0f;

        [Tooltip("视觉角度 (度)")]
        public float Vision_Angle = 120.0f;

        [Tooltip("嗅觉灵敏度 (1.0 = 标准)")]
        public float Scent_Sensitivity = 1.0f;   // 1.0 = 标准

        [Tooltip("听觉阈值 (0.0 - 1.0)，越低越灵敏")]
        public float Hearing_Threshold = 0.2f;   // 越低越灵敏

        [Tooltip("同时关注的目标数量上限")]
        public int Attention_Cap = 3;            // 同时关注目标数

        //感知结果存储结构，供决策系统使用
        [Header("--- 感知结果 (运行时更新) ---")]
        [Tooltip("当前感知到的目标列表 (由 PerceptionSystem 每帧更新)")]
        public List<PerceivedTarget> PerceivedTargets = new List<PerceivedTarget>();

        // ==========================================
        // 决策状态 (由 DecisionSystem 更新)
        // ==========================================
        [Header("--- 决策状态 (由 DecisionSystem 更新) ---")]
        [Tooltip("当前行为状态")]
        public BehaviorState CurrentBehavior = BehaviorState.Idle;

        [Tooltip("目标位置 (用于 MovementSystem, null 表示无目标)")]
        public Vector2? TargetPosition = null; // nullable 类型

        [Tooltip("目标生物 UID (用于战斗/追捕, 为空表示无目标)")]
        public string TargetCreatureUID = null;

        [Tooltip("目标切换冷却时间 (防止疯狂切换目标)")]
        public float TargetSwitchCooldown = 2.0f; // 决策间隔

        // ==========================================
        // 【维度 5】 认知与决策 (Brain)
        // ==========================================
        [Header("Brain & Needs")]
        [Tooltip("饥饿需求 (0-100)，100极度饥饿")]
        public float Need_Hunger = 0f;

        public float Need_Safety = 100f;         // 100安全，0恐慌
        public float Need_Reproduction = 0f;

        [Header("--- 认知 ---")]
        [Tooltip("当前压力值 (0-100)")]
        public float Stress_Current = 0f;

        [Tooltip("压力阈值")]
        public float Stress_Panic_Threshold = 80f;   // 超过进本能模式

        [Tooltip("压力恢复阈值")]
        public float Stress_Recover_Threshold = 50f; // 低于回理性模式

        [Tooltip("记忆")]
        public List<MemoryPOI> LongTermMemories = new List<MemoryPOI>();

        [Tooltip("性格：攻击性 (0=温顺, 1=狂暴)")]
        [Range(0, 1)]
        public float Trait_Aggression = 0.5f;

        [Tooltip("性格：好奇心 (0=保守, 1=冒险)")]
        public float Trait_Curiosity = 0.5f;

        [Tooltip("性格：社交性 (0=独行, 1=群居)")]
        public float Trait_Tenacity = 0.5f;

        // ==========================================
        // 【维度 6 & 7】 遗传与演化 (Genetics)
        // ==========================================
        [Header("Genetics")]
        public List<string> ActiveAffixes = new List<string>();// 当前激活的词缀ID列表

        // B. 隐性潜力 (表观遗传进度) Key: AffixID, Value: Progress(0-100)
        // 由于Unity无法直接序列化Dictionary，建议在Inspector使用辅助List，代码里转Dict
        // 这里为了简单演示，假设你有处理Dictionary的工具，或者用两个List模拟
        [Tooltip("隐性潜力 (表观遗传)。这里存ID")]
        public List<string> Potential_Keys = new List<string>();// 词缀ID列表

        [Tooltip("隐性潜力进度值")]
        public List<float> Potential_Values = new List<float>();// 对应进度值列表

        [Tooltip("基因复杂度。词缀越多，此值越高，适应性越差")]
        public float Genetic_Complexity = 1.0f;         // 复杂度 (影响适应带宽)

        public float Genetic_Stability = 100f;          // 稳定性
        public float Reproductive_Compatibility = 0.7f; //  生殖隔离阈值
        public float Parent_Quality_Index = 1.0f;       //  亲代质量指数 (影响遗传质量)

        [Tooltip("温度适应区间 (在此区间外会扣血/增加代谢)")]
        public MinMaxRange Tolerance_Temp = new MinMaxRange { Min = -10, Max = 40 };
    }
}