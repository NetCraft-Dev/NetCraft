using System.Text;
using System.Text.RegularExpressions;
using NetCraft.Logging;

namespace NetCraft.Storage;

//Symlink target allow list, maps to vanilla PathAllowList
//One entry per line: a bare line matches by prefix, [glob] and [regex] match by pattern, lines starting with # and blank lines are skipped
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

    //Read an allow list from text
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

    //Compile all entries; with none it is always false
    //On a compile failure, fall back to always false as vanilla does, leaving only an error log
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

    //One entry in the allow list
    public sealed record ConfigEntry(string Type, string Pattern)
    {
        public Func<string, bool> Compile() => Type switch
        {
            "prefix" => path => path.StartsWith(Pattern, StringComparison.Ordinal),
            "regex" => CompileRegex(),
            "glob" => CompileGlob(),
            _ => throw new ArgumentException($"Unsupported definition type '{Type}'"),
        };

        //Blank lines and comments give null
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

    //Convert glob to regex: * does not cross a path separator, ** does, ? matches one character, the rest is escaped as is
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
