using NetCraft.Util.Random;

namespace NetCraft.Game.World.Level.LevelGen.Synth;

//NormalNoise 正态分布噪声对应原版 net.minecraft.world.level.levelgen.synth.NormalNoise
//两个 PerlinNoise 实例叠加形成正态分布噪声
//INPUT_FACTOR 防止两噪声相关性valueFactor 缩放到目标标准差
public sealed class NormalNoise
{
    private const double InputFactor = 1.0181268882175227;

    private readonly double _valueFactor;
    private readonly PerlinNoise _first;
    private readonly PerlinNoise _second;
    private readonly double _maxValue;
    private readonly NoiseParameters _parameters;

    //Create 新版工厂对应原版 create走 forkPositional 派生
    public static NormalNoise Create(RandomSource random, int firstOctave, params double[] amplitudes)
        => Create(random, new NoiseParameters(firstOctave, amplitudes));

    //Create 新版工厂接收 NoiseParameters 对应原版 create(random, parameters)
    public static NormalNoise Create(RandomSource random, NoiseParameters parameters)
        => new(random, parameters, true);

    //CreateLegacyNetherBiome 旧版下界生物群系工厂对应原版 createLegacyNetherBiome
    //走 PerlinNoise.CreateLegacyForLegacyNetherBiome 路径保持旧版确定性
    public static NormalNoise CreateLegacyNetherBiome(RandomSource random, NoiseParameters parameters)
        => new(random, parameters, false);

    //私有构造接收 useNewInitialization 对应原版私有构造
    //true 时 PerlinNoise 走新版 forkPositional 派生false 时走 legacy Fork 派生
    private NormalNoise(RandomSource random, NoiseParameters parameters, bool useNewInitialization)
    {
        _parameters = parameters;
        var firstOctave = parameters.FirstOctave;
        var amplitudes = parameters.Amplitudes;
        if (useNewInitialization)
        {
            _first = PerlinNoise.Create(random, firstOctave, amplitudes);
            _second = PerlinNoise.Create(random, firstOctave, amplitudes);
        }
        else
        {
            _first = PerlinNoise.CreateLegacyForLegacyNetherBiome(random, firstOctave, amplitudes);
            _second = PerlinNoise.CreateLegacyForLegacyNetherBiome(random, firstOctave, amplitudes);
        }

        var minOctave = int.MaxValue;
        var maxOctave = int.MinValue;
        for (var i = 0; i < amplitudes.Count; i++)
        {
            if (amplitudes[i] != 0.0)
            {
                if (i < minOctave) minOctave = i;
                if (i > maxOctave) maxOctave = i;
            }
        }
        var expectedDev = ExpectedDeviation(maxOctave - minOctave);
        _valueFactor = 0.16666666666666666 / expectedDev;
        _maxValue = (_first.MaxValue + _second.MaxValue) * _valueFactor;
    }

    //兼容旧测试与简单调用走新版路径
    public NormalNoise(RandomSource random, int firstOctave, params double[] amplitudes)
        : this(random, new NoiseParameters(firstOctave, amplitudes), true) { }

    public double GetValue(double x, double y, double z)
        => (_first.GetValue(x, y, z) + _second.GetValue(x * InputFactor, y * InputFactor, z * InputFactor)) * _valueFactor;

    public double MaxValue => _maxValue;

    public NoiseParameters Parameters => _parameters;

    private static double ExpectedDeviation(int octaveSpan)
        => 0.1 * (1.0 + 1.0 / (octaveSpan + 1));
}
