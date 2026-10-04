namespace NetCraft.Registry;

//Holder所有者标记，判断Holder能否序列化进某注册表上下文
public interface HolderOwner<T>
{
    //默认同一所有者才可序列化
    bool CanSerializeIn(HolderOwner<T> context) => ReferenceEquals(this, context);
}
