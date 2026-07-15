using UnityEngine;

[DisallowMultipleComponent]
public sealed class PickupInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string itemName = "物品";
    [SerializeField] private bool disableOnPickup = true;

    public string InteractionPrompt => "拾取 " + itemName;
    public bool CanInteract => isActiveAndEnabled;

    public void Interact(PlayerInteraction interactor)
    {
        if (disableOnPickup)
            gameObject.SetActive(false);
    }
}
