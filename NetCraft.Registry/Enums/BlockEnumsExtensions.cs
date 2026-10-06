namespace NetCraft.Registry.Enums;

//This file holds hand-written derived members for the enums, kept separate from the generated BlockEnums.cs from __gen_blocks.py
//That script rewrites BlockEnums.cs entirely, so anything written there is lost on every regeneration
//The blocks below correspond to vanilla enum instance methods, moved over item by item from the 26.2 decompiled source

//PushReaction maps to the vanilla enum of the same name, the reaction when pushed by a piston
public enum PushReaction
{
    normal,
    destroy,
    block,
    ignore,
    push_only,
}

//NoteBlockInstrumentExtensions derived predicates and sounds for note block instruments, maps to vanilla NoteBlockInstrument instance methods
//Vanilla splits them into three types by Type: base instruments tune with note, mob heads sit above the note block, and custom heads read the sound from the skull block entity above
public static class NoteBlockInstrumentExtensions
{
    //IsHead the mob-head and custom-head kinds whose timbre does not change with note
    private static bool IsHead(this NoteBlockInstrument instrument)
        => instrument is NoteBlockInstrument.zombie or NoteBlockInstrument.skeleton
            or NoteBlockInstrument.creeper or NoteBlockInstrument.dragon
            or NoteBlockInstrument.wither_skeleton or NoteBlockInstrument.piglin
            or NoteBlockInstrument.custom_head;

    //IsTunable whether the pitch tunes with note; only the base kind does
    public static bool IsTunable(this NoteBlockInstrument instrument) => !instrument.IsHead();

    //WorksAboveNoteBlock whether it is an instrument placed above the note block; true for mob heads and custom heads, false for the base kind
    public static bool WorksAboveNoteBlock(this NoteBlockInstrument instrument) => instrument.IsHead();

    //HasCustomSound custom head; its sound reads the skull block entity above, which this reimplementation does not have
    public static bool HasCustomSound(this NoteBlockInstrument instrument)
        => instrument == NoteBlockInstrument.custom_head;

    //GetSoundEvent the instrument's sound, following vanilla NoteBlockInstrument constructor arguments one by one
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

//RailShapeExtensions derived predicates for rail shapes, maps to vanilla RailShape instance methods
public static class RailShapeExtensions
{
    //IsSlope whether it is a slope; anything other than the four ascending shapes is not
    public static bool IsSlope(this RailShape shape)
        => shape is RailShape.ascending_north or RailShape.ascending_east
            or RailShape.ascending_south or RailShape.ascending_west;
}
