using DeepseaOil.Logic.Services;
using DeepseaOil.Logic.Services.Time;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestTimer : MonoBehaviour
{
    private TimerHandle t;
    void Start()
    {
        TimerManager.Instance.Schedule(120f, () => { Debug.Log("test scale"); });
        t = TimerManager.Instance.ScheduleUnscaled(3f, () => { Debug.Log("test unscale"); });
        TimerManager.Instance.ScheduleRepeating(60f, () => { Debug.Log("test ScheduleRepeating"); });
        TimerManager.Instance.ScheduleRepeatingUnscaled(10f, () => { Debug.Log("test ScheduleRepeatingUnscaled"); });
    }

    // Update is called once per frame
    void Update()
    {
    }
}
