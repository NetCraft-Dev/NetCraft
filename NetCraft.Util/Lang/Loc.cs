using System.Text;
using NetCraft.Network.Chat;

namespace NetCraft;

//Loc 取词入口
//文案键与 lang/<语言码>.json 里的名字一一对应 找不到就退回键本身
//占位符是 %s 与 TranslatableContents 同一套 按出现顺序取参数 不是 C# 的 {0} 风格
//放在 Util 项目是因为日志层也要用
public static class Loc
{
    //Get 取一条文案 没有占位符时用它
    public static string Get(string key) => Language.Instance.GetOrDefault(key);

    //Format 取一条带 %s 占位符的文案并按顺序填入参数
    //%% 输出一个裸百分号 参数多出占位符的部分忽略 少的部分留空不抛
    public static string Format(string key, params object?[] args)
    {
        var format = Language.Instance.GetOrDefault(key);
        if (args.Length == 0) return format;

        var builder = new StringBuilder(format.Length + 16);
        var index = 0;
        for (var i = 0; i < format.Length; i++)
        {
            var c = format[i];
            if (c != '%' || i + 1 >= format.Length)
            {
                builder.Append(c);
                continue;
            }

            var next = format[i + 1];
            if (next == '%')
            {
                builder.Append('%');
                i++;
            }
            else if (next == 's' && index < args.Length)
            {
                builder.Append(args[index++]?.ToString() ?? "null");
                i++;
            }
            else builder.Append(c);
        }

        return builder.ToString();
    }
}
