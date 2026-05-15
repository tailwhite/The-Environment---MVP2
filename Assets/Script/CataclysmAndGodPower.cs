using EvolutionLaws.Data;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.EventSystems;

namespace EvolutionLaws.Core
{
    public enum CataclysmState
    { Idle, Foreboding, Outbreak, Aftermath }

    /// <summary>
    /// 【天灾导演系统】负责按时间轴修改环境。
    /// </summary>
    public class CataclysmDirector
    {
        public CataclysmState CurrentState = CataclysmState.Idle;

        // MVP测试用：时间较短方便观察
        private float _forebodingStartTime = 20f; // 生成局部寒流 (测试用)

        private float _outbreakStartTime = 400f;   // 全球冰河期
        private float _aftermathStartTime = 800f;  // 结束灾难

        private Vector2Int _anomalyCenter;
        private float _anomalyRadius = 15f;

        // 新增：判断是否触发过体验测试的事件选项
        private bool _mvpChoiceTriggered = false;

        public void Tick(EnvironmentData environment, float globalTime)
        {
            if (!_mvpChoiceTriggered && globalTime >= 10f)
            {
                _mvpChoiceTriggered = true;
                TriggerMVPChoiceEvent();
            }
            if (CurrentState == CataclysmState.Idle && globalTime >= _forebodingStartTime)
            {
                TriggerForeboding(environment);
            }
            else if (CurrentState == CataclysmState.Foreboding && globalTime >= _outbreakStartTime)
            {
                TriggerOutbreak(environment);
            }
            else if (CurrentState == CataclysmState.Outbreak && globalTime >= _aftermathStartTime)
            {
                TriggerAftermath(environment);
            }
        }

        private void TriggerForeboding(EnvironmentData environment)
        {
            CurrentState = CataclysmState.Foreboding;
            Debug.LogWarning("<b>[天灾导演] ⚠️ 冰河前兆：地图右上角气温开始骤降！</b>");

            // UI 通知
            EvolutionLaws.UI.NotificationUIManager.Instance?.AddLogMessage("【预警】冰河前兆：局部区域气温开始骤降！", new Color(1f, 0.6f, 0f));

            // 选取右上角作为试炼场
            _anomalyCenter = new Vector2Int(Mathf.RoundToInt(environment.Width * 0.8f), Mathf.RoundToInt(environment.Height * 0.8f));

            for (int x = 0; x < environment.Width; x++)
            {
                for (int y = 0; y < environment.Height; y++)
                {
                    float dist = Vector2Int.Distance(new Vector2Int(x, y), _anomalyCenter);
                    if (dist <= _anomalyRadius)
                    {
                        var tile = environment.GetTile(x, y);
                        // 越靠近中心越冷 (最低压到 -30 度左右)
                        tile.Temperature_Offset = -30f * (1f - dist / _anomalyRadius);
                    }
                }
            }
        }

        private void TriggerOutbreak(EnvironmentData environment)
        {
            CurrentState = CataclysmState.Outbreak;
            Debug.LogWarning("<b>[天灾导演] ❄️ 灾难爆发：全球进入极寒冰河期 (-20°C)！</b>");
            environment.Global_Temperature = -20f;//温度降低20度
            // UI 中央拉大横幅警告 + 侧边记录
            EvolutionLaws.UI.NotificationUIManager.Instance?.ShowCenterAlert("极寒世代降临", "环境气温骤降，适者生存！", Color.cyan);
            EvolutionLaws.UI.NotificationUIManager.Instance?.AddLogMessage("【警告】极寒世代爆发，全球进入冰河期 (-20°C)！", Color.cyan);
        }

        private void TriggerAftermath(EnvironmentData environment)
        {
            CurrentState = CataclysmState.Aftermath;
            Debug.Log("<b>[天灾导演] 🌤️ 灾难退潮：气温开始回暖。</b>");
            environment.Global_Temperature = 20f;

            foreach (var tile in environment.Grid) tile.Temperature_Offset = 0f;
            // UI 宣告危机解除
            EvolutionLaws.UI.NotificationUIManager.Instance?.ShowCenterAlert("冰雪消融", "气温开始回暖，幸存者迎来了新生。", Color.green);
            EvolutionLaws.UI.NotificationUIManager.Instance?.AddLogMessage("【复苏】天灾退潮，全球气温逐渐恢复常态。", Color.green);
        }

