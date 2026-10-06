using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using DeepseaOil.Data;
using DeepseaOil.Logic.Input;

namespace DeepseaOil.Presentation.UI
{
    /// <summary>
    /// 层级枚举
    /// </summary>
    public enum E_UILayer
    {
        /// <summary>
        /// 最底层
        /// </summary>
        Bottom,
        /// <summary>
        /// 中层
        /// </summary>
        Middle,
        /// <summary>
        /// 高层
        /// </summary>
        Top,
        /// <summary>
        /// 系统层 最高层
        /// </summary>
        System,
    }

    /// <summary>
    /// 管理所有UI面板的管理器。<b>普通类，由 <c>GameRoot</c> 持有</b>（半收口：构造权收回来了，
    /// 面板字典 / 栈 / 协程那套内部职责没动）。
    /// </summary>
    /// <remarks>
    /// 注意：面板预设体名要和面板类名一致！！！！！
    /// <para><b>收口前它是 <c>BaseManager&lt;UIMgr&gt;</c></b>（反射取私有构造的伪单例），
    /// 而且<b>私有构造里直接做 IO</b>（同步加载 + 实例化 UI 三件套）。
    /// 现在：构造是空的，三件套的创建搬进 <see cref="Init"/>，只有 <c>GameRoot</c> 调它 ——
    /// 于是"谁造 UI、什么时候造"有唯一答案（<c>GameRoot.Assemble</c>），
    /// 而不是"第一次有人碰 <c>UIMgr.Instance</c> 的那一刻"。</para>
    /// <para><b>为什么没有 <c>Reset</c>：</b>三件套与面板字典同生命周期，"复位自身状态"没有独立语义
    /// （清掉字典而不销毁面板反而会漏引用）；拆除统一由 <see cref="Dispose"/> 负责。
    /// 不留没有调用者的 API。</para>
    /// <para><b>唯一残留的取用点是 <c>MonoMgr</c></b>（协程宿主）：本类不是 <c>MonoBehaviour</c>，
    /// 面板异步加载要靠它 <c>StartCoroutine</c>。这是已知的临时例外，
    /// 且它的用户确实都在 UI 层内（全工程只有 <see cref="CoLoadPanel{T}"/> 一处）。</para>
    /// </remarks>
    public class UIMgr : IUIOperation
    {
        /// <summary>
        /// 主要用于里式替换原则 在字典中 用父类容器装载子类对象
        /// </summary>
        private abstract class BasePanelInfo
        {
            public bool isHide;
            public abstract BasePanel Panel { get; }
            public bool CanBeHide => Panel != null && Panel.CanBeHideByKey;

            public abstract void Hide(bool isDestroy);
        }

        /// <summary>
        /// 用于存储面板信息 和加载完成的回调函数的
        /// </summary>
        /// <typeparam name="T">面板的类型</typeparam>
        private class PanelInfo<T> : BasePanelInfo where T : BasePanel
        {
            public T panel;
            public UnityAction<T> callBack;
            public E_UILayer Layer => panel.Layer;

            /// <summary>回指管理器：<see cref="Hide"/> 要调实例方法，不能再走全局单例。</summary>
            private readonly UIMgr _owner;

            public PanelInfo(UIMgr owner, UnityAction<T> callBack)
            {
                _owner = owner;
                this.callBack += callBack;
            }

            public override BasePanel Panel => panel;

            public override void Hide(bool isDestroy)
            {
                _owner.HidePanel<T>();
            }
        }


        public Camera uiCamera;
        private Canvas uiCanvas;
        private EventSystem uiEventSystem;

        /// <summary>装配是否完成（未完成时所有面板操作是 no-op）。</summary>
        public bool IsReady { get; private set; }

