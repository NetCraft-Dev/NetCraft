using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Suggestion;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Particle;
using NetCraft.Nbt;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Util;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//ParticleArgument 粒子参数对应原版 net.minecraft.commands.arguments.ParticleArgument
//语法 <粒子名>[{参数}] 参数段用 SNBT 与网络编码的字段名一致
//参数依赖未移植子系统的粒子直接拒绝 不会构造出无法编码的选项
public sealed class ParticleArgument : ArgumentType<ParticleOptions>
{
    private static readonly IReadOnlyList<string> ExamplesList = new[] { "foo", "foo:bar", "particle{foo:bar}" };

    public static readonly DynamicCommandExceptionType ErrorUnknownParticle =
        new(name => new LiteralMessage($"未知粒子 {name}"));

    public static readonly DynamicCommandExceptionType ErrorUnsupportedParticle =
        new(name => new LiteralMessage($"暂不支持的粒子 {name}"));

    public static readonly DynamicCommandExceptionType ErrorMissingOptions =
        new(name => new LiteralMessage($"粒子 {name} 缺少必需参数"));

    public static readonly DynamicCommandExceptionType ErrorInvalidOptions =
        new(name => new LiteralMessage($"粒子 {name} 参数非法"));

    public static ParticleArgument Particle() => new();

    public ParticleOptions Parse(StringReader reader)
    {
        var start = reader.Cursor;
        try
        {
            return ParseInternal(reader);
        }
        catch (CommandSyntaxException)
        {
            reader.SetCursor(start);
            throw;
        }
    }

    private static ParticleOptions ParseInternal(StringReader reader)
    {
        var id = IdentifierArgument.ReadIdentifier(reader);
        var type = ParticleTypes.Find(id);
        if (type is null) throw ErrorUnknownParticle.Create(id);
        if (type is UnsupportedParticleType) throw ErrorUnsupportedParticle.Create(id);

        CompoundTag? options = null;
        if (reader.CanRead() && reader.Peek() == '{')
        {
            var nbtReader = new CommandStringReader(reader.String) { Cursor = reader.Cursor };
            try
            {
                options = TagParser<Tag>.ParseCompoundAsArgument(nbtReader);
            }
            catch (CommandSyntaxException)
            {
                throw ErrorInvalidOptions.Create(id);
            }
            reader.SetCursor(nbtReader.Cursor);
        }
        return CreateOptions(type, options, id);
    }

    //CreateOptions 按粒子类型组装选项 无参数类型忽略参数段
    private static ParticleOptions CreateOptions(ParticleType type, CompoundTag? options, Identifier id)
    {
        switch (type)
        {
            case SimpleParticleType simple:
                return simple.Options;
            case BlockParticleType block:
                return new BlockParticleOption(block, ReadBlockState(Require(options, id), id));
            case ItemParticleType item:
                return new ItemParticleOption(item, ReadItem(Require(options, id), id));
            case DustParticleType dust:
                return new DustParticleOptions(dust,
                    (int)ReadNumber(Require(options, id), "color", id),
                    (float)ReadNumber(Require(options, id), "scale", id));
            case DustColorTransitionParticleType transition:
                var nbt = Require(options, id);
                return new DustColorTransitionOptions(transition,
                    (int)ReadNumber(nbt, "from_color", id),
                    (int)ReadNumber(nbt, "to_color", id),
                    (float)ReadNumber(nbt, "scale", id));
            case ColorParticleType color:
                return new ColorParticleOption(color, (int)ReadNumber(Require(options, id), "color", id));
            default:
                throw ErrorUnsupportedParticle.Create(id);
        }
    }

    //Require 带参数的粒子必须写参数段 对应原版 codec 里没有默认值的必填字段
    private static CompoundTag Require(CompoundTag? options, Identifier id)
        => options ?? throw ErrorMissingOptions.Create(id);

    //ReadBlockState 参数段的 block_state 字段是方块状态文本 复用方块状态参数解析
    private static BlockState ReadBlockState(CompoundTag options, Identifier id)
    {
        if (options.GetString("block_state") is not { } tag) throw ErrorMissingOptions.Create(id);
        try
        {
            return new BlockStateArgument().Parse(new StringReader(tag.Value)).State;
        }
        catch (CommandSyntaxException)
        {
            throw ErrorInvalidOptions.Create(id);
        }
    }

    //ReadItem 参数段的 item 字段是物品文本 复用物品参数解析
    private static ItemStack ReadItem(CompoundTag options, Identifier id)
    {
        if (options.GetString("item") is not { } tag) throw ErrorMissingOptions.Create(id);
        try
        {
            var input = new ItemArgument().Parse(new StringReader(tag.Value));
            return new ItemStack(input.Item.BuiltInRegistryHolder, 1, input.Components);
        }
        catch (CommandSyntaxException)
        {
            throw ErrorInvalidOptions.Create(id);
        }
    }

    //ReadNumber 读数值字段 SNBT 里 1.0 是 double 1 是 int 都要接
    private static double ReadNumber(CompoundTag options, string key, Identifier id)
    {
        if (options.GetDouble(key) is { } d) return d.Value;
        if (options.GetFloat(key) is { } f) return f.Value;
        if (options.GetInt(key) is { } i) return i.Value;
        if (options.GetLong(key) is { } l) return l.Value;
        throw ErrorMissingOptions.Create(id);
    }

    //GetParticle 取解析出的粒子选项
    public static ParticleOptions GetParticle(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<ParticleOptions>(name);

    //ListSuggestions 补全全部受支持粒子名 跳过参数未移植的类型
    public Task<Suggestions> ListSuggestions<S>(CommandContext<S> context, SuggestionsBuilder builder)
    {
        foreach (var id in BuiltInRegistries.PARTICLE_TYPE.KeySet)
        {
            if (BuiltInRegistries.PARTICLE_TYPE.GetValue(id) is not ParticleType type) continue;
            if (type is UnsupportedParticleType) continue;
            var text = id.ToShortString();
            if (text.StartsWith(builder.RemainingLowerCase, StringComparison.Ordinal))
                builder.Add(text);
        }
        return builder.BuildFuture();
    }

    public IReadOnlyList<string> Examples => ExamplesList;
}
