using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using DeepseaOil.Data;
using DeepseaOil.Logic.Input;

namespace DeepseaOil.Presentation.UI
{
    public enum E_UILayer
    {
        Bottom,
        Middle,
        Top,
        System,
    }

    /// <summary>管理所有 UI 面板。<b>普通类，由 <c>GameRoot</c> 持有</b>（普通类不是 <c>MonoBehaviour</c>，面板异步加载要靠 <c>MonoMgr</c> 这个协程宿主）。</summary>
    /// <remarks><b>面板预设体名必须和面板类名一致。</b>构造是空的、三件套的创建在 <see cref="Init"/>：<c>GameRoot</c> 是唯一调用点，于是"谁造 UI、什么时候造"有唯一答案。</remarks>
    public class UIMgr : IUIOperation
    {
        private abstract class BasePanelInfo
        {
            public bool isHide;
            public abstract BasePanel Panel { get; }
            public bool CanBeHide => Panel != null && Panel.CanBeHideByKey;

            public abstract void Hide(bool isDestroy);
        }

        private class PanelInfo<T> : BasePanelInfo where T : BasePanel
        {
            public T panel;
            public UnityAction<T> callBack;
            public E_UILayer Layer => panel.Layer;

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

        /// <summary>是否已就"场景里多出来一份 EventSystem"报过一次（那是接线错误、不是每帧状态；少了这个开关它自己会变成新的刷屏源）。</summary>
        private bool _warnedDuplicateEventSystem;

        private Transform bottomLayer;
        private Transform middleLayer;
        private Transform topLayer;
        private Transform systemLayer;

        // 资源 Key：用「Resources 相对路径、不带扩展名」写法（表里存的是「相对 Assets/ 带扩展名」，那是配置表的口径；AssetRegistry.ResolvePath 对两种形式都容忍）。
        private const string UI_CAMERA_KEY    = "ui/UICamera";
        private const string UI_CANVAS_KEY    = "ui/Canvas";
        private const string UI_EVENT_SYS_KEY = "ui/EventSystem";
        private const string UI_PANEL_PREFIX  = "ui/Panel/";

        private readonly Dictionary<string, BasePanelInfo> panelDic = new Dictionary<string, BasePanelInfo>();

        private readonly Dictionary<E_UILayer, Stack<BasePanelInfo>> openPanels = new()
        {
            { E_UILayer.Bottom, new Stack<BasePanelInfo>() },
            { E_UILayer.Middle, new Stack<BasePanelInfo>() },
            { E_UILayer.Top, new Stack<BasePanelInfo>() },
            { E_UILayer.System, new Stack<BasePanelInfo>() }
        };

        public UIMgr()
        {
        }

        /// <summary>装配 UI 三件套（摄像机 / 画布 / 事件系统）。<b>唯一调用点是 <c>GameRoot.Assemble</c>。</b></summary>
        public void Init()
        {
            if (IsReady)
            {
                Debug.LogError("[UI] UIMgr.Init 被调用了两次：它只该由 GameRoot 调一次。");
                return;
            }

            uiCamera = GameObject.Instantiate(AssetModule.Load<GameObject>(UI_CAMERA_KEY)).GetComponent<Camera>();
            GameObject.DontDestroyOnLoad(uiCamera.gameObject);

            uiCanvas = GameObject.Instantiate(AssetModule.Load<GameObject>(UI_CANVAS_KEY)).GetComponent<Canvas>();
            uiCanvas.worldCamera = uiCamera;
            GameObject.DontDestroyOnLoad(uiCanvas.gameObject);

            bottomLayer = uiCanvas.transform.Find("Bottom");
            middleLayer = uiCanvas.transform.Find("Middle");
            topLayer = uiCanvas.transform.Find("Top");
            systemLayer = uiCanvas.transform.Find("System");

            DisableSceneEventSystems();

            uiEventSystem = GameObject.Instantiate(AssetModule.Load<GameObject>(UI_EVENT_SYS_KEY)).GetComponent<EventSystem>();
            GameObject.DontDestroyOnLoad(uiEventSystem.gameObject);

            IsReady = true;
        }

        /// <summary>拆除：销毁三件套（它们带走了全部面板实例）并把资源引用计数还给 <c>AssetModule</c>。</summary>
        /// <remarks><b>必须早于 <c>AssetModule.Dispose</c></b>（<c>Release</c> 要经它）；必须幂等。</remarks>
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

