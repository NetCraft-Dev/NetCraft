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
namespace NetCraft.Client.Gui.Render;

//GuiItemAtlas item atlas abstract base class, maps to vanilla GuiItemAtlas
//Manages the atlas color texture + depth texture + DynamicAtlasAllocator
//Each frame tryPrepareFor checks space and reclaimSpaceFor frees space when short
//getOrUpdate decides DrawToSlot by SlotState (EMPTY draw new/STALE clear then draw/READY return UV directly)
//DrawToSlot is abstract; subclasses use GpuDevice to record 3D item render commands, maps to vanilla item.submit
//There is no 3D item render pipeline yet; subclasses are deferred to later world rendering, defining the data layer and template flow first
public abstract class GuiItemAtlas : IDisposable
{
    //MinimumTextureSize minimum atlas size, maps to vanilla 512
    private const int MinimumTextureSize = 512;

    protected GpuDevice Device { get; }
    //AtlasTexture exposed for external registration with GuiResourceManager to get a textureId for DrawImage sampling
    public GpuTexture AtlasTexture { get; }
    protected GpuTexture AtlasDepth { get; }
    protected DynamicAtlasAllocator<object> Allocator { get; }
    public int TextureSize { get; }
    public int SlotTextureSize { get; }

    protected GuiItemAtlas(GpuDevice device, int textureSize, int slotTextureSize)
    {
        Device = device;
        TextureSize = textureSize;
        SlotTextureSize = slotTextureSize;
        var storageSize = textureSize / slotTextureSize;
        AtlasTexture = device.CreateTexture((GpuTexture.UsageRenderAttachment | GpuTexture.UsageTextureBinding), "texture", GpuFormat.Rgba8Unorm, textureSize, textureSize, 1, 1);
        AtlasDepth = device.CreateTexture(null, GpuTexture.UsageRenderAttachment, GpuFormat.D32Float, textureSize, textureSize, 1, 1);
        Allocator = new DynamicAtlasAllocator<object>(storageSize, storageSize);
    }

    //ComputeTextureSizeFor computes the atlas size from slotTextureSize and the required slot count, maps to vanilla
    //preferredSlotCount = required + required/2, leaving a 50% margin
    //atlasSize = smallest square side, the square root of preferredSlotCount rounded up
    //maxTextureSize is queried from device.Limits.MaxTextureSizeForFormat(R8G8B8A8Unorm) instead of the hardcoded 4096
    public static int ComputeTextureSizeFor(GpuDevice device, int slotTextureSize, int requiredSlotCount)
    {
        var maxTextureSize = device.Limits.MaxTextureSizeForFormat(GpuFormat.Rgba8Unorm);
        return ComputeTextureSizeFor(slotTextureSize, requiredSlotCount, maxTextureSize);
    }

    //ComputeTextureSizeFor explicit maxTextureSize overload for tests and device-independent scenarios
    public static int ComputeTextureSizeFor(int slotTextureSize, int requiredSlotCount, int maxTextureSize)
    {
        var preferredSlotCount = requiredSlotCount + requiredSlotCount / 2;
        var atlasSize = SmallestSquareSide(preferredSlotCount);
        return Math.Clamp(SmallestEncompassingPowerOfTwo(atlasSize * slotTextureSize), MinimumTextureSize, maxTextureSize);
    }

    //SmallestSquareSide smallest square side holding count slots
    private static int SmallestSquareSide(int count)
    {
        if (count <= 0) return 1;
        var side = (int)Math.Ceiling(Math.Sqrt(count));
        return Math.Max(1, side);
    }

    //SmallestEncompassingPowerOfTwo smallest enclosing power of two
    private static int SmallestEncompassingPowerOfTwo(int value)
    {
        if (value <= 1) return 1;
        var power = 1;
        while (power < value) power <<= 1;
        return power;
    }

    //EndFrame frees discardAfterFrame slots at frame end, maps to vanilla
    public void EndFrame() => Allocator.EndFrame();

    //TryPrepareFor checks whether the atlas can hold items and calls reclaimSpaceFor when short
    public bool TryPrepareFor(IReadOnlySet<object> items)
    {
        return Allocator.HasSpaceForAll(items) || Allocator.ReclaimSpaceFor(items);
    }

    //GetOrUpdate looks up/allocates a slot by itemIdentity and decides DrawToSlot by SlotState
    //Returns a SlotView with UVs for BlitRenderState, or null when the atlas is full
    //isAnimated true for animated items, freed at frame end
    public SlotView? GetOrUpdate(object itemIdentity, bool isAnimated)
    {
        var slot = Allocator.GetOrAllocate(itemIdentity, isAnimated);
        if (slot == null) return null;
        switch (slot.State)
        {
            case DynamicAtlasAllocator<object>.SlotState.Empty:
                DrawToSlot(slot.X, slot.Y, clear: false, itemIdentity);
                break;
            case DynamicAtlasAllocator<object>.SlotState.Stale:
                DrawToSlot(slot.X, slot.Y, clear: true, itemIdentity);
                break;
        }
        //Vulkan texture V=0 is the top of the image (opposite to OpenGL); slot(x,y) pixels are at top (y*slotSize)
        //V0=slotY*slotUvSize top UV V1=(slotY+1)*slotUvSize bottom UV; with BlitRenderState V0 pairs with Y0 at the top
        var slotUvSize = (float)SlotTextureSize / TextureSize;
        var u0 = slot.X * slotUvSize;
        var v0 = slot.Y * slotUvSize;
        return new SlotView(AtlasTexture, u0, v0, u0 + slotUvSize, v0 + slotUvSize);
    }

    //DrawToSlot renders a 3D item into the given slot, maps to vanilla drawToSlot
    //slotX/slotY grid coordinates; clear=true means the STALE state needs the slot cleared first
    //itemIdentity item identifier mapped by subclasses to a concrete ItemStackRenderState for 3D rendering
    //Subclasses use GpuDevice to record commands: set orthographic projection+scissor+clear+item.submit
    protected abstract void DrawToSlot(int slotX, int slotY, bool clear, object itemIdentity);

    public void Dispose()
    {
        AtlasTexture.Dispose();
        AtlasDepth.Dispose();
        OnDispose();
        GC.SuppressFinalize(this);
    }

    //OnDispose extra resource-release hook for subclasses
    protected virtual void OnDispose() { }
}

//SlotView atlas slot UV view, maps to vanilla GuiItemAtlas.SlotView
//BlitRenderState uses these UVs to sample the item icon from the atlas texture
public sealed record SlotView(
    GpuTexture Texture,
    float U0,
    float V0,
    float U1,
    float V1);
