using System.Numerics;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;

namespace NetCraft.Client.Blaze3d.Systems;

//CommandEncoder records and submits GPU commands, aligns with vanilla com.mojang.blaze3d.systems.CommandEncoder
//Vanilla owns a TracyGpuProfiler here; NetCraft has no profiler, so the constructor drops that argument
public sealed class CommandEncoder : IDisposable
{
    private readonly GpuDeviceBackend _device;
    private readonly CommandEncoderBackend _backend;
    private bool _isInRenderPass;

    public CommandEncoder(GpuDeviceBackend device, CommandEncoderBackend backend)
    {
        _device = device;
        _backend = backend;
    }

    public CommandEncoderBackend Backend => _backend;

    //Dispose releases the underlying backend, which owns the command buffer and fence
    public void Dispose() => _backend.Dispose();

    public void Submit() => _backend.Submit();

    public TransientMemory TransientMemory() => _backend.TransientMemory();

    public bool IsInRenderPass => _isInRenderPass;

    public RenderPass CreateRenderPass(Func<string> label, GpuTextureView colorTexture, Vector4? clearColor)
        => CreateRenderPass(label, colorTexture, clearColor, null, null);

    public RenderPass CreateRenderPass(Func<string> label, GpuTextureView colorTexture, Vector4? clearColor, GpuTextureView? depthTexture, double? clearDepth)
        => CreateRenderPass(label, colorTexture, clearColor, depthTexture, clearDepth,
            new RenderPass.RenderArea(0, 0, colorTexture.GetWidth(0), colorTexture.GetHeight(0)));

    public RenderPass CreateRenderPass(Func<string> label, GpuTextureView colorTexture, Vector4? clearColor, GpuTextureView? depthTexture, double? clearDepth, RenderPass.RenderArea renderArea)
    {
        var descriptor = RenderPassDescriptor.Create(label).WithColorAttachment(colorTexture, clearColor);
        if (depthTexture != null)
            descriptor.WithDepthAttachment(depthTexture, clearDepth);
        descriptor.WithRenderArea(renderArea);
        return CreateRenderPass(descriptor);
    }

    public RenderPass CreateRenderPass(RenderPassDescriptor descriptor)
    {
        if (_isInRenderPass)
            throw new InvalidOperationException("Close the existing render pass before creating a new one!");

        int maxColorAttachments = _device.GetDeviceInfo().Limits.MaxColorAttachments;
        int colorAttachmentCount = descriptor.ColorAttachments.Count;
        if (colorAttachmentCount > maxColorAttachments)
            throw new InvalidOperationException($"Render pass created with {colorAttachmentCount} color attachments but device only supports {maxColorAttachments}");
        int totalAttachments = colorAttachmentCount + (descriptor.DepthAttachment != null ? 1 : 0);
        if (totalAttachments == 0)
            throw new ArgumentException("At least one attachment (depth or color) must be specified");

        int attachmentWidth;
        int attachmentHeight;
        if (colorAttachmentCount != 0)
        {
            var first = descriptor.ColorAttachments[0]!;
            attachmentWidth = first.TextureView.GetWidth(0);
            attachmentHeight = first.TextureView.GetHeight(0);
        }
        else
        {
            attachmentWidth = descriptor.DepthAttachment!.TextureView.GetWidth(0);
            attachmentHeight = descriptor.DepthAttachment!.TextureView.GetHeight(0);
        }

        if (descriptor.RenderArea == null)
            throw new ArgumentException("RenderPassDescriptor.renderArea must be provided");
        var area = descriptor.RenderArea;
        if (area.X < 0 || area.Y < 0 || area.X + area.Width > attachmentWidth || area.Y + area.Height > attachmentHeight)
            throw new ArgumentException($"RenderPass render area {area} is out of bounds for texture of {attachmentWidth}x{attachmentHeight}");

        for (int i = 0; i < colorAttachmentCount; i++)
        {
            var attachment = descriptor.ColorAttachments[i];
            if (attachment == null) continue;
            var colorTexture = attachment.TextureView;
            if (colorTexture.IsClosed)
                throw new InvalidOperationException($"Color texture {i} is closed");
            if ((colorTexture.Texture.Usage & GpuTexture.UsageRenderAttachment) == 0)
                throw new InvalidOperationException($"Color texture {i} must have USAGE_RENDER_ATTACHMENT");
            if (colorTexture.Texture.DepthOrLayers > 1)
                throw new NotSupportedException($"Color texture {i}: textures with multiple depths or layers are not yet supported as an attachment");
            if (colorTexture.GetWidth(0) != attachmentWidth || colorTexture.GetHeight(0) != attachmentHeight)
                throw new ArgumentException($"Color texture {i}: size does not match expected attachment size. Is {colorTexture.GetWidth(0)}x{colorTexture.GetHeight(0)} expected {attachmentWidth}x{attachmentHeight}");
        }

        if (descriptor.DepthAttachment != null)
        {
            var depthTexture = descriptor.DepthAttachment.TextureView;
            if (depthTexture.IsClosed)
                throw new InvalidOperationException("Depth texture is closed");
            if ((depthTexture.Texture.Usage & GpuTexture.UsageRenderAttachment) == 0)
                throw new InvalidOperationException("Depth texture must have USAGE_RENDER_ATTACHMENT");
            if (depthTexture.Texture.DepthOrLayers > 1)
                throw new NotSupportedException("Depth texture: textures with multiple depths or layers are not yet supported as an attachment");
            if (depthTexture.GetWidth(0) != attachmentWidth || depthTexture.GetHeight(0) != attachmentHeight)
                throw new ArgumentException($"Depth texture: size does not match expected attachment size. Is {depthTexture.GetWidth(0)}x{depthTexture.GetHeight(0)} expected {attachmentWidth}x{attachmentHeight}");
        }

        _isInRenderPass = true;
        return new RenderPass(_backend.CreateRenderPass(descriptor), _device, descriptor.ColorAttachments, SubmitRenderPass, descriptor.RenderArea);
    }

