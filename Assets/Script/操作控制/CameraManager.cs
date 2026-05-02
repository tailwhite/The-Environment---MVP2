using UnityEngine;

namespace EvolutionLaws.Core
{
    /// <summary>
    /// 【相机管理器】
    /// 职责:
    /// 1. WASD移动 + 鼠标中键拖拽
    /// 2. 滚轮缩放 (朝向鼠标位置)
    /// 3. 地图边界限制
    /// 4. 跟随目标功能
    /// 原则:RTS风格相机控制
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraManager : MonoBehaviour
    {
        // ==========================================
        // 引用
        // ==========================================
        [Header("References")]
        [Tooltip("环境管理器 (用于获取地图边界)")]
        public EnvironmentManager EnvironmentManager;

        private Camera _camera;
        private Transform _followTarget;
        private bool _isFollowing = false;

        // ==========================================
        // 移动参数
        // ==========================================
        [Header("Movement Settings")]
        [Tooltip("WASD 移动速度")]
        public float MoveSpeed = 20f;

        [Tooltip("移动平滑速度 (SmoothDamp)")]
        public float MoveSmoothTime = 0.3f;

        [Tooltip("鼠标拖拽灵敏度")]
        public float DragSensitivity = 0.5f;

        private Vector3 _velocity = Vector3.zero;
        private Vector3 _targetPosition;

        // ==========================================
        // 缩放参数
        // ==========================================
        [Header("Zoom Settings")]
        [Tooltip("正交相机大小范围 (Min)")]
        public float MinOrthographicSize = 5f;

        [Tooltip("正交相机大小范围 (Max)")]
        public float MaxOrthographicSize = 50f;

        [Tooltip("缩放速度")]
        public float ZoomSpeed = 10f;

        [Tooltip("缩放平滑速度")]
        public float ZoomSmoothTime = 0.2f;

        private float _targetOrthographicSize;
        private float _zoomVelocity = 0f;

        // ==========================================
        // 边界参数
        // ==========================================
        [Header("Bounds Settings")]
        [Tooltip("边界内缩值 (防止相机看到地图外)")]
        public float BoundsPadding = 2f;

        // ==========================================
        // 跟随参数
        // ==========================================
        [Header("Follow Settings")]
        [Tooltip("跟随模式下的平滑速度")]
        public float FollowSmoothTime = 0.5f;

        // ==========================================
        // 初始化
        // ==========================================
        private void Awake()
        {
            _camera = GetComponent<Camera>();

            // 确保是正交相机
            if (!_camera.orthographic)
            {
                Debug.LogWarning("[CameraManager] 相机不是正交模式,将自动切换");
                _camera.orthographic = true;
            }

            _targetPosition = transform.position;
            _targetOrthographicSize = _camera.orthographicSize;
        }

        private void Start()
        {
            // 初始化相机位置到地图中心
            if (EnvironmentManager != null)
            {
                float centerX = EnvironmentManager.MapWidth / 2f;
                float centerY = EnvironmentManager.MapHeight / 2f;
                _targetPosition = new Vector3(centerX, centerY, transform.position.z);
                transform.position = _targetPosition;
            }
        }

        // ==========================================
        // 每帧更新
        // ==========================================
        private void LateUpdate()
        {
            HandleMovementInput();
            HandleZoomInput();
            ApplyMovement();
            ApplyZoom();
        }

        // ==========================================
        // 移动输入处理
        // ==========================================
        private void HandleMovementInput()
        {
            // ──────────────────────────────────
            // 1. WASD 移动
            // ──────────────────────────────────
            Vector3 moveInput = Vector3.zero;

            if (Input.GetKey(KeyCode.W)) moveInput.y += 1;
            if (Input.GetKey(KeyCode.S)) moveInput.y -= 1;
            if (Input.GetKey(KeyCode.A)) moveInput.x -= 1;
            if (Input.GetKey(KeyCode.D)) moveInput.x += 1;

            // 如果有 WASD 输入,取消跟随
            if (moveInput != Vector3.zero)
            {
                _isFollowing = false;
                _followTarget = null;
                _targetPosition += moveInput.normalized * MoveSpeed * Time.deltaTime;
            }

            // ──────────────────────────────────
            // 2. 鼠标中键拖拽
            // ──────────────────────────────────
            if (Input.GetMouseButton(2)) // 鼠标中键
            {
                _isFollowing = false;
                _followTarget = null;

                float deltaX = -Input.GetAxis("Mouse X") * DragSensitivity;
                float deltaY = -Input.GetAxis("Mouse Y") * DragSensitivity;

                _targetPosition += new Vector3(deltaX, deltaY, 0);
            }

            // ──────────────────────────────────
            // 3. 跟随模式
            // ──────────────────────────────────
            if (_isFollowing && _followTarget != null)
            {
                _targetPosition = new Vector3(
                    _followTarget.position.x,
                    _followTarget.position.y,
                    transform.position.z
                );
            }

            // ──────────────────────────────────
            // 4. 边界限制
            // ──────────────────────────────────
            ClampToBounds();
        }

        // ==========================================
        // 缩放输入处理 (朝向鼠标位置缩放)
        // ==========================================
        private void HandleZoomInput()
        {
            float scrollDelta = Input.GetAxis("Mouse ScrollWheel");

            if (Mathf.Abs(scrollDelta) > 0.01f)
            {
                //边界拦截
                Vector3 mousePos = Input.mousePosition;
                if (mousePos.x < 0 || mousePos.y < 0 || mousePos.x > Screen.width || mousePos.y > Screen.height)
                {
                    return; // 鼠标在游戏窗口外，不执行向鼠标位置缩放的逻辑
                }
                // ──────────────────────────────────
                // 缩放前获取鼠标世界坐标
                // ──────────────────────────────────
                Vector3 mouseWorldPosBefore = _camera.ScreenToWorldPoint(Input.mousePosition);

                // 计算新的缩放值
                _targetOrthographicSize -= scrollDelta * ZoomSpeed;
                _targetOrthographicSize = Mathf.Clamp(_targetOrthographicSize, MinOrthographicSize, MaxOrthographicSize);

                // 临时应用缩放 (用于计算偏移)
                float oldSize = _camera.orthographicSize;
                _camera.orthographicSize = _targetOrthographicSize;

                // ──────────────────────────────────
                // 缩放后获取鼠标世界坐标
                // ──────────────────────────────────
                Vector3 mouseWorldPosAfter = _camera.ScreenToWorldPoint(Input.mousePosition);

                // 计算偏移并调整相机位置
                Vector3 offset = mouseWorldPosBefore - mouseWorldPosAfter;
                _targetPosition += offset;

                // 恢复原缩放值 (等待平滑应用)
                _camera.orthographicSize = oldSize;

                // 边界限制
                ClampToBounds();
            }
        }

        // ==========================================
        // 应用移动 (平滑)
        // ==========================================
        private void ApplyMovement()
        {
            float smoothTime = _isFollowing ? FollowSmoothTime : MoveSmoothTime;

            transform.position = Vector3.SmoothDamp(
                transform.position,
                _targetPosition,
                ref _velocity,
                smoothTime
            );
        }

        // ==========================================
        // 应用缩放 (平滑)
        // ==========================================
        private void ApplyZoom()
        {
            _camera.orthographicSize = Mathf.SmoothDamp(
                _camera.orthographicSize,
                _targetOrthographicSize,
                ref _zoomVelocity,
                ZoomSmoothTime
            );
        }

        // ==========================================
        // 边界限制
        // ==========================================
        private void ClampToBounds()
        {
            if (EnvironmentManager == null) return;

            // 获取地图的绝对边界
            float mapMinX = 0f;
            float mapMaxX = EnvironmentManager.MapWidth;
            float mapMinY = 0f;
            float mapMaxY = EnvironmentManager.MapHeight;

            // 宽松限制：允许摄像机中心点到达地图边缘 + Padding
            // 如果 BoundsPadding 是正数，表示可以稍微看出去一点点
            // 如果 BoundsPadding 是负数，表示必须往里缩一点点

            // 修改策略：不再减去 cameraWidth，而是直接限制中心点坐标
            float minX = mapMinX - BoundsPadding;
            float maxX = mapMaxX + BoundsPadding;
            float minY = mapMinY - BoundsPadding;
            float maxY = mapMaxY + BoundsPadding;

            // 应用限制
            _targetPosition.x = Mathf.Clamp(_targetPosition.x, minX, maxX);
            _targetPosition.y = Mathf.Clamp(_targetPosition.y, minY, maxY);
        }

        // ==========================================
        // 公共接口:跟随目标
        // ==========================================
        /// <summary>
        /// 开始跟随指定目标
        /// </summary>
        public void Follow(Transform target)
        {
            if (target == null)
            {
                Debug.LogWarning("[CameraManager] 跟随目标为空");
                return;
            }

            _followTarget = target;
            _isFollowing = true;

            Debug.Log($"[CameraManager] 开始跟随: {target.name}");
        }

        /// <summary>
        /// 停止跟随
        /// </summary>
        public void StopFollow()
        {
            _isFollowing = false;
            _followTarget = null;
            Debug.Log("[CameraManager] 停止跟随");
        }

        /// <summary>
        /// 检查是否正在跟随
        /// </summary>
        public bool IsFollowing => _isFollowing;

        // ==========================================
        // 调试可视化
        // ==========================================
        private void OnDrawGizmosSelected()
        {
            if (EnvironmentManager == null) return;

            // 绘制地图边界
            Gizmos.color = Color.yellow;
            Vector3 bottomLeft = new Vector3(0, 0, 0);
            Vector3 bottomRight = new Vector3(EnvironmentManager.MapWidth, 0, 0);
            Vector3 topLeft = new Vector3(0, EnvironmentManager.MapHeight, 0);
            Vector3 topRight = new Vector3(EnvironmentManager.MapWidth, EnvironmentManager.MapHeight, 0);

            Gizmos.DrawLine(bottomLeft, bottomRight);
            Gizmos.DrawLine(bottomRight, topRight);
            Gizmos.DrawLine(topRight, topLeft);
            Gizmos.DrawLine(topLeft, bottomLeft);

            // 绘制跟随目标
            if (_isFollowing && _followTarget != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, _followTarget.position);
                Gizmos.DrawWireSphere(_followTarget.position, 1f);
            }
        }
    }
}