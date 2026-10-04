using System.Numerics;

namespace NetCraft.Gpu;

//ItemStackRenderState 物品渲染状态对标原版 ItemStackRenderState
//持有 BakedQuad 列表 submit 时把 pose 快照+quads+tint 丢给 SubmitNodeCollector 延迟渲染
//PoC 简化单层无 LayerRenderState 分层无 specialRenderer/foilType 完整版有 activeLayerCount
//TrackingItemStackRenderState 子类收集 modelIdentity 作 GuiItemAtlas 缓存键
public class ItemStackRenderState
{
    private List<BakedQuad> _quads = new();
    //ModelIdentityElements 物品身份标识 TrackingItemStackRenderState 重写收集
    public virtual void AppendModelIdentityElement(object element) { }

    //SetQuads 设置物品的 BakedQuad 列表 ItemModel.update 时填充
    public void SetQuads(List<BakedQuad> quads) => _quads = quads;

    public bool UsesBlockLight { get; set; } = true;
    public bool IsAnimated { get; set; }

    //Submit 把 pose 快照+quads 丢给 collector 延迟渲染
    //对标原版 item.submit(poseStack, collector, light, overlay, outline)
    public void Submit(PoseStack poseStack, ItemSubmitCollector collector,
        int lightCoords, int overlayCoords, int outlineColor)
    {
        var pose = poseStack.Copy();
        collector.SubmitItem(pose, _quads, lightCoords, overlayCoords, outlineColor);
    }
}

//TrackingItemStackRenderState GUI 物品缓存键对标原版 TrackingItemStackRenderState
//收集 modelIdentityElements 作 GuiItemAtlas GetOrUpdate 的 key
public sealed class TrackingItemStackRenderState : ItemStackRenderState
{
    private readonly List<object> _identityElements = new();

    public override void AppendModelIdentityElement(object element)
        => _identityElements.Add(element);

    //ModelIdentity 返回身份标识列表引用相等比较作缓存键
    public object ModelIdentity => _identityElements;
}

//ItemSubmitCollector 物品提交收集器对标原版 SubmitNodeCollector
//收集 submit 的 pose 快照+quads 后续 ItemFeatureRenderer.Execute 写到 VertexConsumer
//PoC 简化为 List<SubmitNode> 不做 phase 分组(solid/translucent)原版有 15 个 phase
public sealed class ItemSubmitCollector
{
    public readonly List<ItemSubmitNode> Nodes = new();

    public void SubmitItem(Matrix4x4 pose, List<BakedQuad> quads,
        int lightCoords, int overlayCoords, int outlineColor)
    {
        Nodes.Add(new ItemSubmitNode(pose, quads, lightCoords, overlayCoords, outlineColor));
    }
}

//ItemSubmitNode 单次提交的延迟渲染节点对标原版 ItemFeatureRenderer.Submit
//存 pose 快照(quads 引用共享不拷贝) light/overlay/tint execute 时用 putBakedQuad 写出
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
