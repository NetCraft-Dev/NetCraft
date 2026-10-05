using NetCraft.Codec;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.Codec;
using NetCraft.Storage;
using NetCraft.Util;

namespace NetCraft.Game.Advancements.Predicates;

//LocationPredicate 位置谓词 判定某坐标的群系 维度 烟柱 光照 方块与天空可见性
//对应原版 net.minecraft.advancements.predicates.LocationPredicate
//原版还有 structures 与 fluid 两个字段 前者依赖跨层的 StructureManager 后者依赖未实现的 FluidPredicate
//本作先落其余七个字段 待那两个体系接通再补
public sealed record LocationPredicate(
    Optional<PositionPredicate> Position,
    Optional<HolderSet<Biome>> Biomes,
    Optional<Identifier> Dimension,
    Optional<bool> Smokey,
    Optional<LightPredicate> Light,
    Optional<BlockPredicate> Block,
    Optional<bool> CanSeeSky)
{
    //Codec 持久化编解码 字段名 position biomes dimension smokey light block can_see_sky 对应原版 CODEC
    public static readonly Codec<LocationPredicate> Codec = RecordCodecBuilder.Of7(
        PositionPredicate.Codec.OptionalFieldOf("position")
            .ForGetter((LocationPredicate predicate) => predicate.Position),
        HolderSetCodecs.BiomeSet.OptionalFieldOf("biomes")
            .ForGetter((LocationPredicate predicate) => predicate.Biomes),
        IdentifierCodec.Instance.OptionalFieldOf("dimension")
            .ForGetter((LocationPredicate predicate) => predicate.Dimension),
        Codecs.Bool.OptionalFieldOf("smokey")
            .ForGetter((LocationPredicate predicate) => predicate.Smokey),
        LightPredicate.Codec.OptionalFieldOf("light")
            .ForGetter((LocationPredicate predicate) => predicate.Light),
        BlockPredicate.Codec.OptionalFieldOf("block")
            .ForGetter((LocationPredicate predicate) => predicate.Block),
        Codecs.Bool.OptionalFieldOf("can_see_sky")
            .ForGetter((LocationPredicate predicate) => predicate.CanSeeSky),
        (position, biomes, dimension, smokey, light, block, canSeeSky) =>
            new LocationPredicate(position, biomes, dimension, smokey, light, block, canSeeSky));

    //Matches 坐标逐项比对 需要读世界的那几项统一先看区块在不在内存 对应原版 matches
    public bool Matches(ILevelReader level, double x, double y, double z)
    {
        if (Position.IsPresent && !Position.Get().Matches(x, y, z)) return false;
        if (Dimension.IsPresent && Dimension.Get() != level.Dimension) return false;
        var pos = new BlockPos(Mth.Floor(x), Mth.Floor(y), Mth.Floor(z));
        var loaded = level.IsLoaded(pos);
        if (Biomes.IsPresent && (!loaded || level.GetBiome(pos) is not { } biome || !Biomes.Get().Contains(biome)))
            return false;
        if (Smokey.IsPresent
            && (!loaded || Smokey.Get() != NetCraft.Game.World.Level.Block.Blocks.CampfireBlock.IsSmokeyPos(level, pos)))
            return false;
        if (Light.IsPresent && !Light.Get().Matches(level, pos)) return false;
        if (Block.IsPresent && !Block.Get().Matches(level, pos)) return false;
        if (CanSeeSky.IsPresent && CanSeeSky.Get() != level.CanSeeSky(pos)) return false;
        return true;
    }

    //PositionPredicate 坐标区间谓词 定义见文件末尾 主构造参数处直接引用顶层类型
    //Builder 位置谓词构造器 对应原版 Builder
    public sealed class Builder
    {
        private MinMaxBounds.Doubles _x = MinMaxBounds.Doubles.Any;
        private MinMaxBounds.Doubles _y = MinMaxBounds.Doubles.Any;
        private MinMaxBounds.Doubles _z = MinMaxBounds.Doubles.Any;
        private Optional<HolderSet<Biome>> _biomes = Optional<HolderSet<Biome>>.Empty();
        private Optional<Identifier> _dimension = Optional<Identifier>.Empty();
        private Optional<bool> _smokey = Optional<bool>.Empty();
        private Optional<LightPredicate> _light = Optional<LightPredicate>.Empty();
        private Optional<BlockPredicate> _block = Optional<BlockPredicate>.Empty();
        private Optional<bool> _canSeeSky = Optional<bool>.Empty();

        //Location 空构造器 对应原版 location
        public static Builder Location() => new();

        //AtYLocation 只约束纵向坐标 对应原版 atYLocation
        public static Builder AtYLocation(MinMaxBounds.Doubles yLocation) => Location().SetY(yLocation);

        //InBiome 只约束群系 对应原版 inBiome
        public static Builder InBiome(Holder<Biome> biome)
            => Location().SetBiomes(new DirectHolderSet<Biome>(new[] { biome }));

        //InDimension 只约束维度 对应原版 inDimension
        public static Builder InDimension(Identifier dimension) => Location().SetDimension(dimension);

        public Builder SetX(MinMaxBounds.Doubles x) { _x = x; return this; }

        public Builder SetY(MinMaxBounds.Doubles y) { _y = y; return this; }

        public Builder SetZ(MinMaxBounds.Doubles z) { _z = z; return this; }

        public Builder SetBiomes(HolderSet<Biome> biomes) { _biomes = Optional<HolderSet<Biome>>.Of(biomes); return this; }

        public Builder SetDimension(Identifier dimension) { _dimension = Optional<Identifier>.Of(dimension); return this; }

        public Builder SetSmokey(bool smokey) { _smokey = Optional<bool>.Of(smokey); return this; }

        public Builder SetLight(LightPredicate light) { _light = Optional<LightPredicate>.Of(light); return this; }

        public Builder SetBlock(BlockPredicate block) { _block = Optional<BlockPredicate>.Of(block); return this; }

        public Builder SetCanSeeSky(bool canSeeSky) { _canSeeSky = Optional<bool>.Of(canSeeSky); return this; }

        //Build 三轴都是任意区间时不带位置约束 对应原版 build
        public LocationPredicate Build()
            => new(PositionPredicate.Of(_x, _y, _z), _biomes, _dimension, _smokey, _light, _block, _canSeeSky);
    }
}

