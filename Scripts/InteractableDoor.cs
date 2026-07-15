using UnityEngine;

[DisallowMultipleComponent]
public sealed class InteractableDoor : MonoBehaviour, IInteractable
{
    [Header("提示")]
    [SerializeField] private string openPrompt = "开门";
    [SerializeField] private string closePrompt = "关门";

    [Header("旋转")]
    [SerializeField] private float openAngle = 90f;
    [SerializeField, Min(0f)] private float rotationSpeed = 180f;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private bool isOpen;

    public string InteractionPrompt => isOpen ? closePrompt : openPrompt;
    public bool CanInteract => true;

    private void Awake()
    {
        closedRotation = transform.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
    }

    private void Update()
    {
        Quaternion targetRotation = isOpen ? openRotation : closedRotation;
        transform.localRotation = Quaternion.RotateTowards(
            transform.localRotation,
            targetRotation,
            rotationSpeed * Time.deltaTime);
    }

    public void Interact(PlayerInteraction interactor)
    {
        isOpen = !isOpen;
    }
}
