using EvolutionLaws.Core;
using UnityEngine;

namespace EvolutionLaws.UI
{
    /// <summary>
    /// 【温度热力图视图】
    /// 职责：按快捷键 (U) 时，在地图上方覆盖一层半透明的温度颜色层。
    /// 原理：生成一张像素与地图相等的 Texture2D，性能极高。
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class TemperatureHeatmapView : MonoBehaviour
    {
        [Header("References")]
        public EnvironmentManager EnvManager;

        [Header("Settings")]
        [Tooltip("热力图的透明度 (0到1)")]
        [Range(0f, 1f)] public float OverlayAlpha = 0.5f;

        [Tooltip("最低温度 (映射渐变的起点)")]
        public float MinTemp = -20f;

        [Tooltip("最高温度 (映射渐变的终点)")]
        public float MaxTemp = 50f;

        [Tooltip("温度颜色渐变 (蓝 -> 绿 -> 红)")]
        public Gradient TemperatureGradient;

        private SpriteRenderer _renderer;
        private Texture2D _texture;
        private bool _isShowing = false;

        private float _refreshTimer = 0f;
        public float RefreshInterval = 1f; // 每秒刷新一次

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
            _renderer.sortingOrder = 50; // 确保渲染在最顶层 (覆盖地图和生物)
            _renderer.enabled = false;

            // 如果没配渐变，给个自带的默认渐变：蓝(-20) -> 白(15) -> 红(50)
            if (TemperatureGradient == null || TemperatureGradient.colorKeys.Length == 0)
            {
                TemperatureGradient = new Gradient();
                TemperatureGradient.SetKeys(
                    new GradientColorKey[] { new GradientColorKey(Color.blue, 0f), new GradientColorKey(Color.white, 0.5f), new GradientColorKey(Color.red, 1f) },
                    new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
                );
            }
        }

        private void Update()
        {
            // 按下U键切换显示
            if (Input.GetKeyDown(KeyCode.U))
            {
                _isShowing = !_isShowing;
                _renderer.enabled = _isShowing;

                if (_isShowing)
                {
                    RefreshHeatmap();
                    Debug.Log("[Heatmap] 开启温度分布热力图");
                }
                else
                {
                    Debug.Log("[Heatmap] 关闭温度分布热力图");
                }
            }
            // 如果正在显示 → 每秒刷新一次
            if (_isShowing)
            {
                _refreshTimer += Time.deltaTime;
                if (_refreshTimer >= RefreshInterval)
                {
                    _refreshTimer = 0f;
                    RefreshHeatmap();
                }
            }

            // 如果处于显示状态并且天灾系统在改变温度（或者为了图省事），可以每秒刷新一次，这里先做一次性生成
        }

        public void RefreshHeatmap()
        {
            if (EnvManager == null || EnvManager.EnvironmentData == null) return;

            var data = EnvManager.EnvironmentData;
            int w = data.Width;
            int h = data.Height;

            // 1. 若贴图不存在或尺寸变化，重新创建
            if (_texture == null || _texture.width != w || _texture.height != h)
            {
                // RGBA32格式，不开mipmaps（保证像素锐利边界）
                _texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
                _texture.filterMode = FilterMode.Point; // 像素点对齐过滤！极度清晰

                // 生成与 Texture 同样大小的 Sprite (Pivot放在左下角 0,0， PPU设为 1f = 1像素1个单位距离)
                Sprite sprite = Sprite.Create(_texture, new Rect(0, 0, w, h), Vector2.zero, 1f);
                _renderer.sprite = sprite;
                // 确保对齐地图坐标 0，0
                transform.position = Vector3.zero;
            }

            // 2. 填充像素颜色
            Color[] pixels = new Color[w * h];
            float globalTemp = data.Global_Temperature;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    var tile = data.GetTile(x, y);
                    float actualTemp = globalTemp + (tile != null ? tile.Temperature_Offset : 0f);

                    // 将温度归一化到 0~1 的区间
                    float t = Mathf.InverseLerp(MinTemp, MaxTemp, actualTemp);

                    // 从渐变色中取色
                    Color col = TemperatureGradient.Evaluate(t);
                    col.a = OverlayAlpha; // 赋予半透明

                    pixels[y * w + x] = col;
                }
            }

            // 3. 应用回贴图（效率极高）
            _texture.SetPixels(pixels);
            _texture.Apply();
        }
    }
}