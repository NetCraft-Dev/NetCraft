using NetCraft.Util;

namespace NetCraft.Registry.EntityAttribute;

//Attribute 实体属性 对应原版 net.minecraft.world.entity.ai.attributes.Attribute
//持默认值与是否同步客户端 取值统一经 SanitizeValue 钳制
public class Attribute
{
    //Sentiment 属性倾向 决定数值增减提示的配色 对应原版 Attribute.Sentiment
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

    //DescriptionId 语言键 对应原版 getDescriptionId
    public string DescriptionId { get; }

    //DefaultValue 默认值 对应原版 getDefaultValue
    public double DefaultValue { get; }

    //ClientSyncable 是否把取值同步给客户端 对应原版 isClientSyncable
    public bool ClientSyncable { get; private set; }

    //ValueSentiment 属性倾向 对应原版 sentiment
    public Sentiment ValueSentiment { get; private set; } = Sentiment.Positive;

    //SetSyncable 设置是否同步 对应原版 setSyncable
    public Attribute SetSyncable(bool syncable)
    {
        ClientSyncable = syncable;
        return this;
    }

    //SetSentiment 设置倾向 对应原版 setSentiment
    public Attribute SetSentiment(Sentiment sentiment)
    {
        ValueSentiment = sentiment;
        return this;
    }

    //SanitizeValue 钳制取值 基类不钳制 对应原版 sanitizeValue
    public virtual double SanitizeValue(double value) => value;
}

//RangedAttribute 带区间的属性 对应原版 RangedAttribute
//取值越界钳制回区间 取到 NaN 时用下界
public sealed class RangedAttribute : Attribute
{
    public RangedAttribute(string descriptionId, double defaultValue, double minValue, double maxValue)
        : base(descriptionId, defaultValue)
    {
        //原版在构造里直接抛 三个断言都要留 否则注册表里会静默出现越界默认值
        if (minValue > maxValue) throw new ArgumentException($"最小值不能大于最大值 {descriptionId}");
        if (defaultValue < minValue) throw new ArgumentException($"默认值不能小于最小值 {descriptionId}");
        if (defaultValue > maxValue) throw new ArgumentException($"默认值不能大于最大值 {descriptionId}");
        MinValue = minValue;
        MaxValue = maxValue;
    }

    //MinValue 下界 对应原版 getMinValue
    public double MinValue { get; }

    //MaxValue 上界 对应原版 getMaxValue
    public double MaxValue { get; }

    public override double SanitizeValue(double value)
        => double.IsNaN(value) ? MinValue : Mth.Clamp(value, MinValue, MaxValue);
}

//AttributeOperation 修饰符运算方式 对应原版 AttributeModifier.Operation
//声明顺序即运算顺序 先加数值 再乘基值 最后连乘总值 不能重排
public enum AttributeOperation
{
    AddValue,
    AddMultipliedBase,
    AddMultipliedTotal,
}

//AttributeModifier 属性修饰符 对应原版 AttributeModifier
//id 标识 amount 数值 operation 决定运算方式 三值相等即同一个修饰符
public sealed record AttributeModifier(Identifier Id, double Amount, AttributeOperation Operation)
{
    //GetSerializedName 运算的序列化名 对应原版 StringRepresentable 的取值
    public static string GetSerializedName(AttributeOperation operation) => operation switch
    {
        AttributeOperation.AddMultipliedBase => "add_multiplied_base",
        AttributeOperation.AddMultipliedTotal => "add_multiplied_total",
        _ => "add_value",
    };

    //TryFromName 按序列化名解析运算 未知名字返回 null
    public static AttributeOperation? TryFromName(string name) => name switch
    {
        "add_value" => AttributeOperation.AddValue,
        "add_multiplied_base" => AttributeOperation.AddMultipliedBase,
        "add_multiplied_total" => AttributeOperation.AddMultipliedTotal,
        _ => null,
    };
}
