using NetCraft.Util;

namespace NetCraft.Util.Parsing.Packrat.Commands;

//StringReader项工具对应原版net.minecraft.util.parsing.packrat.commands.StringReaderTerms
//提供word/character/characters等终结符工厂供Grammar构造
public static class StringReaderTerms
{
    public static Term<CommandStringReader> Word(string value)
        => new TerminalWord(value);

    public static Term<CommandStringReader> Character(char value)
        => new CharArrayTerminalCharacters([value], c => c == value);

    public static Term<CommandStringReader> Characters(char v1, char v2)
        => new CharArrayTerminalCharacters([v1, v2], c => c == v1 || c == v2);

    //createReader创建CommandStringReader并设置cursor供DelayedException工厂使用
    public static CommandStringReader CreateReader(string contents, int cursor)
    {
        var reader = new CommandStringReader(contents);
        reader.Cursor = cursor;
        return reader;
    }

    public sealed class TerminalWord : Term<CommandStringReader>
    {
        private readonly string _value;
        private readonly DelayedException<CommandSyntaxException> _error;
        private readonly SuggestionSupplier<CommandStringReader> _suggestions;

        public TerminalWord(string value)
        {
            _value = value;
            _error = DelayedExceptionFactories.Create(
                new SimpleCommandExceptionType($"Expected literal {value}"));
            _suggestions = _ => new[] { _value };
        }

        public bool Parse(ParseState<CommandStringReader> state, Scope scope, Control control)
        {
            state.Input.SkipWhitespace();
            var cursor = state.Mark();
            var value = state.Input.ReadUnquotedString();
            if (value != _value)
            {
                state.ErrorCollector.Store(cursor, _suggestions, _error);
                return false;
            }
            return true;
        }

        public override string ToString() => $"terminal[{_value}]";
    }

    public abstract class TerminalCharacters : Term<CommandStringReader>
    {
        private readonly DelayedException<CommandSyntaxException> _error;
        private readonly SuggestionSupplier<CommandStringReader> _suggestions;

        protected TerminalCharacters(char[] values)
        {
            var joined = string.Join("|", values);
            _error = DelayedExceptionFactories.Create(
                new SimpleCommandExceptionType($"Expected one of {joined}"));
            _suggestions = _ => values.Select(c => c.ToString());
        }

        protected abstract bool IsAccepted(char value);

        public bool Parse(ParseState<CommandStringReader> state, Scope scope, Control control)
        {
            state.Input.SkipWhitespace();
            var cursor = state.Mark();
            if (!state.Input.CanRead() || !IsAccepted(state.Input.Read()))
            {
                state.ErrorCollector.Store(cursor, _suggestions, _error);
                return false;
            }
            return true;
        }
    }

    //CharArrayTerminalCharacters基于字符数组构造TerminalCharacters
    private sealed class CharArrayTerminalCharacters : TerminalCharacters
    {
        private readonly Func<char, bool> _acceptor;

        public CharArrayTerminalCharacters(char[] values, Func<char, bool> acceptor)
            : base(values)
        {
            _acceptor = acceptor;
        }

        protected override bool IsAccepted(char value) => _acceptor(value);
    }
}

//DelayedException静态工厂延迟到此处实现依赖StringReaderTerms.CreateReader
public static class DelayedExceptionFactories
{
    public static DelayedException<CommandSyntaxException> Create(SimpleCommandExceptionType type)
        => (contents, position) => type.CreateWithContext(StringReaderTerms.CreateReader(contents, position));

    public static DelayedException<CommandSyntaxException> Create(DynamicCommandExceptionType type, object argument)
        => (contents, position) => type.CreateWithContext(StringReaderTerms.CreateReader(contents, position), argument);
}
