using NetCraft.Client.Blaze3d.Textures;

namespace NetCraft.Client.Blaze3d.Systems;

//GpuSurfaceBackend raw swapchain operations, aligns with vanilla GpuSurfaceBackend
public interface GpuSurfaceBackend : IDisposable
{
    void Configure(GpuSurface.Configuration configuration);

    bool IsSuboptimal { get; }

    void AcquireNextTexture();

    void BlitFromTexture(CommandEncoderBackend commandEncoder, GpuTextureView textureView);

    void Present();

    IReadOnlyCollection<GpuSurface.PresentMode> SupportedPresentModes();
}