        private void TriggerMVPChoiceEvent()
        {
            var sim = SimulationManager.Instance;
            List<ChoiceEventData> choiceList;

            if (sim != null && sim.MVPChoices != null && sim.MVPChoices.Count > 0)
            {
                choiceList = sim.MVPChoices;
            }
            else
            {
                // 运行时动态创建几个选项供你测试。未来你可以通过 Inspector 面板拖拽配好的 SO 资源进来
                var choice1 = ScriptableObject.CreateInstance<ChoiceEventData>();
                choice1.ChoiceName = "神明的馈赠";
                choice1.Description = "立刻获得 300 点造物能量，用于强行干预后续演化。";
                choice1.EffectType = ChoiceType.AddEnergy;
                choice1.EffectValue = 300f;

                var choice2 = ScriptableObject.CreateInstance<ChoiceEventData>();
                choice2.ChoiceName = "寒霜突袭";
                choice2.Description = "提早迎来严寒！全球温度瞬间下降 20°C，只有强者才能生存！";
                choice2.EffectType = ChoiceType.GlobalTempDrop;
                choice2.EffectValue = -20f;

                var choice3 = ScriptableObject.CreateInstance<ChoiceEventData>();
                choice3.ChoiceName = "丰饶降临";
                choice3.Description = "在世界各地随机散落 12 份高能食物，引爆种群繁衍潮。";
                choice3.EffectType = ChoiceType.SpawnFood;
                choice3.EffectValue = 12f;

                choiceList = new List<ChoiceEventData> { choice1, choice2, choice3 };
            }

            if (EvolutionLaws.UI.ChoiceUIManager.Instance != null)
            {
                EvolutionLaws.UI.ChoiceUIManager.Instance.ShowChoices(choiceList);
            }
            else
            {
                Debug.LogWarning("[天灾导演] 无法弹出抉择UI，场景中未找到 ChoiceUIManager 实例！");
            }
        }
    }

    // 用于记录有持续时间的环境修改效果
    public class TempAnaomaly
    {
        public Vector2Int CenterPos;
        public int Radius;
        public float TargetTempOffset;
        public float RemainingTime;
    }

    /// <summary>
    /// 【上帝操纵系统 (God Power System)】
    /// 职责: 管理玩家能量池、处理鼠标点击释放神迹、维护持续性环境异常
    /// </summary>
    public class PlayerGodPowerSystem
    {
        // === 能量池设定 ===
        public float CurrentEnergy { get; private set; } = 300f; // 初始 300 点

        public float MaxEnergy = 1000f;

        // === 当前选中的技能 ===
        public enum SelectedSkill
        { None, Chill, Ark, Feed }

        public SelectedSkill CurrentSkill = SelectedSkill.None;

        // 持续性区域温度异常列表
        private List<TempAnaomaly> _activeAnomalies = new List<TempAnaomaly>();

        // ==========================================
        // 能量获取接口 (供外部系统调用)
        // ==========================================
        public void AddEnergy(float amount, string reason)
        {
            CurrentEnergy = Mathf.Clamp(CurrentEnergy + amount, 0, MaxEnergy);
            // 建议未来把这个做到 UI 的小飘字上
            Debug.Log($"<color=cyan>[上帝能量] +{amount} ({reason}) -> 当前: {CurrentEnergy:F0}</color>");
        }

        public bool TryConsumeEnergy(float amount)
        {
            if (CurrentEnergy >= amount)
            {
                CurrentEnergy -= amount;
                return true;
            }
            return false;
        }

        // ==========================================
        // 主键盘输入 & 持续性效果 Tick
        // ==========================================
        public void TickAndHandleInput(EnvironmentData environment, Camera mainCamera, float deltaTime)
        {
            HandleKeyboardSelection();
            HandleMouseClick(environment, mainCamera);
            UpdateAnomalies(environment, deltaTime);
        }

        private void HandleKeyboardSelection()
        {
            if (Input.GetKeyDown(KeyCode.Z))
            {
                CurrentSkill = SelectedSkill.Chill;
                Debug.Log("[上帝之手] 准备释放：微寒试炼 (耗能:50)");
            }
            if (Input.GetKeyDown(KeyCode.X))
            {
                CurrentSkill = SelectedSkill.Ark;
                Debug.Log("[上帝之手] 准备释放：地热方舟 (耗能:300)");
            }
            if (Input.GetKeyDown(KeyCode.C))
            {
                CurrentSkill = SelectedSkill.Feed;
                Debug.Log("[上帝之手] 准备释放：生命甘霖 (耗能:20)");
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CurrentSkill = SelectedSkill.None;
            }
        }

