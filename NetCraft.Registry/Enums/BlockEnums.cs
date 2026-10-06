namespace NetCraft.Registry.Enums;

//BlockEnums generated from vanilla block property enums; member names are the vanilla serialized names and order is the vanilla declaration order
//The order directly determines BlockState global id assignment and must not be changed casually
//Kept in its own namespace to avoid common names like Direction/Axis colliding with existing types
//This file is rewritten entirely by __gen_blocks.py; hand-written derived members go in BlockEnumsExtensions.cs
public static class BlockEnums
{
    private static readonly Dictionary<string, Type> ByName = new()
    {
        ["AttachFace"] = typeof(AttachFace),
        ["Axis"] = typeof(Axis),
        ["BambooLeaves"] = typeof(BambooLeaves),
        ["BedPart"] = typeof(BedPart),
        ["BellAttachType"] = typeof(BellAttachType),
        ["ChestType"] = typeof(ChestType),
        ["ComparatorMode"] = typeof(ComparatorMode),
        ["CreakingHeartState"] = typeof(CreakingHeartState),
        ["Direction"] = typeof(Direction),
        ["DoorHingeSide"] = typeof(DoorHingeSide),
        ["DoubleBlockHalf"] = typeof(DoubleBlockHalf),
        ["FrontAndTop"] = typeof(FrontAndTop),
        ["Half"] = typeof(Half),
        ["NoteBlockInstrument"] = typeof(NoteBlockInstrument),
        ["PistonType"] = typeof(PistonType),
        ["Pose"] = typeof(Pose),
        ["PotentSulfurState"] = typeof(PotentSulfurState),
        ["RailShape"] = typeof(RailShape),
        ["RedstoneSide"] = typeof(RedstoneSide),
        ["SculkSensorPhase"] = typeof(SculkSensorPhase),
        ["SideChainPart"] = typeof(SideChainPart),
        ["SlabType"] = typeof(SlabType),
        ["SpeleothemThickness"] = typeof(SpeleothemThickness),
        ["StairsShape"] = typeof(StairsShape),
        ["StructureMode"] = typeof(StructureMode),
        ["TestBlockMode"] = typeof(TestBlockMode),
        ["Tilt"] = typeof(Tilt),
        ["TrialSpawnerState"] = typeof(TrialSpawnerState),
        ["VaultState"] = typeof(VaultState),
        ["WallSide"] = typeof(WallSide),
    };

    //Resolve gets the generated enum type by vanilla enum name
    public static Type? Resolve(string name) => ByName.GetValueOrDefault(name);
}

//AttachFace maps to the vanilla enum of the same name
public enum AttachFace
{
    floor,
    wall,
    ceiling,
}

//Axis maps to the vanilla enum of the same name
public enum Axis
{
    x,
    y,
    z,
}

//BambooLeaves maps to the vanilla enum of the same name
public enum BambooLeaves
{
    none,
    small,
    large,
}

//BedPart maps to the vanilla enum of the same name
public enum BedPart
{
    head,
    foot,
}

//BellAttachType maps to the vanilla enum of the same name
public enum BellAttachType
{
    floor,
    ceiling,
    single_wall,
    double_wall,
}

//ChestType maps to the vanilla enum of the same name
public enum ChestType
{
    single,
    left,
    right,
}

//ComparatorMode maps to the vanilla enum of the same name
public enum ComparatorMode
{
    compare,
    subtract,
}

//CreakingHeartState maps to the vanilla enum of the same name
public enum CreakingHeartState
{
    uprooted,
    dormant,
    awake,
}

//Direction maps to the vanilla enum of the same name
public enum Direction
{
    down,
    up,
    north,
    south,
    west,
    east,
}

//DoorHingeSide maps to the vanilla enum of the same name
public enum DoorHingeSide
{
    left,
    right,
}

//DoubleBlockHalf maps to the vanilla enum of the same name
public enum DoubleBlockHalf
{
    upper,
    lower,
}

//FrontAndTop maps to the vanilla enum of the same name
public enum FrontAndTop
{
    down_east,
    down_north,
    down_south,
    down_west,
    up_east,
    up_north,
    up_south,
    up_west,
    west_up,
    east_up,
    north_up,
    south_up,
}

//Half maps to the vanilla enum of the same name
public enum Half
{
    top,
    bottom,
}

//NoteBlockInstrument maps to the vanilla enum of the same name
public enum NoteBlockInstrument
{
    harp,
    basedrum,
    snare,
    hat,
    bass,
    flute,
    bell,
    guitar,
    chime,
    xylophone,
    iron_xylophone,
    cow_bell,
    didgeridoo,
    bit,
    banjo,
    pling,
    trumpet,
    trumpet_exposed,
    trumpet_oxidized,
    trumpet_weathered,
    zombie,
    skeleton,
    creeper,
    dragon,
    wither_skeleton,
    piglin,
    custom_head,
}

//PistonType maps to the vanilla enum of the same name
public enum PistonType
{
    normal,
    sticky,
}

//Pose maps to the vanilla enum of the same name
public enum Pose
{
    standing,
    sitting,
    running,
    star,
}

//PotentSulfurState maps to the vanilla enum of the same name
public enum PotentSulfurState
{
    dry,
    wet,
    dormant,
    erupting,
    continuous,
}

//RailShape maps to the vanilla enum of the same name
public enum RailShape
{
    north_south,
    east_west,
    ascending_east,
    ascending_west,
    ascending_north,
    ascending_south,
    south_east,
    south_west,
    north_west,
    north_east,
}

//RedstoneSide maps to the vanilla enum of the same name
public enum RedstoneSide
{
    up,
    side,
    none,
}

//SculkSensorPhase maps to the vanilla enum of the same name
public enum SculkSensorPhase
{
    inactive,
    active,
    cooldown,
}

//SideChainPart maps to the vanilla enum of the same name
public enum SideChainPart
{
    unconnected,
    right,
    center,
    left,
}

//SlabType maps to the vanilla enum of the same name
public enum SlabType
{
    top,
    bottom,
    @double,
}

//SpeleothemThickness maps to the vanilla enum of the same name
public enum SpeleothemThickness
{
    tip_merge,
    tip,
    frustum,
    middle,
    @base,
}

//StairsShape maps to the vanilla enum of the same name
public enum StairsShape
{
    straight,
    inner_left,
    inner_right,
    outer_left,
    outer_right,
}

//StructureMode maps to the vanilla enum of the same name
public enum StructureMode
{
    save,
    load,
    corner,
    data,
}

//TestBlockMode maps to the vanilla enum of the same name
public enum TestBlockMode
{
    start,
    log,
    fail,
    accept,
}

//Tilt maps to the vanilla enum of the same name
public enum Tilt
{
    none,
    unstable,
    partial,
    full,
}

//TrialSpawnerState maps to the vanilla enum of the same name
public enum TrialSpawnerState
{
    inactive,
    waiting_for_players,
    active,
    waiting_for_reward_ejection,
    ejecting_reward,
    cooldown,
}

//VaultState maps to the vanilla enum of the same name
public enum VaultState
{
    inactive,
    active,
    unlocking,
    ejecting,
}

//WallSide maps to the vanilla enum of the same name
public enum WallSide
{
    none,
    low,
    tall,
}
