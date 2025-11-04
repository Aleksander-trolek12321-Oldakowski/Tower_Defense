namespace Controller
{
    public interface IInteractable
    {
        /// <summary>
        /// Called when the local player taps/clicks the object.
        /// Implement game-specific logic here (e.g. open build menu, select spot).
        /// </summary>
        void OnInteract();
    }
}
