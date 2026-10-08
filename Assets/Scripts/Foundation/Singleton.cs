using UnityEngine;

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
