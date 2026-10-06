namespace NetCraft.Commands.Functions;

//StringTemplate 宏模板对应原版 net.minecraft.commands.functions.StringTemplate
//把 $variable 切成字面段与变量名序列 替换时按段拼接
public sealed record StringTemplate(List<string> Segments, List<string> Variables)
{
    //FromString 解析模板没有变量视为错误
    public static StringTemplate FromString(string input)
    {
        var segments = new List<string>();
        var variables = new List<string>();
        var length = input.Length;
        var start = 0;
        var index = input.IndexOf('$');
        while (index != -1)
        {
            if (index == length - 1 || input[index + 1] != '(')
            {
                index = input.IndexOf('$', index + 1);
                continue;
            }
            segments.Add(input[start..index]);
            var variableEnd = input.IndexOf(')', index + 1);
            if (variableEnd == -1)
            {
                throw new ArgumentException("Unterminated macro variable");
            }
            var variable = input.Substring(index + 2, variableEnd - index - 2);
            if (!IsValidVariableName(variable))
            {
                throw new ArgumentException($"Invalid macro variable name '{variable}'");
            }
            variables.Add(variable);
            start = variableEnd + 1;
            index = input.IndexOf('$', start);
        }
        if (start == 0)
        {
            throw new ArgumentException("No variables in macro");
        }
        if (start != length)
        {
            segments.Add(input[start..]);
        }
        return new StringTemplate(segments, variables);
    }

    //IsValidVariableName 变量名只收字母数字与下划线
    public static bool IsValidVariableName(string variable)
    {
        foreach (var character in variable)
        {
            if (char.IsLetterOrDigit(character) || character == '_') continue;
            return false;
        }
        return true;
    }

    //Substitute 按实参拼回一行命令
    public string Substitute(List<string> arguments)
    {
        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < Variables.Count; ++i)
        {
            builder.Append(Segments[i]).Append(arguments[i]);
            CommandFunctions.CheckCommandLineLength(builder);
        }
        if (Segments.Count > Variables.Count)
        {
            builder.Append(Segments[^1]);
        }
        CommandFunctions.CheckCommandLineLength(builder);
        return builder.ToString();
    }
}
