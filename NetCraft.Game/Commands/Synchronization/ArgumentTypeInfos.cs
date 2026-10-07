using NetCraft.Commands.Arguments;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Network;
using NetCraft.Registry;

namespace NetCraft.Game.Commands.Synchronization;

//ArgumentTypeInfo command argument type network sync descriptor, maps to vanilla net.minecraft.commands.synchronization.ArgumentTypeInfo
//Writes the argument type's extra parameters (numeric range/string pattern) into the command tree packet; the client finds the same descriptor by registry id and restores it
//NC's ArgumentType<T> is distinguished only by generic and has no common base class, so object carries the argument type instance here
public abstract class ArgumentTypeInfo
{
    //Template argument type template, maps to vanilla ArgumentTypeInfo.Template
    public abstract class Template
    {
        //Info the descriptor producing the template
        public abstract ArgumentTypeInfo Info { get; }

        //Instantiate restores the argument type instance; vanilla needs CommandBuildContext, this project's argument types do not depend on context
        public abstract object Instantiate();
    }

    //ArgumentClrType the CLR type of the argument type for this descriptor, for reverse lookup by type
    public abstract Type ArgumentClrType { get; }

    //Unpack takes the template from the argument type instance; a type mismatch throws
    public abstract Template Unpack(object argumentType);

    //SerializeToNetwork writes the type's extra parameters
    public abstract void SerializeToNetwork(Template template, FriendlyByteBuf buf);

    //DeserializeFromNetwork reads the template back
    public abstract Template DeserializeFromNetwork(FriendlyByteBuf buf);
}

//ArgumentTemplate generic template implementation holding the already-constructed argument type instance
public sealed class ArgumentTemplate : ArgumentTypeInfo.Template
{
    public ArgumentTemplate(ArgumentTypeInfo info, object value)
    {
        Info = info;
        Value = value;
    }

    public override ArgumentTypeInfo Info { get; }

    //Value the argument type instance
    public object Value { get; }

    public override object Instantiate() => Value;
}

//ArgumentUtils numeric argument flag helpers, maps to vanilla ArgumentUtils
internal static class ArgumentUtils
{
    //CreateNumberFlags the low two bits indicate whether min/max are present
    public static byte CreateNumberFlags(bool hasMin, bool hasMax)
    {
        byte flags = 0;
        if (hasMin) flags |= 1;
        if (hasMax) flags |= 2;
        return flags;
    }

    public static bool NumberHasMin(byte flags) => (flags & 1) != 0;

    public static bool NumberHasMax(byte flags) => (flags & 2) != 0;
}

//BoolArgumentInfo boolean argument, no extra parameters, maps to vanilla SingletonArgumentInfo.contextFree(BoolArgumentType::bool)
public sealed class BoolArgumentInfo : ArgumentTypeInfo
{
    public static readonly BoolArgumentInfo Instance = new();

    public override Type ArgumentClrType => typeof(BoolArgumentType);

    public override Template Unpack(object argumentType)
        => argumentType is BoolArgumentType
            ? new ArgumentTemplate(this, argumentType)
            : throw new InvalidOperationException($"not a boolean argument type: {argumentType.GetType().Name}");

    public override void SerializeToNetwork(Template template, FriendlyByteBuf buf) { }

    public override Template DeserializeFromNetwork(FriendlyByteBuf buf)
        => new ArgumentTemplate(this, BoolArgumentType.Bool());
}

//IntegerArgumentInfo integer argument, writes the min/max presence flags and optional int values
public sealed class IntegerArgumentInfo : ArgumentTypeInfo
{
    public static readonly IntegerArgumentInfo Instance = new();

    public override Type ArgumentClrType => typeof(IntegerArgumentType);

    public override Template Unpack(object argumentType)
        => argumentType is IntegerArgumentType
            ? new ArgumentTemplate(this, argumentType)
            : throw new InvalidOperationException($"not an integer argument type: {argumentType.GetType().Name}");

    public override void SerializeToNetwork(Template template, FriendlyByteBuf buf)
    {
        var integer = (IntegerArgumentType)((ArgumentTemplate)template).Value;
        var hasMin = integer.Minimum != int.MinValue;
        var hasMax = integer.Maximum != int.MaxValue;
        buf.WriteByte(ArgumentUtils.CreateNumberFlags(hasMin, hasMax));
        if (hasMin) buf.WriteInt(integer.Minimum);
        if (hasMax) buf.WriteInt(integer.Maximum);
    }

