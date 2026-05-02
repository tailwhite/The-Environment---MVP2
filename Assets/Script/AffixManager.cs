using System.Collections.Generic;
using EvolutionLaws.Data;
using UnityEngine;

namespace EvolutionLaws.Config
{
    [System.Serializable]
    public class AffixDatabaseWrapper
    {
        public List<AffixDefinition> Items;
    }

    public static class AffixManager
    {
        private static Dictionary<string, AffixDefinition> _database = new Dictionary<string, AffixDefinition>();

        // 互斥关系预编译缓存 (双向哈希表)
        private static Dictionary<string, HashSet<string>> _incompatibilityMap = new Dictionary<string, HashSet<string>>();

        // 标记是否已经初始化，防止重复读取
        private static bool _isInitialized = false;

        public static void Initialize()
        {
            if (_isInitialized) return;

            _database.Clear();
            _incompatibilityMap.Clear();

            // 重构：从 Resources 加载配好的 ScriptableObject 数据
            // (请确保在 Unity 的 Resources 文件夹中右键 Create -> Evolution Laws -> Affix Database，并命名为 "MainAffixDatabase")
            AffixDatabaseSO dbSO = Resources.Load<AffixDatabaseSO>("MainAffixDatabase");

            if (dbSO != null && dbSO.Affixes != null)
            {
                foreach (var def in dbSO.Affixes)
                {
                    if (!string.IsNullOrEmpty(def.ID))
                    {
                        RegisterAffix(def);
                    }
                }
            }
            else
            {
                Debug.LogError("❌ [AffixManager] 无法在 Resources 目录下找到名为 'MainAffixDatabase' 的词缀数据库!");
            }

            // 预编译双向排斥网
            BuildIncompatibilityMap();
            _isInitialized = true;

            Debug.Log($"[AffixManager] 词缀数据库初始化完成，共载入 {_database.Count} 个词缀");
        }

        public static void RegisterAffix(AffixDefinition def)
        {
            if (!_database.ContainsKey(def.ID))
                _database.Add(def.ID, def);
            else
                Debug.LogWarning($"[AffixManager] 词缀 ID 重复被跳过: {def.ID}");
        }

        // 构建双向关系图
        private static void BuildIncompatibilityMap()
        {
            foreach (var kvp in _database)
            {
                string idA = kvp.Key;
                _incompatibilityMap[idA] = new HashSet<string>();

                foreach (string idB in kvp.Value.Incompatible_IDs)
                {
                    if (string.IsNullOrEmpty(idB)) continue;

                    // A 记录排斥 B
                    _incompatibilityMap[idA].Add(idB);

                    // 主动让 B 记录排斥 A (如果数据库里B存在的话)
                    if (!_incompatibilityMap.ContainsKey(idB))
                        _incompatibilityMap[idB] = new HashSet<string>();

                    _incompatibilityMap[idB].Add(idA);
                }
            }
        }

        //O(1) 高效判断任意两个词缀是否互斥
        public static bool AreIncompatible(string affixA, string affixB)
        {
            if (_incompatibilityMap.TryGetValue(affixA, out var set))
            {
                return set.Contains(affixB);
            }
            return false;
        }

        public static Dictionary<string, AffixDefinition> GetDatabase() => _database;

        public static AffixDefinition GetAffix(string id)
        {
            _database.TryGetValue(id, out var def);
            return def;
        }
    }
}