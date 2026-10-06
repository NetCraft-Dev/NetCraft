using NetCraft.Codec;

namespace NetCraft.Game.World.Level.LevelGen.Synth;

//NoiseParameters noise parameters, maps to vanilla NormalNoise.NoiseParameters
//Holds firstOctave and amplitudes describing the NormalNoise octave configuration
//Moved down from the Game layer to the Registry layer so Registries.NOISE can reference it and avoid a circular dependency
//Namespace stays under the Synth subspace so NormalNoise and test references are unchanged
public sealed class NoiseParameters
{
    //Codec for noise parameter JSON, maps to vanilla NoiseParameters.CODEC
    //Fields are firstOctave(int) + amplitudes(list of double), matching data/minecraft/worldgen/noise/*.json
    public static readonly Codec<NoiseParameters> Codec = RecordCodecBuilder.Of2(
        Codecs.Int.FieldOf("firstOctave").ForGetter<NoiseParameters, int>(p => p.FirstOctave),
        Codecs.Double.ListOf().FieldOf("amplitudes").ForGetter<NoiseParameters, IReadOnlyList<double>>(p => p.Amplitudes),
        (firstOctave, amplitudes) => new NoiseParameters(firstOctave, amplitudes));

    public int FirstOctave { get; }
    public IReadOnlyList<double> Amplitudes { get; }

    public NoiseParameters(int firstOctave, IReadOnlyList<double> amplitudes)
    {
        FirstOctave = firstOctave;
        Amplitudes = amplitudes;
    }

    public NoiseParameters(int firstOctave, params double[] amplitudes)
        : this(firstOctave, (IReadOnlyList<double>)amplitudes) { }
}
