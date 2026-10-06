namespace NetCraft.Commands.Functions;

//CommandFunctions 函数包的非泛型静态工具对应原版 CommandFunction 的静态方法
//C# 泛型接口的静态成员要带类型参数 StringTemplate 这类非泛型类型够不着 故拆到这里
public static class CommandFunctions
{
    //MaxCommandLineLength 单行命令上限
    public const int MaxCommandLineLength = 2_000_000;

    //CheckCommandLineLength 行长熔断 200 万字符对应原版 checkCommandLineLength
    public static void CheckCommandLineLength(System.Text.StringBuilder line)
    {
        if (line.Length > MaxCommandLineLength)
        {
            var truncated = line.ToString(0, Math.Min(512, MaxCommandLineLength));
            throw new InvalidOperationException($"Command too long: {line.Length} characters, contents: {truncated}...");
        }
    }

    public static void CheckCommandLineLength(string line)
    {
        if (line.Length > MaxCommandLineLength)
        {
            var truncated = line[..Math.Min(512, MaxCommandLineLength)];
            throw new InvalidOperationException($"Command too long: {line.Length} characters, contents: {truncated}...");
        }
    }
}
