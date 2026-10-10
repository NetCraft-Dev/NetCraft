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

namespace NetCraft.Client.Render.Texture.Atlas;

//ItemItemAtlas item atlas production subclass, maps to the concrete render logic of vanilla GuiItemAtlas
//DrawToSlot uses PoseStack to translate/scale the item into the slot center, then item.submit and ItemFeatureRenderer.Execute write vertices
//The PoC uses a procedural CubeModel as the item model without loading a real one; itemIdentity maps to TrackingItemStackRenderState
//SupportsGpuRendering=true renders via Vulkan into AtlasTexture, otherwise only CPU vertex generation for tests
public sealed class ItemItemAtlas : GuiItemAtlas
{
    //MvpUniform MVP matrix uniform block with three mat4 model+view+proj, uploaded column-major to the shader
    [StructLayout(LayoutKind.Sequential)]
    private struct MvpUniform
    {
        public Matrix4x4 Model;
        public Matrix4x4 View;
        public Matrix4x4 Proj;
    }

    private readonly PoseStack _poseStack = new();
    private readonly Projection _projection = new();
    private readonly ItemSubmitCollector _collector = new();
    //Caches itemIdentity → ItemStackRenderState, emulating ItemModelResolver.update
    private readonly Dictionary<object, TrackingItemStackRenderState> _items = new();
    //Vertex data from the most recent DrawToSlot, for tests/subsequent GPU upload
    public VertexConsumer3D LastFrameVertices { get; } = new();

    //GPU render resources initialized only when SupportsGpuRendering=true
    private Lighting? _lighting;
    private LightTexture? _lightTexture;
    private ItemTextureAtlas? _itemAtlas;
    private GpuBuffer? _mvpUbo;
    private GpuDescriptorLayout? _mvpLayout;
    private GpuDescriptorSet? _mvpSet;
    private GpuDescriptorLayout? _lightmapLayout;
    private GpuDescriptorSet? _lightmapSet;
    private GpuDescriptorLayout? _atlasLayout;
    private GpuDescriptorSet? _atlasSet;
    private CompiledRenderPipeline? _pipeline;
    private GpuBuffer? _vertexBuffer;
    private GpuBuffer? _indexBuffer;

    public ItemItemAtlas(GpuDevice device, int textureSize, int slotTextureSize)
        : base(device, textureSize, slotTextureSize)
    {
        if (device.SupportsGpuRendering)
            InitGpuResources();
    }

    //InitGpuResources creates Lighting + LightTexture + ItemTextureAtlas + MVP UBO + pipeline + fixed index buffer
    private void InitGpuResources()
    {
        _lighting = new Lighting(Device);
        _lighting.SetupFor(Lighting.Entry.Items3D);
        _lightTexture = new LightTexture(Device);
        _itemAtlas = new ItemTextureAtlas(Device);

        var mvpLayoutDesc = new GpuDescriptorLayoutDescription();
        mvpLayoutDesc.Bindings.Add(new GpuDescriptorBinding
        {
            Binding = 0,
            DescriptorType = GpuDescriptorType.UniformBuffer,
            StageFlags = GpuShaderStageFlags.Vertex
        });
        _mvpLayout = Device.CreateDescriptorLayout(mvpLayoutDesc);
        //3 mat4 = 192 bytes
        _mvpUbo = Device.CreateBuffer(null, GpuBuffer.UsageUniform | GpuBuffer.UsageMapWrite, 192);
        _mvpSet = Device.AllocateDescriptorSet(_mvpLayout);
        _mvpSet.WriteBuffer(0, _mvpUbo, 0, -1);

        //lightmap sampler bound to set 2 binding 0 CombinedImageSampler
        var lightmapLayoutDesc = new GpuDescriptorLayoutDescription();
        lightmapLayoutDesc.Bindings.Add(new GpuDescriptorBinding
        {
            Binding = 0,
            DescriptorType = GpuDescriptorType.CombinedImageSampler,
            StageFlags = GpuShaderStageFlags.Fragment
        });
        _lightmapLayout = Device.CreateDescriptorLayout(lightmapLayoutDesc);
        _lightmapSet = Device.AllocateDescriptorSet(_lightmapLayout);
        _lightmapSet.WriteImage(0, _lightTexture.Texture!, _lightTexture.Sampler!);

        //item texture atlas sampler bound to set 3 binding 0 CombinedImageSampler
        var atlasLayoutDesc = new GpuDescriptorLayoutDescription();
        atlasLayoutDesc.Bindings.Add(new GpuDescriptorBinding
        {
            Binding = 0,
            DescriptorType = GpuDescriptorType.CombinedImageSampler,
            StageFlags = GpuShaderStageFlags.Fragment
        });
        _atlasLayout = Device.CreateDescriptorLayout(atlasLayoutDesc);
        _atlasSet = Device.AllocateDescriptorSet(_atlasLayout);
        _atlasSet.WriteImage(0, _itemAtlas.Texture!, _itemAtlas.Sampler!);

        //Cache the compiled artifact to avoid recompiling on every DrawToSlot
        //extent must match the AtlasTexture size, otherwise the viewport maps vertices to wrong positions and the scissor clips them
        var item3dDesc = RenderPipelineDescription.FromDeclaration(RenderPipelines.ITEM_3D, Device.ShaderManager);
        item3dDesc.TargetWidth = TextureSize;
        item3dDesc.TargetHeight = TextureSize;
        foreach (var layoutDesc in item3dDesc.DescriptorLayoutDescriptions)
            item3dDesc.DescriptorLayouts.Add(Device.CreateDescriptorLayout(layoutDesc));
        item3dDesc.DescriptorLayoutDescriptions.Clear();
        _pipeline = Device.CreateRenderPipeline(item3dDesc);

        //CubeModel 6 faces * 6 indices = 36 indices, uploaded once and fixed
        var indices = GenerateQuadIndices(6);
        _indexBuffer = Device.CreateHostVisibleBuffer(indices.Length * sizeof(ushort), GpuBuffer.UsageIndex | GpuBuffer.UsageCopyDst);
        _indexBuffer.Upload<ushort>(indices);

        //AtlasDepth initial layout transition Undefined->DepthStencilAttachmentOptimal
        //The GuiItemAtlas constructor only CreateImage without Upload, leaving the depth image layout Undefined
        //dynamic rendering expects DepthStencilAttachmentOptimal, requiring an explicit transition
        AtlasDepth.Upload(ReadOnlySpan<byte>.Empty);

        //Initially clears the whole AtlasTexture to opaque black; later DrawToSlot uses LoadOp=Load to preserve other slots' content
        //Without the initial clear, the first DrawToSlot with Load reads Undefined content and validation errors
        using (var initEncoder = Device.CreateCommandEncoder())
        {
            using var initPass = initEncoder.CreateRenderPass(_pipeline!, AtlasTexture, new Vector4(0f, 0f, 0f, 1f), AtlasDepth, 1.0f);
            initPass.Close();
            initEncoder.Submit();
        }
    }

