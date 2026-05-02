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
    [System.Serializable]
    public class AffixJsonWrapper
    {
        public List<AffixDefinition> Affixes = new List<AffixDefinition>();
    }

    [CreateAssetMenu(fileName = "New_AffixDatabase", menuName = "Evolution Laws/Affix Database")]
    public class AffixDatabaseSO : ScriptableObject
    {
        [Header("数据源 (在下方挂载你的 JSON 文件)")]
        public TextAsset JsonSource;

        [Header("全局词缀配置表")]
        public List<AffixDefinition> Affixes = new List<AffixDefinition>();

        [ContextMenu("🔽 一键从 JSON 读取并覆盖列表 (Import from JSON)")]
        public void ImportFromJson()
        {
            if (JsonSource == null)
            {
                Debug.LogError("❌ 请先拖拽 JSON 文件到 JsonSource 槽位中！");
                return;
            }

            var db = JsonUtility.FromJson<AffixJsonWrapper>(JsonSource.text);
            if (db != null && db.Affixes != null)
            {
                Affixes = db.Affixes;
                Debug.Log($"[AffixDatabaseSO] 导入成功！共载入 {Affixes.Count} 个词缀。");
            }
            else
            {
                Debug.LogError("❌ 词缀导入失败！请检查 JSON 格式是否有误。");
            }
        }
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
        public List<string> GrantedTags = new List<string>();//到时候在生物的行为决策系统里可以根据这些标签来触发特殊行为

        // [维度7] 代价与限制
        public float Upkeep_Cost = 0f; // 代谢税 (每秒额外耗能)

        public List<string> Incompatible_IDs = new List<string>(); // 互斥词缀

        [Tooltip("结算时，若生物携带该词缀，将解锁对应的局外印记 (填入印记的ID，若为空则不产出印记)")]
        public string CorrespondingMarkID;// 结算奖励：对应的局外印记ID (如果有的话)

        // 条件规则 (例如: 在水中速度减半)
        public List<ContextRule> ContextRules = new List<ContextRule>();//到时候在生物的属性计算系统里可以根据这些规则来动态调整属性值
    }

    [System.Serializable]
    public struct ContextRule// 条件修正
    {
        public string ContextTag; //有什么特殊规则标签，到时候在其他系统里根据这个标签来判断是否触发这个规则
        public string StatAffected;// 受影响的是什么属性  e.g. "Speed", "Vision_Range"
        public float ModifierValue;// 属性的修正值  e.g. -0.5f (速度减半), +10f (视野增加10)
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