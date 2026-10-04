using NetCraft.Codec;

namespace NetCraft.Game.World.Level.LevelGen.Synth;

//NoiseParameters 噪声参数对应原版 NormalNoise.NoiseParameters
//持有 firstOctave 与 amplitudes 描述 NormalNoise 倍频配置
//从 Game 层下移到 Registry 层供 Registries.NOISE 引用避免循环依赖
//命名空间保持 Synth 子空间使 NormalNoise 与测试引用零改动
public sealed class NoiseParameters
{
    //Codec 噪声参数 JSON 编解码对应原版 NoiseParameters.CODEC
    //字段 firstOctave(int) + amplitudes(double 列表) 与 data/minecraft/worldgen/noise/*.json 一致
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
