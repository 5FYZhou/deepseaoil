using DeepseaOil.Presentation.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeepseaOil.Presentation
{

    /// 默认开始场景打开开始面板

    public class BeginSceneInitializer : MonoBehaviour
    {
        private void Start()
        {
            GameRoot.Instance.UI.ShowPanel<BeginPanel>();
        }
    }
}
