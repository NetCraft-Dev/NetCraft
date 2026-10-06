namespace NetCraft.Network.Protocol.Login;

//GameProfile simplified game profile, maps to vanilla com.mojang.authlib.GameProfile
//Vanilla depends on the authlib library; NetCraft does not include it, so it is simplified to record(UUID, Name)
public sealed record GameProfile(Guid Id, string Name)
{
    //ToString outputs Name(Id)
    public override string ToString() => $"{Name}({Id})";
}
