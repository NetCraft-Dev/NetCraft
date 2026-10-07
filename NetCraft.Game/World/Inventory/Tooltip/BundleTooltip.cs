using NetCraft.Game.World.Items.Component;

namespace NetCraft.Game.World.Inventory.Tooltip;

//BundleTooltip bundle tooltip component, maps to vanilla net.minecraft.world.inventory.tooltip.BundleTooltip
//Only carries the contents, actual drawing is dispatched by type in the render layer
public sealed record BundleTooltip(BundleContents Contents) : TooltipComponent;
