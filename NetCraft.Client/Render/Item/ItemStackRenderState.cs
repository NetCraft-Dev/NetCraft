using System.Numerics;
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

namespace NetCraft.Client.Render.Item;

//ItemStackRenderState item render state, maps to vanilla ItemStackRenderState
//Holds a BakedQuad list; on submit it hands the pose snapshot+quads+tint to SubmitNodeCollector for deferred rendering
//The PoC simplifies to one layer without LayerRenderState layering and without specialRenderer/foilType; the full version has activeLayerCount
//TrackingItemStackRenderState subclass collecting modelIdentity as the GuiItemAtlas cache key
public class ItemStackRenderState
{
    private List<BakedQuad> _quads = new();
    //ModelIdentityElements item identity elements collected by the TrackingItemStackRenderState override
    public virtual void AppendModelIdentityElement(object element) { }

    //SetQuads sets the item's BakedQuad list, filled by ItemModel.update
    public void SetQuads(List<BakedQuad> quads) => _quads = quads;

    public bool UsesBlockLight { get; set; } = true;
    public bool IsAnimated { get; set; }

    //Submit hands the pose snapshot+quads to the collector for deferred rendering
    //maps to vanilla item.submit(poseStack, collector, light, overlay, outline)
    public void Submit(PoseStack poseStack, ItemSubmitCollector collector,
        int lightCoords, int overlayCoords, int outlineColor)
    {
        var pose = poseStack.Copy();
        collector.SubmitItem(pose, _quads, lightCoords, overlayCoords, outlineColor);
    }
}

//TrackingItemStackRenderState GUI item cache key, maps to vanilla TrackingItemStackRenderState
//Collects modelIdentityElements as the GuiItemAtlas GetOrUpdate key
public sealed class TrackingItemStackRenderState : ItemStackRenderState
{
    private readonly List<object> _identityElements = new();

    public override void AppendModelIdentityElement(object element)
        => _identityElements.Add(element);

    //ModelIdentity returns the identity list; reference equality comparison is used as the cache key
    public object ModelIdentity => _identityElements;
}

//ItemSubmitCollector item submit collector, maps to vanilla SubmitNodeCollector
//Collects submitted pose snapshots+quads; later ItemFeatureRenderer.Execute writes them to the VertexConsumer
//The PoC simplifies to List<SubmitNode> without phase grouping (solid/translucent); vanilla has 15 phases
public sealed class ItemSubmitCollector
{
    public readonly List<ItemSubmitNode> Nodes = new();

    public void SubmitItem(Matrix4x4 pose, List<BakedQuad> quads,
        int lightCoords, int overlayCoords, int outlineColor)
    {
        Nodes.Add(new ItemSubmitNode(pose, quads, lightCoords, overlayCoords, outlineColor));
    }
}

//ItemSubmitNode deferred render node for one submit, maps to vanilla ItemFeatureRenderer.Submit
//Stores a pose snapshot (quads are shared by reference, not copied) light/overlay/tint written out with putBakedQuad at execute
public readonly struct ItemSubmitNode
{
    public readonly Matrix4x4 Pose;
    public readonly List<BakedQuad> Quads;
    public readonly int LightCoords;
    public readonly int OverlayCoords;
    public readonly int OutlineColor;

    public ItemSubmitNode(Matrix4x4 pose, List<BakedQuad> quads,
        int lightCoords, int overlayCoords, int outlineColor)
    {
        Pose = pose; Quads = quads;
        LightCoords = lightCoords; OverlayCoords = overlayCoords;
        OutlineColor = outlineColor;
    }
}
