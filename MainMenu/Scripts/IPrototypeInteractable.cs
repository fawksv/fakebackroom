namespace PlayerPrototype
{
    public interface IPrototypeInteractable
    {
        string InteractionPrompt { get; }

        void Interact();
    }
}