//PositionPredicate 坐标区间谓词 三轴都命中才算通过 对应原版 LocationPredicate 内嵌的位置谓词
public sealed record PositionPredicate(
    MinMaxBounds.Doubles X,
    MinMaxBounds.Doubles Y,
    MinMaxBounds.Doubles Z)
{
    //Codec 持久化编解码 字段名 x y z 对应原版 CODEC
    public static readonly Codec<PositionPredicate> Codec = RecordCodecBuilder.Of3(
        MinMaxBounds.Doubles.CODEC.OptionalFieldOf("x", MinMaxBounds.Doubles.Any)
            .ForGetter((PositionPredicate predicate) => predicate.X),
        MinMaxBounds.Doubles.CODEC.OptionalFieldOf("y", MinMaxBounds.Doubles.Any)
            .ForGetter((PositionPredicate predicate) => predicate.Y),
        MinMaxBounds.Doubles.CODEC.OptionalFieldOf("z", MinMaxBounds.Doubles.Any)
            .ForGetter((PositionPredicate predicate) => predicate.Z),
        (x, y, z) => new PositionPredicate(x, y, z));

    //Of 三轴都是任意区间时不加位置约束 对应原版 of
    public static Optional<PositionPredicate> Of(MinMaxBounds.Doubles x, MinMaxBounds.Doubles y,
        MinMaxBounds.Doubles z)
        => x.IsAny && y.IsAny && z.IsAny
            ? Optional<PositionPredicate>.Empty()
            : Optional<PositionPredicate>.Of(new PositionPredicate(x, y, z));

    //Matches 三轴区间逐项判定 对应原版 matches
    public bool Matches(double x, double y, double z) => X.Matches(x) && Y.Matches(y) && Z.Matches(z);
}
