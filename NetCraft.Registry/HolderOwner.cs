namespace NetCraft.Registry;

//Holder owner marker; determines whether a Holder can be serialized into a registry context
public interface HolderOwner<T>
{
    //Only the same owner can serialize by default
    bool CanSerializeIn(HolderOwner<T> context) => ReferenceEquals(this, context);
}