    public override Template DeserializeFromNetwork(FriendlyByteBuf buf)
    {
        var flags = buf.ReadByte();
        var min = ArgumentUtils.NumberHasMin(flags) ? buf.ReadInt() : int.MinValue;
        var max = ArgumentUtils.NumberHasMax(flags) ? buf.ReadInt() : int.MaxValue;
        return new ArgumentTemplate(this, IntegerArgumentType.Integer(min, max));
    }
}

//LongArgumentInfo long argument, the value goes through VarLong
public sealed class LongArgumentInfo : ArgumentTypeInfo
{
    public static readonly LongArgumentInfo Instance = new();

    public override Type ArgumentClrType => typeof(LongArgumentType);

    public override Template Unpack(object argumentType)
        => argumentType is LongArgumentType
            ? new ArgumentTemplate(this, argumentType)
            : throw new InvalidOperationException($"not a long argument type: {argumentType.GetType().Name}");

    public override void SerializeToNetwork(Template template, FriendlyByteBuf buf)
    {
        var value = (LongArgumentType)((ArgumentTemplate)template).Value;
        var hasMin = value.Minimum != long.MinValue;
        var hasMax = value.Maximum != long.MaxValue;
        buf.WriteByte(ArgumentUtils.CreateNumberFlags(hasMin, hasMax));
        if (hasMin) buf.WriteVarLong(value.Minimum);
        if (hasMax) buf.WriteVarLong(value.Maximum);
    }

    public override Template DeserializeFromNetwork(FriendlyByteBuf buf)
    {
        var flags = buf.ReadByte();
        var min = ArgumentUtils.NumberHasMin(flags) ? buf.ReadVarLong() : long.MinValue;
        var max = ArgumentUtils.NumberHasMax(flags) ? buf.ReadVarLong() : long.MaxValue;
        return new ArgumentTemplate(this, LongArgumentType.LongArg(min, max));
    }
}

//FloatArgumentInfo single-precision argument
public sealed class FloatArgumentInfo : ArgumentTypeInfo
{
    public static readonly FloatArgumentInfo Instance = new();

    public override Type ArgumentClrType => typeof(FloatArgumentType);

    public override Template Unpack(object argumentType)
        => argumentType is FloatArgumentType
            ? new ArgumentTemplate(this, argumentType)
            : throw new InvalidOperationException($"not a float argument type: {argumentType.GetType().Name}");

    public override void SerializeToNetwork(Template template, FriendlyByteBuf buf)
    {
        var value = (FloatArgumentType)((ArgumentTemplate)template).Value;
        var hasMin = value.Minimum != -float.MaxValue;
        var hasMax = value.Maximum != float.MaxValue;
        buf.WriteByte(ArgumentUtils.CreateNumberFlags(hasMin, hasMax));
        if (hasMin) buf.WriteFloat(value.Minimum);
        if (hasMax) buf.WriteFloat(value.Maximum);
    }

    public override Template DeserializeFromNetwork(FriendlyByteBuf buf)
    {
        var flags = buf.ReadByte();
        var min = ArgumentUtils.NumberHasMin(flags) ? buf.ReadFloat() : -float.MaxValue;
        var max = ArgumentUtils.NumberHasMax(flags) ? buf.ReadFloat() : float.MaxValue;
        return new ArgumentTemplate(this, FloatArgumentType.FloatArg(min, max));
    }
}

//DoubleArgumentInfo double-precision argument
public sealed class DoubleArgumentInfo : ArgumentTypeInfo
{
    public static readonly DoubleArgumentInfo Instance = new();

    public override Type ArgumentClrType => typeof(DoubleArgumentType);

    public override Template Unpack(object argumentType)
        => argumentType is DoubleArgumentType
            ? new ArgumentTemplate(this, argumentType)
            : throw new InvalidOperationException($"not a double argument type: {argumentType.GetType().Name}");

    public override void SerializeToNetwork(Template template, FriendlyByteBuf buf)
    {
        var value = (DoubleArgumentType)((ArgumentTemplate)template).Value;
        var hasMin = value.Minimum != -double.MaxValue;
        var hasMax = value.Maximum != double.MaxValue;
        buf.WriteByte(ArgumentUtils.CreateNumberFlags(hasMin, hasMax));
        if (hasMin) buf.WriteDouble(value.Minimum);
        if (hasMax) buf.WriteDouble(value.Maximum);
    }

    public override Template DeserializeFromNetwork(FriendlyByteBuf buf)
    {
        var flags = buf.ReadByte();
        var min = ArgumentUtils.NumberHasMin(flags) ? buf.ReadDouble() : -double.MaxValue;
        var max = ArgumentUtils.NumberHasMax(flags) ? buf.ReadDouble() : double.MaxValue;
        return new ArgumentTemplate(this, DoubleArgumentType.DoubleArg(min, max));
    }
}

