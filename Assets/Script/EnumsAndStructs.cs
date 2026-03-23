using System.Collections.Generic;
using UnityEngine;

//存放会用到的数据，用于修改
//所以之后需要对生物进行增加属性时，可以在这里添加对应的枚举和结构体
namespace EvolutionLaws.Data
{
    // --- 核心枚举定义 ---

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
}