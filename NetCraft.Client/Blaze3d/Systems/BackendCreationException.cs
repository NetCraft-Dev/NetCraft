namespace NetCraft.Client.Blaze3d.Systems;

//BackendCreationException thrown when a GPU backend cannot be created, aligns with vanilla BackendCreationException
public sealed class BackendCreationException : Exception
{
    public Reason FailureReason { get; }
    public IReadOnlyList<string> MissingCapabilities { get; }

    public BackendCreationException(string message, Reason reason) : this(message, reason, Array.Empty<string>()) { }

    public BackendCreationException(string message, Reason reason, IReadOnlyList<string> missingCapabilities)
        : base(message)
    {
        FailureReason = reason;
        MissingCapabilities = missingCapabilities;
    }

    //Reason backend creation failure cause, maps to vanilla BackendCreationException.Reason
    public enum Reason
    {
        GlfwError,
        VulkanLoaderMissing,
        VulkanInstanceCreationFailed,
        VulkanNoDevice,
        VulkanDeviceVersionTooLow,
        VulkanNoGraphicsQueue,
        VulkanMissingExtension,
        VulkanMissingFeature,
        OpenGlMissing,
        Other
    }
}