//StringArgumentInfo string argument, writes only the ids of the three parse modes
public sealed class StringArgumentInfo : ArgumentTypeInfo
{
    public static readonly StringArgumentInfo Instance = new();

    public override Type ArgumentClrType => typeof(StringArgumentType);

    public override Template Unpack(object argumentType)
        => argumentType is StringArgumentType
            ? new ArgumentTemplate(this, argumentType)
            : throw new InvalidOperationException($"not a string argument type: {argumentType.GetType().Name}");

    public override void SerializeToNetwork(Template template, FriendlyByteBuf buf)
        => buf.WriteVarInt(TypeId(((StringArgumentType)((ArgumentTemplate)template).Value).Type));

    public override Template DeserializeFromNetwork(FriendlyByteBuf buf)
        => new ArgumentTemplate(this, FromId(buf.ReadVarInt()));

    //TypeId mapping from mode to ordinal, aligned with the vanilla StringType enum order
    private static int TypeId(StringType type)
    {
        if (ReferenceEquals(type, StringType.SingleWord)) return 0;
        if (ReferenceEquals(type, StringType.QuotablePhrase)) return 1;
        return 2;
    }

    private static StringArgumentType FromId(int id) => id switch
    {
        0 => StringArgumentType.Word(),
        1 => StringArgumentType.String(),
        _ => StringArgumentType.GreedyString(),
    };
}

//SingletonArgumentInfo descriptor for a parameterless singleton argument, maps to vanilla SingletonArgumentInfo
//serializeToNetwork writes no bytes; the client restores via a factory by registry id
public sealed class SingletonArgumentInfo(Type clrType, Func<object> factory) : ArgumentTypeInfo
{
    public override Type ArgumentClrType { get; } = clrType;

    public override Template Unpack(object argumentType)
        => argumentType.GetType() == clrType
            ? new ArgumentTemplate(this, argumentType)
            : throw new InvalidOperationException($"not the expected argument type: {argumentType.GetType().Name}");

    public override void SerializeToNetwork(Template template, FriendlyByteBuf buf) { }

    public override Template DeserializeFromNetwork(FriendlyByteBuf buf)
        => new ArgumentTemplate(this, factory());
}

//PlaceholderArgumentInfo placeholder descriptor for an unimplemented argument type
//Only occupies the registry id to stay aligned with the client network id; unusable in the command tree
//The registry deduplicates by value reference, so each placeholder must be a new instance
public sealed class PlaceholderArgumentInfo : ArgumentTypeInfo
{
    public override Type ArgumentClrType => typeof(object);

    public override Template Unpack(object argumentType)
        => throw new NotSupportedException($"argument type not implemented; cannot write into the command tree: {argumentType.GetType().Name}");

    public override void SerializeToNetwork(Template template, FriendlyByteBuf buf) { }

    public override Template DeserializeFromNetwork(FriendlyByteBuf buf)
        => throw new NotSupportedException("placeholder argument types do not support deserialization");
}

//TimeArgumentInfo time argument descriptor, writes the allowed minimum tick count
public sealed class TimeArgumentInfo : ArgumentTypeInfo
{
    public static readonly TimeArgumentInfo Instance = new();

    public override Type ArgumentClrType => typeof(TimeArgument);

    public override Template Unpack(object argumentType)
        => argumentType is TimeArgument time
            ? new ArgumentTemplate(this, time)
            : throw new InvalidOperationException($"not a time argument type: {argumentType.GetType().Name}");

    public override void SerializeToNetwork(Template template, FriendlyByteBuf buf)
        => buf.WriteInt(((TimeArgument)((ArgumentTemplate)template).Value).Minimum);

    public override Template DeserializeFromNetwork(FriendlyByteBuf buf)
        => new ArgumentTemplate(this, TimeArgument.Time(buf.ReadInt()));
}

//ResourceArgumentInfo resource argument descriptor, writes the target registry id
public sealed class ResourceArgumentInfo : ArgumentTypeInfo
{
    public static readonly ResourceArgumentInfo Instance = new();

    public override Type ArgumentClrType => typeof(ResourceArgument);

    public override Template Unpack(object argumentType)
        => argumentType is ResourceArgument resource
            ? new ArgumentTemplate(this, resource)
            : throw new InvalidOperationException($"not a resource argument type: {argumentType.GetType().Name}");

