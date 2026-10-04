namespace NetCraft;

//Settings 泛型 properties 配置基类
//对应原版 net.minecraft.server.dedicated.Settings<T extends Settings<T>>
//子类继承并通过 Get/GetInt 等方法暴露具体业务字段
//泛型 T 用于约束子类自身与原版 self type 模式对齐
public abstract class Settings<T> where T : Settings<T>, new()
{
    //配置容器子类通过 GetXxx 间接访问
    protected PropertiesConfig Properties { get; } = new();

    //加载 properties 文件到当前实例返回自身
    public T Load(string path)
    {
        Properties.Load(path);
        return (T)this;
    }

    //保存当前配置到文件
    public void Save(string path) => Properties.Save(path);

    //GetOrDefault 查询字符串字段
    protected string GetOrDefault(string key, string defaultValue)
        => Properties.GetOrDefault(key, defaultValue);

    //GetInt 查询 int 字段
    protected int GetInt(string key, int defaultValue)
        => Properties.GetInt(key, defaultValue);

    //GetBool 查询 bool 字段
    protected bool GetBool(string key, bool defaultValue)
        => Properties.GetBool(key, defaultValue);

    //GetFloat 查询 float 字段
    protected float GetFloat(string key, float defaultValue)
        => Properties.GetFloat(key, defaultValue);

    //GetSize 查询带 K/M/G 后缀的字节数
    protected long GetSize(string key, long defaultValue)
        => Properties.GetSize(key, defaultValue);

    //Set 写入字符串字段
    protected void Set(string key, string value) => Properties.Set(key, value);

    //SetInt 写入 int 字段
    protected void SetInt(string key, int value) => Properties.SetInt(key, value);

    //SetBool 写入 bool 字段
    protected void SetBool(string key, bool value) => Properties.SetBool(key, value);
}