    //RegisterItem registers an item; the PoC uses a procedurally generated CubeModel
    //The full version goes through ItemModelResolver.update to fill quads from ItemModel
    public TrackingItemStackRenderState RegisterItem(object identity, float modelSize = 1f)
    {
        if (_items.TryGetValue(identity, out var existing)) return existing;
        var state = new TrackingItemStackRenderState();
        state.AppendModelIdentityElement(identity);
        state.SetQuads(CubeModel.Create(modelSize));
        _items[identity] = state;
        return state;
    }

    //ReadbackSlot reads the given slot's AtlasTexture pixels back to the CPU for integration tests to verify render output
    //Returns a SlotTextureSize x SlotTextureSize x 4(RGBA) byte array
    //slotX/slotY grid coordinates matching the slot allocated by GetOrUpdate
    //Internally calls AtlasTexture.Readback to read the whole atlas then crops the slot region
    public byte[] ReadbackSlot(int slotX, int slotY)
    {
        var fullPixels = AtlasTexture.Readback();
        var slotPixels = new byte[SlotTextureSize * SlotTextureSize * 4];
        var srcStride = TextureSize * 4;
        var dstStride = SlotTextureSize * 4;
        var srcOffsetY = slotY * SlotTextureSize;
        var srcOffsetX = slotX * SlotTextureSize;
        for (int y = 0; y < SlotTextureSize; y++)
        {
            var srcRow = (srcOffsetY + y) * srcStride + srcOffsetX * 4;
            var dstRow = y * dstStride;
            Array.Copy(fullPixels, srcRow, slotPixels, dstRow, dstStride);
        }
        return slotPixels;
    }

    //ReadbackFullAtlas reads the whole AtlasTexture back to the CPU for diagnostic tests
    //Returns a TextureSize x TextureSize x 4(RGBA) byte array
    public byte[] ReadbackFullAtlas() => AtlasTexture.Readback();

    //DrawToSlot renders the item into a slot, maps to vanilla GuiItemAtlas.drawToSlot
    //poseStack translates into the slot center and scale(slotSize,-slotSize,slotSize) flips Y
    //After item.submit, ItemFeatureRenderer.Execute writes vertices into LastFrameVertices
    //When SupportsGpuRendering=true it uploads vertices + records Vulkan commands to render into the AtlasTexture slot region
    protected override void DrawToSlot(int slotX, int slotY, bool clear, object itemIdentity)
    {
        var left = slotX * SlotTextureSize;
        var top = slotY * SlotTextureSize;
        //The STALE state clears the slot; the PoC clears LastFrameVertices and the full version clears the AtlasTexture region
        if (clear) LastFrameVertices.Clear();

        //Sets the orthographic projection to the atlas size with invertY=true to flip Y
        _projection.SetupOrtho(-1000f, 1000f, TextureSize, TextureSize, true);

        _poseStack.PushPose();
        //With row-major, v*(S*T)=(v*S)*T scales then translates, so the translation is not scaled
        //Vanilla column-major T*S*v scales then translates; with NetCraft's row-major the order must be swapped
        _poseStack.Scale(SlotTextureSize, -SlotTextureSize, SlotTextureSize);
        _poseStack.Translate(left + SlotTextureSize / 2f, top + SlotTextureSize / 2f, 0f);

        if (_items.TryGetValue(itemIdentity, out var item))
        {
            _collector.Nodes.Clear();
            item.Submit(_poseStack, _collector,
                ItemFeatureRenderer.FullBright, ItemFeatureRenderer.NoOverlay, 0);
            //Clears the current frame's vertices then writes the new ones
            LastFrameVertices.Clear();
            ItemFeatureRenderer.Execute(_collector, LastFrameVertices);
        }
        _poseStack.PopPose();

        if (Device.SupportsGpuRendering && LastFrameVertices.VertexCount > 0)
            RenderToGpu(slotX, slotY);
    }

