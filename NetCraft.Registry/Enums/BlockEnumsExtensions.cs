namespace NetCraft.Registry.Enums;

//本文件放枚举的手写派生成员 与 __gen_blocks.py 生成的 BlockEnums.cs 分开放
//那个脚本会整份重写 BlockEnums.cs 写在那里的东西每次重新生成都会丢
//下面几块对应原版枚举的实例方法 语义照 26.2 反编译源码逐条搬

//PushReaction 对应原版同名枚举 被活塞推动时的反应
public enum PushReaction
{
    normal,
    destroy,
    block,
    ignore,
    push_only,
}

//NoteBlockInstrumentExtensions 音符盒乐器的派生判定与音效 对应原版 NoteBlockInstrument 的实例方法
//原版按 Type 分三类 底座类随 note 调音 生物头压在音符盒上方 自定义头要读上方头颅方块实体的音效
public static class NoteBlockInstrumentExtensions
{
    //IsHead 生物头与自定义头这两类 音色不随 note 变
    private static bool IsHead(this NoteBlockInstrument instrument)
        => instrument is NoteBlockInstrument.zombie or NoteBlockInstrument.skeleton
            or NoteBlockInstrument.creeper or NoteBlockInstrument.dragon
            or NoteBlockInstrument.wither_skeleton or NoteBlockInstrument.piglin
            or NoteBlockInstrument.custom_head;

    //IsTunable 是否随 note 调音高 只有底座那一类会
    public static bool IsTunable(this NoteBlockInstrument instrument) => !instrument.IsHead();

    //WorksAboveNoteBlock 是否属于压在音符盒上方的乐器 生物头与自定义头为真 底座类为假
    public static bool WorksAboveNoteBlock(this NoteBlockInstrument instrument) => instrument.IsHead();

    //HasCustomSound 自定义头 音效要读上方头颅方块实体 本作没有这类方块实体
    public static bool HasCustomSound(this NoteBlockInstrument instrument)
        => instrument == NoteBlockInstrument.custom_head;

    //GetSoundEvent 该乐器的音效 逐个照原版 NoteBlockInstrument 的构造参数
    public static SoundEvent GetSoundEvent(this NoteBlockInstrument instrument) => instrument switch
    {
        NoteBlockInstrument.harp => SoundEvents.NoteBlockHarp,
        NoteBlockInstrument.basedrum => SoundEvents.NoteBlockBasedrum,
        NoteBlockInstrument.snare => SoundEvents.NoteBlockSnare,
        NoteBlockInstrument.hat => SoundEvents.NoteBlockHat,
        NoteBlockInstrument.bass => SoundEvents.NoteBlockBass,
        NoteBlockInstrument.flute => SoundEvents.NoteBlockFlute,
        NoteBlockInstrument.bell => SoundEvents.NoteBlockBell,
        NoteBlockInstrument.guitar => SoundEvents.NoteBlockGuitar,
        NoteBlockInstrument.chime => SoundEvents.NoteBlockChime,
        NoteBlockInstrument.xylophone => SoundEvents.NoteBlockXylophone,
        NoteBlockInstrument.iron_xylophone => SoundEvents.NoteBlockIronXylophone,
        NoteBlockInstrument.cow_bell => SoundEvents.NoteBlockCowBell,
        NoteBlockInstrument.didgeridoo => SoundEvents.NoteBlockDidgeridoo,
        NoteBlockInstrument.bit => SoundEvents.NoteBlockBit,
        NoteBlockInstrument.banjo => SoundEvents.NoteBlockBanjo,
        NoteBlockInstrument.pling => SoundEvents.NoteBlockPling,
        NoteBlockInstrument.trumpet => SoundEvents.NoteBlockTrumpet,
        NoteBlockInstrument.trumpet_exposed => SoundEvents.NoteBlockTrumpetExposed,
        NoteBlockInstrument.trumpet_oxidized => SoundEvents.NoteBlockTrumpetOxidized,
        NoteBlockInstrument.trumpet_weathered => SoundEvents.NoteBlockTrumpetWeathered,
        NoteBlockInstrument.zombie => SoundEvents.NoteBlockImitateZombie,
        NoteBlockInstrument.skeleton => SoundEvents.NoteBlockImitateSkeleton,
        NoteBlockInstrument.creeper => SoundEvents.NoteBlockImitateCreeper,
        NoteBlockInstrument.dragon => SoundEvents.NoteBlockImitateEnderDragon,
        NoteBlockInstrument.wither_skeleton => SoundEvents.NoteBlockImitateWitherSkeleton,
        NoteBlockInstrument.piglin => SoundEvents.NoteBlockImitatePiglin,
        _ => SoundEvents.UiButtonClick,
    };
}

//RailShapeExtensions 轨道形状的派生判定 对应原版 RailShape 的实例方法
public static class RailShapeExtensions
{
    //IsSlope 是否斜轨 四个 ascending 之外的都不是
    public static bool IsSlope(this RailShape shape)
        => shape is RailShape.ascending_north or RailShape.ascending_east
            or RailShape.ascending_south or RailShape.ascending_west;
}
