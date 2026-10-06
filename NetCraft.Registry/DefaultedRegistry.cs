namespace NetCraft.Registry;

//Registry interface with a default value; an unregistered key returns the default instead of null
//Extends WritableRegistry, aligning with vanilla DefaultedRegistry extends WritableRegistry
public interface DefaultedRegistry<T> : WritableRegistry<T> where T : class
{
    //Default registry name
    Identifier DefaultKey { get; }
}
