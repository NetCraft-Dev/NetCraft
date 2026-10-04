using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Registry;

namespace NetCraft.Game.Server;

//ServerSounds 服务端播放音效对应原版 Level.playSound 的广播语义
//声音走内联 holder 不做距离裁剪 全服直接广播
public static class ServerSounds
{
    //PlaySound 向所有在线玩家播放位置音效
    public static void PlaySound(PlayerList players, SoundEvent sound, SoundSource source, double x, double y, double z, float volume, float pitch)
        => players.BroadcastAll(new ClientboundSoundPacket(sound, source, x, y, z, volume, pitch, Random.Shared.NextInt64()));

    //PlaySoundExcept 向除指定玩家外的所有在线玩家播放位置音效
    public static void PlaySoundExcept(PlayerList players, ServerPlayer? exclude, SoundEvent sound, SoundSource source, double x, double y, double z, float volume, float pitch)
    {
        var packet = new ClientboundSoundPacket(sound, source, x, y, z, volume, pitch, Random.Shared.NextInt64());
        if (exclude is null) players.BroadcastAll(packet);
        else players.BroadcastAllExcept(exclude, packet);
    }

    //PlaySoundEntity 向所有在线玩家播放实体音效 实体 id 由调用方给
    public static void PlaySoundEntity(PlayerList players, SoundEvent sound, SoundSource source, int entityId, float volume, float pitch)
        => players.BroadcastAll(new ClientboundSoundEntityPacket(sound, source, entityId, volume, pitch, Random.Shared.NextInt64()));
}
