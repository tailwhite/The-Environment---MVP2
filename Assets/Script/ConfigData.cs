using System.Collections.Generic;
using UnityEngine;

// 词缀与潜力的配置数据
//定义词缀应该是什么样子的
/*整个词缀系统的设计思路是这样的：
 * 《定义词缀是什么样的》——>《定义词缀如何影响生物属性》————数据定义层面
 * 《存放所有词缀的词典》
 */

namespace EvolutionLaws.Data
{
    [CreateAssetMenu(fileName = "New_AffixDatabase", menuName = "Evolution Laws/Affix Database")]
    public class AffixDatabaseSO : ScriptableObject
    {
        [Header("全局词缀配置表")]
        public List<AffixDefinition> Affixes = new List<AffixDefinition>();
    }

    // 词缀的静态定义 (规则书)
    [System.Serializable]
    public class AffixDefinition// 词缀定义类——————用来定义词缀的属性和效果
    {
        public string ID;              // 需要有id——"Thick_Fur"
        public string DisplayName;// 需要有展示的名称——"厚实的皮毛"
        public AffixType Type;// 需要有分类——————结构/代谢/神经/缺陷

        [TextArea]//在unity inspector显示多行文本框
        public string Description;// 词缀描述

        // [属性层面]：用你已经写好的 StatModifier 进行属性修饰
        public List<StatModifier> Modifiers = new List<StatModifier>();

        // 【行为层面】：赋予生物的特殊能力标签！(如 "Photosynthesis", "NightVision")
        public List<string> GrantedTags = new List<string>();

        // [维度7] 代价与限制
        public float Upkeep_Cost = 0f; // 代谢税 (每秒额外耗能)

        public List<string> Incompatible_IDs = new List<string>(); // 互斥词缀

        // 条件规则 (例如: 在水中速度减半)
        public List<ContextRule> ContextRules = new List<ContextRule>();
    }

    [System.Serializable]
    public struct ContextRule// 条件修正
    {
        public string ContextTag; //规则标签  e.g. "In_Water", "In_Darkness"
        public string StatAffected;// 受影响的属性  e.g. "Speed", "Vision_Range"
        public float ModifierValue;// 修正值  e.g. 0.5 (表示减半)
    }

    // 潜力触发映射 (表观遗传规则)
    [System.Serializable]
    public class PotentialTriggerConfig
    {
        // 触发事件ID (如 "Damage_Cold") -> 对应的潜力ID (如 "Thick_Fur")
        // 为了在Inspector中编辑，使用List包裹Struct————————方便到时候在编辑器中进行选择
        public List<TriggerMapping> Mappings = new List<TriggerMapping>();
    }

    [System.Serializable]
    public struct TriggerMapping
    {
        public string TriggerID;// 触发事件ID
        public string PotentialID;// 对应潜力ID
        public float GrowthMultiplier; // 增长系数
    }
}