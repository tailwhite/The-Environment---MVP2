using UnityEngine;

namespace EvolutionLaws.Core
{
    /// <summary>
    /// 【抉择效果处理器】
    /// 集中解析 ChoiceType 并执行对应的游戏逻辑
    /// </summary>
    public static class ChoiceEffectHandler
    {
        public static void ApplyEffect(ChoiceEventData choice)
        {
            if (SimulationManager.Instance == null) return;
            var sim = SimulationManager.Instance;

            switch (choice.EffectType)
            {
                case ChoiceType.AddEnergy:
                    sim.GodPower.AddEnergy(choice.EffectValue, choice.ChoiceName);
                    break;

                case ChoiceType.GlobalTempDrop:
                    // 改变全局基础温度
                    sim.Environment.Global_Temperature += choice.EffectValue;
                    Debug.Log($"[抉择效果] 全局温度改变了 {choice.EffectValue} 度，当前基础温度: {sim.Environment.Global_Temperature}");
                    break;

                case ChoiceType.SpawnFood:
                    // 随机生成数坨高能食物
                    int spawnCount = Mathf.RoundToInt(choice.EffectValue);
                    for (int i = 0; i < spawnCount; i++)
                    {
                        int rx = Random.Range(0, sim.Environment.Width);
                        int ry = Random.Range(0, sim.Environment.Height);
                        var tile = sim.Environment.GetTile(rx, ry);
                        if (tile != null)
                        {
                            tile.Biomass_Plant += 500f; // 大补的植物
                            tile.Biomass_Meat += 100f;  // 顺带一点肉
                        }
                    }
                    Debug.Log($"[抉择效果] 天将甘霖，散落了 {spawnCount} 份高能食物！");
                    break;

                default:
                    Debug.LogWarning($"[抉择效果] 未实现的效果类型: {choice.EffectType}");
                    break;
            }
        }
    }
}