    public override void SerializeToNetwork(Template template, FriendlyByteBuf buf)
        => buf.WriteIdentifier(((ResourceArgument)((ArgumentTemplate)template).Value).RegistryKey);

    public override Template DeserializeFromNetwork(FriendlyByteBuf buf)
        => new ArgumentTemplate(this, new ResourceArgument(buf.ReadIdentifier()));
}

//ResourceKeyArgumentInfo registry key argument descriptor, writes the target registry id
//Difference from ResourceArgumentInfo is on the client: this only builds the key from the identifier without looking up whether the registry exists
//So referencing a registry not synced to the client such as recipe does not crash the client
public sealed class ResourceKeyArgumentInfo : ArgumentTypeInfo
{
    public static readonly ResourceKeyArgumentInfo Instance = new();

    public override Type ArgumentClrType => typeof(ResourceKeyArgument);

    public override Template Unpack(object argumentType)
        => argumentType is ResourceKeyArgument key
            ? new ArgumentTemplate(this, key)
            : throw new InvalidOperationException($"not a registry key argument type: {argumentType.GetType().Name}");

    public override void SerializeToNetwork(Template template, FriendlyByteBuf buf)
        => buf.WriteIdentifier(((ResourceKeyArgument)((ArgumentTemplate)template).Value).RegistryKey);

    public override Template DeserializeFromNetwork(FriendlyByteBuf buf)
        => new ArgumentTemplate(this, new ResourceKeyArgument(buf.ReadIdentifier()));
}

//EntityArgumentInfo entity argument descriptor, writes the single and playersOnly flag bytes
public sealed class EntityArgumentInfo : ArgumentTypeInfo
{
    private const byte FlagSingle = 1;
    private const byte FlagPlayersOnly = 2;

    public static readonly EntityArgumentInfo Instance = new();

    public override Type ArgumentClrType => typeof(EntityArgument);

    public override Template Unpack(object argumentType)
        => argumentType is EntityArgument entity
            ? new ArgumentTemplate(this, entity)
            : throw new InvalidOperationException($"not an entity argument type: {argumentType.GetType().Name}");

    public override void SerializeToNetwork(Template template, FriendlyByteBuf buf)
    {
        var entity = (EntityArgument)((ArgumentTemplate)template).Value;
        byte flags = 0;
        if (entity.Single) flags |= FlagSingle;
        if (entity.PlayersOnly) flags |= FlagPlayersOnly;
        buf.WriteByte(flags);
    }

    public override Template DeserializeFromNetwork(FriendlyByteBuf buf)
    {
        var flags = buf.ReadByte();
        return new ArgumentTemplate(this, new EntityArgument(
            (flags & FlagSingle) != 0, (flags & FlagPlayersOnly) != 0));
    }
}

//ArgumentTypeInfos argument type descriptor registry, maps to vanilla ArgumentTypeInfos
public static class ArgumentTypeInfos
{
    //ByClass index from argument type CLR type to descriptor
    private static readonly Dictionary<Type, ArgumentTypeInfo> ByClass = new();

