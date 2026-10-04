using NetCraft.Commands;
using NetCraft.Commands.Exceptions;
using NetCraft.Game.Server;
using NetCraft.Game.World.Entity;
using NetCraft.Primitives;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//EntitySelectorParser 选择器解析器对应原版 net.minecraft.commands.arguments.selector.EntitySelectorParser
//解析@选择器与玩家名/UUID组装EntitySelector 玩家模型下level按经验等级0匹配
//suggestions建议状态机省略 服务端命令树不置位custom suggestions客户端不会请求选择器建议
public sealed class EntitySelectorParser
{
    public const char SyntaxSelectorStart = '@';
    private const char SyntaxOptionsStart = '[';
    private const char SyntaxOptionsEnd = ']';
    public const char SyntaxOptionsKeyValueSeparator = '=';
    private const char SyntaxOptionsSeparator = ',';
    public const char SyntaxNot = '!';
    public const char SyntaxTag = '#';

    public static readonly SimpleCommandExceptionType ErrorInvalidNameOrUuid =
        new(new TranslatableMessage("argument.entity.invalid"));
    public static readonly DynamicCommandExceptionType ErrorUnknownSelectorType =
        new(type => new TranslatableMessage("argument.entity.selector.unknown", type));
    public static readonly SimpleCommandExceptionType ErrorMissingSelectorType =
        new(new TranslatableMessage("argument.entity.selector.missing"));
    public static readonly SimpleCommandExceptionType ErrorSelectorsNotAllowed =
        new(new TranslatableMessage("argument.entity.selector.not_allowed"));
    public static readonly SimpleCommandExceptionType ErrorExpectedEndOfOptions =
        new(new TranslatableMessage("argument.entity.options.unterminated"));
    public static readonly DynamicCommandExceptionType ErrorExpectedOptionValue =
        new(name => new TranslatableMessage("argument.entity.options.valueless", name));

    //OrderNearest 按到基准点距离升序
    public static readonly EntitySelector.Orderer OrderNearest =
        (pos, list) => list.Sort((a, b) => a.Position.DistanceToSqr(pos).CompareTo(b.Position.DistanceToSqr(pos)));

    //OrderFurthest 按到基准点距离降序
    public static readonly EntitySelector.Orderer OrderFurthest =
        (pos, list) => list.Sort((a, b) => b.Position.DistanceToSqr(pos).CompareTo(a.Position.DistanceToSqr(pos)));

