using System.Collections.Generic;
using UnityEngine;

namespace EvolutionLaws.Meta
{
    // 定义单个局外物品（潜能/印记）的基础结构
    [System.Serializable]
    public class MetaItemConfig
    {
        public string ID;// 唯一标识符，必须与局内使用的 ID 完全一致（区分大小写）
        public string NameCN;// 中文名称，用于 UI 显示

        [TextArea(2, 4)]
        public string DescriptionCN;// 中文描述，支持多行文本

        // 只有装备了这个潜能，局内才允许演化出以下这些词缀ID
        public List<string> UnlockableAffixes = new List<string>();// 例如 "Fire_Resistance", "Swift_Strike" 等
    }

    // ? 数据载体本身，类名必须和文件名 MetaDatabaseSO.cs 完全一致！
    [CreateAssetMenu(fileName = "New_MetaDatabase", menuName = "Evolution Laws/Meta Database")]
    public class MetaDatabaseSO : ScriptableObject
    {
        [Header("数据源 (在此挂载你的 JSON 文件)")]
        public TextAsset JsonSource;

        [Header("潜能列表")]
        public List<MetaItemConfig> Potentials = new List<MetaItemConfig>();

        [Header("印记列表")]
        public List<MetaItemConfig> Marks = new List<MetaItemConfig>();

        [ContextMenu("🔽 一键从 JSON 读取并覆盖列表 (Import from JSON)")]
        public void ImportFromJson()
        {
            if (JsonSource == null)
            {
                Debug.LogError("❌ 请先拖拽 JSON 文件到 JsonSource 槽位中！");
                return;
            }

            // 【神级API】：直接把 JSON 数据暴力覆盖给当前这个 SO 对象！
            JsonUtility.FromJsonOverwrite(JsonSource.text, this);

            Debug.Log($"[MetaDatabaseSO] 导入成功！共载入 {Potentials.Count} 个潜能，{Marks.Count} 个印记。");
        }
    }
}