namespace NetCraft.Registry.Enums;

//BlockEnums 从原版方块属性枚举生成 成员名即原版序列化名 顺序即原版声明顺序
//顺序直接决定 BlockState 全局 id 的分配 不能随意调整
//单独放一个命名空间 免得 Direction/Axis 这类常见名与既有类型撞车
//本文件由 __gen_blocks.py 整份重写 手写的派生成员放 BlockEnumsExtensions.cs
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

    //Resolve 按原版枚举名取生成的枚举类型
    public static Type? Resolve(string name) => ByName.GetValueOrDefault(name);
}

//AttachFace 对应原版同名枚举
public enum AttachFace
{
    floor,
    wall,
    ceiling,
}

//Axis 对应原版同名枚举
public enum Axis
{
    x,
    y,
    z,
}

//BambooLeaves 对应原版同名枚举
public enum BambooLeaves
{
    none,
    small,
    large,
}

//BedPart 对应原版同名枚举
public enum BedPart
{
    head,
    foot,
}

//BellAttachType 对应原版同名枚举
public enum BellAttachType
{
    floor,
    ceiling,
    single_wall,
    double_wall,
}

//ChestType 对应原版同名枚举
public enum ChestType
{
    single,
    left,
    right,
}

//ComparatorMode 对应原版同名枚举
public enum ComparatorMode
{
    compare,
    subtract,
}

//CreakingHeartState 对应原版同名枚举
public enum CreakingHeartState
{
    uprooted,
    dormant,
    awake,
}

//Direction 对应原版同名枚举
public enum Direction
{
    down,
    up,
    north,
    south,
    west,
    east,
}

//DoorHingeSide 对应原版同名枚举
public enum DoorHingeSide
{
    left,
    right,
}

//DoubleBlockHalf 对应原版同名枚举
public enum DoubleBlockHalf
{
    upper,
    lower,
}

//FrontAndTop 对应原版同名枚举
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

//Half 对应原版同名枚举
public enum Half
{
    top,
    bottom,
}

//NoteBlockInstrument 对应原版同名枚举
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

//PistonType 对应原版同名枚举
public enum PistonType
{
    normal,
    sticky,
}

//Pose 对应原版同名枚举
public enum Pose
{
    standing,
    sitting,
    running,
    star,
}

//PotentSulfurState 对应原版同名枚举
public enum PotentSulfurState
{
    dry,
    wet,
    dormant,
    erupting,
    continuous,
}

//RailShape 对应原版同名枚举
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

//RedstoneSide 对应原版同名枚举
public enum RedstoneSide
{
    up,
    side,
    none,
}

//SculkSensorPhase 对应原版同名枚举
public enum SculkSensorPhase
{
    inactive,
    active,
    cooldown,
}

//SideChainPart 对应原版同名枚举
public enum SideChainPart
{
    unconnected,
    right,
    center,
    left,
}

//SlabType 对应原版同名枚举
public enum SlabType
{
    top,
    bottom,
    @double,
}

//SpeleothemThickness 对应原版同名枚举
public enum SpeleothemThickness
{
    tip_merge,
    tip,
    frustum,
    middle,
    @base,
}

//StairsShape 对应原版同名枚举
public enum StairsShape
{
    straight,
    inner_left,
    inner_right,
    outer_left,
    outer_right,
}

//StructureMode 对应原版同名枚举
public enum StructureMode
{
    save,
    load,
    corner,
    data,
}

//TestBlockMode 对应原版同名枚举
public enum TestBlockMode
{
    start,
    log,
    fail,
    accept,
}

//Tilt 对应原版同名枚举
public enum Tilt
{
    none,
    unstable,
    partial,
    full,
}

//TrialSpawnerState 对应原版同名枚举
public enum TrialSpawnerState
{
    inactive,
    waiting_for_players,
    active,
    waiting_for_reward_ejection,
    ejecting_reward,
    cooldown,
}

//VaultState 对应原版同名枚举
public enum VaultState
{
    inactive,
    active,
    unlocking,
    ejecting,
}

//WallSide 对应原版同名枚举
public enum WallSide
{
    none,
    low,
    tall,
}
