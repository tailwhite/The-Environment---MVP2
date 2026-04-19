using System.Collections.Generic;
using UnityEngine;

namespace EvolutionLaws.Meta
{
    // 定义单个局外物品（潜能/印记）在 JSON 中的样子
    [System.Serializable]
    public class MetaItemConfig
    {
        public string ID;
        public string NameCN;
        public string DescriptionCN;

        //只有装备了这个潜能，局内才允许演化出以下这些词缀ID
        public List<string> UnlockableAffixes;
    }

    // 包装类，用于读取 JSON 数组
    [System.Serializable]
    public class MetaConfigDatabase
    {
        public List<MetaItemConfig> Potentials;
        public List<MetaItemConfig> Marks;
    }

    public static class MetaConfigManager
    {
        private static Dictionary<string, MetaItemConfig> _potentialsDict = new Dictionary<string, MetaItemConfig>();
        private static Dictionary<string, MetaItemConfig> _marksDict = new Dictionary<string, MetaItemConfig>();

        public static void Initialize(string jsonContent)
        {
            _potentialsDict.Clear();
            _marksDict.Clear();

            var db = JsonUtility.FromJson<MetaConfigDatabase>(jsonContent);
            if (db != null)
            {
                if (db.Potentials != null)
                    foreach (var p in db.Potentials) _potentialsDict[p.ID] = p;

                if (db.Marks != null)
                    foreach (var m in db.Marks) _marksDict[m.ID] = m;
            }
        }

        // 提供给 UI 查找中文名的接口
        public static string GetPotentialName(string id)
        {
            return _potentialsDict.TryGetValue(id, out var cfg) ? cfg.NameCN : id;
        }

        public static string GetMarkName(string id)
        {
            return _marksDict.TryGetValue(id, out var cfg) ? cfg.NameCN : id;
        }

        public static bool CanPotentialUnlockAffix(string potentialID, string affixID)
        {
            if (_potentialsDict.TryGetValue(potentialID, out var cfg))
            {
                if (cfg.UnlockableAffixes != null && cfg.UnlockableAffixes.Contains(affixID))
                    return true;
            }
            return false;
        }
    }
}