        /// <summary>
        /// 是不是已经就"场景里多出来一份 EventSystem"报过一次。
        /// </summary>
        /// <remarks>
        /// 这条提示描述的是<b>一次性的接线错误</b>，不是每帧状态，所以只该报一次；
        /// 少了这个开关，它自己就会变成新的刷屏源——正是这次要修的那个病。
        /// <para>它改成实例字段（收口前是 static）：static 会跨 Play / 跨实例残留，
        /// 而 Domain Reload 关闭时那种残留最难查。</para>
        /// </remarks>
        private bool _warnedDuplicateEventSystem;

        //层级父对象
        private Transform bottomLayer;
        private Transform middleLayer;
        private Transform topLayer;
        private Transform systemLayer;

        // ── 资源 Key（交给 Data 层 AssetModule）──
        // 这里用的是「Resources 相对路径、不带扩展名」写法。表里存「相对 Assets/ 带扩展名」
        // 是给**配置表**定的，两套语义与转换点见 Docs/框架设计/分层设计/数据层.md §4；
        // AssetRegistry.ResolvePath 对两种形式都容忍，所以沿用旧 ResMgr 的 Key 字面量。
        private const string UI_CAMERA_KEY    = "ui/UICamera";
        private const string UI_CANVAS_KEY    = "ui/Canvas";
        private const string UI_EVENT_SYS_KEY = "ui/EventSystem";
        private const string UI_PANEL_PREFIX  = "ui/Panel/";

        /// <summary>
        /// 用于存储所有的面板对象
        /// </summary>
        private readonly Dictionary<string, BasePanelInfo> panelDic = new Dictionary<string, BasePanelInfo>();

        /// <summary>
        /// 用于按层存储已打开的面板，懒更新
        /// </summary>
        private readonly Dictionary<E_UILayer, Stack<BasePanelInfo>> openPanels = new()
        {
            { E_UILayer.Bottom, new Stack<BasePanelInfo>() },
            { E_UILayer.Middle, new Stack<BasePanelInfo>() },
            { E_UILayer.Top, new Stack<BasePanelInfo>() },
            { E_UILayer.System, new Stack<BasePanelInfo>() }
        };

        /// <summary>
        /// 构造<b>不做事</b>：资源 IO 全部在 <see cref="Init"/> 里。
        /// </summary>
        /// <remarks>
        /// 收口前这里直接 <c>AssetModule.Load</c> + <c>Instantiate</c> 三件套 ——
        /// 那让"<c>new UIMgr()</c>"这个动作本身产生资源 IO，装配顺序就再也说不清了。
        /// </remarks>
        public UIMgr()
        {
        }

        /// <summary>
        /// 装配 UI 三件套（摄像机 / 画布 / 事件系统）。<b>唯一调用点是 <c>GameRoot.Assemble</c>。</b>
        /// </summary>
        /// <remarks>
        /// 三件套从 <c>Assets/Resources/ui/</c> 取并 <c>DontDestroyOnLoad</c>：
        /// 它们跨场景常驻，而 <c>GameRoot</c> 本身现在也常驻，所以三者寿命一致。
        /// </remarks>
        public void Init()
        {
            if (IsReady)
            {
                Debug.LogError("[UI] UIMgr.Init 被调用了两次：它只该由 GameRoot 调一次。");
                return;
            }

            //动态创建唯一的Canvas和EventSystem（摄像机）
            uiCamera = GameObject.Instantiate(AssetModule.Load<GameObject>(UI_CAMERA_KEY)).GetComponent<Camera>();
            //ui摄像机过场景不移除 专门用来渲染UI面板
            GameObject.DontDestroyOnLoad(uiCamera.gameObject);

            //动态创建Canvas
            uiCanvas = GameObject.Instantiate(AssetModule.Load<GameObject>(UI_CANVAS_KEY)).GetComponent<Canvas>();
            //设置使用的UI摄像机
            uiCanvas.worldCamera = uiCamera;
            //过场景不移除
            GameObject.DontDestroyOnLoad(uiCanvas.gameObject);

            //找到层级父对象
            bottomLayer = uiCanvas.transform.Find("Bottom");
            middleLayer = uiCanvas.transform.Find("Middle");
            topLayer = uiCanvas.transform.Find("Top");
            systemLayer = uiCanvas.transform.Find("System");

            //场景里可能已经有一份 EventSystem（Unity 在用户新建 UI 文本时会自动补一个），
            //先把它停掉，再建自己那份，场上才只有一份。见方法注释。
            DisableSceneEventSystems();

            //动态创建EventSystem
            uiEventSystem = GameObject.Instantiate(AssetModule.Load<GameObject>(UI_EVENT_SYS_KEY)).GetComponent<EventSystem>();
            GameObject.DontDestroyOnLoad(uiEventSystem.gameObject);

            IsReady = true;
        }