    //SubmitRenderPass closes the current render pass; public in NetCraft because callers driving the backend directly need it
    public void SubmitRenderPass()
    {
        if (!_isInRenderPass)
            throw new InvalidOperationException("Can't submit a render pass if one isn't open");
        _isInRenderPass = false;
        _backend.SubmitRenderPass();
    }

    public void ClearColorTexture(GpuTexture colorTexture, Vector4 clearColor)
    {
        EnsureOutsideRenderPass();
        VerifyColorTexture(colorTexture);
        _backend.ClearColorTexture(colorTexture, clearColor);
    }

    public void ClearColorAndDepthTextures(GpuTexture colorTexture, Vector4 clearColor, GpuTexture depthTexture, double clearDepth)
    {
        EnsureOutsideRenderPass();
        VerifyColorTexture(colorTexture);
        VerifyDepthTexture(depthTexture);
        _backend.ClearColorAndDepthTextures(colorTexture, clearColor, depthTexture, clearDepth);
    }

    public void ClearColorAndDepthTextures(GpuTexture colorTexture, Vector4 clearColor, GpuTexture depthTexture, double clearDepth, int regionX, int regionY, int regionWidth, int regionHeight)
    {
        EnsureOutsideRenderPass();
        VerifyColorTexture(colorTexture);
        VerifyDepthTexture(depthTexture);
        VerifyRegion(colorTexture, regionX, regionY, regionWidth, regionHeight);
        _backend.ClearColorAndDepthTextures(colorTexture, clearColor, depthTexture, clearDepth, regionX, regionY, regionWidth, regionHeight);
    }

    public void ClearDepthTexture(GpuTexture depthTexture, double clearDepth)
    {
        EnsureOutsideRenderPass();
        VerifyDepthTexture(depthTexture);
        _backend.ClearDepthTexture(depthTexture, clearDepth);
    }

    public void WriteToBuffer(GpuBufferSlice destination, ReadOnlySpan<byte> data)
    {
        EnsureOutsideRenderPass();
        var buffer = destination.Buffer;
        if (buffer.IsClosed)
            throw new InvalidOperationException("Buffer already closed");
        if ((buffer.Usage & GpuBuffer.UsageCopyDst) == 0)
            throw new InvalidOperationException("Buffer needs USAGE_COPY_DST to be a destination for a copy");
        long length = data.Length;
        if (length > destination.Length)
            throw new ArgumentException($"Cannot write more data than the slice allows (attempting to write {length} bytes into a slice of length {destination.Length})");
        if (destination.Length + destination.Offset > buffer.Size)
            throw new ArgumentException($"Cannot write more data than this buffer can hold (attempting to write {length} bytes at offset {destination.Offset} to {buffer.Size} size buffer)");
        _backend.WriteToBuffer(destination, data);
    }

