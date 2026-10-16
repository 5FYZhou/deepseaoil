using System;
using UnityEngine;

namespace DeepseaOil.Presentation.World
{
    [RequireComponent(typeof(Collider2D))]
    public sealed class PlayerMoveTrigger : MonoBehaviour
    {
        public event Action<PlayerController> PlayerExited;
        private bool playerIn;

        private void Awake()
        {
            GetComponent<Collider2D>().isTrigger = true;
        }

        private void OnTriggerStay2D(Collider2D collision)
        {
            PlayerController player = collision.GetComponentInParent<PlayerController>();
            if (player != null)
                playerIn=true;
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            PlayerController player = collision.GetComponentInParent<PlayerController>();
            if (player != null || playerIn)
                PlayerExited?.Invoke(player);
        }
    }
}
