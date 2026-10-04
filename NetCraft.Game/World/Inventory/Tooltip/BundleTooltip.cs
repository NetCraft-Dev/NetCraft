using NetCraft.Game.World.Items.Component;

namespace NetCraft.Game.World.Inventory.Tooltip;

//BundleTooltip 收纳袋提示组件 对应原版 net.minecraft.world.inventory.tooltip.BundleTooltip
//只承载内容 具体绘制由渲染层按类型分派
public sealed record BundleTooltip(BundleContents Contents) : TooltipComponent;
