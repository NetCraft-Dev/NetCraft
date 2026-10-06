using NetCraft.Registry;

namespace NetCraft.Network.Inventory;

//MenuType menu type, maps to vanilla net.minecraft.world.inventory.MenuType
//Vanilla holds a MenuSupplier delegate and a FeatureFlagSet; the FeatureFlag subsystem is not implemented so it is simplified here
//MENU registry uses weak-typed object to avoid cross-layer circular dependencies; the StreamCodec is hand-written, looking up the registry via Lookup and casting
//The create method depends on AbstractContainerMenu (Game layer) and is extended by the Game layer once the container subsystem is ready
public sealed class MenuType
{
    //STREAM_CODEC encodes MenuType through the MENU registry id, maps to vanilla ByteBufCodecs.registry(Registries.MENU)
    //MENU is a Registry<object>, so decode casts object to MenuType and encode casts MenuType to object
    public static StreamCodec<RegistryFriendlyByteBuf, MenuType> StreamCodec { get; }
        = new MenuTypeRegistryCodec();

    //Create builds the MenuType ResourceKey, aligns with vanilla ResourceKey.create
    public static ResourceKey<object> Create(string name)
        => ResourceKey<object>.Create(Registries.MENU, Identifier.WithDefaultNamespace(name));
}

//MenuTypeRegistryCodec codes MenuType through the MENU registry id
//Maps to vanilla ByteBufCodecs.registry(Registries.MENU); only encodes by id with no Direct form
//MENU is a Registry<object>, so decode casts object to MenuType and encode casts MenuType to object
internal sealed class MenuTypeRegistryCodec : StreamCodec<RegistryFriendlyByteBuf, MenuType>
{
    public MenuType Decode(RegistryFriendlyByteBuf buf)
    {
        int id = buf.ReadVarInt();
        var registry = buf.Lookup(Registries.MENU);
        var holder = registry.Get(id);
        if (holder is null)
            throw new InvalidOperationException($"unknown menu id {id} in MENU");
        return holder.Value as MenuType
            ?? throw new InvalidOperationException($"MENU registry value is not a MenuType: {holder.Value}");
    }

    public void Encode(RegistryFriendlyByteBuf buf, MenuType value)
    {
        var registry = buf.Lookup(Registries.MENU);
        int id = registry.GetId((object)value);
        if (id == IdMap<object>.Default)
            throw new InvalidOperationException($"menu value not registered: {value}");
        buf.WriteVarInt(id);
    }
}
