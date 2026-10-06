using System.Text;
using NetCraft.Network.Chat;

namespace NetCraft;

//Loc text lookup entry point
//Text keys correspond one-to-one with names in lang/<language code>.json, falling back to the key itself when not found
//Placeholders are %s, the same set as TranslatableContents, filled in order of appearance, not the C# {0} style
//Placed in the Util project because the log layer also needs it
public static class Loc
{
    //Get fetches a text with no placeholders
    public static string Get(string key) => Language.Instance.GetOrDefault(key);

    //Format fetches a text with %s placeholders and fills in arguments in order
    //%% outputs a literal percent sign; extra arguments are ignored, missing ones are left blank without throwing
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