    public void CopyToBuffer(GpuBufferSlice source, GpuBufferSlice target)
    {
        EnsureOutsideRenderPass();
        var sourceBuffer = source.Buffer;
        if (sourceBuffer.IsClosed)
            throw new InvalidOperationException("Source buffer already closed");
        if ((sourceBuffer.Usage & GpuBuffer.UsageCopySrc) == 0)
            throw new InvalidOperationException("Source buffer needs USAGE_COPY_SRC to be a source for a copy");
        var targetBuffer = target.Buffer;
        if (targetBuffer.IsClosed)
            throw new InvalidOperationException("Target buffer already closed");
        if ((targetBuffer.Usage & GpuBuffer.UsageCopyDst) == 0)
            throw new InvalidOperationException("Target buffer needs USAGE_COPY_DST to be a destination for a copy");
        if (source.Length != target.Length)
            throw new ArgumentException($"Cannot copy from slice of size {source.Length} to slice of size {target.Length}, they must be equal");
        if (source.Offset + source.Length > sourceBuffer.Size)
            throw new ArgumentException($"Cannot copy more data than the source buffer holds (attempting to copy {source.Length} bytes at offset {source.Offset} from {sourceBuffer.Size} size buffer)");
        if (target.Offset + target.Length > targetBuffer.Size)
            throw new ArgumentException($"Cannot copy more data than the target buffer can hold (attempting to copy {target.Length} bytes at offset {target.Offset} to {targetBuffer.Size} size buffer)");
        _backend.CopyToBuffer(source, target);
    }

    public void WriteToTexture(GpuTexture destination, GpuTexture source, int mipLevel, int depthOrLayer, int destX, int destY)
        => WriteToTexture(destination, source, mipLevel, depthOrLayer, destX, destY, source.Width, source.Height);

    public void WriteToTexture(GpuTexture destination, GpuTexture source, int mipLevel, int depthOrLayer, int destX, int destY, int width, int height)
    {
        throw new NotSupportedException("Writing from a texture source is not implemented; pass pixel data instead");
    }

    public void WriteToTexture(GpuTexture destination, ReadOnlySpan<byte> source, int mipLevel, int depthOrLayer, int destX, int destY, int width, int height)
    {
        EnsureOutsideRenderPass();
        if (mipLevel < 0 || mipLevel >= destination.MipLevels)
            throw new ArgumentException($"Invalid mipLevel, must be >= 0 and < {destination.MipLevels}");
        if ((long)width * height * destination.Format.BlockSize() > source.Length)
            throw new ArgumentException($"Copy would overrun the source buffer (remaining length of {source.Length}, but copy is {width}x{height} of format {destination.Format})");
        if (destX + width > destination.GetWidth(mipLevel) || destY + height > destination.GetHeight(mipLevel))
            throw new ArgumentException($"Dest texture ({destination.GetWidth(mipLevel)}x{destination.GetHeight(mipLevel)}) is not large enough to write a rectangle of {width}x{height} at {destX}x{destY}");
        if (destination.IsClosed)
            throw new InvalidOperationException("Destination texture is closed");
        if ((destination.Usage & GpuTexture.UsageCopyDst) == 0)
            throw new InvalidOperationException("Color texture must have USAGE_COPY_DST to be a destination for a write");
        if (depthOrLayer >= destination.DepthOrLayers)
            throw new NotSupportedException($"Depth or layer is out of range, must be >= 0 and < {destination.DepthOrLayers}");
        _backend.WriteToTexture(destination, source, mipLevel, depthOrLayer, destX, destY, width, height);
    }

    public void CopyBufferToTexture(GpuBufferSlice source, int sourceX, int sourceY, int sourceWidth, int sourceHeight, GpuTexture destination, int destinationX, int destinationY, int copyWidth, int copyHeight, int mipLevel, int arrayLayer)
    {
        EnsureOutsideRenderPass();
        if (mipLevel < 0 || mipLevel >= destination.MipLevels)
            throw new ArgumentException($"Invalid mipLevel, must be >= 0 and < {destination.MipLevels}");
        if (sourceX + copyWidth > sourceWidth || sourceY + copyHeight > sourceHeight)
            throw new ArgumentException($"Copy source ({sourceWidth}x{sourceHeight}) is not large enough to read a rectangle of {copyWidth}x{copyHeight} from {sourceX}x{sourceY}");
        if ((long)sourceWidth * copyHeight * destination.Format.BlockSize() > source.Length)
            throw new ArgumentException($"Copy would overrun the source buffer (remaining length of {source.Length}, but copy is {copyWidth}x{copyHeight} of format {destination.Format})");
        if (destinationX + copyWidth > destination.GetWidth(mipLevel) || destinationY + copyHeight > destination.GetHeight(mipLevel))
            throw new ArgumentException($"Dest texture ({destination.GetWidth(mipLevel)}x{destination.GetHeight(mipLevel)}) is not large enough to write a rectangle of {copyWidth}x{copyHeight} at {destinationX}x{destinationY}");
        if (source.Buffer.IsClosed)
            throw new InvalidOperationException("Source buffer is closed");
        if ((source.Buffer.Usage & GpuBuffer.UsageCopySrc) == 0)
            throw new InvalidOperationException("Source buffer must have USAGE_COPY_SRC to be a source for a read");
        if (destination.IsClosed)
            throw new InvalidOperationException("Destination texture is closed");
        if ((destination.Usage & GpuTexture.UsageCopyDst) == 0)
            throw new InvalidOperationException("Color texture must have USAGE_COPY_DST to be a destination for a write");
        if (arrayLayer >= destination.DepthOrLayers)
            throw new NotSupportedException($"Depth or layer is out of range, must be >= 0 and < {destination.DepthOrLayers}");
        _backend.CopyBufferToTexture(source, sourceX, sourceY, sourceWidth, sourceHeight, destination, destinationX, destinationY, copyWidth, copyHeight, mipLevel, arrayLayer);
    }

