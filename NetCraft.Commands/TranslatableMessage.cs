namespace NetCraft.Commands;

//TranslatableMessage translatable message
//Maps to vanilla using Component.translatable as a command exception message; the translation is resolved only at display time
//When a key is missing Loc returns it unchanged, so a missing translation shows up as a raw key in the error and is obvious at a glance
public sealed class TranslatableMessage : IMessage
{
    private readonly string _key;
    private readonly object?[] _arguments;

    public TranslatableMessage(string key, params object?[] arguments)
    {
        _key = key;
        _arguments = arguments;
    }

    //Key key into the language table
    public string Key => _key;

    public string GetString() => _arguments.Length == 0 ? Loc.Get(_key) : Loc.Format(_key, _arguments);

    public override string ToString() => GetString();
}
