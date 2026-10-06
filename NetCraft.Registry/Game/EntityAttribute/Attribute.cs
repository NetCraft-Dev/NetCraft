using NetCraft.Codec;
using NetCraft.Registry.Codec;
using NetCraft.Util;

namespace NetCraft.Registry.EntityAttribute;

//Attribute entity attribute, maps to vanilla net.minecraft.world.entity.ai.attributes.Attribute
//Holds the default value and whether it syncs to the client; values are always clamped through SanitizeValue
public class Attribute
{
    //Sentiment attribute sentiment, determining the color of increase/decrease hints, maps to vanilla Attribute.Sentiment
    public enum Sentiment
    {
        Positive,
        Neutral,
        Negative,
    }

    public Attribute(string descriptionId, double defaultValue)
    {
        DescriptionId = descriptionId;
        DefaultValue = defaultValue;
    }

    //DescriptionId language key, maps to vanilla getDescriptionId
    public string DescriptionId { get; }

    //DefaultValue default value, maps to vanilla getDefaultValue
    public double DefaultValue { get; }

    //ClientSyncable whether the value syncs to the client, maps to vanilla isClientSyncable
    public bool ClientSyncable { get; private set; }

    //ValueSentiment attribute sentiment, maps to vanilla sentiment
    public Sentiment ValueSentiment { get; private set; } = Sentiment.Positive;

    //SetSyncable sets whether it syncs, maps to vanilla setSyncable
    public Attribute SetSyncable(bool syncable)
    {
        ClientSyncable = syncable;
        return this;
    }

    //SetSentiment sets the sentiment, maps to vanilla setSentiment
    public Attribute SetSentiment(Sentiment sentiment)
    {
        ValueSentiment = sentiment;
        return this;
    }

    //SanitizeValue clamps the value; the base class does not clamp, maps to vanilla sanitizeValue
    public virtual double SanitizeValue(double value) => value;
}

//RangedAttribute ranged attribute, maps to vanilla RangedAttribute
//Out-of-range values are clamped back into the range; NaN falls back to the lower bound
public sealed class RangedAttribute : Attribute
{
    public RangedAttribute(string descriptionId, double defaultValue, double minValue, double maxValue)
        : base(descriptionId, defaultValue)
    {
        //Vanilla throws right in the constructor; all three assertions are kept, otherwise an out-of-range default would silently appear in the registry
        if (minValue > maxValue) throw new ArgumentException($"Min value cannot be greater than max value {descriptionId}");
        if (defaultValue < minValue) throw new ArgumentException($"Default value cannot be less than min value {descriptionId}");
        if (defaultValue > maxValue) throw new ArgumentException($"Default value cannot be greater than max value {descriptionId}");
        MinValue = minValue;
        MaxValue = maxValue;
    }

    //MinValue lower bound, maps to vanilla getMinValue
    public double MinValue { get; }

    //MaxValue upper bound, maps to vanilla getMaxValue
    public double MaxValue { get; }

    public override double SanitizeValue(double value)
        => double.IsNaN(value) ? MinValue : Mth.Clamp(value, MinValue, MaxValue);
}

//AttributeOperation modifier operation, maps to vanilla AttributeModifier.Operation
//Declaration order is the operation order: add value, then multiply base, then multiply total; cannot be reordered
public enum AttributeOperation
{
    AddValue,
    AddMultipliedBase,
    AddMultipliedTotal,
}

//AttributeModifier attribute modifier, maps to vanilla AttributeModifier
//id identifies, amount is the value, operation determines the computation; equal on all three means the same modifier
public sealed record AttributeModifier(Identifier Id, double Amount, AttributeOperation Operation)
{
    //OperationCodec encodes/decodes the operation by serialized name, maps to vanilla Operation.CODEC
    //Must be declared before MapCodec since static fields initialize in declaration order
    public static readonly Codec<AttributeOperation> OperationCodec = Codecs.String.ComapFlatMap(
        name => TryFromName(name) is { } operation
            ? DataResult<AttributeOperation>.Success(operation)
            : DataResult<AttributeOperation>.Error(() => $"Unknown operation: {name}"),
        GetSerializedName);

    //MapCodec persistence codec with id, amount and operation, maps to vanilla MAP_CODEC
    public static readonly Codec<AttributeModifier> MapCodec = RecordCodecBuilder.Of3(
        IdentifierCodec.Instance.FieldOf("id").ForGetter((AttributeModifier modifier) => modifier.Id),
        Codecs.Double.FieldOf("amount").ForGetter((AttributeModifier modifier) => modifier.Amount),
        OperationCodec.FieldOf("operation").ForGetter((AttributeModifier modifier) => modifier.Operation),
        (id, amount, operation) => new AttributeModifier(id, amount, operation));

    //Codec is identical to MapCodec, maps to vanilla CODEC
    public static readonly Codec<AttributeModifier> Codec = MapCodec;

    //GetSerializedName serialized name of the operation, maps to vanilla StringRepresentable's value
    public static string GetSerializedName(AttributeOperation operation) => operation switch
    {
        AttributeOperation.AddMultipliedBase => "add_multiplied_base",
        AttributeOperation.AddMultipliedTotal => "add_multiplied_total",
        _ => "add_value",
    };

    //TryFromName parses the operation by serialized name, returning null for unknown names
    public static AttributeOperation? TryFromName(string name) => name switch
    {
        "add_value" => AttributeOperation.AddValue,
        "add_multiplied_base" => AttributeOperation.AddMultipliedBase,
        "add_multiplied_total" => AttributeOperation.AddMultipliedTotal,
        _ => null,
    };
}


