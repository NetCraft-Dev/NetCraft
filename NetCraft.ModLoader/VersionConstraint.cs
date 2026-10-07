namespace NetCraft.ModLoader;

//VersionConstraint: version constraint evaluation for mod dependencies
//Constraint syntax
//  *        any version, equivalent to omitting the dependency
//  1.2.3    segment prefix match; 1.2 matches both 1.2.3 and 1.2
//  ^1.2.3   same major and not below the baseline; when the major is 0 it compares minor instead, so 0.1 and 0.2 are incompatible
//  >=1.2.3  not below the baseline
//Version numbers take only the leading digits of each segment, so 26.2-netcraft compares as 26.2
internal static class VersionConstraint
{
    //Matches: whether the version satisfies the constraint; empty or * always passes
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

    //Major: the major segment, counted as 0 if absent
    private static int Major(int[] parts) => parts.Length > 0 ? parts[0] : 0;

    //Minor: the minor segment, counted as 0 if absent
    private static int Minor(int[] parts) => parts.Length > 1 ? parts[1] : 0;

    //Compare: compares segment by segment, missing segments are padded with 0, returns -1 / 0 / 1
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

    //ParseParts: takes the digit prefix of each version segment and stops at the first segment not starting with a digit
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
