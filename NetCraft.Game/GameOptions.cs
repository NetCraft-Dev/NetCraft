using NetCraft;

namespace NetCraft.Game;

//GameOptions startup argument container for the Game module
//Subscribes to the LaunchOptions.UnhandledArgument event to accumulate arguments the kernel does not recognize
//Parse rule: --flag adds to Flags, --opt value or --opt=value adds to Options
public sealed class GameOptions
{
    private readonly HashSet<string> _flags = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _options = new(StringComparer.Ordinal);
    private readonly List<string> _positionals = new();
    private bool _subscribed;

    //Flags boolean argument set, e.g. --demo --fullscreen
    public IReadOnlySet<string> Flags => _flags;

    //Options dictionary of valued arguments, e.g. --game-dir /path
    public IReadOnlyDictionary<string, string> Options => _options;

    //Positionals bare tokens without a -- prefix
    public IReadOnlyList<string> Positionals => _positionals;

    //Subscribe subscribes to the LaunchOptions.UnhandledArgument event
    //Must be subscribed before NetCraftKernel.Initialize is called to receive the events
    public void Subscribe()
    {
        if (_subscribed) return;
        LaunchOptions.UnhandledArgument += OnUnhandled;
        _subscribed = true;
    }

    //Unsubscribe normally not needed unless for test isolation
    public void Unsubscribe()
    {
        if (!_subscribed) return;
        LaunchOptions.UnhandledArgument -= OnUnhandled;
        _subscribed = false;
    }

    //HasFlag whether the given boolean argument is present
    public bool HasFlag(string name) => _flags.Contains(name);

    //TryGetOption queries a valued argument
    public bool TryGetOption(string name, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out string value)
        => _options.TryGetValue(name, out value);

    //GetOptionOrDefault queries a valued argument and returns the default when missing
    public string GetOptionOrDefault(string name, string defaultValue)
        => _options.TryGetValue(name, out var v) ? v : defaultValue;

    //Clear clears all accumulated arguments, for test isolation
    public void Clear()
    {
        _flags.Clear();
        _options.Clear();
        _positionals.Clear();
    }

    //OnUnhandled event callback accumulates unrecognized arguments
    //Parse rule: --flag adds to Flags, --opt value takes the next token as the value, --opt=value adds to Options
    //A bare token adds to Positionals
    private void OnUnhandled(object? sender, LaunchArgEventArgs e)
    {
        var token = e.Token;
        if (!token.StartsWith("--", StringComparison.Ordinal))
        {
            //A bare token may be the value of the previous --opt, handled by the internal _pendingOption
            if (_pendingOption is not null)
            {
                _options[_pendingOption] = token;
                _pendingOption = null;
            }
            else
            {
                _positionals.Add(token);
            }
            return;
        }

        var body = token.AsSpan(2);
        int eqIdx = body.IndexOf('=');
        if (eqIdx >= 0)
        {
            var name = body.Slice(0, eqIdx).ToString();
            var value = body.Slice(eqIdx + 1).ToString();
            _options[name] = value;
            _pendingOption = null;
            return;
        }

        var key = body.ToString();
        if (_pendingOption is not null)
        {
            //The previous --opt got no value and the current one is a new --flag, so the previous is downgraded to a flag
            _flags.Add(_pendingOption);
        }
        _pendingOption = key;
    }

    //FlushPending after parsing ends, treats a still-pending --opt as a flag
    public void FlushPending()
    {
        if (_pendingOption is not null)
        {
            _flags.Add(_pendingOption);
            _pendingOption = null;
        }
    }

    private string? _pendingOption;
}
