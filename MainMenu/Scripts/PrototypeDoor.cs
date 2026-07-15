using UnityEngine;

namespace PlayerPrototype
{
    public sealed class PrototypeDoor : MonoBehaviour, IPrototypeInteractable
    {
        [SerializeField] private string openPrompt = "开门";
        [SerializeField] private string closePrompt = "关门";
        [SerializeField] private float openAngle = 90f;
        [SerializeField] private float rotationSpeed = 180f;

        private Quaternion closedRotation;
        private Quaternion openRotation;
        private bool isOpen;

        public string InteractionPrompt
        {
            get { return isOpen ? closePrompt : openPrompt; }
        }

        private void Awake()
        {
            closedRotation = transform.localRotation;
            openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
        }

        private void Update()
        {
            Quaternion targetRotation = isOpen ? openRotation : closedRotation;
            transform.localRotation = Quaternion.RotateTowards(transform.localRotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        public void Interact()
        {
            isOpen = !isOpen;
        }
    }
}
