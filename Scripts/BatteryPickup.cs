using UnityEngine;

public class BatteryPickup : MonoBehaviour, IInteractable
{
    public string InteractionPrompt => "拾取电池";

    public bool CanInteract => true;

    public void Interact(PlayerInteraction interactor)
    {
        Inventory inv = interactor.GetComponent<Inventory>();
        if (inv == null)
            return;

        if (inv.AddItem(Inventory.ItemType.Battery))
            Destroy(gameObject);
    }
}
