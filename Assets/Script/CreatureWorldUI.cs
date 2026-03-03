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
        [Header("UI Elements")]
        [Tooltip("健康条(Vitality)填充图片")]
        public Image HealthBarFill;

        [Tooltip("能量条(Energy)填充图片")]
        public Image EnergyBarFill;

        [Tooltip("代数文本")]
        public TextMeshProUGUI GenerationText;

        [Tooltip("物种ID文本(可选)")]
        public TextMeshProUGUI SpeciesText;

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

        [Tooltip("能量条颜色")]
        public Color EnergyColor = new Color(0.3f, 0.7f, 1f); // 淡蓝色

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
            _data = data;

            // 初始化文本
            if (GenerationText != null)
                GenerationText.text = $"G{data.Generation}";

            if (SpeciesText != null)
                SpeciesText.text = data.SpeciesID;

            // 初始化颜色
            if (EnergyBarFill != null)
                EnergyBarFill.color = EnergyColor;
        }

        // ==========================================
        // 每帧更新
        // ==========================================
        private void LateUpdate()
        {
            if (_data == null) return;

            // 1. 更新位置 (跟随生物,并保持垂直偏移)
            transform.position = new Vector3(
                _data.Position.x,
                _data.Position.y + VerticalOffset,
                0
            );

            // 2. 面向摄像机 (Billboard效果)
            if (BillboardMode && Camera.main != null)
            {
                transform.rotation = Quaternion.Euler(0, 0, 0);// 重置旋转
                transform.LookAt(transform.position + Camera.main.transform.rotation * Vector3.forward,
                                 Camera.main.transform.rotation * Vector3.up);
            }

            // 3. 更新血条
            if (HealthBarFill != null)
            {
                float healthPercent = _data.Vitality_Current / _data.Vitality_Max;
                HealthBarFill.fillAmount = healthPercent;
                HealthBarFill.color = HealthColorGradient.Evaluate(healthPercent);
            }

            // 4. 更新能量条
            if (EnergyBarFill != null)
            {
                float energyPercent = _data.Energy / _data.Energy_Max;
                EnergyBarFill.fillAmount = energyPercent;
            }

            // 5. 隐藏死亡生物的UI
            if (_data.IsDead)
            {
                gameObject.SetActive(false);
            }
        }
    }
}