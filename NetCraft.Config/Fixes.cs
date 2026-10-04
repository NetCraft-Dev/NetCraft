namespace NetCraft.Config;
//修复行为开关（对应原版SharedConstants.FIX_*与26.x实际修复的bug）
//默认值反映是否启用对应bug修复，原版未启用的保持false以维持兼容行为
public static class Fixes
{
    //TNT复制修复，原版默认false以保持可刷行为
    //启用后TNT被piston推动时不会保留原实体，从而破坏TNT复制机
    public const bool TntDupe = false;
    //沙子/重力方块复制修复，原版默认false以保持可刷行为
    //启用后重力方块被piston推动时不会重复生成
    public const bool SandDupe = false;
    //蝙蝠刷线机修复，已在26.2实装
    //对应原版Entity.isIgnoringBlockTriggers()在Bat中返回true
    //蝙蝠不再触发绊线/压力板，刷线机因此失效
    public const bool BatStringFarm = true;
    //Marker盔甲架不触发压力板，已在26.2实装
    //对应原版ArmorStand.isIgnoringBlockTriggers()返回isMarker()
    public const bool MarkerArmorStandNoTrigger = true;
    //Display/Interaction/Marker/OminousItemSpawner不触发方块触发器，已在26.2实装
    //对应原版这些实体的isIgnoringBlockTriggers()返回true
    public const bool NonInteractiveEntityNoTrigger = true;
    //活塞推动绊线钩不触发更新，已在原版实装
    //对应原版TripWireHookBlock.affectNeighborsAfterRemoval中movedByPiston=true直接返回
    public const bool PistonPushedTripwireHookNoUpdate = true;
    //修复下落的方块在piston推动时不掉落方块实体，已在原版实装
    public const bool FallingBlockPistonDupe = true;
    //修复村民交易时物品NBT复制漏洞
    public const bool VillagerTradeNbtDupe = true;
    //修复shulker box物品内容在特定条件下被复制的问题
    public const bool ShulkerBoxDupe = true;
    //修复实体在区块边界传送时的位置同步问题
    public const bool EntityChunkBorderTeleportSync = true;
    //修复低分辨率屏幕下文本渲染被裁剪的问题
    public const bool LowResolutionTextClipping = true;
    //修复worldgen在高度边界处溢出，属于non-determinism修复
    //注意：启用后世界生成将与原版存在微小差异
    public const bool WorldgenBoundaryOverflow = false;
}