        private void HandleMouseClick(EnvironmentData environment, Camera mainCamera)
        {
            if (Input.GetMouseButtonDown(0) && CurrentSkill != SelectedSkill.None) // 左键释放技能
            {
                // 防穿透检测: 放置技能时，不能点到了底部的UI面板
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    return;

                Vector3 mouseWorldPos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
                int tileX = Mathf.FloorToInt(mouseWorldPos.x);
                int tileY = Mathf.FloorToInt(mouseWorldPos.y);

                if (tileX < 0 || tileX >= environment.Width || tileY < 0 || tileY >= environment.Height) return; // 点到地图外

                switch (CurrentSkill)
                {
                    case SelectedSkill.Chill: CastChill(environment, tileX, tileY); break;
                    case SelectedSkill.Ark: CastArk(environment, tileX, tileY); break;
                    case SelectedSkill.Feed: CastFeed(environment, tileX, tileY); break;
                }

                // 释放完如果是大招/试炼，切回空手状态防误触；投喂可以连点，不切。
                if (CurrentSkill != SelectedSkill.Feed) CurrentSkill = SelectedSkill.None;
            }
        }

        // ==========================================
        // 具体技能效果
        // ==========================================
        private void CastChill(EnvironmentData env, int x, int y)
        {
            if (!TryConsumeEnergy(50f)) { Debug.LogWarning("能量不足！需要 50 点"); return; }

            // 范围 3x3 (半径1)，降温 -20，持续 30s
            _activeAnomalies.Add(new TempAnaomaly { CenterPos = new Vector2Int(x, y), Radius = 1, TargetTempOffset = -30f, RemainingTime = 30f });
            ApplyAnomalyToGrid(env, _activeAnomalies[_activeAnomalies.Count - 1], true);
            Debug.Log($"<color=blue>[神迹] 局部寒潮降临 ({x},{y})！</color>");
        }

        private void CastArk(EnvironmentData env, int x, int y)
        {
            if (!TryConsumeEnergy(300f)) { Debug.LogWarning("能量不足！需要 300 点"); return; }

            // 范围 5x5 (半径2)，升温 +50，持续 20s
            _activeAnomalies.Add(new TempAnaomaly { CenterPos = new Vector2Int(x, y), Radius = 2, TargetTempOffset = 50f, RemainingTime = 20f });
            ApplyAnomalyToGrid(env, _activeAnomalies[_activeAnomalies.Count - 1], true);
            Debug.Log($"<color=red>[神迹] 地热方舟开启 ({x},{y})！</color>");
        }

        private void CastFeed(EnvironmentData env, int x, int y)
        {
            if (!TryConsumeEnergy(20f)) { Debug.LogWarning("能量不足！需要 20 点"); return; }

            var tile = env.GetTile(x, y);
            if (tile != null)
            {
                // 加 500 点食物肉量 (模拟高热量，或者你可以写 Biomass_Plant)
                tile.Biomass_Meat += 500f;
                tile.Biomass_Plant += 500f;
                tile.Biomass_Mineral += 500f;
                Debug.Log($"<color=green>[神迹] 甘霖天降 ({x},{y})，食物暴增！</color>");
            }
        }

        // ==========================================
        // 异常区域维护 (应用与撤销)
        // ==========================================
        private void UpdateAnomalies(EnvironmentData env, float deltaTime)
        {
            for (int i = _activeAnomalies.Count - 1; i >= 0; i--)
            {
                var anomaly = _activeAnomalies[i];
                anomaly.RemainingTime -= deltaTime;

                if (anomaly.RemainingTime <= 0f)
                {
                    // 到期，撤销温度修改
                    ApplyAnomalyToGrid(env, anomaly, false);
                    _activeAnomalies.RemoveAt(i);
                }
            }
        }

        private void ApplyAnomalyToGrid(EnvironmentData env, TempAnaomaly anomaly, bool isApplying)
        {
            float sign = isApplying ? 1f : -1f;
            float val = anomaly.TargetTempOffset * sign;

            for (int dx = -anomaly.Radius; dx <= anomaly.Radius; dx++)
            {
                for (int dy = -anomaly.Radius; dy <= anomaly.Radius; dy++)
                {
                    int nx = anomaly.CenterPos.x + dx;
                    int ny = anomaly.CenterPos.y + dy;
                    var tile = env.GetTile(nx, ny);
                    if (tile != null)
                    {
                        tile.Temperature_Offset += val;
                    }
                }
            }
        }
    }
}