    //OrderRandom 洗牌随机排序
    public static readonly EntitySelector.Orderer OrderRandom =
        (_, list) =>
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = Random.Shared.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        };

    private readonly StringReader _reader;
    private readonly bool _allowSelectors;
    private int _maxResults;
    private bool _includesEntities;
    private bool _worldLimited;
    private MinMaxBounds.Doubles? _distance;
    private MinMaxBounds.Ints? _level;
    private double? _x;
    private double? _y;
    private double? _z;
    private double? _deltaX;
    private double? _deltaY;
    private double? _deltaZ;
    private MinMaxBounds.FloatDegrees? _rotX;
    private MinMaxBounds.FloatDegrees? _rotY;
    private bool _currentEntity;
    private string? _playerName;
    private Guid? _entityUuid;
    private EntityType<object>? _type;
    private bool _usesSelectors;
    private readonly List<Predicate<CommandTarget>> _predicates = new();
    private EntitySelector.Orderer _order = EntitySelector.OrderArbitrary;
    private readonly InvertableSetOptionState _nameOption = new();
    private readonly SetOnceOptionState _limitedOption = new();
    private readonly SetOnceOptionState _sortedOption = new();
    private readonly InvertableSetOptionState _gamemodeOption = new();
    private readonly InvertableSetOptionState _teamOption = new();
    private readonly InvertableSetOptionState _typeOption = new();
    private readonly SetOnceOptionState _scoresOption = new();
    private readonly SetOnceOptionState _advancementsOption = new();

    public EntitySelectorParser(StringReader reader, bool allowSelectors)
    {
        _reader = reader;
        _allowSelectors = allowSelectors;
    }

    //Parse 入口@走选择器分支其余按玩家名/UUID解析后组装 选择器需要权限放行
    public EntitySelector Parse()
    {
        if (_reader.CanRead() && _reader.Peek() == '@')
        {
            if (!_allowSelectors)
                throw ErrorSelectorsNotAllowed.CreateWithContext(_reader);
            _reader.Skip();
            ParseSelector();
        }
        else
        {
            ParseNameOrUuid();
        }
        FinalizePredicates();
        return GetSelector();
    }

    //ParseSelector 六选择器分支maxResults/includesEntities/order与存活谓词按原版逐一对齐
    private void ParseSelector()
    {
        _usesSelectors = true;
        if (!_reader.CanRead())
            throw ErrorMissingSelectorType.CreateWithContext(_reader);
        var start = _reader.Cursor;
        var type = _reader.Read();
        switch (type)
        {
            case 'a':
                _maxResults = int.MaxValue;
                _includesEntities = false;
                _order = EntitySelector.OrderArbitrary;
                LimitToType(EntityTypes.PLAYER);
                break;
            case 'e':
                _maxResults = int.MaxValue;
                _includesEntities = true;
                _order = EntitySelector.OrderArbitrary;
                //仅存活过滤 玩家恒存活
                _predicates.Add(_ => true);
                break;
            case 'n':
                _maxResults = 1;
                _includesEntities = true;
                _order = OrderNearest;
                _predicates.Add(_ => true);
                break;
            case 'p':
                _maxResults = 1;
                _includesEntities = false;
                _order = OrderNearest;
                LimitToType(EntityTypes.PLAYER);
                break;
            case 'r':
                _maxResults = 1;
                _includesEntities = false;
                _order = OrderRandom;
                LimitToType(EntityTypes.PLAYER);
                break;
            case 's':
                _maxResults = 1;
                _includesEntities = true;
                _currentEntity = true;
                break;
            default:
                _reader.SetCursor(start);
                throw ErrorUnknownSelectorType.CreateWithContext(_reader, "@" + type);
        }
        if (_reader.CanRead() && _reader.Peek() == SyntaxOptionsStart)
        {
            _reader.Skip();
            ParseOptions();
        }
    }

    //ParseNameOrUuid 玩家名或标准连字符UUID UUID成功按实体语义否则按玩家名1-16字符
    private void ParseNameOrUuid()
    {
        var start = _reader.Cursor;
        var name = _reader.ReadString();
        if (Guid.TryParseExact(name, "D", out var uuid))
        {
            _entityUuid = uuid;
            _includesEntities = true;
        }
        else
        {
            if (name.Length is < 1 or > 16)
            {
                _reader.SetCursor(start);
                throw ErrorInvalidNameOrUuid.CreateWithContext(_reader);
            }
            _includesEntities = false;
            _playerName = name;
        }
        _maxResults = 1;
    }

    //ParseOptions 选项列表解析按原版字节码还原
    //空[]合法 非法key或不可用选项由EntitySelectorOptions.Get抛 选项间必须逗号分隔
    private void ParseOptions()
    {
        _reader.SkipWhitespace();
        while (_reader.CanRead() && _reader.Peek() != SyntaxOptionsEnd)
        {
            _reader.SkipWhitespace();
            var start = _reader.Cursor;
            var key = _reader.ReadString();
            var modifier = EntitySelectorOptions.Get(this, key, start);
            _reader.SkipWhitespace();
            if (!_reader.CanRead() || _reader.Peek() != SyntaxOptionsKeyValueSeparator)
            {
                _reader.SetCursor(start);
                throw ErrorExpectedOptionValue.CreateWithContext(_reader, key);
            }
            _reader.Skip();
            _reader.SkipWhitespace();
            modifier(this);
            _reader.SkipWhitespace();
            if (_reader.CanRead())
            {
                if (_reader.Peek() == SyntaxOptionsSeparator)
                {
                    _reader.Skip();
                }
                else if (_reader.Peek() != SyntaxOptionsEnd)
                {
                    throw ErrorExpectedEndOfOptions.CreateWithContext(_reader);
                }
            }
        }
        if (!_reader.CanRead())
            throw ErrorExpectedEndOfOptions.CreateWithContext(_reader);
        _reader.Skip();
    }

    //FinalizePredicates 解析收尾把旋转与等级条件追加进谓词链
    private void FinalizePredicates()
    {
        if (_rotX is not null)
            _predicates.Add(CreateRotationPredicate(_rotX, e => e.Pitch));
        if (_rotY is not null)
            _predicates.Add(CreateRotationPredicate(_rotY, e => e.Yaw));
        if (_level is not null)
        {
            var level = _level;
            //玩家经验等级未接入恒按0匹配
            _predicates.Add(e => level.Matches(0));
        }
    }

    //CreateRotationPredicate 角度谓词min默认0max默认359环绕后min>max表示跨±180区间
    private static Predicate<CommandTarget> CreateRotationPredicate(MinMaxBounds.FloatDegrees range,
        Func<CommandTarget, float> getter)
    {
        var min = WrapDegrees(range.Min ?? 0f);
        var max = WrapDegrees(range.Max ?? 359f);
        return e =>
        {
            var rotation = WrapDegrees(getter(e));
            return min > max ? rotation >= min || rotation <= max : rotation >= min && rotation <= max;
        };
    }

    //WrapDegrees 角度归一到[-180,180)对应原版Mth.wrapDegrees
    public static float WrapDegrees(float value)
    {
        var m = value % 360f;
        if (m >= 180f) m -= 360f;
        if (m < -180f) m += 360f;
        return m;
    }

    //GetSelector 组装EntitySelector dx/dy/dz与distance.max构造相对aabb x/y/z构造坐标覆盖
    public EntitySelector GetSelector()
    {
        AABB? aabb;
        if (_deltaX is not null || _deltaY is not null || _deltaZ is not null)
        {
            aabb = CreateAabb(_deltaX ?? 0.0, _deltaY ?? 0.0, _deltaZ ?? 0.0);
        }
        else if (_distance is not null && _distance.Max is not null)
        {
            var maxRange = _distance.Max.Value;
            aabb = new AABB(-maxRange, -maxRange, -maxRange, maxRange + 1.0, maxRange + 1.0, maxRange + 1.0);
        }
        else
        {
            aabb = null;
        }
        Func<Vec3, Vec3> position;
        if (_x is null && _y is null && _z is null)
        {
            position = o => o;
        }
        else
        {
            position = o => new Vec3(_x ?? o.X, _y ?? o.Y, _z ?? o.Z);
        }
        return new EntitySelector(_maxResults, _includesEntities, _worldLimited,
            _predicates.ToList(), _distance, position, aabb, _order, _currentEntity,
            _playerName, _entityUuid, _type, _usesSelectors);
    }

    //CreateAabb 负向delta取负值到0正向取0到值+1 构造以基准点为原点的相对盒
    private static AABB CreateAabb(double x, double y, double z)
    {
        var xNeg = x < 0.0;
        var yNeg = y < 0.0;
        var zNeg = z < 0.0;
        var xMin = xNeg ? x : 0.0;
        var yMin = yNeg ? y : 0.0;
        var zMin = zNeg ? z : 0.0;
        var xMax = (xNeg ? 0.0 : x) + 1.0;
        var yMax = (yNeg ? 0.0 : y) + 1.0;
        var zMax = (zNeg ? 0.0 : z) + 1.0;
        return new AABB(xMin, yMin, zMin, xMax, yMax, zMax);
    }

    //ShouldInvertValue 消费'!'前缀返回是否反转
    public bool ShouldInvertValue()
    {
        _reader.SkipWhitespace();
        if (_reader.CanRead() && _reader.Peek() == SyntaxNot)
        {
            _reader.Skip();
            _reader.SkipWhitespace();
            return true;
        }
        return false;
    }

    //IsTag 消费'#'前缀返回是否标签引用
    public bool IsTag()
    {
        _reader.SkipWhitespace();
        if (_reader.CanRead() && _reader.Peek() == SyntaxTag)
        {
            _reader.Skip();
            _reader.SkipWhitespace();
            return true;
        }
        return false;
    }

    public StringReader Reader => _reader;

    public void AddPredicate(Predicate<CommandTarget> predicate) => _predicates.Add(predicate);

    public void SetWorldLimited() => _worldLimited = true;

    public MinMaxBounds.Doubles? Distance => _distance;
    public void SetDistance(MinMaxBounds.Doubles value) => _distance = value;

    public MinMaxBounds.Ints? Level => _level;
    public void SetLevel(MinMaxBounds.Ints value) => _level = value;

    public MinMaxBounds.FloatDegrees? RotX => _rotX;
    public void SetRotX(MinMaxBounds.FloatDegrees value) => _rotX = value;

    public MinMaxBounds.FloatDegrees? RotY => _rotY;
    public void SetRotY(MinMaxBounds.FloatDegrees value) => _rotY = value;

    public double? X => _x;
    public void SetX(double value) => _x = value;

    public double? Y => _y;
    public void SetY(double value) => _y = value;

    public double? Z => _z;
    public void SetZ(double value) => _z = value;

    public double? DeltaX => _deltaX;
    public void SetDeltaX(double value) => _deltaX = value;

    public double? DeltaY => _deltaY;
    public void SetDeltaY(double value) => _deltaY = value;

    public double? DeltaZ => _deltaZ;
    public void SetDeltaZ(double value) => _deltaZ = value;

    public void SetMaxResults(int value) => _maxResults = value;

    public void SetIncludesEntities(bool value) => _includesEntities = value;

    public EntitySelector.Orderer Order => _order;
    public void SetOrder(EntitySelector.Orderer value) => _order = value;

    public bool IsCurrentEntity => _currentEntity;

    //LimitToType 类型限定 @a/@p/@r与type选项正向取值用
    public void LimitToType(EntityType<object> type) => _type = type;

    public InvertableSetOptionState NameOption => _nameOption;
    public SetOnceOptionState LimitedOption => _limitedOption;
    public SetOnceOptionState SortedOption => _sortedOption;
    public InvertableSetOptionState GamemodeOption => _gamemodeOption;
    public InvertableSetOptionState TeamOption => _teamOption;
    public InvertableSetOptionState TypeOption => _typeOption;
    public SetOnceOptionState ScoresOption => _scoresOption;
    public SetOnceOptionState AdvancementsOption => _advancementsOption;
}
