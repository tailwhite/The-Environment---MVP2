using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

namespace EvolutionLaws.UI
{
    public class SettlementUI : MonoBehaviour
    {
        public static SettlementUI Instance { get; private set; }

        public GameObject PanelRoot;
        public TextMeshProUGUI TitleText;
        public TextMeshProUGUI DetailText;

        private void Awake()
        {
            Instance = this;
        }

        // 外部调用展示面板
        public void Show(bool isVictory, string details)
        {
            PanelRoot.SetActive(true);

            // 冻结游戏时间
            Time.timeScale = 0f;

            if (isVictory)
            {
                TitleText.text = "恭喜造物主！\n你的物种成功熬过了冰河纪元！";
                TitleText.color = Color.yellow;
            }
            else
            {
                TitleText.text = "演化终结";
                TitleText.color = Color.red;
            }

            DetailText.text = details;
        }

        // 绑定给按钮点击的事件
        public void OnReturnButtonClicked()
        {
            // 恢复时间流逝，否则下一个场景会卡死
            Time.timeScale = 1f;
            SceneManager.LoadScene("MetaScene");
        }
    }
}