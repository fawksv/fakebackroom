using UnityEngine;

public class FlashlightPickup : MonoBehaviour, IInteractable
{
    public string InteractionPrompt => "拾取手电";
    public bool CanInteract => true;

    public void Interact(PlayerInteraction interactor)
    {
        FlashlightController flashlight = interactor.GetComponentInChildren<FlashlightController>(true);
        if (flashlight == null)
        {
            Camera playerCamera = interactor.GetComponentInChildren<Camera>(true);
            if (playerCamera == null)
            {
                Debug.LogWarning(
                    "FlashlightPickup: Player 下没有找到 Camera，无法创建手电筒灯光。",
                    this);
                return;
            }

            GameObject lightObject = new GameObject("FlashlightLight");
            lightObject.transform.SetParent(playerCamera.transform, false);
            lightObject.transform.localPosition = Vector3.zero;
            lightObject.transform.localRotation = Quaternion.identity;

            flashlight = lightObject.AddComponent<FlashlightController>();
        }

        flashlight.enabled = true;
        Destroy(gameObject);
    }
}
