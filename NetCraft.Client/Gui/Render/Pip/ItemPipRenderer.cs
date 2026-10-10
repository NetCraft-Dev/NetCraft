using System.Numerics;
using System.Runtime.InteropServices;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Render;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
using NetCraft.Client.Render.Model;
using NetCraft.Client.Render.Texture;
using NetCraft.Client.Render.Texture.Atlas;
using NetCraft.Client.Render.Item;
using NetCraft.Client.Render.Entity;
using NetCraft.Client.Render.Entity.State;
using NetCraft.Client.Render.State.Gui;
using NetCraft.Client.Gui;
using NetCraft.Client.Gui.Render;
using NetCraft.Client.Gui.Render.State;
using NetCraft.Client.Gui.Render.Pip;
using NetCraft.Client.Gui.Navigation;
using NetCraft.Client.Gui.Layouts;
using NetCraft.Client.Gui.Font;
using NetCraft.Client.Gui.Font.Providers;
using NetCraft.Client.Gui.Font.Glyphs;
using NetCraft.Client.Model;
using NetCraft.Client.Model.Geom;
using NetCraft.Client.Resources.Metadata.Gui;
using NetCraft.Client.Blaze3d;

namespace NetCraft.Client.Gui.Render.Pip;

//ItemPipState PIP item render state, maps to vanilla OversizedItemRenderState
//Carries oversized items that exceed their atlas slot; rendered 3D to offscreen then blitted to the GUI
//The PoC identifies items by ItemIdentity; the renderer looks up RegisterItem to map to ItemStackRenderState
public sealed record ItemPipState(
    int X0, int Y0, int X1, int Y1,
    float Scale,
    ScreenRectangle ScissorArea,
    Matrix3x2 Pose,
    object ItemIdentity,
    float RotationY)
    : PictureInPictureRenderState
{
    public ScreenRectangle Bounds => PictureInPictureRenderState.GetBounds(X0, Y0, X1, Y1, ScissorArea);
}

