using UnityEngine;
using TMPro;
using EvolutionLaws.Core;

namespace EvolutionLaws.UI
{
    public class SimulationHUD : MonoBehaviour
    {
        [Header("状态显示文本")]
        public TextMeshProUGUI EnergyText;//上帝点数

        public TextMeshProUGUI TimeSpeedText;//时间流速
        public TextMeshProUGUI CurrentSkillText;//当前手持技能

        // 缓存上一次的状态，用于差异对比 (Dirty Check 性能优化)
        private float _lastEnergy = -1f;

        private float _lastTimeScale = -1f;
        private bool _lastIsPaused = false;
        private PlayerGodPowerSystem.SelectedSkill _lastSkill = (PlayerGodPowerSystem.SelectedSkill)(-1);

        // 每帧或者按需刷新界面
        private void Update()
        {
            var sim = SimulationManager.Instance;
            if (sim == null) return;

            // 1. 刷新能量 (仅当数值实质发生变化时才做字符串拼接，大幅减少 GC 垃圾)
            if (EnergyText != null && _lastEnergy != sim.GodPower.CurrentEnergy)
            {
                _lastEnergy = sim.GodPower.CurrentEnergy;
                EnergyText.text = $"能量: {sim.GodPower.CurrentEnergy:F0} / {sim.GodPower.MaxEnergy}";
            }

            // 2. 刷新时间流速
            if (TimeSpeedText != null && (_lastTimeScale != sim.CurrentTimeScale || _lastIsPaused != sim.IsPaused))
            {
                _lastTimeScale = sim.CurrentTimeScale;
                _lastIsPaused = sim.IsPaused;
                TimeSpeedText.text = sim.IsPaused ? "已暂停" : $"当前流速: {sim.CurrentTimeScale}x";
            }

            // 3. 刷新当前手持技能
            if (CurrentSkillText != null && _lastSkill != sim.GodPower.CurrentSkill)
            {
                _lastSkill = sim.GodPower.CurrentSkill;
                string skillName = _lastSkill switch
                {
                    PlayerGodPowerSystem.SelectedSkill.Chill => "微寒试炼 (耗能:50)",
                    PlayerGodPowerSystem.SelectedSkill.Ark => "地热方舟 (耗能:300)",
                    PlayerGodPowerSystem.SelectedSkill.Feed => "生命甘霖 (耗能:20)",
                    _ => "空手"
                };
                CurrentSkillText.text = $"当前神迹: {skillName}";
            }
        }

        // ======================= 绑定给 UI 按钮的事件 =======================

        // 速度控制按钮
        public void OnClickSpeedPause() => SimulationManager.Instance?.TogglePause();

        public void OnClickSpeed1x() => SimulationManager.Instance?.SetTimeScale(1.0f);

        public void OnClickSpeed5x() => SimulationManager.Instance?.SetTimeScale(5.0f);

        public void OnClickSpeedMax() => SimulationManager.Instance?.SetTimeScale(50.0f);

        // 技能选择按钮
        public void OnSelectSkillChill() => SetSkill(PlayerGodPowerSystem.SelectedSkill.Chill);//微寒试炼

        public void OnSelectSkillArk() => SetSkill(PlayerGodPowerSystem.SelectedSkill.Ark);//地热方舟

        public void OnSelectSkillFeed() => SetSkill(PlayerGodPowerSystem.SelectedSkill.Feed);

        public void OnUnequipSkill() => SetSkill(PlayerGodPowerSystem.SelectedSkill.None);//无技能

        private void SetSkill(PlayerGodPowerSystem.SelectedSkill skill)
        {
            if (SimulationManager.Instance != null)
            {
                SimulationManager.Instance.GodPower.CurrentSkill = skill;
            }
        }
    }
}