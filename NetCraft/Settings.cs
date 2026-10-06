namespace NetCraft;

//Settings generic properties config base class
//Maps to vanilla net.minecraft.server.dedicated.Settings<T extends Settings<T>>
//Subclasses inherit and expose concrete business fields through methods like Get/GetInt
//The generic T constrains the subclass itself, aligning with vanilla's self type pattern
public abstract class Settings<T> where T : Settings<T>, new()
{
    //Config container, accessed indirectly by subclasses via GetXxx
    protected PropertiesConfig Properties { get; } = new();

    //Load a properties file into the current instance and return itself
    public T Load(string path)
    {
        Properties.Load(path);
        return (T)this;
    }

    //Save the current config to a file
    public void Save(string path) => Properties.Save(path);

    //GetOrDefault looks up a string field
    protected string GetOrDefault(string key, string defaultValue)
        => Properties.GetOrDefault(key, defaultValue);

    //GetInt looks up an int field
    protected int GetInt(string key, int defaultValue)
        => Properties.GetInt(key, defaultValue);

    //GetBool looks up a bool field
    protected bool GetBool(string key, bool defaultValue)
        => Properties.GetBool(key, defaultValue);

    //GetFloat looks up a float field
    protected float GetFloat(string key, float defaultValue)
        => Properties.GetFloat(key, defaultValue);

    //GetSize looks up a byte count with a K/M/G suffix
    protected long GetSize(string key, long defaultValue)
        => Properties.GetSize(key, defaultValue);

    //Set writes a string field
    protected void Set(string key, string value) => Properties.Set(key, value);

    //SetInt writes an int field
    protected void SetInt(string key, int value) => Properties.SetInt(key, value);

    //SetBool writes a bool field
    protected void SetBool(string key, bool value) => Properties.SetBool(key, value);
}
