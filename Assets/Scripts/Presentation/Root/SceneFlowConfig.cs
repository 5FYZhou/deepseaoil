using DeepseaOil.Logic;
using UnityEngine;

namespace DeepseaOil.Presentation
{
    /// <summary>入口状态：Attach 时推一次</summary>
    /// <remarks>Order 取 SceneOrder.Entry：其余场景根的 Attach 都排在它之后。</remarks>
    [DisallowMultipleComponent]
    public sealed class SceneFlowConfig : MonoBehaviour, ISceneRoot
    {
        [Tooltip("本场景的入口状态：游戏场景填 Running，开始场景填 Menu")]
        [SerializeField] private GameState entryState = GameState.Running;

        private GameRoot _root;

        public int Order => SceneOrder.Entry;

        public void Attach()
        {
            GameRoot root = GameRoot.Instance;
            if (root == null || root.Game == null) return;

            root.Game.SetSceneTransitionTargetState(entryState);

            Debug.Log($"[Flow] 场景声明入口状态 {entryState}（{gameObject.scene.name}）");
        }

        private void Start()
        {
            _root = GameRoot.Instance;
            if (_root != null) _root.RegisterSceneRoot(this);
        }

        private void OnDestroy()
        {
            // 用 Start 缓存的 _root：销毁期再问 GameRoot.Instance 可能当场造一个新的
            if (_root != null) _root.UnregisterSceneRoot(this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (entryState == GameState.None
                || entryState == GameState.Exit
                || entryState == GameState.Settle
                || entryState == GameState.SceneTransition)
            {
                Debug.LogWarning(
                    $"[Flow] SceneFlowConfig.entryState = {entryState} 非法：只允许 Menu / CG / Running / Paused。",
                    this);
            }
        }
#endif
    }
}
