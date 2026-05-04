using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EvolutionLaws.Data;

namespace EvolutionLaws.View
{
    /// <summary>
    /// 【生物世界UI】
    /// 职责:在生物头顶显示血条、能量条、代数
    /// 原则:纯视觉显示,从CreatureData读取数据
    /// </summary>
    public class CreatureWorldUI : MonoBehaviour
    {
        // ==========================================
        // UI引用 (在Inspector中拖入)
        // ==========================================
        [Header("UI Elements (SpriteRenderers)")]
        [Tooltip("健康条(Vitality)的填充物 Transform (用于更改缩放 X)")]
        public Transform HealthBarFillTransform;

        [Tooltip("健康条(Vitality)的填充物 Renderer (用于更改颜色)")]
        public SpriteRenderer HealthBarRenderer;

        [Tooltip("能量条(Energy)的填充物 Transform (用于更改缩放 X)")]
        public Transform EnergyBarFillTransform;

        [Tooltip("代数文本")]
        public TextMeshProUGUI GenerationText;

        [Tooltip("物种ID文本(可选)")]
        public TextMeshProUGUI SpeciesText;

        [Header("Evolution Message")]
        [Tooltip("在Prefab中预留的进化文字组件 (默认隐藏)")]
        public TextMeshPro EvolutionText;

        public float FloatSpeed = 1.0f; // 飘字上升速度
        private Vector3 _evoTextOriginPos; // 记录原始化锚点

        // ==========================================
        // 数据引用
        // ==========================================
        private CreatureData _data;

        // ==========================================
        // 配置参数
        // ==========================================
        [Header("Visual Settings")]
        [Tooltip("血条颜色渐变 (健康->危险)")]
        public Gradient HealthColorGradient = new Gradient
        {
            colorKeys = new GradientColorKey[]
            {
                new GradientColorKey(Color.red, 0f),      // 0% = 红色
                new GradientColorKey(Color.yellow, 0.5f), // 50% = 黄色
                new GradientColorKey(Color.green, 1f)     // 100% = 绿色
            }
        };

        [Tooltip("UI与生物的垂直偏移")]
        public float VerticalOffset = 1.5f;

        [Tooltip("是否始终面向摄像机")]
        public bool BillboardMode = true;

        // ==========================================
        // 初始化
        // ==========================================
        /// <summary>
        /// 由CreatureView调用,绑定数据
        /// </summary>
        public void Initialize(CreatureData data)
        {
            // 记住预制体内文本的初始相对坐标
            if (EvolutionText != null)
            {
                _evoTextOriginPos = EvolutionText.transform.localPosition;
                EvolutionText.gameObject.SetActive(false);
            }
            _data = data;
        }

        // ==========================================
        // 每帧更新
        // ==========================================
        private void LateUpdate()
        {
            if (_data == null) return;

            // 1. 位置跟随 (注意：由于它通常是 Creature 的子物体，如果是挂载在本体下，只要调 localPosition 即可，开销更低！)
            // 如果你的 CreatureWorldUI 是独立在世界里的再用世界坐标，如果是挂在预制体下面，你可以把下列代码屏蔽：
            // transform.position = new Vector3(_data.Position.x, _data.Position.y + VerticalOffset, 0);

            // 2. 更新血条 (使用数学 Scale 模拟进度条)
            if (HealthBarFillTransform != null)
            {
                // 计算比例 (防除0)
                float healthPercent = Mathf.Clamp01(_data.Vitality_Current / Mathf.Max(0.1f, _data.Vitality_Max));

                // 仅修改 localScale 的 X 轴，性能极高
                Vector3 scale = HealthBarFillTransform.localScale;
                scale.x = healthPercent;
                HealthBarFillTransform.localScale = scale;

                // 颜色渐变
                if (HealthBarRenderer != null)
                {
                    HealthBarRenderer.color = HealthColorGradient.Evaluate(healthPercent);
                }
            }

            // 3. 更新能量条 (如果有布置的话)
            if (EnergyBarFillTransform != null)
            {
                float energyPercent = Mathf.Clamp01(_data.Energy / Mathf.Max(0.1f, _data.Energy_Max));
                Vector3 scale = EnergyBarFillTransform.localScale;
                scale.x = energyPercent;
                EnergyBarFillTransform.localScale = scale;
            }
            // 5. 零开销的进化消息复用
            if (EvolutionText != null)
            {
                if (_data.EvoMsgTimer > 0)
                {
                    if (!EvolutionText.gameObject.activeSelf)
                    {
                        // 刚刚触发进化时：将其归位并显示
                        EvolutionText.gameObject.SetActive(true);
                        EvolutionText.transform.localPosition = _evoTextOriginPos;
                        EvolutionText.text = _data.EvolutionMsg;
                    }

                    // 向上飘动 (操作相对坐标)
                    EvolutionText.transform.localPosition += Vector3.up * FloatSpeed * Time.deltaTime;

                    // 最后一秒进行淡出表现
                    Color c = EvolutionText.color;
                    c.a = Mathf.Clamp01(_data.EvoMsgTimer);
                    EvolutionText.color = c;
                }
                else if (EvolutionText.gameObject.activeSelf)
                {
                    // 时间到了，回收隐藏
                    EvolutionText.gameObject.SetActive(false);
                }
            }
            // 4. 隐藏死亡生物的UI
            if (_data.IsDead)
            {
                gameObject.SetActive(false);
            }
        }
    }
}