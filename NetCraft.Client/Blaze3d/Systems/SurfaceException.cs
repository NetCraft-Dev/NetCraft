namespace NetCraft.Client.Blaze3d.Systems;

//SurfaceException thrown when a surface configuration or acquisition fails, aligns with vanilla SurfaceException
public sealed class SurfaceException : Exception
{
    public SurfaceException(string message) : base(message) { }

    public SurfaceException(Exception cause) : base(cause.Message, cause) { }
}
