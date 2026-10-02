using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioRoot : MonoBehaviour
{
    private void Awake()
    {
        DontDestroyOnLoad(this);
    }
}
