using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Registry;

namespace NetCraft.Game.Server;

//ServerSounds server-side sound playback, maps to the broadcast semantics of vanilla Level.playSound
//Sounds use an inline holder with no distance culling and broadcast to the whole server
public static class ServerSounds
{
    //PlaySound plays a positional sound to all online players
    public static void PlaySound(PlayerList players, SoundEvent sound, SoundSource source, double x, double y, double z, float volume, float pitch)
        => players.BroadcastAll(new ClientboundSoundPacket(sound, source, x, y, z, volume, pitch, Random.Shared.NextInt64()));

    //PlaySoundExcept plays a positional sound to all online players except the given one
    public static void PlaySoundExcept(PlayerList players, ServerPlayer? exclude, SoundEvent sound, SoundSource source, double x, double y, double z, float volume, float pitch)
    {
        var packet = new ClientboundSoundPacket(sound, source, x, y, z, volume, pitch, Random.Shared.NextInt64());
        if (exclude is null) players.BroadcastAll(packet);
        else players.BroadcastAllExcept(exclude, packet);
    }

    //PlaySoundEntity plays an entity sound to all online players; the entity id is given by the caller
    public static void PlaySoundEntity(PlayerList players, SoundEvent sound, SoundSource source, int entityId, float volume, float pitch)
        => players.BroadcastAll(new ClientboundSoundEntityPacket(sound, source, entityId, volume, pitch, Random.Shared.NextInt64()));
}