    public void CopyTextureToBuffer(GpuTexture source, GpuBuffer destination, long offset, System.Action? callback, int mipLevel)
    {
        EnsureOutsideRenderPass();
        _backend.CopyTextureToBuffer(source, destination, offset, callback, mipLevel);
    }

    public void CopyTextureToBuffer(GpuTexture source, GpuBuffer destination, long offset, System.Action? callback, int mipLevel, int x, int y, int width, int height)
    {
        EnsureOutsideRenderPass();
        if (mipLevel < 0 || mipLevel >= source.MipLevels)
            throw new ArgumentException($"Invalid mipLevel {mipLevel}, must be >= 0 and < {source.MipLevels}");
        if ((long)width * height * source.Format.BlockSize() + offset > destination.Size)
            throw new ArgumentException($"Buffer of size {destination.Size} is not large enough to hold {width}x{height} pixels ({source.Format.BlockSize()} bytes each) starting from offset {offset}");
        if ((source.Usage & GpuTexture.UsageCopySrc) == 0)
            throw new ArgumentException("Texture needs USAGE_COPY_SRC to be a source for a copy");
        if ((destination.Usage & GpuBuffer.UsageCopyDst) == 0)
            throw new ArgumentException("Buffer needs USAGE_COPY_DST to be a destination for a copy");
        if (x + width > source.GetWidth(mipLevel) || y + height > source.GetHeight(mipLevel))
            throw new ArgumentException($"Copy source texture ({source.GetWidth(mipLevel)}x{source.GetHeight(mipLevel)}) is not large enough to read a rectangle of {width}x{height} from {x},{y}");
        if (source.IsClosed)
            throw new InvalidOperationException("Source texture is closed");
        if (destination.IsClosed)
            throw new InvalidOperationException("Destination buffer is closed");
        if (source.DepthOrLayers > 1)
            throw new NotSupportedException("Textures with multiple depths or layers are not yet supported for copying");
        _backend.CopyTextureToBuffer(source, destination, offset, callback, mipLevel, x, y, width, height);
    }

    public void CopyTextureToTexture(GpuTexture source, GpuTexture destination, int mipLevel, int destX, int destY, int sourceX, int sourceY, int width, int height)
    {
        EnsureOutsideRenderPass();
        if (mipLevel < 0 || mipLevel >= source.MipLevels || mipLevel >= destination.MipLevels)
            throw new ArgumentException($"Invalid mipLevel {mipLevel}, must be >= 0 and < {source.MipLevels} and < {destination.MipLevels}");
        if (destX + width > destination.GetWidth(mipLevel) || destY + height > destination.GetHeight(mipLevel))
            throw new ArgumentException($"Dest texture ({destination.GetWidth(mipLevel)}x{destination.GetHeight(mipLevel)}) is not large enough to write a rectangle of {width}x{height} at {destX}x{destY}");
        if (sourceX + width > source.GetWidth(mipLevel) || sourceY + height > source.GetHeight(mipLevel))
            throw new ArgumentException($"Source texture ({source.GetWidth(mipLevel)}x{source.GetHeight(mipLevel)}) is not large enough to read a rectangle of {width}x{height} at {sourceX}x{sourceY}");
        if (source.IsClosed)
            throw new InvalidOperationException("Source texture is closed");
        if (destination.IsClosed)
            throw new InvalidOperationException("Destination texture is closed");
        if ((source.Usage & GpuTexture.UsageCopySrc) == 0)
            throw new ArgumentException("Texture needs USAGE_COPY_SRC to be a source for a copy");
        if ((destination.Usage & GpuTexture.UsageCopyDst) == 0)
            throw new ArgumentException("Texture needs USAGE_COPY_DST to be a destination for a copy");
        if (source.DepthOrLayers > 1)
            throw new NotSupportedException("Textures with multiple depths or layers are not yet supported for copying");
        if (destination.DepthOrLayers > 1)
            throw new NotSupportedException("Textures with multiple depths or layers are not yet supported for copying");
        _backend.CopyTextureToTexture(source, destination, mipLevel, destX, destY, sourceX, sourceY, width, height);
    }

