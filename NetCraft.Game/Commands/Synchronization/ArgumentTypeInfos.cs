using NetCraft.Commands.Arguments;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Network;
using NetCraft.Registry;

namespace NetCraft.Game.Commands.Synchronization;

//ArgumentTypeInfo 命令参数类型网络同步描述符对应原版 net.minecraft.commands.synchronization.ArgumentTypeInfo
//把参数类型的附加参数(数值范围/字符串模式)写进命令树包 客户端按注册表 id 找同类描述符再还原
//NC 的 ArgumentType<T> 只按泛型区分没有共同基类 所以这里统一用 object 承载参数类型实例
public abstract class ArgumentTypeInfo
{
    //Template 参数类型模板对应原版 ArgumentTypeInfo.Template
    public abstract class Template
    {
        //Info 产生该模板的描述符
        public abstract ArgumentTypeInfo Info { get; }

        //Instantiate 还原参数类型实例 原版需要 CommandBuildContext 本作参数类型不依赖上下文
        public abstract object Instantiate();
    }

    //ArgumentClrType 该描述符对应的参数类型 CLR 类型 供按类型反查
    public abstract Type ArgumentClrType { get; }

    //Unpack 从参数类型实例取模板 类型不匹配抛异常
    public abstract Template Unpack(object argumentType);

    //SerializeToNetwork 写该类型的附加参数
    public abstract void SerializeToNetwork(Template template, FriendlyByteBuf buf);

    //DeserializeFromNetwork 读回模板
    public abstract Template DeserializeFromNetwork(FriendlyByteBuf buf);
}

//ArgumentTemplate 通用模板实现 直接持有已构造的参数类型实例
public sealed class ArgumentTemplate : ArgumentTypeInfo.Template
{
    public ArgumentTemplate(ArgumentTypeInfo info, object value)
    {
        Info = info;
        Value = value;
    }

    public override ArgumentTypeInfo Info { get; }

    //Value 参数类型实例
    public object Value { get; }

    public override object Instantiate() => Value;
}

//ArgumentUtils 数值参数标志位工具对应原版 ArgumentUtils
internal static class ArgumentUtils
{
    //CreateNumberFlags 低两位表示是否带 min/max
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

//BoolArgumentInfo 布尔参数 无附加参数 对应原版 SingletonArgumentInfo.contextFree(BoolArgumentType::bool)
public sealed class BoolArgumentInfo : ArgumentTypeInfo
{
    public static readonly BoolArgumentInfo Instance = new();

    public override Type ArgumentClrType => typeof(BoolArgumentType);

    public override Template Unpack(object argumentType)
        => argumentType is BoolArgumentType
            ? new ArgumentTemplate(this, argumentType)
            : throw new InvalidOperationException($"不是布尔参数类型: {argumentType.GetType().Name}");

    public override void SerializeToNetwork(Template template, FriendlyByteBuf buf) { }

    public override Template DeserializeFromNetwork(FriendlyByteBuf buf)
        => new ArgumentTemplate(this, BoolArgumentType.Bool());
}

//IntegerArgumentInfo 整数参数 写 min/max 存在标志与可选 int 值
public sealed class IntegerArgumentInfo : ArgumentTypeInfo
{
    public static readonly IntegerArgumentInfo Instance = new();

    public override Type ArgumentClrType => typeof(IntegerArgumentType);

    public override Template Unpack(object argumentType)
        => argumentType is IntegerArgumentType
            ? new ArgumentTemplate(this, argumentType)
            : throw new InvalidOperationException($"不是整数参数类型: {argumentType.GetType().Name}");

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

//LongArgumentInfo 长整数参数 值走 VarLong
public sealed class LongArgumentInfo : ArgumentTypeInfo
{
    public static readonly LongArgumentInfo Instance = new();

    public override Type ArgumentClrType => typeof(LongArgumentType);