        /// <summary>
        /// 拆除：销毁三件套（它们带走了全部面板实例）并把资源引用计数还给 <c>AssetModule</c>。
        /// </summary>
        /// <remarks>
        /// <b>必须早于 <c>AssetModule.Dispose</c></b>：<c>Release</c> 要经它。
        /// 必须幂等：未装配 / 重复调用都是 no-op。
        /// </remarks>
        public void Dispose()
        {
            if (!IsReady) return;

            IsReady = false;

            // 面板是 Canvas 的子物体：销毁 Canvas 会带走它们。这里只负责把引用计数还掉。
            foreach (KeyValuePair<string, BasePanelInfo> kv in panelDic)
            {
                AssetModule.Release(UI_PANEL_PREFIX + kv.Key);
            }

            panelDic.Clear();

            foreach (Stack<BasePanelInfo> stack in openPanels.Values)
            {
                stack.Clear();
            }

            if (uiEventSystem != null) GameObject.Destroy(uiEventSystem.gameObject);
            if (uiCanvas != null) GameObject.Destroy(uiCanvas.gameObject);
            if (uiCamera != null) GameObject.Destroy(uiCamera.gameObject);

            AssetModule.Release(UI_EVENT_SYS_KEY);
            AssetModule.Release(UI_CANVAS_KEY);
            AssetModule.Release(UI_CAMERA_KEY);

            uiEventSystem = null;
            uiCanvas = null;
            uiCamera = null;
            bottomLayer = null;
            middleLayer = null;
            topLayer = null;
            systemLayer = null;
        }

