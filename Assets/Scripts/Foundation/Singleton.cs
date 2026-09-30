using UnityEngine;


/*
 * 本文件有部分参考祝老师（项目《恶龙与律师》）
 * 用途：仅用于学习与非商业Game Jam作品
 */


namespace DeepseaOil.Foundation
{
    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T instance;
        public static T Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<T>();
                    if (instance == null)
                    {
                        GameObject obj = new GameObject(typeof(T).ToString());
                        instance = obj.AddComponent<T>();
                    }
                    DontDestroyOnLoad(instance.gameObject);
                }
                return instance;
            }
        }

        protected virtual void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this as T;
            DontDestroyOnLoad(gameObject);
        }
    }
}
