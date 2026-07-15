using UnityEngine;

namespace PlayerPrototype
{
    public sealed class PrototypePickup : MonoBehaviour, IPrototypeInteractable
    {
        [SerializeField] private string itemName = "物品";
        [SerializeField] private bool disableOnPickup = true;

        public string InteractionPrompt
        {
            get { return "拾取 " + itemName; }
        }

        public void Interact()
        {
            if (disableOnPickup)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
