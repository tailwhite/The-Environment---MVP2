using System.Collections.Generic;
using UnityEngine;
using System.IO;

namespace EvolutionLaws.Meta
{
    [System.Serializable]
    public class MetaSaveData
    {
        // 1. 玩家资产（永久保留）
        public int ResearchPoints = 0;

        public int MaxPopulationLevel = 0;
        public List<string> UnlockedMarks = new List<string>();//解锁的先祖印记列表，玩家每次灭绝后可能会解锁新的印记，保存在这里
        public List<string> UnlockedPotentials = new List<string>();// 你可以在这里预设一些默认解锁的潜能，比如“生存适应”潜能，保证玩家一开始就有一些选择

        // 【新增】地图种子。0 代表随机生成
        public int MapSeed = 0;

        // 2. 本局出战配置（Loadout，带进下一局）
        public List<string> EquippedMarks = new List<string>();

        public List<string> EquippedPotentials = new List<string>();

        // 记录出战分配，Key: 物种ID (SpeciesID), Value: 分配数量
        public Dictionary<string, int> DeployedSpecies = new Dictionary<string, int>();

        public int GetStartingPopulation()
        {
            return 50 + (MaxPopulationLevel * 10);
        }

        // 3. 初始化默认值
        public void InitDefault()
        {
            // 默认给玩家解锁并装备一个生存适应潜能
            if (!UnlockedPotentials.Contains("Survival_Potential_Base"))
                UnlockedPotentials.Add("Survival_Potential_Base");

            if (!EquippedPotentials.Contains("Survival_Potential_Base"))
                EquippedPotentials.Add("Survival_Potential_Base");

            // 印记默认为空
        }
    }

    public static class MetaDataManager
    {
        public static MetaSaveData Current { get; private set; }
        private static string SaveFilePath => Application.persistentDataPath + "/EvolutionMetaSave.json";

        public static void Load()
        {
            if (File.Exists(SaveFilePath))
            {
                Current = JsonUtility.FromJson<MetaSaveData>(File.ReadAllText(SaveFilePath));
                // 兜底检查（防止旧存档报错）
                if (Current.UnlockedPotentials.Count == 0) Current.InitDefault();
            }
            else
            {
                Current = new MetaSaveData();
                Current.InitDefault();
                Save();
            }
        }

        public static void Save()
        {
            if (Current == null)
            {
                Current = new MetaSaveData();
                Current.InitDefault();
            }
            File.WriteAllText(SaveFilePath, JsonUtility.ToJson(Current, true));
        }
    }
}