    //Bootstrap registers built-in argument types; the registration order is the command tree packet network id 0-56, strictly aligned with vanilla bootstrap
    //Unimplemented types use placeholder descriptors to keep the order; the client restores the command tree from its local registry by the same id
    public static void Bootstrap()
    {
        Register("brigadier:bool", BoolArgumentInfo.Instance);                                 //0
        Register("brigadier:float", FloatArgumentInfo.Instance);                               //1
        Register("brigadier:double", DoubleArgumentInfo.Instance);                             //2
        Register("brigadier:integer", IntegerArgumentInfo.Instance);                           //3
        Register("brigadier:long", LongArgumentInfo.Instance);                                 //4
        Register("brigadier:string", StringArgumentInfo.Instance);                             //5
        Register("entity", EntityArgumentInfo.Instance);                                        //6
        Register("game_profile", new PlaceholderArgumentInfo());                            //7
        Register("block_pos", new SingletonArgumentInfo(typeof(BlockPosArgument), BlockPosArgument.BlockPos)); //8
        Register("column_pos", new SingletonArgumentInfo(typeof(ColumnPosArgument), ColumnPosArgument.ColumnPos)); //9
        Register("vec3", new SingletonArgumentInfo(typeof(Vec3Argument), Vec3Argument.Vec3));  //10
        Register("vec2", new SingletonArgumentInfo(typeof(Vec2Argument), () => Vec2Argument.Vec2())); //11
        Register("block_state", new SingletonArgumentInfo(typeof(BlockStateArgument), BlockStateArgument.Block)); //12
        Register("block_predicate", new SingletonArgumentInfo(typeof(BlockPredicateArgument), BlockPredicateArgument.BlockPredicate)); //13
        Register("item_stack", new SingletonArgumentInfo(typeof(ItemArgument), ItemArgument.Item)); //14
        Register("item_predicate", new SingletonArgumentInfo(typeof(ItemPredicateArgument), ItemPredicateArgument.ItemPredicate)); //15
        Register("team_color", new PlaceholderArgumentInfo());                              //16
        Register("hex_color", new PlaceholderArgumentInfo());                               //17
        Register("component", new SingletonArgumentInfo(typeof(ComponentArgument), () => ComponentArgument.TextComponent())); //18
        Register("style", new PlaceholderArgumentInfo());                                   //19
        Register("message", new SingletonArgumentInfo(typeof(MessageArgument), () => MessageArgument.Message())); //20
        Register("nbt_compound_tag", new SingletonArgumentInfo(typeof(CompoundTagArgument), CompoundTagArgument.CompoundTag)); //21
        Register("nbt_tag", new SingletonArgumentInfo(typeof(NbtTagArgument), NbtTagArgument.NbtTag));   //22
        Register("nbt_path", new SingletonArgumentInfo(typeof(NbtPathArgument), NbtPathArgument.NbtPathArg)); //23
        Register("objective", new PlaceholderArgumentInfo());                               //24
        Register("objective_criteria", new PlaceholderArgumentInfo());                      //25
        Register("operation", new PlaceholderArgumentInfo());                               //26
        Register("particle", new SingletonArgumentInfo(typeof(ParticleArgument), ParticleArgument.Particle)); //27
        Register("angle", new PlaceholderArgumentInfo());                                   //28
        Register("rotation", new SingletonArgumentInfo(typeof(RotationArgument), RotationArgument.Rotation)); //29
        Register("scoreboard_slot", new PlaceholderArgumentInfo());                         //30
        Register("score_holder", new PlaceholderArgumentInfo());                            //31
        Register("swizzle", new PlaceholderArgumentInfo());                                 //32
        Register("team", new PlaceholderArgumentInfo());                                    //33
        Register("item_slot", new SingletonArgumentInfo(typeof(SlotArgument), SlotArgument.Slot));    //34
        Register("item_slots", new PlaceholderArgumentInfo());                              //35
        Register("resource_location", new SingletonArgumentInfo(typeof(IdentifierArgument), IdentifierArgument.Id)); //36
        Register("function", new PlaceholderArgumentInfo());                                //37
        Register("entity_anchor", new SingletonArgumentInfo(typeof(EntityAnchorArgument), EntityAnchorArgument.EntityAnchor)); //38
        Register("int_range", new PlaceholderArgumentInfo());                               //39
        Register("float_range", new PlaceholderArgumentInfo());                             //40
        Register("dimension", new SingletonArgumentInfo(typeof(DimensionArgument), DimensionArgument.Dimension)); //41
        Register("gamemode", new SingletonArgumentInfo(typeof(GameModeArgument), GameModeArgument.GameMode)); //42
        Register("time", TimeArgumentInfo.Instance);                                           //43
        Register("resource_or_tag", new PlaceholderArgumentInfo());                         //44
        Register("resource_or_tag_key", new PlaceholderArgumentInfo());                     //45
        Register("resource", ResourceArgumentInfo.Instance);                                   //46
        Register("resource_key", ResourceKeyArgumentInfo.Instance);                             //47
        Register("resource_selector", new PlaceholderArgumentInfo());                       //48
        Register("template_mirror", new PlaceholderArgumentInfo());                         //49
        Register("template_rotation", new PlaceholderArgumentInfo());                       //50
        Register("heightmap", new PlaceholderArgumentInfo());                               //51
        Register("loot_table", new PlaceholderArgumentInfo());                              //52
        Register("loot_predicate", new PlaceholderArgumentInfo());                          //53
        Register("loot_modifier", new PlaceholderArgumentInfo());                           //54
        Register("dialog", new PlaceholderArgumentInfo());                                  //55
        Register("uuid", new PlaceholderArgumentInfo());                                    //56
    }

    //Unpack looks up the descriptor by argument type instance; returns null when unregistered
    public static ArgumentTypeInfo? Unpack(object argumentType)
        => ByClass.TryGetValue(argumentType.GetType(), out var info) ? info : null;

    private static void Register(string id, ArgumentTypeInfo info)
    {
        Registry<object>.Register(BuiltInRegistries.COMMAND_ARGUMENT_TYPE, Identifier.Parse(id), info);
        ByClass[info.ArgumentClrType] = info;
    }
}
