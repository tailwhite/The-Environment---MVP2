using EvolutionLaws.Core;
using EvolutionLaws.Data;
using UnityEngine;

namespace EvolutionLaws.View
{
    /// <summary>
    /// 【生物视图】
    /// 职责:纯视觉同步,不含任何游戏逻辑
    /// 原则:"皮肤"层,只负责将数据映射到Transform和Sprite
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class CreatureView : MonoBehaviour
    {
        // ==========================================
        // 数据引用 (只读)
        // ==========================================
        private CreatureData _data;

        private SpriteRenderer _renderer;// 视觉组件引用
        private CreatureWorldUI _worldUI; //

        // ==========================================
        // 可视化调试 (可选)
        // ==========================================
        [Header("Debug Visualization")]
        public bool ShowDebugInfo = true;

        public Color AliveColor = Color.white;// 活着时的正常颜色
        public Color DeadColor = Color.gray;// 死亡时的灰色
        public Color UnconsciousColor = Color.yellow;// 昏迷时的黄色

        // ==========================================
        // 状态染色
        // ==========================================
        [Header("State Tinting")]
        [Tooltip("饥饿时的红色叠加强度")]
        [Range(0f, 1f)]
        public float HungerTintStrength = 0.3f;// 饥饿时的红色叠加强度

        [Tooltip("生病时的绿色叠加强度")]
        [Range(0f, 1f)]
        public float SickTintStrength = 0.3f;// 生病时的绿色叠加强度

        // ==========================================
        // 移动参数
        // ==========================================
        [Header("Movement Smoothing")]
        [Tooltip("位置插值速度 (越大越快对齐数据)")]
        [Range(1f, 20f)]
        public float MovementSmoothSpeed = 10f;//

        [Tooltip("距离阈值,小于此值视为已到达")]
        public float SnapThreshold = 0.05f;

        // ==========================================
        // 初始化
        // ==========================================
        /// <summary>
        /// 由 SimulationManager 调用,绑定数据和外观
        /// </summary>
        public void Initialize(CreatureData data, Sprite sprite)//初始化方法,绑定数据和外观
        {
            _data = data;
            _renderer = GetComponent<SpriteRenderer>();// 从自身获取SpriteRenderer组件
            _renderer.sprite = sprite;//将自身的SpriteRenderer的sprite设置为传入的sprite参数

            // 初始位置同步
            transform.position = new Vector3(data.Position.x, data.Position.y, 0);

            // 初始化头顶UI (如果Prefab中包含)
            _worldUI = GetComponentInChildren<CreatureWorldUI>();
            if (_worldUI != null)
            {
                _worldUI.Initialize(data);
            }

            //Debug.Log($"[CreatureView] 初始化完成 | UID: {data.UID} | 物种: {data.SpeciesID}");
        }

        // ==========================================
        // 每帧同步
        // ==========================================
        private void Update()//每帧更新,同步位置、缩放和状态
        {
            if (_data == null || SimulationManager.Instance == null) return;

            Vector3 targetPosition = new Vector3(_data.Position.x, _data.Position.y, 0);
            float distance = Vector3.Distance(transform.position, targetPosition);

            if (distance > SnapThreshold)// 如果距离较远,则平滑移动
            {
                // 使用Lerp平滑移动
                transform.position = Vector3.Lerp(
                    transform.position,
                    targetPosition,
                    Time.deltaTime * MovementSmoothSpeed
                );
            }
            else
            {
                // 距离很近时直接对齐,避免抖动
                transform.position = targetPosition;
            }

            // 2. 同步缩放 (基于 Size)
            //transform.localScale = Vector3.one * _data.Size;线性缩放可能导致过大或过小,使用幂函数调整缩放曲线
            //float visualScale = Mathf.Sqrt(_data.Size);开方
            // 假设 age 可以通过 SimulationManager.GlobalTime - _data.BirthTimestamp 算出来
            float age = SimulationManager.Instance.GlobalTime - _data.BirthTimestamp;

            // 计算生长比例：如果是成年直接是 1.0；如果是幼年，体型从 0.3 平滑过渡到 1.0
            float growthProgress = Mathf.Clamp01(age / _data.Maturity_Age);
            float ageScaleMultiplier = Mathf.Lerp(0.3f, 1.0f, growthProgress);

            // 最终显示的大小 = 基因体型大小 × 年龄比例
            transform.localScale = Vector3.one * (_data.Size * ageScaleMultiplier);

            // 3. 高级状态颜色
            UpdateVisualState();

            // 4. 死亡自动销毁
            if (_data.IsDead && ShowDebugInfo)
            {
                Debug.Log($"[CreatureView] {_data.SpeciesID} ({_data.UID}) 死亡,销毁视图");
                Destroy(gameObject, 1f);
            }
        }

        // ==========================================
        // 动态视觉状态更新
        // ==========================================
        private void UpdateVisualState()
        {
            Color targetColor = AliveColor;

            // 死亡状态
            if (_data.IsDead)
            {
                targetColor = DeadColor;
            }
            // 昏迷状态
            else if (_data.IsUnconscious)
            {
                targetColor = UnconsciousColor;
            }
            // 活着但需要状态染色
            else
            {
                // 计算饥饿程度 (Energy < 30%)
                float energyPercent = _data.Energy / _data.Energy_Max;
                if (energyPercent < 0.3f)
                {
                    float hungerIntensity = (0.3f - energyPercent) / 0.3f; // 0-30%映射到0-1
                    Color hungryTint = Color.Lerp(Color.white, Color.red, hungerIntensity * HungerTintStrength);
                    targetColor *= hungryTint;
                }

                // 计算生病程度 (Vitality < 50%)
                float vitalityPercent = _data.Vitality_Current / _data.Vitality_Max;
                if (vitalityPercent < 0.5f)
                {
                    float sickIntensity = (0.5f - vitalityPercent) / 0.5f; // 0-50%映射到0-1
                    Color sickTint = Color.Lerp(Color.white, Color.green, sickIntensity * SickTintStrength);
                    targetColor *= sickTint;
                }
            }

            // 平滑过渡颜色
            _renderer.color = Color.Lerp(_renderer.color, targetColor, Time.deltaTime * 5f);
        }

        // ==========================================
        // 调试可视化 (Gizmos)
        // ==========================================
        private void OnDrawGizmosSelected()
        {
            if (_data == null || !ShowDebugInfo) return;

            // 绘制视野范围
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, _data.Vision_Range);
            //绘制感知目标
            if (_data.PerceivedTargets != null)
            {
                foreach (var target in _data.PerceivedTargets)
                {
                    // 根据类型选择颜色
                    Gizmos.color = target.Type switch
                    {
                        TargetType.FoodResource => Color.green,
                        TargetType.Predator => Color.red,
                        TargetType.Prey => new Color(1f, 0.5f, 0f), // 补上橘色：猎物连线
                        TargetType.Ally => Color.blue,
                        _ => Color.white
                    };

                    // 画线连接
                    Gizmos.DrawLine(transform.position, target.Position);
                    // 画球标记目标
                    Gizmos.DrawWireSphere(target.Position, 0.5f);
                }
            }
            //绘制决策目标
            if (_data.TargetPosition.HasValue)
            {
                // 根据行为状态选择颜色
                Gizmos.color = _data.CurrentBehavior switch
                {
                    BehaviorState.Foraging => Color.green,
                    BehaviorState.Fleeing => Color.red,
                    BehaviorState.Hunting => Color.yellow,
                    _ => Color.gray
                };

                // 画箭头指向目标
                Gizmos.DrawLine(transform.position, _data.TargetPosition.Value);
                Gizmos.DrawWireSphere(_data.TargetPosition.Value, 0.8f);

                // 在目标位置上方显示行为状态
#if UNITY_EDITOR
                UnityEditor.Handles.Label(
                    _data.TargetPosition.Value + Vector2.up * 0.5f,
                    $"[{_data.CurrentBehavior}]"
                );
#endif
            }
            // 绘制健康条 (简易版)
            Vector3 barPos = transform.position + Vector3.up * 1.5f;// 血条长度根据当前生命值百分比调整
            float healthPercent = _data.Vitality_Current / _data.Vitality_Max;// 从红色到绿色渐变
            Gizmos.color = Color.Lerp(Color.red, Color.green, healthPercent);// 绘制血条背景,插值颜色
            Gizmos.DrawLine(barPos, barPos + Vector3.right * healthPercent);// 绘制血条前景
        }

        // ==========================================
        // 公共访问器 (用于外部查询)
        // ==========================================
        public CreatureData GetData() => _data;
    }
}