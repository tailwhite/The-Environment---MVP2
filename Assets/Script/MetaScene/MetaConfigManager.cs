using System.Collections.Generic;
using UnityEngine;

namespace EvolutionLaws.Meta
{
    public static class MetaConfigManager
    {
        private static Dictionary<string, MetaItemConfig> _potentialsDict = new Dictionary<string, MetaItemConfig>();
        private static Dictionary<string, MetaItemConfig> _marksDict = new Dictionary<string, MetaItemConfig>();
        private static bool _isInitialized = false;

        public static void Initialize()
        {
            if (_isInitialized) return;

            _potentialsDict.Clear();
            _marksDict.Clear();

            // 从资源文件夹加载刚才生成并烘焙好的 SO
            MetaDatabaseSO dbSO = Resources.Load<MetaDatabaseSO>("MainMetaDatabase");

            if (dbSO != null)
            {
                foreach (var p in dbSO.Potentials) _potentialsDict[p.ID] = p;
                foreach (var m in dbSO.Marks) _marksDict[m.ID] = m;
                _isInitialized = true;
                Debug.Log($"[MetaConfigManager] 局外配置初始化完毕。潜能:{_potentialsDict.Count}，印记:{_marksDict.Count}");
            }
            else
            {
                Debug.LogError("❌ [MetaConfigManager] 找不到名为 'MainMetaDatabase' 的配置 SO！");
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