using UnityEngine;

namespace EvolutionLaws.Core
{
    public enum ChoiceType
    {
        AddEnergy,          // 增加神力能量
        GlobalTempDrop,     // 降低全球温度
        SpawnFood           // 随机天降食物
        // 未来可以无限拓展：UnlockAffix, KillHalfPopulation 等
    }

    [CreateAssetMenu(fileName = "NewChoiceEvent", menuName = "Evolution Laws/Choice Event")]
    public class ChoiceEventData : ScriptableObject
    {
        public string ChoiceName;

        [TextArea(2, 5)]
        public string Description;

        public ChoiceType EffectType;

        [Tooltip("效果数值 (例如增加的能量值, 或降温的具体度数)")]
        public float EffectValue;
    }
}