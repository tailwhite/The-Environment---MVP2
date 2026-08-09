using UnityEngine;
using EvolutionLaws.Core;

namespace EvolutionLaws.UI
{
    public class PauseMenuUI : MonoBehaviour
    {
        [Tooltip("暂停面板的整体根节点")]
        public GameObject PausePanel;

        private bool _isMenuOpen = false;
        private float _timeScaleBeforePause = 1f;

        private void Start()
        {
            if (PausePanel != null)
                PausePanel.SetActive(false);
        }

        private void Update()
        {
            // 按下 ESC 键呼出/关闭菜单
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ToggleMenu();
            }
        }

        public void ToggleMenu()
        {
            _isMenuOpen = !_isMenuOpen;

            if (_isMenuOpen)
            {
                PausePanel.SetActive(true);

                // 如果按 ESC 前游戏没在战术暂停，记录速度并冻结时间
                if (!SimulationManager.Instance.IsPaused)
                {
                    _timeScaleBeforePause = Time.timeScale;
                    Time.timeScale = 0f;
                }
            }
            else
            {
                PausePanel.SetActive(false);

                // 退出菜单时，如果本身不是被空格战术暂停的，就恢复速度
                if (!SimulationManager.Instance.IsPaused)
                {
                    Time.timeScale = _timeScaleBeforePause;
                }
            }
        }

        // ==========================================
        // 绑定给 UI 按钮的事件
        // ==========================================

        public void OnResumeClicked()
        {
            ToggleMenu(); // 继续游戏，直接相当于再按一次 ESC
        }

        public void OnReturnLobbyClicked()
        {
            // 调用写在大管家里面的安全返程方法
            SimulationManager.Instance.ReturnToLobby();
        }

        public void OnQuitGameClicked()
        {
            // 调用写在大管家里面的安全退出方法
            SimulationManager.Instance.QuitDemo();
        }
    }
}