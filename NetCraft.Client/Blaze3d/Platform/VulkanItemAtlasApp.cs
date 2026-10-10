using System.Numerics;
using Silk.NET.Vulkan;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
using NetCraft.Client.Render;
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

namespace NetCraft.Client.Blaze3d.Platform;

//VulkanItemAtlasApp 3D item atlas rendering integration test app
//Verifies that ItemItemAtlas.DrawToSlot runs on a real Vulkan backend
//Rotates through GetOrUpdate every frame, triggering DrawToSlot to render items into AtlasTexture
//OnRecordCommandBuffer only clears the swapchain without showing atlas content, verifying the GPU command recording chain is stable
public sealed unsafe class VulkanItemAtlasApp : VulkanAppBase
{
    private const int AtlasTextureSize = 512;
    private const int SlotTextureSize = 64;
    private const int ItemCount = 10;

    private VulkanRenderPipeline _clearPipeline = null!;
    private VulkanImage _depthImage = null!;
    private GpuTextureView _depthView = null!;
    private ItemItemAtlas _itemAtlas = null!;
    private readonly object[] _itemIds = new object[ItemCount];

    public VulkanItemAtlasApp() : base(800, 600) { }

    protected override string WindowTitle => "NetCraft.Gpu Vulkan ItemAtlas PoC";

    protected override void OnCreatePipelineResources()
    {
        CreateDepthImage();
        CreateClearPipeline();
        CreateItemAtlas();
    }

    protected override void OnSwapchainRecreated()
    {
        _depthImage.Dispose();
        _depthView.Dispose();
        _clearPipeline.Dispose();
        CreateDepthImage();
        CreateClearPipeline();
    }

    //CreateDepthImage creates a depth attachment for the clear pipeline
    private void CreateDepthImage()
    {
        _depthImage = (VulkanImage)_device.CreateTexture(null, GpuTexture.UsageRenderAttachment, GpuFormat.D32Float, (int)_swapchainExtent.Width, (int)_swapchainExtent.Height, 1, 1);
        _depthImage.Upload(ReadOnlySpan<byte>.Empty);
        _depthView = _device.CreateTextureView(_depthImage);
    }

    //CreateClearPipeline creates a pipeline that only clears the swapchain, using the built-in SpirvShaders shader
    private void CreateClearPipeline()
    {
        var description = new RenderPipelineDescription
        {
            DepthTestEnabled = true
        };
        _clearPipeline = new VulkanRenderPipeline(_vkDevice.Api, _vkDevice.Device, _swapchainImageFormat, _swapchainExtent, description);
    }

    //CreateItemAtlas creates ItemItemAtlas and registers 10 items
    private void CreateItemAtlas()
    {
        _itemAtlas = new ItemItemAtlas(_device, AtlasTextureSize, SlotTextureSize);
        for (int i = 0; i < ItemCount; i++)
        {
            _itemIds[i] = $"item_{i}";
            _itemAtlas.RegisterItem(_itemIds[i], 1f);
        }
    }

    //OnRecordCommandBuffer rotates through GetOrUpdate every frame, triggering DrawToSlot to render items into AtlasTexture
    //The scene target is only cleared, verifying the render loop stays stable while the atlas is updated separately
    protected override void OnRecordCommandBuffer(CommandEncoder encoder, GpuTextureView sceneView)
    {
        var frame = _framesRendered % ItemCount;
        _itemAtlas.GetOrUpdate(_itemIds[frame], isAnimated: false);

        var descriptor = RenderPassDescriptor.Create(() => "atlas-clear")
            .WithColorAttachment(sceneView, new Vector4(0.1f, 0.1f, 0.1f, 1f))
            .WithDepthAttachment(_depthView, 1.0)
            .WithRenderArea(new NetCraft.Client.Blaze3d.Systems.RenderPass.RenderArea(0, 0, (int)_swapchainExtent.Width, (int)_swapchainExtent.Height));
        var pass = (VulkanRenderPass)encoder.Backend.CreateRenderPass(descriptor);
        pass.SetCompiledPipeline(_clearPipeline);
        encoder.SubmitRenderPass();
    }

    //RenderSlotAndReadback renders the given item into a slot and reads back the slot pixels for integration tests
    //Returns a SlotTextureSize x SlotTextureSize x 4(RGBA) byte array
    //slotX/slotY specified by the test, usually (0,0) since the first item is allocated slot (0,0)
    public byte[] RenderSlotAndReadback(int itemIndex, int slotX, int slotY)
    {
        _itemAtlas.GetOrUpdate(_itemIds[itemIndex], isAnimated: false);
        _vkDevice.WaitIdle();
        return _itemAtlas.ReadbackSlot(slotX, slotY);
    }

    //RenderAndReadbackFullAtlas renders items and reads back the whole atlas for diagnostic tests
    //Returns an AtlasTextureSize x AtlasTextureSize x 4(RGBA) byte array
    public byte[] RenderAndReadbackFullAtlas(int itemIndex)
    {
        _itemAtlas.GetOrUpdate(_itemIds[itemIndex], isAnimated: false);
        _vkDevice.WaitIdle();
        return _itemAtlas.ReadbackFullAtlas();
    }

    //Slot0ReadbackPixels the readback pixels of the first item's slot (0,0) for test assertions
    //Filled by calling RenderSlotAndReadback(0,0,0) in OnBeforeRun; the test reads it after RunFor returns
    public byte[]? Slot0ReadbackPixels { get; private set; }
    //FullAtlasReadbackPixels whole-atlas readback pixels for diagnosing render write positions
    public byte[]? FullAtlasReadbackPixels { get; private set; }
    //LastVertexCount vertex count of the most recent DrawToSlot, for diagnosing whether RenderToGpu was called
    public int LastVertexCount { get; private set; }
    //WriteTextureReadbackPixels readback pixels of the WriteToTexture test for test assertions
    //OnBeforeRun uses CreateCommandEncoder+WriteToTexture to write known pixels and verifies with a readback after Submit
    public byte[]? WriteTextureReadbackPixels { get; private set; }

