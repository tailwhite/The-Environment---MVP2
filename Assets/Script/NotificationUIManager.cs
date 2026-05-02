using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace EvolutionLaws.UI
{
    public class NotificationUIManager : MonoBehaviour
    {
        public static NotificationUIManager Instance { get; private set; }

        [Header("Scroll View 事件日志")]
        [Tooltip("放入 Scroll View -> Viewport -> Content 里的 TextMeshProUGUI 组件")]
        public TextMeshProUGUI LogText;

        private List<string> _logLines = new List<string>();
        public int MaxLogLines = 30; // 最多保留多少行防止爆内存

        [Header("中央天灾警告")]
        [Tooltip("中央警告面板对象")]
        public GameObject CenterAlertRoot;

        public TextMeshProUGUI CenterTitle;
        public TextMeshProUGUI CenterSub;
        private Coroutine _alertCoroutine;

        private void Awake()
        {
            Instance = this;
            if (CenterAlertRoot != null) CenterAlertRoot.SetActive(false);
            if (LogText != null) LogText.text = "";
        }

        // ===================================
        // 核心功能 1：左下角全局日志播报
        // ===================================
        public void AddLogMessage(string msg, Color color)
        {
            if (LogText == null) return;
            string hex = ColorUtility.ToHtmlStringRGBA(color);
            string formatted = $"<color=#{hex}>{msg}</color>";

            _logLines.Add(formatted);
            if (_logLines.Count > MaxLogLines) _logLines.RemoveAt(0); // 顶掉最老的信息

            LogText.text = string.Join("\n", _logLines);
        }

        // ===================================
        // 核心功能 2：屏幕中央天灾拉屏警告
        // ===================================
        public void ShowCenterAlert(string title, string sub, Color color, float duration = 4f)
        {
            if (CenterAlertRoot == null) return;

            if (CenterTitle != null) { CenterTitle.text = title; CenterTitle.color = color; }
            if (CenterSub != null) CenterSub.text = sub;

            if (_alertCoroutine != null) StopCoroutine(_alertCoroutine);
            _alertCoroutine = StartCoroutine(AlertRoutine(duration));
        }

        private IEnumerator AlertRoutine(float duration)
        {
            CenterAlertRoot.SetActive(true);
            yield return new WaitForSeconds(duration);
            CenterAlertRoot.SetActive(false);
        }

        // ===================================
        // 核心功能 3：世界坐标(3D)简单廉价暴力的飘字
        // ===================================
        public void ShowFloatingText(Vector2 worldPos, string text, Color color)
        {
            GameObject go = new GameObject("FloatingText");
            go.transform.position = new Vector3(worldPos.x, worldPos.y + 1.0f, 0); // 抬高一点
            go.layer = LayerMask.NameToLayer("UI");

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.color = color;
            tmp.fontSize = 4.0f; // 放大字体
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.sortingOrder = 10000; // 确保绝对在最顶层

            // 添加外发光/描边以防在亮色背景看不清
            tmp.fontMaterial.EnableKeyword("OUTLINE_ON");
            tmp.outlineColor = new Color32(0, 0, 0, 255);
            tmp.outlineWidth = 0.2f;

            var anim = go.AddComponent<FloatingTextAnimator>();
            anim.Setup(5.0f); // 滞留时长改为 5 秒
        }
    }

    /// <summary>
    /// 极其廉价的漂浮动画组件 (向上飘 1.5 秒后自己销毁)
    /// </summary>
    public class FloatingTextAnimator : MonoBehaviour
    {
        private float _lifetime = 5.0f; // 默认 5 秒
        private float _speed = 0.5f;    // 飘慢一点
        private TextMeshPro _tmp;

        public void Setup(float time)
        {
            _lifetime = time;
        }

        private void Start()
        {
            _tmp = GetComponent<TextMeshPro>();
            Destroy(gameObject, _lifetime);
        }

        private void Update()
        {
            transform.position += Vector3.up * _speed * Time.deltaTime;

            if (_tmp != null)
            {
                Color c = _tmp.color;
                // 最后 1 秒才开始变透明，前面保持全亮
                if (_lifetime - Time.timeSinceLevelLoad < 1f)
                {
                    c.a -= Time.deltaTime;
                    _tmp.color = c;
                }
            }
        }
    }
}