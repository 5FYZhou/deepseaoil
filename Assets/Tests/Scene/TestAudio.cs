using DeepseaOil.Data;
using DeepseaOil.Presentation;
using DeepseaOil.Presentation.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestAudio : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {

        UIMgr.Instance.ShowPanel<BeginPanel>();
        AudioManager.Instance.PlayBgm(AudioId.Bgm);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
