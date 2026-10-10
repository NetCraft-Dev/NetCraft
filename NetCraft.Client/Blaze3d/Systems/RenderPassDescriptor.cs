using System.Numerics;
using NetCraft.Client.Blaze3d.Textures;

namespace NetCraft.Client.Blaze3d.Systems;

//RenderPassDescriptor describes the attachments of a render pass, aligns with vanilla RenderPassDescriptor
public sealed class RenderPassDescriptor
{
    private readonly Func<string> _label;

    public List<Attachment<Vector4?>?> ColorAttachments { get; } = new();
    public Attachment<double?>? DepthAttachment { get; set; }
    public RenderPass.RenderArea? RenderArea { get; set; }

    private RenderPassDescriptor(Func<string> label)
    {
        _label = label;
    }

    public static RenderPassDescriptor Create(Func<string> label) => new(label);

    public RenderPassDescriptor WithColorAttachment(GpuTextureView textureView)
        => WithColorAttachment(textureView, null);

    public RenderPassDescriptor WithColorAttachment(GpuTextureView textureView, Vector4? clearValue)
    {
        ColorAttachments.Add(new Attachment<Vector4?>(textureView, clearValue));
        return this;
    }

    public RenderPassDescriptor WithUnusedColorAttachment()
    {
        ColorAttachments.Add(null);
        return this;
    }

    public RenderPassDescriptor WithDepthAttachment(GpuTextureView textureView)
        => WithDepthAttachment(textureView, null);

    public RenderPassDescriptor WithDepthAttachment(GpuTextureView textureView, double? clearValue)
    {
        DepthAttachment = new Attachment<double?>(textureView, clearValue);
        return this;
    }

    public RenderPassDescriptor WithRenderArea(RenderPass.RenderArea renderArea)
    {
        RenderArea = renderArea;
        return this;
    }

    public Func<string> Label => _label;

    //Attachment a texture view plus the value to clear it with, null clearValue means load the existing content
    public sealed record Attachment<T>(GpuTextureView TextureView, T ClearValue);
}