        /// <summary>
        /// 停用场景里多出来的 EventSystem（UIMgr 自己那份除外），并就"该去清理场景"报一次错。
        /// </summary>
        /// <remarks>
        /// <b>为什么要有这一步：</b>Unity 在用户"在场景里新建 UI 文本 / 按钮"时会自动补一个
        /// EventSystem（以及一个 Canvas）。而本框架的约定是 UI 三件套（Camera / Canvas /
        /// EventSystem）一律由 <c>UIMgr</c> 从 Resources 统一建、跨场景常驻，场景里不该有第二份。
        /// 两份同时在场上时，<c>UnityEngine.UI.EventSystem.Update</c> 每帧都会打一条
        /// 「There are 2 event systems in the scene.」——实测一局刷出 38627 条，真问题全被淹掉。
        /// 框架不该因为场景里多了一个 Unity 自动生成的物体就每帧刷日志。
        /// <para><b>为什么停用而不是销毁：</b>场景里那份是<b>用户的资产</b>，改场景是编辑器里的动作，
        /// 运行时替用户删物体既越权又无法持久化。停用足以让它走 <c>OnDisable</c> 从
        /// <c>EventSystem.m_EventSystems</c> 里摘掉，计数回到 1、警告随之消失；
        /// UI 的功能不受影响，因为跨场景的那份由 UIMgr 提供。
        /// <b>为什么用 includeInactive 的重载：</b>失活的那份同样要处理——它什么时候被谁激活，
        /// 警告就什么时候回来；这里一次性把场景里所有非 UIMgr 的份都停干净。</para>
        /// <para><b>为什么它必须只报一次：</b>见 <see cref="_warnedDuplicateEventSystem"/>。</para>
        /// </remarks>
        private void DisableSceneEventSystems()
        {
            //本方法在实例化 ui/EventSystem **之前**调用，此刻场上查得到的 EventSystem 都不是
            //UIMgr 自己那份。
            EventSystem[] sceneEventSystems = UnityEngine.Object.FindObjectsOfType<EventSystem>(true);

            foreach (EventSystem sceneEventSystem in sceneEventSystems)
            {
                //保留这个判断是为了让"绝不碰自己那份"成为方法自身的不变量，
                //而不是依赖调用点在装配序里的位置。
                if (sceneEventSystem == uiEventSystem)
                    continue;

                //停用：这一步才是真正止住每帧警告的动作
                sceneEventSystem.gameObject.SetActive(false);

                if (_warnedDuplicateEventSystem)
                    continue;

                _warnedDuplicateEventSystem = true;
                //带上 context，Console 里双击这条日志就能在 Hierarchy 里定位到场景那份
                Debug.LogError(
                    "[UI] 场景里已经有一份 EventSystem（以及建 UI 文本时 Unity 自动生成的 Canvas），" +
                    "UIMgr 已停用场景那份、改用自己从 Resources 创建并跨场景常驻的那一份；" +
                    "否则 Unity 会每帧刷一条「There are 2 event systems in the scene.」。\n" +
                    "清理方法：在 Hierarchy 里删掉场景自带的 EventSystem 与那个自动生成的 Canvas —— " +
                    "UI 的 Camera / Canvas / EventSystem 三件套由 UIMgr 从 Assets/Resources/ui/ 自动创建，" +
                    "场景里不需要第二份。\n" +
                    "这条只报一次（本局不会再刷）。",
                    sceneEventSystem);
            }
        }

        /// <summary>
        /// 获取对应层级的父对象
        /// </summary>
        /// <param name="layer">层级枚举值</param>
        /// <returns></returns>
        public Transform GetLayerFather(E_UILayer layer)
        {
            switch (layer)
            {
                case E_UILayer.Bottom:
                    return bottomLayer;
                case E_UILayer.Middle:
                    return middleLayer;
                case E_UILayer.Top:
                    return topLayer;
                case E_UILayer.System:
                    return systemLayer;
                default:
                    return null;
            }
        }

        /// <summary>
        /// 显示面板
        /// </summary>
        /// <typeparam name="T">面板的类型</typeparam>
        /// <param name="callBack">由于可能是异步加载 因此通过委托回调的形式 将加载完成的面板传递出去进行使用</param>
        /// <param name="isSync">是否采用同步加载。⚠️ 默认值为 true，但方法体**从不读它**——见蓝图 D7：
        /// 照它做会让唯一的调用方（GameRoot）走同步路径，异步链路永远跑不到。</param>
        public void ShowPanel<T>(UnityAction<T> callBack = null, bool isSync = true) where T : BasePanel
        {
            //获取面板名 预设体名必须和面板类名一致 
            string panelName = typeof(T).Name;
            //存在面板
            if (panelDic.ContainsKey(panelName))
            {
                //取出字典中已经占好位置的数据
                PanelInfo<T> panelInfo = panelDic[panelName] as PanelInfo<T>;
                //正在异步加载中
                if (panelInfo.panel == null)
                {
                    //如果之前显示了又隐藏 现在又想显示 那么直接设为false
                    panelInfo.isHide = false;

                    //如果正在异步加载 应该等待它加载完毕 只需要记录回调函数 加载完后去调用即可
                    if (callBack != null)
                        panelInfo.callBack += callBack;
                }
                else//已经加载结束
                {
                    //如果是失活状态 直接激活面板 就可以显示了
                    if (!panelInfo.panel.gameObject.activeSelf)
                        panelInfo.panel.gameObject.SetActive(true);

                    //如果要显示面板 会执行一次面板的默认显示逻辑
                    panelInfo.panel.ShowMe();
                    panelInfo.isHide = false;
                    //如果存在回调 直接返回出去即可
                    callBack?.Invoke(panelInfo.panel);
                    //添加到已打开面板栈中
                    openPanels[panelInfo.Layer].Push(panelInfo);
                }
                return;
            }

            //不存在面板 先存入字典当中 占个位置 之后如果又显示 我才能得到字典中的信息进行判断
            panelDic.Add(panelName, new PanelInfo<T>(this, callBack));

            //异步加载面板：用协程轮询 AsyncHandle，不用 await（理由见 CoLoadPanel 注释）
            //MonoMgr 是全工程唯一的协程宿主（UIMgr 不是 MonoBehaviour）
            MonoMgr.Instance.StartCoroutine(CoLoadPanel<T>(panelName));
        }

