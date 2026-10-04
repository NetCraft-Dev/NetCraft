using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen;

//MultiNoiseBiomeSource 多噪声生物群系源对应原版 net.minecraft.world.level.biome.MultiNoiseBiomeSource
//阶段 E 接入 Climate.Sampler + MultiNoiseBiomeSourceParameterList 真实派生
//GetBiome 按坐标采样 6 维度参数查找参数空间距离最近的 Biome
public sealed class MultiNoiseBiomeSource : BiomeSource
{
    public MultiNoiseBiomeSourceParameterList? ParameterList { get; }
    public Climate.Sampler? Sampler { get; }

    public MultiNoiseBiomeSource(MultiNoiseBiomeSourceParameterList parameterList, Climate.Sampler sampler)
    {
        ParameterList = parameterList;
        Sampler = sampler;
    }

    //单参数构造留给数据驱动装配 采样器由 NoiseBasedChunkGenerator 构造时按噪声路由注入
    public MultiNoiseBiomeSource(MultiNoiseBiomeSourceParameterList parameterList)
    {
        ParameterList = parameterList;
    }

    //LegacyConstructor 无参数列表与采样器占位返回平原群系
    //用于 Bootstrap 之前或测试场景
    public MultiNoiseBiomeSource() { }

    //PossibleBiomes 参数表里出现过的群系去重 无参数表时只有占位平原
    //未绑定的表项跳过 它们指向还没装载的群系
    public IReadOnlyList<Biome> PossibleBiomes
    {
        get
        {
            if (ParameterList is null) return new[] { Biome.Plains };
            var seen = new HashSet<Biome>(ReferenceEqualityComparer.Instance);
            var result = new List<Biome>();
            foreach (var (_, holder) in ParameterList.Entries)
            {
                if (!holder.IsBound()) continue;
                if (seen.Add(holder.Value)) result.Add(holder.Value);
            }
            return result.Count == 0 ? new[] { Biome.Plains } : result;
        }
    }

    //GetBiome 按坐标采样 6 维度参数查找参数空间距离最近的 Biome
    //无 ParameterList/Sampler 时占位返回 Biome.Plains 单例避免 palette 爆炸
    public Biome GetBiome(int x, int y, int z)
    {
        if (ParameterList is null || Sampler is null)
            return Biome.Plains;

        var target = new Climate.ParameterPoint(
            Sampler.Temperature(x, y, z),
            Sampler.Humidity(x, y, z),
            Sampler.Continentalness(x, y, z),
            Sampler.Erosion(x, y, z),
            Sampler.Depth(x, y, z),
            Sampler.Weirdness(x, y, z),
            0L);
        return ParameterList.FindClosest(target).Value;
    }
}