    public override Template Unpack(object argumentType)
        => argumentType is LongArgumentType
            ? new ArgumentTemplate(this, argumentType)
            : throw new InvalidOperationException($"不是长整数参数类型: {argumentType.GetType().Name}");

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

//FloatArgumentInfo 单精度参数
public sealed class FloatArgumentInfo : ArgumentTypeInfo
{
    public static readonly FloatArgumentInfo Instance = new();

    public override Type ArgumentClrType => typeof(FloatArgumentType);

    public override Template Unpack(object argumentType)
        => argumentType is FloatArgumentType
            ? new ArgumentTemplate(this, argumentType)
            : throw new InvalidOperationException($"不是单精度参数类型: {argumentType.GetType().Name}");

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

//DoubleArgumentInfo 双精度参数
public sealed class DoubleArgumentInfo : ArgumentTypeInfo
{
    public static readonly DoubleArgumentInfo Instance = new();

    public override Type ArgumentClrType => typeof(DoubleArgumentType);

    public override Template Unpack(object argumentType)
        => argumentType is DoubleArgumentType
            ? new ArgumentTemplate(this, argumentType)
            : throw new InvalidOperationException($"不是双精度参数类型: {argumentType.GetType().Name}");

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

//StringArgumentInfo 字符串参数 只写三种解析模式的 id
public sealed class StringArgumentInfo : ArgumentTypeInfo
{
    public static readonly StringArgumentInfo Instance = new();

    public override Type ArgumentClrType => typeof(StringArgumentType);

    public override Template Unpack(object argumentType)
        => argumentType is StringArgumentType
            ? new ArgumentTemplate(this, argumentType)
            : throw new InvalidOperationException($"不是字符串参数类型: {argumentType.GetType().Name}");

    public override void SerializeToNetwork(Template template, FriendlyByteBuf buf)
        => buf.WriteVarInt(TypeId(((StringArgumentType)((ArgumentTemplate)template).Value).Type));

    public override Template DeserializeFromNetwork(FriendlyByteBuf buf)
        => new ArgumentTemplate(this, FromId(buf.ReadVarInt()));

    //TypeId 模式到 ordinal 的映射 对齐原版 StringType 枚举顺序
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

//SingletonArgumentInfo 无附加参数的单例参数描述符对应原版 SingletonArgumentInfo
//serializeToNetwork不写字节 客户端按注册表id用工厂还原
public sealed class SingletonArgumentInfo(Type clrType, Func<object> factory) : ArgumentTypeInfo
{
    public override Type ArgumentClrType { get; } = clrType;

    public override Template Unpack(object argumentType)
        => argumentType.GetType() == clrType
            ? new ArgumentTemplate(this, argumentType)
            : throw new InvalidOperationException($"不是预期的参数类型: {argumentType.GetType().Name}");

    public override void SerializeToNetwork(Template template, FriendlyByteBuf buf) { }

    public override Template DeserializeFromNetwork(FriendlyByteBuf buf)
        => new ArgumentTemplate(this, factory());
}

//PlaceholderArgumentInfo 未实现参数类型的占位描述符
//只占注册表id保证与客户端网络id对齐 命令树不可使用
//注册表按值引用查重每个占位必须新实例
public sealed class PlaceholderArgumentInfo : ArgumentTypeInfo
{
    public override Type ArgumentClrType => typeof(object);

    public override Template Unpack(object argumentType)
        => throw new NotSupportedException($"参数类型未实现不支持写入命令树: {argumentType.GetType().Name}");

    public override void SerializeToNetwork(Template template, FriendlyByteBuf buf) { }

    public override Template DeserializeFromNetwork(FriendlyByteBuf buf)
        => throw new NotSupportedException("占位参数类型不支持反序列化");
}

//TimeArgumentInfo 时间参数描述符 写允许的最小tick数
public sealed class TimeArgumentInfo : ArgumentTypeInfo
{
    public static readonly TimeArgumentInfo Instance = new();

    public override Type ArgumentClrType => typeof(TimeArgument);

    public override Template Unpack(object argumentType)
        => argumentType is TimeArgument time
            ? new ArgumentTemplate(this, time)
            : throw new InvalidOperationException($"不是时间参数类型: {argumentType.GetType().Name}");

