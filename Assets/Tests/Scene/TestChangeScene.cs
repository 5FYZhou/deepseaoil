using DeepseaOil.Logic.Events;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace DeepseaOil.Presentation
{
    public class TestChangeScene : MonoBehaviour
    {
        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision != null)
            {
                EventBus<RequestChangeScene>.Publish(new RequestChangeScene("TestConfig"));
            }
        }
    }
}