    //RenderToGpu uploads vertices + updates the MVP UBO + records Vulkan commands to render into the AtlasTexture slot region
    //model is not uploaded because VertexConsumer3D.PutBakedQuad already applies the pose on the CPU and the shader uses Model=Identity
    private void RenderToGpu(int slotX, int slotY)
    {
        //Uploads the vertex buffer, reusing it across frames and rebuilding only when the size is insufficient
        var vertices = LastFrameVertices.Vertices;
        var vertexBytes = VerticesToBytes(vertices);
        if (_vertexBuffer == null || _vertexBuffer.Size < vertexBytes.Length)
        {
            _vertexBuffer?.Dispose();
            _vertexBuffer = Device.CreateHostVisibleBuffer(vertexBytes.Length, GpuBuffer.UsageVertex | GpuBuffer.UsageCopyDst);
        }
        _vertexBuffer.Upload<byte>(vertexBytes);

        //Updates the MVP UBO with model=Identity because VertexConsumer3D.PutBakedQuad already applies the pose on the CPU
        //Blaze3d transforms vertices on the CPU with Model=Identity in the shader; applying it again pushes vertices outside the NDC view volume and they get clipped
        //SetupOrtho invertY=true assumes OpenGL Y up while the Vulkan framebuffer has Y down, so M22+M42 are flipped to match Vulkan
        //Converts OpenGL Z [-1,1] to Vulkan Z [0,1] with M33*=0.5 M43=M43*0.5+0.5
        var proj = _projection.GetMatrix();
        proj.M22 *= -1;
        proj.M42 *= -1;
        proj.M33 *= 0.5f;
        proj.M43 = proj.M43 * 0.5f + 0.5f;
        //System.Numerics row-major memory is memcpy'd directly into a GLSL column-major mat4
        //GLSL m*v (column vector multiply) is equivalent to CPU v*M (row vector multiply); both give the same result
        //Uploading the transpose would turn it into CPU M*v (column vector multiply), giving the w component the wrong sign and clipping vertices
        var mvp = new MvpUniform
        {
            Model = Matrix4x4.Identity,
            View = Matrix4x4.Identity,
            Proj = proj
        };
        _mvpUbo!.Upload<MvpUniform>(new[] { mvp });

        //Records render commands with colorLoadOp=Load to preserve other slots' content and depth cleared to 1.0 each time
        //scissor restricts rendering to the current slot region without polluting other slots
        using var encoder = Device.CreateCommandEncoder();
        var clearColor = new Vector4(0f, 0f, 0f, 1f);
        using var pass = encoder.CreateRenderPass(_pipeline!, AtlasTexture, clearColor, AtlasDepth, 1.0f, GpuLoadOp.Load);
        pass.SetVertexBuffer(0, _vertexBuffer!);
        pass.SetIndexBuffer(_indexBuffer!, GpuIndexType.UInt16);
        pass.BindDescriptorSet(_mvpSet!, 0);
        pass.BindDescriptorSet(_lighting!.CurrentDescriptorSet!, 1);
        pass.BindDescriptorSet(_lightmapSet!, 2);
        pass.BindDescriptorSet(_atlasSet!, 3);
        pass.EnableScissor(slotX * SlotTextureSize, slotY * SlotTextureSize, SlotTextureSize, SlotTextureSize);
        //CubeModel 6 faces * 6 indices = 36
        pass.DrawIndexed(36);
        pass.Close();
        encoder.Submit();
    }

    //VerticesToBytes converts List<float> vertices to byte[] via direct memcpy
    private static byte[] VerticesToBytes(List<float> vertices)
    {
        var span = CollectionsMarshal.AsSpan(vertices);
        return MemoryMarshal.AsBytes(span).ToArray();
    }

    //GenerateQuadIndices generates the index array for quadCount quads, 6 indices per quad (0,1,2,2,3,0) accumulating baseVertex
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
        _vertexBuffer?.Dispose();
        _indexBuffer?.Dispose();
        _atlasSet?.Dispose();
        _atlasLayout?.Dispose();
        _lightmapSet?.Dispose();
        _lightmapLayout?.Dispose();
        _mvpSet?.Dispose();
        _mvpLayout?.Dispose();
        _mvpUbo?.Dispose();
        _pipeline?.Dispose();
        _itemAtlas?.Dispose();
        _lightTexture?.Dispose();
        _lighting?.Dispose();
    }
}