//ItemPipRenderer PIP item renderer subclass, maps to vanilla OversizedItemRenderer
//RenderToTexture uses PoseStack to translate/scale/rotate the item into the offscreen center
//After item.submit, ItemFeatureRenderer.Execute writes vertices, then it uploads to the GPU + records Vulkan commands to render into the OffscreenTexture
//EnsureTexturesAndProjection creates the offscreen texture+depth+clear+perspective projection+compiled pipeline
//BlitTexture adds the offscreen texture as a BlitRenderState to guiRenderState
public sealed class ItemPipRenderer : PictureInPictureRenderer<ItemPipState>
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MvpUniform
    {
        public Matrix4x4 Model;
        public Matrix4x4 View;
        public Matrix4x4 Proj;
    }

    private readonly GpuDevice _device;
    private readonly PoseStack _poseStack = new();
    private readonly Projection _projection = new();
    private readonly ItemSubmitCollector _collector = new();
    private readonly Dictionary<object, TrackingItemStackRenderState> _items = new();
    //Vertex data from the most recent RenderToTexture, for test diagnostics
    public VertexConsumer3D LastFrameVertices { get; } = new();

    //GPU render resources are lazily initialized in EnsureTexturesAndProjection and rebuilt when the size changes
    private Lighting? _lighting;
    private LightTexture? _lightTexture;
    private ItemTextureAtlas? _itemAtlas;
    private GpuBuffer? _mvpUbo;
    private GpuTextureView? _lightmapView;
    private GpuTextureView? _atlasView;
    private GpuBuffer? _vertexBuffer;
    private GpuBuffer? _indexBuffer;
    //_blitSampler the offscreen→GUI blit sampler with linear filtering, reused across frames to avoid leaking a CreateSampler every frame
    private GpuSampler? _blitSampler;
    private int _pipelineWidth;
    private int _pipelineHeight;
    //Double-buffered offscreen texture+depth+encoder ping-pong; this frame writes _writeIndex and blit reads _readIndex
    //_readIndex=-1 the first frame has no history; blit the currently written one, and the queue submission order guarantees the GPU dependency
    //Async SubmitAsync does not wait for the GPU so the CPU keeps recording the main cmd; the two encoders rotate to avoid command buffer reuse contention
    private readonly GpuTexture?[] _offscreenTextures = new GpuTexture[2];
    private readonly GpuTexture?[] _offscreenDepths = new GpuTexture[2];
    private readonly GpuTextureView?[] _offscreenViews = new GpuTextureView[2];
    private readonly GpuTextureView?[] _offscreenDepthViews = new GpuTextureView[2];
    private readonly CommandEncoder?[] _encoders = new CommandEncoder[2];
    private int _writeIndex;
    private int _readIndex = -1;

    public override Type RenderStateClass => typeof(ItemPipState);

    public ItemPipRenderer(GpuDevice device) => _device = device;

    //RegisterItem registers an item; the PoC uses a procedurally generated CubeModel
    public TrackingItemStackRenderState RegisterItem(object identity, float modelSize = 1f)
    {
        if (_items.TryGetValue(identity, out var existing)) return existing;
        var state = new TrackingItemStackRenderState();
        state.AppendModelIdentityElement(identity);
        state.SetQuads(CubeModel.Create(modelSize));
        _items[identity] = state;
        return state;
    }

    //EnsureTexturesAndProjection creates the double offscreen texture+depth+clear+perspective projection+compiled pipeline
    //The base Prepare still calls this method when needsResize=false; the double textures already exist so only the projection is updated to avoid rebuilding every frame
    //Rebuilding every frame would leak offscreen textures + descriptor sets and exhaust the pool
    //The pipeline viewport is fixed to the offscreen size and recompiled when the size changes
    //With SupportsGpuRendering=false it only creates the texture+sets the projection, taking the CPU path for unit tests and not calling EnsureGpuResources/clear
    //The two encoders are reused across resizes and not released in DisposeTextures to avoid rebuild cost
    protected override void EnsureTexturesAndProjection(int width, int height)
    {
        //Both textures are already created and reused; only the projection is updated
        if (_offscreenTextures[0] is not null && _offscreenTextures[1] is not null)
        {
            _projection.SetupPerspective(0.05f, 1000f, MathF.PI / 4f, width, height);
            return;
        }
        for (var i = 0; i < 2; i++)
        {
            _offscreenTextures[i] = _device.CreateTexture(null, (GpuTexture.UsageRenderAttachment | GpuTexture.UsageTextureBinding), GpuFormat.Rgba8Unorm, width, height, 1, 1);
            _offscreenDepths[i] = _device.CreateTexture(null, GpuTexture.UsageRenderAttachment, GpuFormat.D32Float, width, height, 1, 1);
            _offscreenViews[i] = _device.CreateTextureView(_offscreenTextures[i]!);
            _offscreenDepthViews[i] = _device.CreateTextureView(_offscreenDepths[i]!);
        }
        //Perspective projection for offscreen 3D rendering; MockDevice also sets the projection for tests
        _projection.SetupPerspective(0.05f, 1000f, MathF.PI / 4f, width, height);
        //The base OffscreenTexture is set to the first texture so the base's needsResize sees non-null
        OffscreenTexture = _offscreenTextures[0];
        OffscreenDepth = _offscreenDepths[0];
        //GPU resources+initial clear run only when SupportsGpuRendering=true; MockDevice takes the CPU path and only generates vertices
        if (!_device.SupportsGpuRendering) return;
        //Both depths initial layout transition Undefined->DepthStencilAttachmentOptimal
        foreach (var depth in _offscreenDepths)
            depth!.Upload(ReadOnlySpan<byte>.Empty);
        //The pipeline is shared; the viewport comes from the render area so no per-size recompile is needed
        if (_pipelineWidth != width || _pipelineHeight != height)
        {
            _pipelineWidth = width;
            _pipelineHeight = height;
            Device_Precompile();
        }
        //The two encoders are reused across frames with async Submit ping-pong; created on first use and reused after resize
        _encoders[0] ??= _device.CreateCommandEncoder();
        _encoders[1] ??= _device.CreateCommandEncoder();
        //Initially clears both textures to opaque black so the first frame's blit does not sample undefined content
        for (var i = 0; i < 2; i++)
        {
            using var initEncoder = _device.CreateCommandEncoder();
            var initDescriptor = RenderPassDescriptor.Create(() => "pip-init")
                .WithColorAttachment(_offscreenViews[i]!, new Vector4(0f, 0f, 0f, 1f))
                .WithDepthAttachment(_offscreenDepthViews[i]!, 1.0)
                .WithRenderArea(new NetCraft.Client.Blaze3d.Systems.RenderPass.RenderArea(0, 0, width, height));
            using var initPass = initEncoder.CreateRenderPass(initDescriptor);
            initPass.SetPipeline(RenderPipelines.ITEM_3D);
            initEncoder.Submit();
        }
    }

    //Device_Precompile keeps the pipeline warm for the offscreen size; the declarative pipeline is shared
    private void Device_Precompile() => _device.PrecompilePipeline(RenderPipelines.ITEM_3D);

    //EnsureGpuResources creates Lighting+LightTexture+ItemTextureAtlas+MVP UBO+descriptor sets+pipeline+index buffer
    private void EnsureGpuResources()
    {
        _lighting ??= new Lighting(_device);
        _lighting.SetupFor(Lighting.Entry.Items3D);
        _lightTexture ??= new LightTexture(_device);
        _itemAtlas ??= new ItemTextureAtlas(_device);

        //The MVP matrix is bound by the name "Mvp" (3 mat4 = 192 bytes); the lightmap/atlas by "LightmapSampler"/"AtlasSampler"
        if (_mvpUbo == null)
            _mvpUbo = _device.CreateBuffer(null, GpuBuffer.UsageUniform | GpuBuffer.UsageMapWrite, 192);
        _lightmapView ??= _device.CreateTextureView(_lightTexture.Texture!);
        _atlasView ??= _device.CreateTextureView(_itemAtlas.Texture!);
        if (_indexBuffer == null)
        {
            var indices = GenerateQuadIndices(6);
            _indexBuffer = _device.CreateHostVisibleBuffer(indices.Length * sizeof(ushort), GpuBuffer.UsageIndex | GpuBuffer.UsageCopyDst);
            _indexBuffer.Upload<ushort>(indices);
        }
        //_blitSampler linear filtering of the offscreen 3D render result blitted to the GUI, smoothly reused across frames
        _blitSampler ??= _device.CreateSampler(AddressMode.ClampToEdge, AddressMode.ClampToEdge, FilterMode.Linear, FilterMode.Linear, 1, null);

        //Precompiles the shared 3D item pipeline
        Device_Precompile();
    }

    //RenderToTexture renders the 3D item to offscreen
    //poseStack translates to the offscreen center + pulls back + scale + rotate
    //After item.submit, ItemFeatureRenderer.Execute writes vertices, then it uploads to the GPU + records Vulkan commands to render
    protected override void RenderToTexture(ItemPipState renderState)
    {
        var width = _pipelineWidth;
        var height = _pipelineHeight;
        _poseStack.SetIdentity();
        //Translate to the offscreen center + pull back so the item is inside the viewport
        _poseStack.Translate(width / 2f, height / 2f, -3f);
        //Scale by Scale
        _poseStack.Scale(renderState.Scale, renderState.Scale, renderState.Scale);
        //Rotate around Y by RotationY
        if (renderState.RotationY != 0f)
            _poseStack.Rotate(Quaternion.CreateFromAxisAngle(Vector3.UnitY, renderState.RotationY));

        if (_items.TryGetValue(renderState.ItemIdentity, out var item))
        {
            _collector.Nodes.Clear();
            item.Submit(_poseStack, _collector,
                ItemFeatureRenderer.FullBright, ItemFeatureRenderer.NoOverlay, 0);
            LastFrameVertices.Clear();
            ItemFeatureRenderer.Execute(_collector, LastFrameVertices);
        }

        if (LastFrameVertices.VertexCount == 0) return;
        //With SupportsGpuRendering=false it only generates vertices for unit tests and records no GPU commands; MockDevice.CreateCommandEncoder throws NotSupportedException
        if (_device.SupportsGpuRendering)
            RenderToGpu();
    }

    //RenderToGpu uploads vertices + updates the MVP UBO + records Vulkan commands to render into _offscreenTextures[_writeIndex]
    //Double encoder ping-pong; this frame uses _encoders[_writeIndex] whose last round's commands were already SubmitAsync'd
    //WaitForCompletion waits for the last round before Reset and reuse; skipped on the first time when not SubmitAsync'd
    //SubmitAsync submits asynchronously without waiting for the GPU so the CPU keeps recording the main cmd; the same queue submission order guarantees the GPU dependency
    //model=Identity because VertexConsumer3D.PutBakedQuad already applies the pose on the CPU
    //The perspective projection must flip Y and convert the Z range to match Vulkan
    private void RenderToGpu()
    {
        var vertices = LastFrameVertices.Vertices;
        var vertexBytes = VerticesToBytes(vertices);
        if (_vertexBuffer == null || _vertexBuffer.Size < vertexBytes.Length)
        {
            _vertexBuffer?.Dispose();
            _vertexBuffer = _device.CreateHostVisibleBuffer(vertexBytes.Length, GpuBuffer.UsageVertex | GpuBuffer.UsageCopyDst);
        }
        _vertexBuffer.Upload<byte>(vertexBytes);

        var proj = _projection.GetMatrix();
        proj.M22 *= -1;
        proj.M42 *= -1;
        proj.M33 *= 0.5f;
        proj.M43 = proj.M43 * 0.5f + 0.5f;
        var mvp = new MvpUniform
        {
            Model = Matrix4x4.Identity,
            View = Matrix4x4.Identity,
            Proj = proj
        };
        _mvpUbo!.Upload<MvpUniform>(new[] { mvp });

        var idx = _writeIndex;
        var encoder = _encoders[idx]!;
        //Waits for the last round of this encoder before Reset and command buffer reuse; skipped on the first time when not SubmitAsync'd
        encoder.WaitForCompletion();
        encoder.BeginRecording();
        //This frame writes the texture; at the end of the previous frame it was transitioned to ShaderReadOnly for blit and is transitioned back to ColorAttachment before this frame's render
        //After the first render's initial clear it is already ColorAttachmentOptimal; TransitionImageLayout internally skips it as a no-op
        encoder.TransitionImageLayout(_offscreenTextures[idx]!, GpuImageLayout.ColorAttachment);
        var clearColor = new Vector4(0f, 0f, 0f, 1f);
        var descriptor = RenderPassDescriptor.Create(() => "pip-item")
            .WithColorAttachment(_offscreenViews[idx]!, clearColor)
            .WithDepthAttachment(_offscreenDepthViews[idx]!, 1.0)
            .WithRenderArea(new NetCraft.Client.Blaze3d.Systems.RenderPass.RenderArea(0, 0, _pipelineWidth, _pipelineHeight));
        using (var pass = encoder.CreateRenderPass(descriptor))
        {
            pass.SetPipeline(RenderPipelines.ITEM_3D);
            pass.SetUniform("Mvp", _mvpUbo!);
            pass.SetUniform("Lighting", _lighting!.CurrentBuffer!);
            pass.BindTexture("LightmapSampler", _lightmapView!, _lightTexture!.Sampler!);
            pass.BindTexture("AtlasSampler", _atlasView!, _itemAtlas!.Sampler!);
            pass.SetVertexBuffer(0, _vertexBuffer!.Slice());
            pass.SetIndexBuffer(_indexBuffer!, NetCraft.Client.Blaze3d.IndexType.Short);
            //DisableScissor resets the scissor to the full render area before drawing
            pass.DisableScissor();
            //CubeModel 6 faces * 6 indices = 36
            pass.DrawIndexed(36, 1, 0, 0, 0);
        }
        //Transition to ShaderReadOnly after render for BlitTexture to sample; sampling ColorAttachmentOptimal would crash the driver
        encoder.TransitionImageLayout(_offscreenTextures[idx]!, GpuImageLayout.ShaderReadOnly);
        //Async Submit does not wait for the GPU so the CPU keeps recording the main cmd; blit reads the previous frame's texture with no dependency on this frame
        encoder.SubmitAsync();
    }

    //GetBlitTextureSetup returns the read buffer's TextureSetup for BlitTexture to blit to the GUI
    //_readIndex=-1 the first frame has no history; blit the currently written one (_writeIndex) and the queue submission order keeps the main cmd after the PIP
    //_readIndex>=0 later frames blit the previous frame's (_readIndex) already SubmitAsync'd with no dependency on this frame
    //_blitSampler reused across frames to avoid leaking a CreateSampler every frame, lazily created in EnsureGpuResources
    protected override TextureSetup GetBlitTextureSetup()
    {
        var idx = _readIndex < 0 ? _writeIndex : _readIndex;
        var tex = _offscreenTextures[idx];
        return tex is not null && _blitSampler is not null
            ? TextureSetup.SingleTexture(tex, _blitSampler)
            : TextureSetup.NoTexture;
    }

    //BlitTexture override: after the base adds the BlitRenderState it flips the read/write indices for ping-pong
    //What this frame wrote becomes the read one and the other is written next; the two encoders rotate to avoid command buffer reuse contention
    //Updates the base OffscreenTexture to the new read buffer for the base's needsResize check
    protected override void BlitTexture(ItemPipState renderState, GuiRenderState guiRenderState)
    {
        base.BlitTexture(renderState, guiRenderState);
        _readIndex = _writeIndex;
        _writeIndex = 1 - _writeIndex;
        if (_readIndex >= 0 && _offscreenTextures[_readIndex] is not null)
        {
            OffscreenTexture = _offscreenTextures[_readIndex];
            OffscreenDepth = _offscreenDepths[_readIndex];
        }
    }

    //DisposeTextures override releases the double texture+depth, called on a size change or Dispose
    //First waits for both encoders to finish before releasing the textures so the GPU no longer uses them
    //The encoders are reused across resizes and not released here to avoid rebuild cost
    protected override void DisposeTextures()
    {
        foreach (var enc in _encoders)
            enc?.WaitForCompletion();
        foreach (var view in _offscreenViews)
            view?.Dispose();
        foreach (var view in _offscreenDepthViews)
            view?.Dispose();
        foreach (var tex in _offscreenTextures)
            tex?.Dispose();
        foreach (var depth in _offscreenDepths)
            depth?.Dispose();
        Array.Fill(_offscreenViews, null);
        Array.Fill(_offscreenDepthViews, null);
        Array.Fill(_offscreenTextures, null);
        Array.Fill(_offscreenDepths, null);
        OffscreenTexture = null;
        OffscreenDepth = null;
        _readIndex = -1;
        _writeIndex = 0;
    }

    private static byte[] VerticesToBytes(List<float> vertices)
    {
        var span = CollectionsMarshal.AsSpan(vertices);
        return MemoryMarshal.AsBytes(span).ToArray();
    }

    private static ushort[] GenerateQuadIndices(int quadCount)
    {
        var indices = new ushort[quadCount * 6];
        for (int i = 0; i < quadCount; i++)
        {
            var baseVertex = i * 4;
            var offset = i * 6;
            indices[offset + 0] = (ushort)(baseVertex + 0);
            indices[offset + 1] = (ushort)(baseVertex + 1);
            indices[offset + 2] = (ushort)(baseVertex + 2);
            indices[offset + 3] = (ushort)(baseVertex + 2);
            indices[offset + 4] = (ushort)(baseVertex + 3);
            indices[offset + 5] = (ushort)(baseVertex + 0);
        }
        return indices;
    }

    protected override void OnDispose()
    {
        //Release both encoders; DisposeTextures already WaitForCompletion'd so Dispose here does not wait for the GPU again
        foreach (var enc in _encoders)
            enc?.Dispose();
        Array.Fill(_encoders, null);
        _vertexBuffer?.Dispose();
        _indexBuffer?.Dispose();
        _blitSampler?.Dispose();
        _atlasView?.Dispose();
        _lightmapView?.Dispose();
        _mvpUbo?.Dispose();
        _itemAtlas?.Dispose();
        _lightTexture?.Dispose();
        _lighting?.Dispose();
    }
}
