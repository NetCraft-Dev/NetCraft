namespace NetCraft.Config;
//Behavior fix switches (map to vanilla SharedConstants.FIX_* plus the bugs actually fixed in 26.x)
//Defaults reflect whether the corresponding bug fix is enabled, ones vanilla leaves off stay false to preserve compatible behavior
public static class Fixes
{
    //TNT dupe fix, vanilla defaults to false to keep duping possible
    //When enabled a TNT entity pushed by a piston no longer keeps the original entity, which breaks TNT dupers
    public const bool TntDupe = false;
    //Sand/gravity block dupe fix, vanilla defaults to false to keep duping possible
    //When enabled a gravity block pushed by a piston is no longer respawned
    public const bool SandDupe = false;
    //Bat string farm fix, shipped in 26.2
    //Maps to vanilla Entity.isIgnoringBlockTriggers() returning true for Bat
    //Bats no longer trigger tripwires/pressure plates, so string farms stop working
    public const bool BatStringFarm = true;
    //Marker armor stands no longer trigger pressure plates, shipped in 26.2
    //Maps to vanilla ArmorStand.isIgnoringBlockTriggers() returning isMarker()
    public const bool MarkerArmorStandNoTrigger = true;
    //Display/Interaction/Marker/OminousItemSpawner no longer trigger block triggers, shipped in 26.2
    //Maps to vanilla isIgnoringBlockTriggers() returning true for these entities
    public const bool NonInteractiveEntityNoTrigger = true;
    //A piston pushing a tripwire hook no longer triggers updates, shipped in vanilla
    //Maps to vanilla TripWireHookBlock.affectNeighborsAfterRemoval returning early when movedByPiston is true
    public const bool PistonPushedTripwireHookNoUpdate = true;
    //Fix falling blocks not dropping a block entity when pushed by a piston, shipped in vanilla
    public const bool FallingBlockPistonDupe = true;
    //Fix the item NBT duplication exploit during villager trades
    public const bool VillagerTradeNbtDupe = true;
    //Fix shulker box item contents being duplicated under certain conditions
    public const bool ShulkerBoxDupe = true;
    //Fix position desync when an entity teleports across a chunk border
    public const bool EntityChunkBorderTeleportSync = true;
    //Fix text rendering being clipped on low-resolution screens
    public const bool LowResolutionTextClipping = true;
    //Fix worldgen overflow at height boundaries, a non-determinism fix
    //Note: enabling this introduces minor differences from vanilla world generation
    public const bool WorldgenBoundaryOverflow = false;
}