        /// <summary>
        /// 异步加载面板并挂到指定层。
        /// 为什么轮询而不是 await：AssetModule 在
        /// GameRoot.Update → AssetModule.Tick → 调度器分发 内部完成句柄，
        /// await 的续体会**内联**在那里执行——等于在调度器的分发循环里再进一次 UIMgr。
        /// 轮询把挂载推迟到下一帧（代价 1 帧），换掉那个重入风险。
        /// </summary>
        private System.Collections.IEnumerator CoLoadPanel<T>(string panelName) where T : BasePanel
        {
            string key = UI_PANEL_PREFIX + panelName;
            var handle = AssetModule.LoadAsync<GameObject>(key);

            while (!handle.IsDone)
                yield return null;

            //期间可能被 HidePanel 摘掉了占位
            if (!panelDic.TryGetValue(panelName, out var raw) || !(raw is PanelInfo<T> panelInfo))
            {
                AssetModule.Release(key);
                yield break;
            }

            //表示异步加载结束前 就想要隐藏该面板了
            if (panelInfo.isHide)
            {
                panelDic.Remove(panelName);
                AssetModule.Release(key);
                yield break;
            }

            var prefab = handle.Asset;
            if (prefab == null)
            {
                //降级资源也可能为 null：不实例化，摘掉占位以免永久占坑
                Debug.LogError($"[UI] 面板加载失败，已放弃显示：{panelName}");
                panelDic.Remove(panelName);
                yield break;
            }

            //将面板预设体创建到对应父对象下 并且保持原本的缩放大小
            GameObject panelObj = GameObject.Instantiate(prefab, middleLayer, false);

            //获取对应UI组件返回出去
            T panel = panelObj.GetComponent<T>();
            //层级的处理；避免没有按指定规则传递层级参数 避免为空
            Transform father = GetLayerFather(panel.Layer) ?? middleLayer;
            if (panel.transform.parent != father) 
                panel.transform.SetParent(father, false);

            //显示面板时执行的默认方法
            panel.ShowMe();
            //传出去使用
            panelInfo.callBack?.Invoke(panel);
            //回调执行完 将其清空 避免内存泄漏
            panelInfo.callBack = null;
            //存储panel
            panelInfo.panel = panel;
            //添加到以打开面板栈中
            openPanels[panelInfo.Layer].Push(panelInfo);
        }

