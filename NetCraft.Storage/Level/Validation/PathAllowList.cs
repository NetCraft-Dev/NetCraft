using System.Text;
using System.Text.RegularExpressions;
using NetCraft.Logging;

namespace NetCraft.Storage;

//符号链接目标允许列表对应原版PathAllowList
//一行一条，裸行按前缀比，[glob] 与 [regex] 按模式比，# 开头的行与空行跳过
public sealed class PathAllowList
{
    private const string CommentPrefix = "#";

    private readonly IReadOnlyList<ConfigEntry> _entries;

    private readonly Lazy<Func<string, bool>> _matcher;

    public PathAllowList(IReadOnlyList<ConfigEntry> entries)
    {
        _entries = entries;
        _matcher = new Lazy<Func<string, bool>>(Compile);
    }

    public bool Matches(string path) => _matcher.Value(path);

    //读一份允许列表文本
    public static PathAllowList ReadPlain(TextReader reader)
    {
        var entries = new List<ConfigEntry>();
        while (reader.ReadLine() is { } line)
        {
            var entry = ConfigEntry.Parse(line);
            if (entry is not null)
                entries.Add(entry);
        }
        return new PathAllowList(entries);
    }

    //编译所有条目，一条都没有就恒false
    //编译炸了按原版做法整份退成恒false，只留一条错误日志
    private Func<string, bool> Compile()
    {
        List<Func<string, bool>> matchers;
        try
        {
            matchers = _entries.Select(entry => entry.Compile()).ToList();
        }
        catch (Exception error)
        {
            Log.Error($"Failed to compile file pattern list: {error.Message}");
            return _ => false;
        }
        return matchers.Count switch
        {
            0 => _ => false,
            1 => matchers[0],
            _ => path => matchers.Any(matcher => matcher(path)),
        };
    }

    //允许列表里的一条
    public sealed record ConfigEntry(string Type, string Pattern)
    {
        public Func<string, bool> Compile() => Type switch
        {
            "prefix" => path => path.StartsWith(Pattern, StringComparison.Ordinal),
            "regex" => CompileRegex(),
            "glob" => CompileGlob(),
            _ => throw new ArgumentException($"Unsupported definition type '{Type}'"),
        };

        //空行与注释给null
        public static ConfigEntry? Parse(string definition)
        {
            if (string.IsNullOrWhiteSpace(definition) || definition.StartsWith(CommentPrefix, StringComparison.Ordinal))
                return null;
            if (!definition.StartsWith('['))
                return new ConfigEntry("prefix", definition);
            var split = definition.IndexOf(']', 1);
            if (split == -1)
                throw new ArgumentException($"Unterminated type in line '{definition}'");
            var type = definition[1..split];
            var contents = definition[(split + 1)..];
            return type switch
            {
                "glob" or "regex" or "prefix" => new ConfigEntry(type, contents),
                _ => throw new ArgumentException($"Unsupported definition type in line '{definition}'"),
            };
        }

        private Func<string, bool> CompileRegex()
        {
            var regex = new Regex(Pattern);
            return path => regex.IsMatch(path);
        }

        private Func<string, bool> CompileGlob()
        {
            var regex = new Regex(GlobToRegex(Pattern));
            return path => regex.IsMatch(path);
        }
    }

    //glob 转正则，* 不跨目录分隔符 ** 跨 ? 单个字符，其余原样转义
    private static string GlobToRegex(string glob)
    {
        var builder = new StringBuilder("^");
        for (var index = 0; index < glob.Length; index++)
        {
            var ch = glob[index];
            switch (ch)
            {
                case '*':
                    if (index + 1 < glob.Length && glob[index + 1] == '*')
                    {
                        builder.Append(".*");
                        index++;
                    }
                    else
                    {
                        builder.Append("[^/\\\\]*");
                    }
                    break;
                case '?':
                    builder.Append("[^/\\\\]");
                    break;
                default:
                    builder.Append(Regex.Escape(ch.ToString()));
                    break;
            }
        }
        builder.Append('$');
        return builder.ToString();
    }
}