    public override void SerializeToNetwork(Template template, FriendlyByteBuf buf)
        => buf.WriteInt(((TimeArgument)((ArgumentTemplate)template).Value).Minimum);

    public override Template DeserializeFromNetwork(FriendlyByteBuf buf)
        => new ArgumentTemplate(this, TimeArgument.Time(buf.ReadInt()));
}

//ResourceArgumentInfo 资源参数描述符 写目标注册表标识
public sealed class ResourceArgumentInfo : ArgumentTypeInfo
{
    public static readonly ResourceArgumentInfo Instance = new();

    public override Type ArgumentClrType => typeof(ResourceArgument);

    public override Template Unpack(object argumentType)
        => argumentType is ResourceArgument resource
            ? new ArgumentTemplate(this, resource)
            : throw new InvalidOperationException($"不是资源参数类型: {argumentType.GetType().Name}");

    public override void SerializeToNetwork(Template template, FriendlyByteBuf buf)
        => buf.WriteIdentifier(((ResourceArgument)((ArgumentTemplate)template).Value).RegistryKey);

    public override Template DeserializeFromNetwork(FriendlyByteBuf buf)
        => new ArgumentTemplate(this, new ResourceArgument(buf.ReadIdentifier()));
}

//ResourceKeyArgumentInfo 注册表键参数描述符 写目标注册表标识
//与 ResourceArgumentInfo 的差别在客户端: 这个只按标识符拼 key 不查注册表是否存在
//所以引用 recipe 这类不同步给客户端的注册表不会让客户端崩
public sealed class ResourceKeyArgumentInfo : ArgumentTypeInfo
{
    public static readonly ResourceKeyArgumentInfo Instance = new();

    public override Type ArgumentClrType => typeof(ResourceKeyArgument);

    public override Template Unpack(object argumentType)
        => argumentType is ResourceKeyArgument key
            ? new ArgumentTemplate(this, key)
            : throw new InvalidOperationException($"不是注册表键参数类型: {argumentType.GetType().Name}");

    public override void SerializeToNetwork(Template template, FriendlyByteBuf buf)
        => buf.WriteIdentifier(((ResourceKeyArgument)((ArgumentTemplate)template).Value).RegistryKey);

    public override Template DeserializeFromNetwork(FriendlyByteBuf buf)
        => new ArgumentTemplate(this, new ResourceKeyArgument(buf.ReadIdentifier()));
}

//EntityArgumentInfo 实体参数描述符 写single与playersOnly标志字节
public sealed class EntityArgumentInfo : ArgumentTypeInfo
{
    private const byte FlagSingle = 1;
    private const byte FlagPlayersOnly = 2;

    public static readonly EntityArgumentInfo Instance = new();

    public override Type ArgumentClrType => typeof(EntityArgument);

    public override Template Unpack(object argumentType)
        => argumentType is EntityArgument entity
            ? new ArgumentTemplate(this, entity)
            : throw new InvalidOperationException($"不是实体参数类型: {argumentType.GetType().Name}");

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

//ArgumentTypeInfos 参数类型描述符注册表对应原版 ArgumentTypeInfos
public static class ArgumentTypeInfos
{
    //ByClass 参数类型 CLR 类型到描述符的索引
    private static readonly Dictionary<Type, ArgumentTypeInfo> ByClass = new();

    //Bootstrap 注册内置参数类型 注册顺序即命令树包网络id 0-56 严格对齐原版bootstrap
    //未实现类型用占位描述符保序 客户端按同id本地注册表还原命令树
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

    //Unpack 按参数类型实例查描述符 未注册返回 null
    public static ArgumentTypeInfo? Unpack(object argumentType)
        => ByClass.TryGetValue(argumentType.GetType(), out var info) ? info : null;

    private static void Register(string id, ArgumentTypeInfo info)
    {
        Registry<object>.Register(BuiltInRegistries.COMMAND_ARGUMENT_TYPE, Identifier.Parse(id), info);
        ByClass[info.ArgumentClrType] = info;
    }
}
