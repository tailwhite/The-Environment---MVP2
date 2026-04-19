using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EvolutionLaws.Core;

namespace EvolutionLaws.UI
{
    public class ChoiceUIManager : MonoBehaviour
    {
        public static ChoiceUIManager Instance { get; private set; }

        [Header("UI 引用")]
        public GameObject PanelRoot;

        [Tooltip("三个选项的按钮数组")]
        public Button[] ChoiceButtons;

        [Tooltip("对应按钮的标题文本数组")]
        public TextMeshProUGUI[] TitleTexts;

        [Tooltip("对应按钮的描述文本数组")]
        public TextMeshProUGUI[] DescTexts;

        private void Awake()
        {
            Instance = this;
            PanelRoot.SetActive(false);
        }

        public void ShowChoices(List<ChoiceEventData> choices)
        {
            if (choices == null || choices.Count == 0) return;

            // 呼出面板并暂停游戏
            PanelRoot.SetActive(true);
            Time.timeScale = 0f;
            // 强制关闭地形提示 UI
            if (TileTooltipUI.Instance != null) TileTooltipUI.Instance.SetEnabled(false);

            for (int i = 0; i < ChoiceButtons.Length; i++)
            {
                if (i < choices.Count)
                {
                    ChoiceButtons[i].gameObject.SetActive(true);

                    // 刷新文本
                    TitleTexts[i].text = choices[i].ChoiceName;
                    DescTexts[i].text = choices[i].Description;

                    // 必须缓存局部变量，供闭包使用
                    ChoiceEventData capturedChoice = choices[i];

                    // 绑定事件
                    ChoiceButtons[i].onClick.RemoveAllListeners();
                    ChoiceButtons[i].onClick.AddListener(() => OnChoiceSelected(capturedChoice));
                }
                else
                {
                    // 数据不足 3 个时隐藏多余的按钮
                    ChoiceButtons[i].gameObject.SetActive(false);
                }
            }
        }

        private void OnChoiceSelected(ChoiceEventData choiceData)
        {
            // 1. 执行具体效果
            ChoiceEffectHandler.ApplyEffect(choiceData);

            // 2. 隐藏UI并恢复游戏时间
            PanelRoot.SetActive(false);
            Time.timeScale = 1f;
            // 选项结束，恢复地形提示 UI
            if (TileTooltipUI.Instance != null) TileTooltipUI.Instance.SetEnabled(true);

            Debug.Log($"<color=yellow>[抉择系统] 玩家选择了: {choiceData.ChoiceName}</color>");
        }
    }
}