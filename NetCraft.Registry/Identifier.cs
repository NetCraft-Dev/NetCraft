using System.Globalization;

namespace NetCraft.Registry;

//标识符，对应原版Identifier（26.2由ResourceLocation重命名）
//namespace:path格式，readonly struct避免堆分配
public readonly struct Identifier : IEquatable<Identifier>, IComparable<Identifier>
{
    public const char NamespaceSeparator = ':';
    public const string DefaultNamespace = "minecraft";
    public const string RealmsNamespace = "realms";
    public const string AllowedNamespaceCharacters = "[a-z0-9_.-]";

    public string Namespace { get; }
    public string Path { get; }

    private Identifier(string @namespace, string path)
    {
        Namespace = @namespace;
        Path = path;
    }

    //从命名空间和路径构造，校验合法性
    public static Identifier FromNamespaceAndPath(string @namespace, string path)
    {
        AssertValidNamespace(@namespace, path);
        AssertValidPath(@namespace, path);
        return new Identifier(@namespace, path);
    }

    //解析"namespace:path"
    public static Identifier Parse(string identifier) => BySeparator(identifier, NamespaceSeparator);

    //用默认命名空间minecraft构造
    public static Identifier WithDefaultNamespace(string path)
    {
        AssertValidPath(DefaultNamespace, path);
        return new Identifier(DefaultNamespace, path);
    }

    //尝试解析，失败返回null
    public static Identifier? TryParse(string identifier) => TryBySeparator(identifier, NamespaceSeparator);

    //尝试构造，非法返回null
    public static Identifier? TryBuild(string @namespace, string path)
        => IsValidNamespace(@namespace) && IsValidPath(path) ? new Identifier(@namespace, path) : null;

    //按分隔符切分构造
    public static Identifier BySeparator(string identifier, char separator)
    {
        var separatorIndex = identifier.IndexOf(separator);
        if (separatorIndex >= 0)
        {
            var path = identifier.Substring(separatorIndex + 1);
            if (separatorIndex != 0)
            {
                var ns = identifier.Substring(0, separatorIndex);
                return FromNamespaceAndPath(ns, path);
            }
            return WithDefaultNamespace(path);
        }
        return WithDefaultNamespace(identifier);
    }

    //尝试按分隔符切分，非法返回null
    public static Identifier? TryBySeparator(string identifier, char separator)
    {
        var separatorIndex = identifier.IndexOf(separator);
        if (separatorIndex >= 0)
        {
            var path = identifier.Substring(separatorIndex + 1);
            if (!IsValidPath(path)) return null;
            if (separatorIndex != 0)
            {
                var ns = identifier.Substring(0, separatorIndex);
                if (IsValidNamespace(ns)) return new Identifier(ns, path);
                return null;
            }
            return new Identifier(DefaultNamespace, path);
        }
        if (IsValidPath(identifier)) return new Identifier(DefaultNamespace, identifier);
        return null;
    }

    public Identifier WithPath(string newPath)
    {
        AssertValidPath(Namespace, newPath);
        return new Identifier(Namespace, newPath);
    }

    public Identifier WithPath(Func<string, string> modifier) => WithPath(modifier(Path));

    public Identifier WithPrefix(string prefix) => WithPath(prefix + Path);
    public Identifier WithSuffix(string suffix) => WithPath(Path + suffix);

    public override string ToString() => Namespace + ":" + Path;

    public bool Equals(Identifier other) => Namespace == other.Namespace && Path == other.Path;
    public override bool Equals(object? obj) => obj is Identifier o && Equals(o);
    public override int GetHashCode() => (31 * Namespace.GetHashCode()) + Path.GetHashCode();

    //先比path后比namespace，序号比较保证跨文化稳定
    public int CompareTo(Identifier other)
    {
        var result = string.CompareOrdinal(Path, other.Path);
        if (result == 0) result = string.CompareOrdinal(Namespace, other.Namespace);
        return result;
    }

    public static bool operator ==(Identifier left, Identifier right) => left.Equals(right);
    public static bool operator !=(Identifier left, Identifier right) => !left.Equals(right);

    //转调试用文件名
    public string ToDebugFileName() => ToString().Replace('/', '_').Replace(':', '_');

    //转语言键namespace.path
    public string ToLanguageKey() => Namespace + "." + Path;

    //默认命名空间时省略前缀
    public string ToShortLanguageKey() => Namespace == DefaultNamespace ? Path : ToLanguageKey();
    public string ToShortString() => Namespace == DefaultNamespace ? Path : ToString();

    public string ToLanguageKey(string prefix) => prefix + "." + ToLanguageKey();
    public string ToLanguageKey(string prefix, string suffix) => prefix + "." + ToLanguageKey() + "." + suffix;

    public static bool IsAllowedInIdentifier(char c)
        => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || c == '_' || c == ':' || c == '/' || c == '.' || c == '-';

    public static bool IsValidPath(string path)
    {
        foreach (var c in path)
            if (!ValidPathChar(c)) return false;
        return true;
    }

    public static bool IsValidNamespace(string @namespace)
    {
        if (@namespace == "..") return false;
        foreach (var c in @namespace)
            if (!ValidNamespaceChar(c)) return false;
        return true;
    }

    public static bool ValidPathChar(char c)
        => c == '_' || c == '-' || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '/' || c == '.';

    public static bool ValidNamespaceChar(char c)
        => c == '_' || c == '-' || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.';

    private static void AssertValidNamespace(string @namespace, string path)
    {
        if (!IsValidNamespace(@namespace))
            throw new IdentifierException($"Non [a-z0-9_.-] character in namespace of identifier: {@namespace}:{path}");
    }

    private static void AssertValidPath(string @namespace, string path)
    {
        if (!IsValidPath(path))
            throw new IdentifierException($"Non [a-z0-9/._-] character in path of location: {@namespace}:{path}");
    }
}

//标识符解析异常
public class IdentifierException : Exception
{
    public IdentifierException(string message) : base(message) { }
    public IdentifierException(string message, Exception inner) : base(message, inner) { }
}
