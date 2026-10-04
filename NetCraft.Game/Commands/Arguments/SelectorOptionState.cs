using NetCraft.Registry;

namespace NetCraft.Game.Commands.Arguments;

//InvertableSetOptionState 可反转集合选项状态对应原版 options.InvertableSetOptionState
//name/gamemode/team/type选项共用 正向取值后锁定单值 反向取值或标签可多值
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

//SetOnceOptionState 单次选项状态对应原版 options.SetOnceOptionState
//limit/sort/scores/advancements选项防重复解析
public sealed class SetOnceOptionState
{
    private bool _hasValue;

    public bool CanParse => !_hasValue;

    public void MarkParsed() => _hasValue = true;
}
