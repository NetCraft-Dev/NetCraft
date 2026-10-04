using System.Collections;
using NetCraft.Codec;
using NetCraft.DataFixer.Fixes;
using NetCraft.DataFixer.Functions;
using NetCraft.Util;
using T = NetCraft.DataFixer.Types;

namespace NetCraft.DataFixer;

//额外DFU工具类对应原版net.minecraft.util.datafix.ExtraDataFixUtils
//提供BlockPos/BlockState/Cast/PatchSubType/ChainAllFilters/FixStringField等便捷操作
//blockState不依赖NbtOps调用方传入template ops避免跨ops转换
public static class ExtraDataFixUtils
{
    //把含X/Y/Z字段的BlockPos修复为List形式
    public static Dynamic<object> FixBlockPos(Dynamic<object> pos)
    {
        var x = pos.Get("X").AsNumber().Result();
        var y = pos.Get("Y").AsNumber().Result();
        var z = pos.Get("Z").AsNumber().Result();
        if (!x.IsPresent || !y.IsPresent || !z.IsPresent) return pos;
        return CreateBlockPos(pos, (int)x.Get(), (int)y.Get(), (int)z.Get());
    }

    //把内联X/Y/Z字段移到newField的List形式
    public static Dynamic<object> FixInlineBlockPos(Dynamic<object> input, string fieldX, string fieldY, string fieldZ, string newField)
    {
        var x = input.Get(fieldX).AsNumber().Result();
        var y = input.Get(fieldY).AsNumber().Result();
        var z = input.Get(fieldZ).AsNumber().Result();
        if (!x.IsPresent || !y.IsPresent || !z.IsPresent) return input;
        return input.Remove(fieldX).Remove(fieldY).Remove(fieldZ).Set(newField, CreateBlockPos(input, (int)x.Get(), (int)y.Get(), (int)z.Get()));
    }

    //构造[x,y,z]列表对应原版createBlockPos
    public static Dynamic<object> CreateBlockPos(Dynamic<object> dynamic, int x, int y, int z)
        => dynamic.CreateList(new[] { dynamic.CreateInt(x), dynamic.CreateInt(y), dynamic.CreateInt(z) });

    //强转Typed到目标Type值与ops保持不变
    public static Typed<R> Cast<TOther, R>(T.Type<R> type, Typed<TOther> typed)
        => new(type, typed.Ops, (R)(object)typed.Value!);

    //直接用值与ops构造Typed
    public static Typed<TA> Cast<TA>(T.Type<TA> type, object value, DynamicOps<object> ops)
        => new(type, ops, (TA)(object)value!);

    //把type中所有匹配find的子类型替换为replace后返回新Type
    //依赖RewriteResult+View+TypeRewriteRule.everywhere/ifSame+PointFreeRule.nop
    public static T.Type<object> PatchSubType(T.Type<object> type, T.Type<object> find, T.Type<object> replace)
    {
        var rule = TypePatcher(find, replace);
        var result = type.All(rule, true, false);
        return result.View().NewType();
    }

    //类型Patcher规则占位实现抛NotSupportedException对应原版typePatcher
    private static TypeRewriteRule TypePatcher(T.Type<object> inputType, T.Type<object> outputType)
    {
        var view = View<object, object>.Create("Patcher", inputType, outputType, _ => _ => throw new NotSupportedException("Patcher not implemented"));
        var rewriteResult = RewriteResult<object, object>.Create(view, new BitSet());
        return TypeRewriteRule.Everywhere(TypeRewriteRule.IfSame(inputType, rewriteResult), PointFreeRule.NopRule.Instance, true, true);
    }

    //串联多个Typed修复函数返回单个组合函数
    public static Func<Typed<object>, Typed<object>> ChainAllFilters(params Func<Typed<object>, Typed<object>>[] fixers)
        => typed =>
        {
            foreach (var fixer in fixers) typed = fixer(typed);
            return typed;
        };

    //用template的ops构造BlockState的Dynamic对应原版blockState(id,properties)
    //原版用NbtOps+CompoundTag这里用template避免跨ops转换
    public static Dynamic<object> BlockState(Dynamic<object> template, string id, Dictionary<string, string> properties)
    {
        var blockState = template.EmptyMap().Set(FixConstants.StateHolderName, template.CreateString(id));
        if (properties.Count > 0)
        {
            var mapEntries = properties.Select(kv => new Pair<Dynamic<object>, Dynamic<object>>(
                template.CreateString(kv.Key), template.CreateString(kv.Value)));
            blockState = blockState.Set(FixConstants.StateHolderProperties, template.CreateMap(mapEntries));
        }
        return blockState;
    }

    public static Dynamic<object> BlockState(Dynamic<object> template, string id)
        => BlockState(template, id, new Dictionary<string, string>());

    //对Dynamic的fieldName字段应用fix函数返回新Dynamic
    public static Dynamic<object> FixStringField(Dynamic<object> dynamic, string fieldName, Func<string, string> fix)
        => dynamic.Update(fieldName, field =>
        {
            var mapped = field.AsString().Map(fix);
            return DataFixUtils.OrElse(mapped.Map(dynamic.CreateString).Result(), field);
        });

    //染料色ID转名称对应原版dyeColorIdToName
    public static string DyeColorIdToName(int id) => id switch
    {
        1 => "orange",
        2 => "magenta",
        3 => "light_blue",
        4 => "yellow",
        5 => "lime",
        6 => "pink",
        7 => "gray",
        8 => "light_gray",
        9 => "cyan",
        10 => "purple",
        11 => "blue",
        12 => "brown",
        13 => "green",
        14 => "red",
        15 => "black",
        _ => "white",
    };

    //读取Dynamic到Typed并通过optic设置对应原版readAndSet
    public static Typed<object> ReadAndSet<TA>(Typed<object> target, OpticFinder<TA> optic, Dynamic<object> value)
        => target.Set(optic, DataFixUtils.ReadTypedOrThrow(optic.Type(), value, true));
}
