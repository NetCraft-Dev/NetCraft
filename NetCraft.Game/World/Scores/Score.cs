using NetCraft.Codec;
using NetCraft.Network.Chat;

namespace NetCraft.Game.World.Scores;

//Score a single score, maps to vanilla net.minecraft.world.scores.Score
//Holds the score value, a locked flag and an optional custom display text; new scores are locked by default
public sealed class Score : ReadOnlyScoreInfo
{
    private int _value;
    private bool _locked = true;
    private Component? _display;

    //Value reads the score value, maps to vanilla value
    public int Value() => _value;

    //SetValue writes the score value, maps to vanilla value(int)
    public void SetValue(int value) => _value = value;

    //IsLocked whether the score is locked, maps to vanilla isLocked
    public bool IsLocked() => _locked;

    //SetLocked sets the locked flag, maps to vanilla setLocked
    public void SetLocked(bool locked) => _locked = locked;

    //Display reads the custom display text, maps to vanilla display
    public Component? Display() => _display;

    //SetDisplay writes the custom display text, maps to vanilla display(Component)
    public void SetDisplay(Component? display) => _display = display;

    //Packed save form of the score, maps to vanilla Score.Packed
    //The vanilla number format fields depend on NumberFormat, not wired up, so they are omitted
    public sealed record Packed(int Value, bool Locked, Optional<Component> Display)
    {
        //Codec persistence codec, field names Score/Locked/display, maps to vanilla MAP_CODEC
        public static readonly Codec<Packed> Codec = RecordCodecBuilder.Of3(
            Codecs.Int.OptionalFieldOf("Score", 0).ForGetter((Packed packed) => packed.Value),
            Codecs.Bool.OptionalFieldOf("Locked", false).ForGetter((Packed packed) => packed.Locked),
            ComponentSerialization.Codec.OptionalFieldOf("display").ForGetter((Packed packed) => packed.Display),
            (value, locked, display) => new Packed(value, locked, display));
    }
}