    public GpuFence CreateFence()
    {
        EnsureOutsideRenderPass();
        return _backend.CreateFence();
    }

    //BeginRecording restarts recording so the encoder can be reused across frames, a NetCraft extension
    public void BeginRecording() => _backend.BeginRecording();

    //SubmitAsync submits without waiting, a NetCraft extension used by the offscreen PIP path
    public void SubmitAsync() => _backend.SubmitAsync();

    //WaitForCompletion waits for an async submit to finish, a NetCraft extension
    public void WaitForCompletion() => _backend.WaitForCompletion();

    //TransitionImageLayout records an explicit layout transition, a NetCraft extension
    public void TransitionImageLayout(GpuTexture image, GpuImageLayout newLayout) => _backend.TransitionImageLayout(image, newLayout);

    public void WriteTimestamp(GpuQueryPool pool, int index)
    {
        if (index < 0 || index > pool.Size)
            throw new InvalidOperationException($"Index {index} is out of range for query pool of size {pool.Size}");
        _backend.WriteTimestamp(pool, index);
    }

    private void EnsureOutsideRenderPass()
    {
        if (_isInRenderPass)
            throw new InvalidOperationException("Close the existing render pass before performing additional commands");
    }

    private static void VerifyColorTexture(GpuTexture colorTexture)
    {
        if (!colorTexture.Format.HasColorAspect())
            throw new InvalidOperationException("Trying to clear a non-color texture as color");
        if (colorTexture.IsClosed)
            throw new InvalidOperationException("Color texture is closed");
        if ((colorTexture.Usage & GpuTexture.UsageRenderAttachment) == 0)
            throw new InvalidOperationException("Color texture must have USAGE_RENDER_ATTACHMENT");
        if ((colorTexture.Usage & GpuTexture.UsageCopyDst) == 0)
            throw new InvalidOperationException("Color texture must have USAGE_COPY_DST");
        if (colorTexture.DepthOrLayers > 1)
            throw new NotSupportedException("Clearing a texture with multiple layers or depths is not yet supported");
    }

    private static void VerifyDepthTexture(GpuTexture depthTexture)
    {
        if (!depthTexture.Format.HasDepthAspect())
            throw new InvalidOperationException("Trying to clear a non-depth texture as depth");
        if (depthTexture.IsClosed)
            throw new InvalidOperationException("Depth texture is closed");
        if ((depthTexture.Usage & GpuTexture.UsageRenderAttachment) == 0)
            throw new InvalidOperationException("Depth texture must have USAGE_RENDER_ATTACHMENT");
        if ((depthTexture.Usage & GpuTexture.UsageCopyDst) == 0)
            throw new InvalidOperationException("Depth texture must have USAGE_COPY_DST");
        if (depthTexture.DepthOrLayers > 1)
            throw new NotSupportedException("Clearing a texture with multiple layers or depths is not yet supported");
    }

    private static void VerifyRegion(GpuTexture colorTexture, int regionX, int regionY, int regionWidth, int regionHeight)
    {
        if (regionX < 0 || regionX >= colorTexture.GetWidth(0))
            throw new ArgumentException("regionX should not be outside of the texture");
        if (regionY < 0 || regionY >= colorTexture.GetHeight(0))
            throw new ArgumentException("regionY should not be outside of the texture");
        if (regionWidth <= 0)
            throw new ArgumentException("regionWidth should be greater than 0");
        if (regionX + regionWidth > colorTexture.GetWidth(0))
            throw new ArgumentException("regionWidth + regionX should be less than the texture width");
        if (regionHeight <= 0)
            throw new ArgumentException("regionHeight should be greater than 0");
        if (regionY + regionHeight > colorTexture.GetHeight(0))
            throw new ArgumentException("regionHeight + regionY should be less than the texture height");
    }
}
