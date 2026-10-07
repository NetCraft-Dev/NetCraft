using System.Collections;
using NetCraft.Codec;
using NetCraft.DataFixer.Fixes;
using NetCraft.DataFixer.Functions;
using NetCraft.Util;
using T = NetCraft.DataFixer.Types;

namespace NetCraft.DataFixer;

//extra DFU utility class maps to vanilla net.minecraft.util.datafix.ExtraDataFixUtils
//provides convenience operations such as BlockPos/BlockState/Cast/PatchSubType/ChainAllFilters/FixStringField
//blockState does not rely on NbtOps; the caller passes template ops to avoid cross-ops conversion
public static class ExtraDataFixUtils
{
    //fixes a BlockPos with X/Y/Z fields into a List form
    public static Dynamic<object> FixBlockPos(Dynamic<object> pos)
    {
        var x = pos.Get("X").AsNumber().Result();
        var y = pos.Get("Y").AsNumber().Result();
        var z = pos.Get("Z").AsNumber().Result();
        if (!x.IsPresent || !y.IsPresent || !z.IsPresent) return pos;
        return CreateBlockPos(pos, (int)x.Get(), (int)y.Get(), (int)z.Get());
    }

    //moves the inline X/Y/Z fields into a List form under newField
    public static Dynamic<object> FixInlineBlockPos(Dynamic<object> input, string fieldX, string fieldY, string fieldZ, string newField)
    {
        var x = input.Get(fieldX).AsNumber().Result();
        var y = input.Get(fieldY).AsNumber().Result();
        var z = input.Get(fieldZ).AsNumber().Result();
        if (!x.IsPresent || !y.IsPresent || !z.IsPresent) return input;
        return input.Remove(fieldX).Remove(fieldY).Remove(fieldZ).Set(newField, CreateBlockPos(input, (int)x.Get(), (int)y.Get(), (int)z.Get()));
    }

    //builds the [x,y,z] list, maps to vanilla createBlockPos
    public static Dynamic<object> CreateBlockPos(Dynamic<object> dynamic, int x, int y, int z)
        => dynamic.CreateList(new[] { dynamic.CreateInt(x), dynamic.CreateInt(y), dynamic.CreateInt(z) });

    //casts a Typed to the target Type, keeping the value and ops unchanged
    public static Typed<R> Cast<TOther, R>(T.Type<R> type, Typed<TOther> typed)
        => new(type, typed.Ops, (R)(object)typed.Value!);

    //builds a Typed directly from the value and ops
    public static Typed<TA> Cast<TA>(T.Type<TA> type, object value, DynamicOps<object> ops)
        => new(type, ops, (TA)(object)value!);

    //replaces all child types in type matching find with replace and returns the new Type
    //relies on RewriteResult+View+TypeRewriteRule.everywhere/ifSame+PointFreeRule.nop
    public static T.Type<object> PatchSubType(T.Type<object> type, T.Type<object> find, T.Type<object> replace)
    {
        var rule = TypePatcher(find, replace);
        var result = type.All(rule, true, false);
        return result.View().NewType();
    }

    //type Patcher rule placeholder throwing NotSupportedException, maps to vanilla typePatcher
    private static TypeRewriteRule TypePatcher(T.Type<object> inputType, T.Type<object> outputType)
    {
        var view = View<object, object>.Create("Patcher", inputType, outputType, _ => _ => throw new NotSupportedException("Patcher not implemented"));
        var rewriteResult = RewriteResult<object, object>.Create(view, new BitSet());
        return TypeRewriteRule.Everywhere(TypeRewriteRule.IfSame(inputType, rewriteResult), PointFreeRule.NopRule.Instance, true, true);
    }

    //chains multiple Typed fix functions into a single combined function
    public static Func<Typed<object>, Typed<object>> ChainAllFilters(params Func<Typed<object>, Typed<object>>[] fixers)
        => typed =>
        {
            foreach (var fixer in fixers) typed = fixer(typed);
            return typed;
        };

    //builds the BlockState Dynamic using template's ops, maps to vanilla blockState(id,properties)
    //vanilla uses NbtOps+CompoundTag; here template is used to avoid cross-ops conversion
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

    //applies the fix function to the fieldName field of the Dynamic and returns a new Dynamic
    public static Dynamic<object> FixStringField(Dynamic<object> dynamic, string fieldName, Func<string, string> fix)
        => dynamic.Update(fieldName, field =>
        {
            var mapped = field.AsString().Map(fix);
            return DataFixUtils.OrElse(mapped.Map(dynamic.CreateString).Result(), field);
        });

    //converts a dye color ID to a name, maps to vanilla dyeColorIdToName
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

    //reads a Dynamic into a Typed and sets through an optic, maps to vanilla readAndSet
    public static Typed<object> ReadAndSet<TA>(Typed<object> target, OpticFinder<TA> optic, Dynamic<object> value)
        => target.Set(optic, DataFixUtils.ReadTypedOrThrow(optic.Type(), value, true));
}