        /// <summary>停用场景里多出来的 EventSystem（UIMgr 自己那份除外），并就"该去清理场景"报一次错。</summary>
        /// <remarks>Unity 在用户"在场景里新建 UI 文本 / 按钮"时会自动补一个 EventSystem，而两份同时在场上时它会每帧打一条「There are 2 event systems in the scene.」。
        /// <b>停用而不是销毁</b>：场景里那份是用户的资产，运行时替用户删物体既越权又无法持久化；停用足以让它走 <c>OnDisable</c> 从 <c>EventSystem.m_EventSystems</c> 里摘掉。
        /// <b>用 includeInactive 的重载</b>：失活的那份同样要处理 —— 它什么时候被谁激活，警告就什么时候回来。</remarks>
        private void DisableSceneEventSystems()
        {
            //本方法在实例化 ui/EventSystem **之前**调用，此刻场上查得到的 EventSystem 都不是 UIMgr 自己那份。
            EventSystem[] sceneEventSystems = UnityEngine.Object.FindObjectsOfType<EventSystem>(true);

            foreach (EventSystem sceneEventSystem in sceneEventSystems)
            {
                //绝不自碰：不依赖调用点在装配序里的位置
                if (sceneEventSystem == uiEventSystem)
                    continue;

                sceneEventSystem.gameObject.SetActive(false);

                if (_warnedDuplicateEventSystem)
                    continue;

                _warnedDuplicateEventSystem = true;
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

        /// <summary>显示面板（可能在异步加载中，故用回调交付）；<paramref name="isSync"/> ⚠️ 默认 <c>true</c>，但方法体<b>从不读它</b>。</summary>
        public void ShowPanel<T>(UnityAction<T> callBack = null, bool isSync = true) where T : BasePanel
        {
            string panelName = typeof(T).Name;
            if (panelDic.ContainsKey(panelName))
            {
                PanelInfo<T> panelInfo = panelDic[panelName] as PanelInfo<T>;
                if (panelInfo.panel == null)
                {
                    panelInfo.isHide = false;

                    if (callBack != null)
                        panelInfo.callBack += callBack;
                }
                else
                {
                    if (!panelInfo.panel.gameObject.activeSelf)
                        panelInfo.panel.gameObject.SetActive(true);

                    panelInfo.panel.ShowMe();
                    panelInfo.isHide = false;
                    callBack?.Invoke(panelInfo.panel);
                    openPanels[panelInfo.Layer].Push(panelInfo);
                }
                return;
            }

            //先占位，之后再次显示才能从字典里拿到状态
            panelDic.Add(panelName, new PanelInfo<T>(this, callBack));

            //MonoMgr 是全工程唯一的协程宿主（UIMgr 不是 MonoBehaviour）
            MonoMgr.Instance.StartCoroutine(CoLoadPanel<T>(panelName));
        }

        /// <summary>异步加载面板并挂到指定层。</summary>
        /// <remarks>轮询而不是 <c>await</c>：句柄在 <c>AssetModule.Tick</c> 的分发循环内部完成，<c>await</c> 的续体会内联在那里执行，等于在调度器的分发循环里再进一次 UIMgr。
        /// 轮询把挂载推迟到下一帧（代价 1 帧），换掉那个重入风险。</remarks>
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

            GameObject panelObj = GameObject.Instantiate(prefab, middleLayer, false);

            T panel = panelObj.GetComponent<T>();
            //层级兜底：面板没按规则给层级时落到 middleLayer
            Transform father = GetLayerFather(panel.Layer) ?? middleLayer;
            if (panel.transform.parent != father) 
                panel.transform.SetParent(father, false);

            panel.ShowMe();
            panelInfo.callBack?.Invoke(panel);
            panelInfo.callBack = null;
            panelInfo.panel = panel;
            openPanels[panelInfo.Layer].Push(panelInfo);
        }

        public void HidePanel<T>(bool isDestory = false) where T : BasePanel
        {
            string panelName = typeof(T).Name;
            if (panelDic.ContainsKey(panelName))
            {
                PanelInfo<T> panelInfo = panelDic[panelName] as PanelInfo<T>;
                if (panelInfo.panel == null)
                {
                    panelInfo.isHide = true;
                    panelInfo.callBack = null;
                }
                else
                {
                    if (panelInfo.isHide)
                        return;                       // 已经在隐藏流程中，重入直接短路

                    panelInfo.isHide = true;
                    if (isDestory)
                    {
                        GameObject.Destroy(panelInfo.panel.gameObject);
                        panelDic.Remove(panelName);
                        //与 ShowPanel 的 LoadAsync 成对：引用计数归零 → 进冷却期，不立即卸载
                        AssetModule.Release(UI_PANEL_PREFIX + panelName);
                    }
                    else
                        panelInfo.panel.gameObject.SetActive(false);
                    panelInfo.panel.HideMe();
                }
            }
        }

        /// <summary>获取面板（加载中就挂回调，加载完且未隐藏才立刻回调）。</summary>
        public void GetPanel<T>(UnityAction<T> callBack) where T : BasePanel
        {
            string panelName = typeof(T).Name;
            if (panelDic.ContainsKey(panelName))
            {
                PanelInfo<T> panelInfo = panelDic[panelName] as PanelInfo<T>;
                if (panelInfo.panel == null)
                {
                    panelInfo.callBack += callBack;
                }
                else if (!panelInfo.isHide)
                {
                    callBack?.Invoke(panelInfo.panel);
                }
            }
            else
            {
                Debug.LogWarning($"面板{typeof(T)}还未被加载");
            }
        }


        /// <summary>为控件添加自定义事件（控件上只挂一个 <c>EventTrigger</c>）。</summary>
        public static void AddCustomEventListener(UIBehaviour control, EventTriggerType type, UnityAction<BaseEventData> callBack)
        {
            EventTrigger trigger = control.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = control.gameObject.AddComponent<EventTrigger>();

            EventTrigger.Entry entry = new EventTrigger.Entry();
            entry.eventID = type;
            entry.callback.AddListener(callBack);

            trigger.triggers.Add(entry);
        }


        /// <summary>尝试关闭可被 Esc 关闭的、最上层的面板；顺带懒更新 openPanels（移除已 Hide 的）。</summary>
        public bool TryCloseTopmostPanel()
        {
            E_UILayer[] layers = {E_UILayer.System, E_UILayer.Top, E_UILayer.Middle, E_UILayer.Bottom};

            foreach (var layer in layers)
            {
                var stack = openPanels[layer];

                while (stack.Count > 0)
                {
                    BasePanelInfo info = stack.Peek();

                    if (info.Panel == null || info.isHide)
                    {
                        stack.Pop();
                        continue;
                    }

                    if (!info.Panel.CanBeHideByKey)
                    {
                        return false;
                    }

                    stack.Pop();
                    info.Hide(false);
                    return true;
                }
            }
            return false;
        }
        
        public void OpenPausePanel()
        {
            ShowPanel<PausePanel>();
        }

        public void OpenExitConfirmPanel()
        {
            ShowPanel<ExitConfirmPanel>();
        }
    }
}
