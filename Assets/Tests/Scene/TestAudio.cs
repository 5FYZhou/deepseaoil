using DeepseaOil.Data;
using DeepseaOil.Presentation;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestAudio : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        // 音频已去单例（由 GameRoot 持有并作为 IService 驱动），取值经 GameRoot。
        // 入队而不是直接播：请求在 GameRoot 的服务通道（unscaled 时间）里被统一消费。
        GameRoot.Instance.Audio.EnqueueBgm(AudioId.Bgm);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
