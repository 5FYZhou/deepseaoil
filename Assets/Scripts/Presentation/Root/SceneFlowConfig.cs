using DeepseaOil.Logic;
using UnityEngine;

namespace DeepseaOil.Presentation
{
    /// <summary>场景声明的入口状态：Attach 时推一次，取代 GameRoot 装配末尾的默认 Menu</summary>
    /// <remarks>Order 取 SceneOrder.Entry：其余场景根的 Attach 都排在它之后，首次装配时就已在入口状态。</remarks>
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

            // ChangeState 内部已有幂等早退：目标与当前一致时无副作用
            root.Game.ChangeState(entryState);

            Debug.Log($"[Flow] 场景声明入口状态 {entryState}（{gameObject.scene.name}）");
        }

        private void Start()
        {
            _root = GameRoot.Instance;
            if (_root != null) _root.RegisterSceneRoot(this);
        }

        private void OnDestroy()
        {
            if (_root != null) _root.UnregisterSceneRoot(this);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (entryState == GameState.None || entryState == GameState.Exit)
            {
                Debug.LogWarning(
                    $"[Flow] SceneFlowConfig.entryState = {entryState} 非法：只允许 Menu / CG / Running / Paused。",
                    this);
            }
        }
#endif
    }
}
