using NetCraft.Codec;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.Codec;
using NetCraft.Storage;
using NetCraft.Util;

namespace NetCraft.Game.Advancements.Predicates;

//LocationPredicate location predicate, checks a coordinate's biome, dimension, smokey, light, block and sky visibility
//maps to vanilla net.minecraft.advancements.predicates.LocationPredicate
//Vanilla also has the structures and fluid fields; the former depends on the cross-layer StructureManager and the latter on the unimplemented FluidPredicate
//This project implements the other seven fields first and will add those two once the systems are wired up
public sealed record LocationPredicate(
    Optional<PositionPredicate> Position,
    Optional<HolderSet<Biome>> Biomes,
    Optional<Identifier> Dimension,
    Optional<bool> Smokey,
    Optional<LightPredicate> Light,
    Optional<BlockPredicate> Block,
    Optional<bool> CanSeeSky)
{
    //Codec persistence codec, field names position biomes dimension smokey light block can_see_sky, maps to vanilla CODEC
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

    //Matches coordinates checked item by item; items needing the world first check whether the chunk is in memory, maps to vanilla matches
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

    //PositionPredicate coordinate range predicate, defined at the end of the file; the primary constructor references the top-level type directly
    //Builder location predicate builder, maps to vanilla Builder
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

        //Location empty constructor, maps to vanilla location
        public static Builder Location() => new();

        //AtYLocation constrains only the vertical coordinate, maps to vanilla atYLocation
        public static Builder AtYLocation(MinMaxBounds.Doubles yLocation) => Location().SetY(yLocation);

        //InBiome constrains only the biome, maps to vanilla inBiome
        public static Builder InBiome(Holder<Biome> biome)
            => Location().SetBiomes(new DirectHolderSet<Biome>(new[] { biome }));

        //InDimension constrains only the dimension, maps to vanilla inDimension
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

        //Build omits the position constraint when all three axes are any-range, maps to vanilla build
        public LocationPredicate Build()
            => new(PositionPredicate.Of(_x, _y, _z), _biomes, _dimension, _smokey, _light, _block, _canSeeSky);
    }
}

//PositionPredicate coordinate range predicate, passes only when all three axes hit, maps to vanilla the position predicate nested in LocationPredicate
public sealed record PositionPredicate(
    MinMaxBounds.Doubles X,
    MinMaxBounds.Doubles Y,
    MinMaxBounds.Doubles Z)
{
    //Codec persistence codec, field names x/y/z, maps to vanilla CODEC
    public static readonly Codec<PositionPredicate> Codec = RecordCodecBuilder.Of3(
        MinMaxBounds.Doubles.CODEC.OptionalFieldOf("x", MinMaxBounds.Doubles.Any)
            .ForGetter((PositionPredicate predicate) => predicate.X),
        MinMaxBounds.Doubles.CODEC.OptionalFieldOf("y", MinMaxBounds.Doubles.Any)
            .ForGetter((PositionPredicate predicate) => predicate.Y),
        MinMaxBounds.Doubles.CODEC.OptionalFieldOf("z", MinMaxBounds.Doubles.Any)
            .ForGetter((PositionPredicate predicate) => predicate.Z),
        (x, y, z) => new PositionPredicate(x, y, z));

    //Of adds no position constraint when all three axes are any-range, maps to vanilla of
    public static Optional<PositionPredicate> Of(MinMaxBounds.Doubles x, MinMaxBounds.Doubles y,
        MinMaxBounds.Doubles z)
        => x.IsAny && y.IsAny && z.IsAny
            ? Optional<PositionPredicate>.Empty()
            : Optional<PositionPredicate>.Of(new PositionPredicate(x, y, z));

    //Matches three-axis ranges checked item by item, maps to vanilla matches
    public bool Matches(double x, double y, double z) => X.Matches(x) && Y.Matches(y) && Z.Matches(z);
}
