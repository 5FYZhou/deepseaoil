using DeepseaOil.Logic;
using DeepseaOil.Logic.Events;
using DeepseaOil.Logic.Input;
using DeepseaOil.Presentation.UI;
using UnityEngine;

namespace DeepseaOil.Presentation
{
    /// <summary>
    /// 游戏状态机：切状态时连带切面板、发暂停 / 恢复意图。<b>普通类，由 <c>GameRoot</c> 持有。</b>
    /// </summary>
    /// <remarks>
    /// <b>它不再是自己 <c>new</c> 自己的单例。</b>收口前它是 <c>BaseManager&lt;GameManager&gt;</c>
    /// （反射取私有构造），"谁造它、谁清它"没有答案；现在唯一的构造点是
    /// <c>GameRoot.Assemble</c>，别人只能经 <c>GameRoot.Instance.Game</c> 拿到。
    /// <para><b>面板操作走构造注入的 <c>UIMgr</c>，不是全局入口</b>：本类只认识"UI 能干这几件事"，
    /// 不认识 <c>GameRoot</c>，所以它不参与循环依赖。</para>
    /// <para><b><see cref="GameState"/> 已搬到 <c>Logic/Input/GameState.cs</c></b>：
    /// 枚举是逻辑概念（<c>UILogicContext</c> 要带它进逻辑层），而"切状态要做什么"是表现层的事。</para>
    /// <para><b>它同时是 <see cref="IUIStateRequest"/> 的实现</b>：UI 输入逻辑（逻辑层）只能"请求"切状态，
    /// 不能直接调它 —— 依赖方向保持 Logic → 接口。</para>
    /// </remarks>
    public sealed class GameManager : IUIStateRequest
    {
        private readonly UIMgr _ui;

        /// <summary>当前状态。<b>只读</b>：改它只有 <see cref="ChangeState"/> 一条路。</summary>
        public GameState CurState { get; private set; } = GameState.None;

        /// <param name="ui">面板操作口；由 <c>GameRoot</c> 注入。</param>
        public GameManager(UIMgr ui)
        {
            _ui = ui;
        }

        /// <inheritdoc />
        public void RequestState(GameState state)
        {
            ChangeState(state);
        }

        /// <summary>
        /// 切换游戏状态。同状态是 no-op。
        /// </summary>
        /// <remarks>面板的显示 / 隐藏与暂停意图都在这里发生，所以它是"状态"的唯一入口。</remarks>
        public void ChangeState(GameState newState)
        {
            if (CurState == newState) return;

            switch (newState)
            {
                case GameState.Menu:
                    _ui.ShowPanel<BeginPanel>();

                    if (CurState == GameState.Paused)
                        _ui.HidePanel<PausePanel>();

                    // 回菜单时收起战斗 HUD：它是"局内读数"，留在菜单上会盖住开始面板。
                    _ui.HidePanel<HudPanel>();

                    // 请求暂停（PlayerController 监听了暂停事件，暂停时会关掉输入）
                    EventBus<RequestPause>.Publish(new RequestPause());
                    break;

                case GameState.Running:
                    // 请求恢复
                    EventBus<RequestResume>.Publish(new RequestResume());

                    // 关闭面板
                    if (CurState == GameState.Menu)
                        _ui.HidePanel<BeginPanel>();
                    if (CurState == GameState.Paused)
                        _ui.HidePanel<PausePanel>();

                    // 打开玩家面板（战斗 HUD）
                    _ui.ShowPanel<HudPanel>();
                    break;

                case GameState.Paused:
                    // 请求暂停
                    EventBus<RequestPause>.Publish(new RequestPause());
                    // 打开暂停面板
                    _ui.ShowPanel<PausePanel>();
                    break;

                case GameState.BeforeExit:
                    _ui.ShowPanel<ExitConfirmPanel>();
                    break;

                case GameState.Exit:
                    // 退出游戏
                    Application.Quit();
                    break;
            }

            CurState = newState;
        }
    }
}