    //OnBeforeRun override renders all registered items into their slots before the window main loop then reads them back
    //Each item's GetOrUpdate allocates an independent slot via the Allocator and DrawToSlot renders into that slot region
    //Reads back the whole atlas + slot(0,0) for tests to verify multi-slot rendering and single-slot content
    //Additionally tests ICommandEncoder.WriteToTexture command recording+Submit writing texture pixels
    protected override void OnBeforeRun()
    {
        base.OnBeforeRun();
        for (int i = 0; i < ItemCount; i++)
            _itemAtlas.GetOrUpdate(_itemIds[i], isAnimated: false);
        _vkDevice.WaitIdle();
        LastVertexCount = _itemAtlas.LastFrameVertices.VertexCount;
        FullAtlasReadbackPixels = _itemAtlas.ReadbackFullAtlas();
        Slot0ReadbackPixels = _itemAtlas.ReadbackSlot(0, 0);
        WriteTextureReadbackPixels = TestWriteToTexture();
    }

    //TestWriteToTexture verifies ICommandEncoder.WriteToTexture records commands+Submit writes pixels correctly
    //Creates a 4x4 SampledImage, writes full red RGBA(255,0,0,255) with WriteToTexture and verifies with a readback after Submit
    //Verifies staging buffer lifetime management: not released before Submit so the GPU can read the data correctly
    private byte[] TestWriteToTexture()
    {
        var img = (VulkanImage)_device.CreateTexture(null, GpuTexture.UsageRenderAttachment, GpuFormat.D32Float, (int)_swapchainExtent.Width, (int)_swapchainExtent.Height, 1, 1);
        try
        {
            //4x4 full red RGBA(255,0,0,255)
            var pixels = new byte[4 * 4 * 4];
            for (int i = 0; i < pixels.Length; i += 4)
            {
                pixels[i] = 255;
                pixels[i + 1] = 0;
                pixels[i + 2] = 0;
                pixels[i + 3] = 255;
            }
            using var encoder = _device.CreateCommandEncoder();
            encoder.WriteToTexture(img, pixels, 0, 0, 0, 0, 4, 4);
            encoder.Submit();
            return img.Readback();
        }
        finally
        {
            img.Dispose();
        }
    }

    //CountNonBlackPixels counts non-zero RGB pixels in the slot for tests to verify the actual render content
    //The clear color is (0,0,0,1) black; after rendering non-zero RGB pixels > 0 mean DrawToSlot wrote item pixels
    public static int CountNonBlackPixels(byte[] slotPixels)
    {
        int count = 0;
        for (int i = 0; i < slotPixels.Length; i += 4)
        {
            if (slotPixels[i] > 0 || slotPixels[i + 1] > 0 || slotPixels[i + 2] > 0) count++;
        }
        return count;
    }

    //CountNonBlackPixelsRegion counts non-zero RGB pixels in the given rectangle to diagnose render write positions
    //fullPixels is the whole-atlas pixels width is the atlas size region is the sub-rectangle to count
    public static int CountNonBlackPixelsRegion(byte[] fullPixels, int atlasWidth, int regionX, int regionY, int regionW, int regionH)
    {
        int count = 0;
        for (int y = regionY; y < regionY + regionH && y < atlasWidth; y++)
        {
            for (int x = regionX; x < regionX + regionW && x < atlasWidth; x++)
            {
                var idx = (y * atlasWidth + x) * 4;
                if (fullPixels[idx] > 0 || fullPixels[idx + 1] > 0 || fullPixels[idx + 2] > 0) count++;
            }
        }
        return count;
    }

    //CountAlphaPixels counts pixels with the given alpha to verify the clear color was written correctly
    //The clear color (0,0,0,1) should have alpha 255 before rendering
    public static int CountAlphaPixels(byte[] pixels, byte alpha)
    {
        int count = 0;
        for (int i = 0; i < pixels.Length; i += 4)
        {
            if (pixels[i + 3] == alpha) count++;
        }
        return count;
    }

    //CountAlphaPixelsRegion counts pixels with the given alpha in the given rectangle to verify the clear color's write region
    public static int CountAlphaPixelsRegion(byte[] fullPixels, int atlasWidth, int regionX, int regionY, int regionW, int regionH, byte alpha)
    {
        int count = 0;
        for (int y = regionY; y < regionY + regionH && y < atlasWidth; y++)
        {
            for (int x = regionX; x < regionX + regionW && x < atlasWidth; x++)
            {
                var idx = (y * atlasWidth + x) * 4;
                if (fullPixels[idx + 3] == alpha) count++;
            }
        }
        return count;
    }

    //FindFirstNonBlackPixel finds the first non-zero RGB pixel in the whole atlas to diagnose render write positions
    //Returns (x, y), or (-1, -1) if none was found
    public static (int x, int y) FindFirstNonBlackPixel(byte[] fullPixels, int atlasWidth)
    {
        for (int i = 0; i < fullPixels.Length; i += 4)
        {
            if (fullPixels[i] > 0 || fullPixels[i + 1] > 0 || fullPixels[i + 2] > 0)
            {
                var pixelIdx = i / 4;
                return (pixelIdx % atlasWidth, pixelIdx / atlasWidth);
            }
        }
        return (-1, -1);
    }

    protected override void OnCleanupPipelineResources()
    {
        _itemAtlas.Dispose();
        _depthImage.Dispose();
        _clearPipeline.Dispose();
    }
}
