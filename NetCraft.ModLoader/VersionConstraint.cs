namespace NetCraft.ModLoader;

//VersionConstraint 模组依赖的版本约束判定
//约束写法
//  *        任意版本 与不写这个依赖等价
//  1.2.3    段前缀匹配 1.2 能匹配 1.2.3 与 1.2
//  ^1.2.3   同主版本且不小于基准 主版本为 0 时改看次版本 0.1 与 0.2 算不兼容
//  >=1.2.3  不小于基准
//版本号只取各段的前导数字 26.2-netcraft 按 26.2 参与比较
internal static class VersionConstraint
{
    //Matches 版本是否满足约束 约束为空或 * 时一律通过
    public static bool Matches(string? constraint, string? version)
    {
        if (string.IsNullOrWhiteSpace(constraint))
            return true;

        var text = constraint.Trim();
        if (text == "*")
            return true;

        var actual = ParseParts(version);

        if (text.StartsWith(">=", StringComparison.Ordinal))
            return Compare(actual, ParseParts(text[2..])) >= 0;

        if (text.StartsWith('^'))
        {
            var baseline = ParseParts(text[1..]);
            if (Compare(actual, baseline) < 0)
                return false;
            return Major(baseline) == 0
                ? Minor(actual) == Minor(baseline)
                : Major(actual) == Major(baseline);
        }

        var parts = ParseParts(text);
        if (parts.Length == 0 || parts.Length > actual.Length)
            return false;
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i] != actual[i])
                return false;
        }
        return true;
    }

    //Major 主版本段 取不到算 0
    private static int Major(int[] parts) => parts.Length > 0 ? parts[0] : 0;

    //Minor 次版本段 取不到算 0
    private static int Minor(int[] parts) => parts.Length > 1 ? parts[1] : 0;

    //Compare 逐段比较 缺的段按 0 补 返回 -1 / 0 / 1
    private static int Compare(int[] left, int[] right)
    {
        var length = Math.Max(left.Length, right.Length);
        for (var i = 0; i < length; i++)
        {
            var a = i < left.Length ? left[i] : 0;
            var b = i < right.Length ? right[i] : 0;
            if (a != b)
                return a < b ? -1 : 1;
        }
        return 0;
    }

    //ParseParts 取版本号各段的数字前缀 遇到不以数字开头的段就截断
    private static int[] ParseParts(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return Array.Empty<int>();

        var parts = new List<int>();
        foreach (var segment in version.Split('.'))
        {
            var digits = 0;
            while (digits < segment.Length && char.IsAsciiDigit(segment[digits]))
                digits++;
            if (digits == 0)
                break;
            parts.Add(int.Parse(segment.AsSpan(0, digits)));
        }
        return parts.ToArray();
    }
}
