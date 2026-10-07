using NetCraft.Codec;
using NetCraft.DataFixer.Schemas;

namespace NetCraft.Game.DFU.Fixes;

using NetCraft.DataFixer.Fixes;

using NetCraft.DataFixer;

//Jukebox playback timing field fix, maps to vanilla JukeboxTicksSinceSongStartedFix
//1.21 removes IsPlaying/TickCount/RecordStartTick and uses ticks_since_song_started to record elapsed ticks
public class JukeboxTicksSinceSongStartedFix : NamedEntityFix
{
    public JukeboxTicksSinceSongStartedFix(Schema outputSchema)
        : base(outputSchema, false, "JukeboxTicksSinceSongStartedFix", References.BlockEntity, "minecraft:jukebox") { }

    public Dynamic<object> FixTag(Dynamic<object> input)
    {
        long ticksSinceSongStarted = input.Get("TickCount").AsLong(0L) - input.Get("RecordStartTick").AsLong(0L);
        var result = input.Remove("IsPlaying").Remove("TickCount").Remove("RecordStartTick");
        if (ticksSinceSongStarted > 0)
        {
            return result.Set(FixConstants.JukeboxBlockEntityTicksSinceSongStarted, input.CreateLong(ticksSinceSongStarted));
        }
        return result;
    }

    protected override Typed<object> Fix(Typed<object> entity)
        => entity.Update(DSL.RemainderFinder(), FixTag);
}
