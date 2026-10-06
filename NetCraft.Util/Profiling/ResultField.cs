namespace NetCraft.Util.Profiling;

//Profiler result field, maps to vanilla net.minecraft.util.profiling.ResultField
//Single path timing percentage and count
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

    //Color based on the name hash, maps to vanilla getColor
    public int GetColor() => (Name.GetHashCode() & 11184810) - 12303292;
}
