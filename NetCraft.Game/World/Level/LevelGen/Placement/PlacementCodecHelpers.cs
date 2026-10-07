using NetCraft.Codec;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//SingleFieldPlacementCodec single-field map codec, maps to the single-field form of vanilla RecordCodecBuilder
//The project's RecordCodecBuilder starts at two fields, so single-field modifiers wrap with this
internal sealed class SingleFieldPlacementCodec<T, F> : AbstractMapCodec<T>
{
    private readonly MapCodec<F> _field;
    private readonly Func<F, T> _ctor;
    private readonly Func<T, F> _getter;

    public SingleFieldPlacementCodec(MapCodec<F> field, Func<F, T> ctor, Func<T, F> getter)
    {
        _field = field;
        _ctor = ctor;
        _getter = getter;
    }

    public override DataResult<T> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => _field.Decode(ops, input).Map(_ctor);

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, T value, RecordBuilder<U> builder)
        => _field.EncodeTo(ops, _getter(value), builder);
}

//UnitPlacementCodec parameterless modifier codec, maps to vanilla MapCodec.unit
internal sealed class UnitPlacementCodec<T> : AbstractMapCodec<T>
{
    private readonly Func<T> _factory;

    public UnitPlacementCodec(Func<T> factory) => _factory = factory;

    public override DataResult<T> Decode<U>(DynamicOps<U> ops, MapLike<U> input)
        => DataResult<T>.Success(_factory());

    public override RecordBuilder<U> EncodeTo<U>(DynamicOps<U> ops, T value, RecordBuilder<U> builder) => builder;
}
