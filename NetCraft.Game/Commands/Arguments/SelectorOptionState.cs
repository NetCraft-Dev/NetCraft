using NetCraft.Registry;

namespace NetCraft.Game.Commands.Arguments;

//InvertableSetOptionState invertible set option state, maps to vanilla options.InvertableSetOptionState
//Shared by the name/gamemode/team/type options; a positive value locks to a single value while inversion or tags allow multiple
public sealed class InvertableSetOptionState
{
    private enum Limitation { None, Single, Multiple }

    private Limitation _state = Limitation.None;
    private readonly HashSet<Identifier> _tags = new();

    private bool CanLimitToSingle => _state == Limitation.None;
    private bool CanLimitToMultiple => _state != Limitation.Single;

    public bool CanParsePositiveElement => CanLimitToSingle;
    public bool CanParseNegativeElement => CanLimitToMultiple;
    public bool CanParseAnyTag => CanLimitToMultiple;
    public bool CanParseAny => _state != Limitation.Single;

    public bool CanParseElement(bool inverted)
        => inverted ? CanParseNegativeElement : CanParsePositiveElement;

    public bool CanParseTag(Identifier tag)
        => CanParseAnyTag && !_tags.Contains(tag);

    public void MarkParsedElement(bool inverted)
    {
        if (inverted) _state = Limitation.Multiple;
        else _state = Limitation.Single;
    }

    public void MarkParsedTag(Identifier tag)
    {
        _state = Limitation.Multiple;
        _tags.Add(tag);
    }
}

//SetOnceOptionState set-once option state, maps to vanilla options.SetOnceOptionState
//Prevents repeated parsing for the limit/sort/scores/advancements options
public sealed class SetOnceOptionState
{
    private bool _hasValue;

    public bool CanParse => !_hasValue;

    public void MarkParsed() => _hasValue = true;
}
