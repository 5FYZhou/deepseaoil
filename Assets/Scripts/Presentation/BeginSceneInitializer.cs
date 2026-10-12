using DeepseaOil.Presentation.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Presentation
{
    /// <summary>
    /// 默认开始场景打开开始面板
    /// </summary>
    public class BeginSceneInitializer : MonoBehaviour
    {
        private void Start()
        {
            GameRoot.Instance.UI.ShowPanel<BeginPanel>();
        }
    }
}