        /// <summary>
        /// 隐藏面板
        /// </summary>
        /// <typeparam name="T">面板类型</typeparam>
        public void HidePanel<T>(bool isDestory = false) where T : BasePanel
        {
            string panelName = typeof(T).Name;
            if (panelDic.ContainsKey(panelName))
            {
                //取出字典中已经占好位置的数据
                PanelInfo<T> panelInfo = panelDic[panelName] as PanelInfo<T>;
                //但是正在加载中
                if (panelInfo.panel == null)
                {
                    //修改隐藏表示 表示 这个面板即将要隐藏
                    panelInfo.isHide = true;
                    //既然要隐藏了 回调函数都不会调用了 直接置空
                    panelInfo.callBack = null;
                }
                else//已经加载结束
                {
                    if (panelInfo.isHide)
                        return;                       // 已经在隐藏流程中，重入直接短路

                    panelInfo.isHide = true;
                    //如果要销毁  就直接将面板销毁从字典中移除记录
                    if (isDestory)
                    {
                        //销毁面板
                        GameObject.Destroy(panelInfo.panel.gameObject);
                        //从容器中移除
                        panelDic.Remove(panelName);
                        //与 ShowPanel 的 LoadAsync 成对：引用计数归零 → 进冷却期，不立即卸载
                        AssetModule.Release(UI_PANEL_PREFIX + panelName);
                    }
                    //如果不销毁 那么就只是失活 下次再显示的时候 直接复用即可
                    else
                        panelInfo.panel.gameObject.SetActive(false);
                    //执行默认的隐藏面板想要做的事情
                    panelInfo.panel.HideMe();
                }
            }
        }

        /// <summary>
        /// 获取面板
        /// </summary>
        /// <typeparam name="T">面板的类型</typeparam>
        public void GetPanel<T>(UnityAction<T> callBack) where T : BasePanel
        {
            string panelName = typeof(T).Name;
            if (panelDic.ContainsKey(panelName))
            {
                //取出字典中已经占好位置的数据
                PanelInfo<T> panelInfo = panelDic[panelName] as PanelInfo<T>;
                //正在加载中
                if (panelInfo.panel == null)
                {
                    //加载中 应该等待加载结束 再通过回调传递给外部去使用
                    panelInfo.callBack += callBack;
                }
                else if (!panelInfo.isHide)//加载结束 并且没有隐藏
                {
                    callBack?.Invoke(panelInfo.panel);
                }
            }
            else
            {
                Debug.LogWarning($"面板{typeof(T)}还未被加载");
            }
        }


        /// <summary>
        /// 为控件添加自定义事件
        /// </summary>
        /// <param name="control">对应的控件</param>
        /// <param name="type">事件的类型</param>
        /// <param name="callBack">响应的函数</param>
        public static void AddCustomEventListener(UIBehaviour control, EventTriggerType type, UnityAction<BaseEventData> callBack)
        {
            //这种逻辑主要是用于保证 控件上只会挂载一个EventTrigger
            EventTrigger trigger = control.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = control.gameObject.AddComponent<EventTrigger>();

            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = type;
            entry.callback.AddListener(callBack);

            trigger.triggers.Add(entry);
        }


        /// <summary>
        /// 尝试关闭可被Esc关闭的、最上层的面板
        /// 被调用时懒更新openPanels，移除已被Hide的面板
        /// </summary>
        /// <returns></returns>
        public bool TryCloseTopmostPanel()
        {
            E_UILayer[] layers = {E_UILayer.System, E_UILayer.Top, E_UILayer.Middle, E_UILayer.Bottom};

            foreach (var layer in layers)
            {
                var stack = openPanels[layer];

                while (stack.Count > 0)
                {
                    BasePanelInfo info = stack.Peek();

                    // 栈顶已经失效/隐藏，清掉继续找
                    if (info.Panel == null || info.isHide)
                    {
                        stack.Pop();
                        continue;
                    }

                    // 栈顶不能关闭
                    if (!info.Panel.CanBeHideByKey)
                    {
                        return false;
                    }

                    // 关闭栈顶
                    stack.Pop();
                    info.Hide(false);
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// 游戏进行时按Ecs调用
        /// </summary>
        public void OpenPausePanel()
        {
            ShowPanel<PausePanel>();
        }

        /// <summary>
        /// 开始菜单界面按Esc调用
        /// </summary>
        public void OpenExitConfirmPanel()
        {
            ShowPanel<ExitConfirmPanel>();
        }
    }
}
