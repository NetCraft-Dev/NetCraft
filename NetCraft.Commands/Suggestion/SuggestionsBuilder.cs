using System.Globalization;

using NetCraft.Commands.Context;

namespace NetCraft.Commands.Suggestion;

//SuggestionsBuilder 建议构建器对应原版com.mojang.brigadier.suggestion.SuggestionsBuilder
//累积Suggestion条目并构建Suggestions或Task<Suggestions>结果
public sealed class SuggestionsBuilder
{
    private readonly string _input;
    private readonly string _inputLowerCase;
    private readonly int _start;
    private readonly string _remaining;
    private readonly string _remainingLowerCase;
    private readonly List<Suggestion> _result = new();

    public SuggestionsBuilder(string input, string inputLowerCase, int start)
    {
        _input = input;
        _inputLowerCase = inputLowerCase;
        _start = start;
        _remaining = input[start..];
        _remainingLowerCase = inputLowerCase[start..];
    }

    public SuggestionsBuilder(string input, int start)
        : this(input, input.ToLower(CultureInfo.InvariantCulture), start)
    {
    }

    public string Input => _input;
    public int Start => _start;
    public string Remaining => _remaining;
    public string RemainingLowerCase => _remainingLowerCase;

    public Suggestions Build()
    {
        return Suggestions.Create(_input, _result).GetAwaiter().GetResult();
    }

    public Task<Suggestions> BuildFuture() => Suggestions.Create(_input, _result);

    public SuggestionsBuilder Add(string text)
    {
        if (text == _remaining)
        {
            return this;
        }
        _result.Add(new Suggestion(StringRange.Between(_start, _input.Length), text));
        return this;
    }

    public SuggestionsBuilder Add(string text, IMessage tooltip)
    {
        if (text == _remaining)
        {
            return this;
        }
        _result.Add(new Suggestion(StringRange.Between(_start, _input.Length), text, tooltip));
        return this;
    }

    public SuggestionsBuilder Add(int value)
    {
        _result.Add(new IntegerSuggestion(StringRange.Between(_start, _input.Length), value));
        return this;
    }

    public SuggestionsBuilder Add(int value, IMessage tooltip)
    {
        _result.Add(new IntegerSuggestion(StringRange.Between(_start, _input.Length), value, tooltip));
        return this;
    }

    public SuggestionsBuilder Add(Suggestion suggestion)
    {
        _result.Add(suggestion);
        return this;
    }

    public SuggestionsBuilder Add(SuggestionsBuilder other)
    {
        _result.AddRange(other._result);
        return this;
    }

    public SuggestionsBuilder CreateOffset(int start)
    {
        return new SuggestionsBuilder(_input, _inputLowerCase, start);
    }

    public SuggestionsBuilder Restart() => CreateOffset(_start);
}
