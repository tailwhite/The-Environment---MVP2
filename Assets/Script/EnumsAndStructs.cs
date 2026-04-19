using System.Collections.Generic;
using UnityEngine;

//存放会用到的数据，用于修改
//所以之后需要对生物进行增加属性时，可以在这里添加对应的枚举和结构体
namespace EvolutionLaws.Data
{
    // --- 核心枚举定义 ---
    public enum DeathCause
    {
        None,
        Starvation, // 饿死 (脂肪耗尽导致免疫归零)
        Killed,     // 被杀 (物理结构被打破/咬死)
        Environment,// 环境 (温度/毒性等导致免疫归零)
        OldAge      // 老死 (到达寿命极限)
    }

    public enum LifeStage// 生命周期阶段
    {
        Larva,  // 幼年
        Adult,  // 成年
        Elder   // 老年
    }

    public enum ResourceType// 资源类型——有哪些资源
    {
        None,
        Plant_Fiber, // 植物纤维
        Meat,        // 肉
        Carrion,     // 腐肉
        Mineral      // 矿物 (特殊进食)
    }

    public enum AffixType//词缀类型——有哪些词缀
    {
        Structural, // 改变物理结构 (如：硬壳)
        Metabolic,  // 改变代谢 (如：快速消化)
        Neural,     // 改变感知/AI (如：夜视)
        Defect      // 缺陷 (如：脆骨)
    }

    public enum TargetType//感知到的目标类型 (用于决策系统)
    {
        None,           // 无效目标
        FoodResource,   // 食物资源 (植物/矿物/肉块)
        Predator,       // 捕食者 (威胁)
        Prey,           // 猎物 (未来肉食动物用)
        Ally,           // 同类/盟友
        Corpse,         // 尸体 (未来用)
        Danger          // 环境危险 (火/毒/陷阱)
    }

    public enum BehaviorState//生物的行为状态
    {
        Idle,           // 闲逛 (默认状态,随机漫游)
        Foraging,       // 觅食 (主动走向食物)
        Fleeing,        // 逃跑 (远离威胁)
        Hunting,        // 狩猎 (追捕猎物) - 未来肉食动物用
        Resting,        // 休息 (停止移动,恢复体力)
        Socializing     // 社交 (靠近同类) - 未来群居动物用
    }

    // --- 基础数据结构 ---

    // 属性修正器 (用于词缀)
    [System.Serializable]
    public struct StatModifier
    {
        public string StatName; // 例如 "Max_Health", "Speed"
        public float Value;     // 例如 10.0
        public bool IsMultiplier; // true表示乘法(+10%)，false表示加法(+10)
    }

    // 记忆中的兴趣点 (POI)
    [System.Serializable]
    public class MemoryPOI
    {
        public Vector2 Position;
        public ResourceType Type;
        public float Confidence; // 置信度 (0.0 - 1.0)，随时间衰减
        public float Timestamp;  // 发现时间
    }

    //感知到的目标数据结构
    [System.Serializable]
    public struct PerceivedTarget
    {
        public TargetType Type;         // 目标类型
        public Vector2 Position;        // 目标位置 (世界坐标)
        public string UID;              // 目标UID (如果是生物, 否则为空)
        public ResourceType ResourceType; // 资源类型 (如果是食物)
        public float Distance;          // 距离
        public float Attractiveness;    // 吸引力 (0-1, 用于决策排序)
        public float Threat;            // 威胁度 (0-1, 用于逃跑决策)
        public float LastSeenTime;      // 上次感知时间 (仿真时间)
    }

    // 动作消耗配置 (双重消耗)
    [System.Serializable]
    public struct ActionCostData
    {
        public float Energy;    // 消耗体力
        public float Structure; // 磨损结构
        public float Vitality;  // 损伤健康
    }

    // 环境容忍区间 (维度7: 适应性带宽)
    [System.Serializable]
    public struct MinMaxRange
    {
        public float Min;
        public float Max;

        public bool IsInRange(float value)//判断数值是否在区间内
        {
            return value >= Min && value <= Max;
        }
    }

    // ==========================================
    // 蓝图配置结构体 (数据降维)
    // ==========================================
    [System.Serializable]
    public struct PhysiologyConfig
    {
        [Tooltip("结构完整度上限 (物理血量)")]
        public float MaxHealth;

        [Tooltip("质量以及碰撞体积大小倍率")]
        public float Mass;

        [Tooltip("体型系数 (1.0 = 标准)")]
        public float Size;

        [Tooltip("瞬时体力上限 (用于奔跑和攻击)")]
        public float MaxEnergy;

        [Tooltip("长期营养储备上限 (脂肪，影响抗饿能力)")]
        public float MaxNutrients;

        [Tooltip("基础耗能乘数 (待机时的能量流失速度)")]
        public float BaseMetabolicRate;
    }

    [System.Serializable]
    public struct ActionConfig
    {
        [Tooltip("移动速度 (米/秒)")]
        public float MoveSpeed;

        [Tooltip("攻击伤害 (基础值)")]
        public float AttackDamage;

        [Tooltip("攻击范围")]
        public float AttackRange;

        [Tooltip("攻击冷却时间")]
        public float AttackCooldown;
    }

    [System.Serializable]
    public struct SenseConfig
    {
        [Tooltip("视觉范围 (米)")]
        public float VisionRange;

        [Tooltip("视觉角度 (度)")]
        public float VisionAngle;

        [Tooltip("嗅觉灵敏度 (1.0 = 标准)")]
        public float ScentSensitivity;   // 1.0 = 标准

        [Tooltip("听觉阈值 (0.0 - 1.0)，越低越灵敏")]
        public float HearingThreshold;   // 越低越灵敏

        [Tooltip("同时关注的目标数量上限")]
        public int AttentionCap;            // 同时关注目标数
    }

    [System.Serializable]
    public struct BrainConfig
    {
        [Tooltip("压力恐慌阈值 (0.0 - 1.0)，超过后进入恐慌状态")]
        public float StressPanicThreshold;

        [Tooltip("压力恢复阈值 (0.0 - 1.0)，低于后退出恐慌状态")]
        public float StressRecoverThreshold;

        [Tooltip("性格：攻击性 (0=温顺, 1=狂暴)")]
        [Range(0, 1)]
        public float TraitAggression;

        [Range(0, 1)]
        [Tooltip("性格：好奇心 (0=保守, 1=冒险)")]
        public float TraitCuriosity;

        [Range(0, 1)]
        [Tooltip("性格：社交性 (0=独行, 1=群居)")]
        public float TraitTenacity;
    }

    [System.Serializable]
    public struct ReproductionConfig
    {
        [Tooltip("成熟年龄 (秒) - 达到此年龄才能繁殖")]
        public float MaturityAge;

        [Tooltip("最大寿命 (秒) - 达到此年龄后死亡概率增加")]
        public float MaxLifespan;

        [Tooltip("怀孕时长 (秒)")]
        public float PregnancyDuration;

        [Tooltip("繁殖冷却时间 (秒) - 繁殖后需要冷却才能再次繁殖")]
        public float ReproductionCooldown;

        [Tooltip("繁衍后代个数")]
        public MinMaxRange OffspringCount;

        [Tooltip("基因突变率 (0.0 - 1.0)，每个基因有此概率发生突变")]
        [Range(0f, 1f)] public float MutationRate;
    }
}