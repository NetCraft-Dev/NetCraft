using NetCraft.Codec;
using NetCraft.Game.Server;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.Timers;

//TimerCallbacks codec registry for scheduled callbacks, maps to vanilla net.minecraft.world.level.timers.TimerCallbacks
//LateBoundIdMapper replaced with a dictionary; the dispatch codec reads the type field and looks it up
public class TimerCallbacks<C>
{
    //ServerCallbacks server callback table, registers function and function_tag
    public static readonly TimerCallbacks<MinecraftServer> ServerCallbacks = new TimerCallbacks<MinecraftServer>()
        .Register(Identifier.WithDefaultNamespace("function"), FunctionCallback.Codec)
        .Register(Identifier.WithDefaultNamespace("function_tag"), FunctionTagCallback.Codec);

    private readonly Dictionary<Identifier, MapCodec<TimerCallback<C>>> _codecs = new();

    //Register registers the codec for one callback kind
    public TimerCallbacks<C> Register(Identifier id, MapCodec<TimerCallback<C>> codec)
    {
        _codecs[id] = codec;
        return this;
    }

    //Codec dispatches by the type field, maps to vanilla LateBoundIdMapper.codec().dispatch
    public Codec<TimerCallback<C>> Codec() => new DispatchCodec(this);

    //DispatchCodec table-based dispatch; encoding maps the GetCodec instance back to its registered name
    private sealed class DispatchCodec(TimerCallbacks<C> owner) : ScalarCodec<TimerCallback<C>>
    {
        public override DataResult<TimerCallback<C>> Parse<U>(DynamicOps<U> ops, U input)
            => ops.GetMap(input).FlatMap(map => DecodeCallback(ops, map));

        private DataResult<TimerCallback<C>> DecodeCallback<U>(DynamicOps<U> ops, MapLike<U> input)
        {
            var typeTag = input.Get("type");
            if (!typeTag.IsPresent) return DataResult<TimerCallback<C>>.Error(() => "Timer callback is missing the type field");
            var typeText = ops.GetStringValue(typeTag.Get());
            if (!typeText.Result().IsPresent)
                return DataResult<TimerCallback<C>>.Error(() => "Timer callback type must be a string");
            var typeId = Identifier.TryParse(typeText.GetOrThrow());
            if (typeId is null)
                return DataResult<TimerCallback<C>>.Error(() => $"Invalid timer callback type: {typeText.GetOrThrow()}");
            if (!owner._codecs.TryGetValue(typeId.Value, out var codec))
                return DataResult<TimerCallback<C>>.Error(() => $"Unknown timer callback type: {typeId}");
            return codec.Decode(ops, input);
        }

        public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, TimerCallback<C> value)
        {
            foreach (var (id, codec) in owner._codecs)
            {
                if (!ReferenceEquals(codec, value.GetCodec())) continue;
                var builder = ops.MapBuilder();
                builder.Add("type", ops.CreateString(id.ToString()));
                return value.GetCodec().EncodeTo(ops, value, builder).Build(ops.Empty());
            }
            return DataResult<U>.Error(() => $"Timer callback codec is not registered: {value.GetCodec()}");
        }
    }
}
