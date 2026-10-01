using UnityEngine.Events;
using DeepseaOil.Foundation;

namespace DeepseaOil.Presentation
{
    /// <summary>
    /// 公共Mono模块管理器
    /// </summary>
    public class MonoMgr : Singleton<MonoMgr>
    {
        private event UnityAction updateEvent;
        private event UnityAction fixedUpdateEvent;
        private event UnityAction lateUpdateEvent;

        public void AddUpdateListener(UnityAction updateFun)
        {
            updateEvent += updateFun;
        }

        public void RemoveUpdateListener(UnityAction updateFun)
        {
            updateEvent -= updateFun;
        }
        public void AddFixedUpdateListener(UnityAction updateFun)
        {
            fixedUpdateEvent += updateFun;
        }
        public void RemoveFixedUpdateListener(UnityAction updateFun)
        {
            fixedUpdateEvent -= updateFun;
        }
        public void AddLateUpdateListener(UnityAction updateFun)
        {
            lateUpdateEvent += updateFun;
        }
        public void RemoveLateUpdateListener(UnityAction updateFun)
        {
            lateUpdateEvent -= updateFun;
        }


        private void Update()
        {
            updateEvent?.Invoke();
        }

        private void FixedUpdate()
        {
            fixedUpdateEvent?.Invoke();
        }

        private void LateUpdate()
        {
            lateUpdateEvent?.Invoke();
        }
    }
}
