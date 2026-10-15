using Cinemachine;
using UnityEngine;

namespace DeepseaOil.Presentation.Effects.Drivers
{
    /// <summary>相机震动挂件：挂在相机上，等相机系统写完机位再叠加衰减偏移</summary>
    /// <remarks>施加点要晚于相机系统写机位：同物体上的 CinemachineBrain 也走 LateUpdate，两者先后没有保证，写早了会被覆盖。偏移从当前机位里加、下一帧再减掉。</remarks>
    [DisallowMultipleComponent]
    public sealed class CameraShaker : MonoBehaviour
    {
        private static CameraShaker _instance;

        public static CameraShaker Instance => _instance;

        private float _remaining;
        private float _duration;
        private float _amplitude;

        /// <summary>上一帧加出去的偏移 x/y</summary>
        private float _appliedX;
        private float _appliedY;

        private void Awake()
        {
            _instance = this;
        }

        private void OnEnable()
        {
            CinemachineCore.CameraUpdatedEvent.AddListener(OnCameraUpdated);
        }

        private void OnDisable()
        {
            CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCameraUpdated);

            RemoveOffset();
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(_instance, this)) _instance = null;
        }

        /// <summary>叠加一次震动：幅度与时长取历史最大值，后到的短震不会把长震掐短</summary>
        public void Shake(float amplitude, float duration)
        {
            if (amplitude <= 0f || duration <= 0f) return;

            _amplitude = Mathf.Max(_amplitude, amplitude);
            _duration = Mathf.Max(_duration, duration);
            _remaining = _duration;
        }

        /// <summary>无相机系统时的施加点；有虚拟相机时机位已在回调里写过</summary>
        private void LateUpdate()
        {
            RemoveOffset();
        }

        private void OnCameraUpdated(CinemachineBrain brain)
        {
            RemoveOffset();

            ApplyShake();
        }

        private void RemoveOffset()
        {
            if (_appliedX == 0f && _appliedY == 0f) return;

            Vector3 position = transform.position;

            _appliedX = 0f;
            _appliedY = 0f;

            transform.position = position;
        }

        private void ApplyShake()
        {
            if (_remaining <= 0f) return;

            // 用 unscaledDeltaTime：暂停与慢动作下照样抖完
            _remaining -= Time.unscaledDeltaTime;

            if (_remaining <= 0f)
            {
                _duration = 0f;
                _amplitude = 0f;

                return;
            }

            float decay = _duration > 0f ? _remaining / _duration : 0f;
            Vector2 offset = Random.insideUnitCircle * (_amplitude * decay);

            _appliedX = offset.x;
            _appliedY = offset.y;

            // 相对当前机位加：不缓存基准位置，相机系统整帧平移也不会被抵消掉
            transform.position += new Vector3(_appliedX, _appliedY, 0f);
        }
    }
}
