using NetCraft.Client.Blaze3d.Textures;

namespace NetCraft.Client.Blaze3d.Systems;

//GpuSurface a swapchain-backed presentation surface, aligns with vanilla com.mojang.blaze3d.systems.GpuSurface
//Wraps GpuSurfaceBackend and tracks the acquire/blit/present state machine
public sealed class GpuSurface : IDisposable
{
    private readonly GpuSurfaceBackend _backend;
    private bool _hasImageAcquired;
    private bool _hasBlittedTexture;
    private Configuration? _currentConfiguration;

    public GpuSurface(GpuSurfaceBackend backend)
    {
        _backend = backend;
    }

    public void Dispose()
    {
        if (_hasImageAcquired)
            throw new InvalidOperationException("Cannot close a surface while it is acquired");
        _backend.Dispose();
    }

    public void Configure(Configuration config)
    {
        if (_hasImageAcquired)
            throw new InvalidOperationException("Cannot configure a surface while it is acquired");
        if (!SupportedPresentModes().Contains(config.PresentMode))
            throw new SurfaceException($"Surface does not support present mode {config.PresentMode} (supported: {string.Join(", ", SupportedPresentModes())})");
        _backend.Configure(config);
        _currentConfiguration = config;
    }

    public Configuration? CurrentConfiguration => _currentConfiguration;

    public IReadOnlyCollection<PresentMode> SupportedPresentModes() => _backend.SupportedPresentModes();

    public bool IsSuboptimal => _backend.IsSuboptimal;

    public bool IsAcquired => _hasImageAcquired;

    public void AcquireNextTexture()
    {
        if (_hasImageAcquired)
            throw new InvalidOperationException("Cannot acquire a surface while it is already acquired");
        if (_currentConfiguration == null)
            throw new InvalidOperationException("Cannot acquire an unconfigured surface");
        _backend.AcquireNextTexture();
        _hasImageAcquired = true;
        _hasBlittedTexture = false;
    }

    public void BlitFromTexture(CommandEncoder commandEncoder, GpuTextureView textureView)
    {
        if (commandEncoder.IsInRenderPass)
            throw new InvalidOperationException("Close the existing render pass before presenting with a command encoder");
        if (!textureView.Texture.Format.HasColorAspect())
            throw new InvalidOperationException("Cannot present a non-color texture!");
        if ((textureView.Texture.Usage & GpuTexture.UsageCopySrc) == 0)
            throw new InvalidOperationException("Color texture must have USAGE_COPY_SRC to be presented to the screen");
        if (textureView.Texture.DepthOrLayers > 1)
            throw new NotSupportedException("Textures with multiple depths or layers are not yet supported for presentation");
        if (!_hasImageAcquired)
            throw new InvalidOperationException("Cannot present to an unacquired surface");
        if (_hasBlittedTexture)
            throw new InvalidOperationException("Already blitted to this frame!");
        _backend.BlitFromTexture(commandEncoder.Backend, textureView);
        _hasBlittedTexture = true;
    }

    public void Present()
    {
        if (!_hasImageAcquired)
            throw new InvalidOperationException("Cannot present to a surface if it isn't acquired");
        if (!_hasBlittedTexture)
            throw new InvalidOperationException("Must blit to surface before presenting!");
        _backend.Present();
        _hasImageAcquired = false;
    }

    //GetSupportedVsyncMode the preferred present mode for the vsync setting, maps to vanilla PresentMode.getSupportedVsyncMode
    public static PresentMode GetSupportedVsyncMode(IReadOnlyCollection<PresentMode> supportedModes, bool vsync)
    {
        PresentMode[] preferred = vsync
            ? new[] { PresentMode.FifoRelaxed, PresentMode.Fifo }
            : new[] { PresentMode.Immediate, PresentMode.Mailbox, PresentMode.Fifo };
        foreach (var mode in preferred)
        {
            if (supportedModes.Contains(mode))
                return mode;
        }
        throw new InvalidOperationException("No supported presentation mode was found");
    }

    //Configuration surface size and present mode, maps to vanilla GpuSurface.Configuration
    public sealed record Configuration(int Width, int Height, PresentMode PresentMode);

    //PresentMode swapchain present modes, maps to vanilla GpuSurface.PresentMode
    public enum PresentMode
    {
        Immediate,
        Mailbox,
        Fifo,
        FifoRelaxed
    }
}
