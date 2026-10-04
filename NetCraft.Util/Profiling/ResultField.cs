namespace NetCraft.Util.Profiling;

//profiler结果字段对应原版net.minecraft.util.profiling.ResultField
//单条路径耗时百分比与计数
public sealed class ResultField : IComparable<ResultField>
{
    public double Percentage { get; }
    public double GlobalPercentage { get; }
    public long Count { get; }
    public string Name { get; }

    public ResultField(string name, double percentage, double globalPercentage, long count)
    {
        Name = name;
        Percentage = percentage;
        GlobalPercentage = globalPercentage;
        Count = count;
    }

    public int CompareTo(ResultField? other)
    {
        if (other is null) return 1;
        if (other.Percentage < Percentage) return -1;
        if (other.Percentage > Percentage) return 1;
        return string.Compare(other.Name, Name, StringComparison.Ordinal);
    }

    //颜色基于name哈希对应原版getColor
    public int GetColor() => (Name.GetHashCode() & 11184810) - 12303292;
}
