namespace NetCraft.Network.Protocol.Login;

//GameProfile 游戏档案简化对应原版 com.mojang.authlib.GameProfile
//原版依赖 authlib 库 NetCraft 未引入简化为 record(UUID, Name)
public sealed record GameProfile(Guid Id, string Name)
{
    //ToString 输出 Name(Id)
    public override string ToString() => $"{Name}({Id})";
}
