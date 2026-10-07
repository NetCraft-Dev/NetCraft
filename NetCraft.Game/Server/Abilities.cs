using NetCraft.Game.World.Level;
using NetCraft.Nbt;

namespace NetCraft.Game.Server;

//Abilities player ability state, maps to vanilla net.minecraft.world.entity.player.Abilities
//Creative can fly and instabuild, spectator is invulnerable and forced to fly, survival/adventure revoke everything, maps to vanilla GameType.updatePlayerAbilities
//mayfly is the permission from the mode; flying is the state the player toggled; the two are not the same
//Switching to creative does not touch flying; the save must carry it too or the player falls on rejoin
public sealed class Abilities
{
    //Invulnerable invulnerable, true for creative and spectator, maps to vanilla abilities.invulnerable
    public bool Invulnerable { get; set; }

    //Flying currently flying, reported by the client player_abilities packet, maps to vanilla abilities.flying
    public bool Flying { get; set; }

    //MayFly allowed to fly, true for creative and spectator, maps to vanilla abilities.mayfly
    public bool MayFly { get; set; }

    //Instabuild instabuild, true only for creative, maps to vanilla abilities.instabuild
    public bool Instabuild { get; set; }

    //MayBuild allowed to change blocks, false for adventure and spectator, maps to vanilla abilities.mayBuild
    public bool MayBuild { get; set; } = true;

    //FlyingSpeed/WalkingSpeed vanilla defaults; speed modifiers are not wired up here
    public float FlyingSpeed { get; set; } = 0.05f;
    public float WalkingSpeed { get; set; } = 0.1f;

    //ApplyGameType derives the abilities from the game type, maps to vanilla GameType.updatePlayerAbilities
    //The creative branch does not touch Flying, the flight state the player toggled must be kept; other branches revoke everything
    public void ApplyGameType(GameType gameType)
    {
        if (gameType == GameType.Creative)
        {
            MayFly = true;
            Instabuild = true;
            Invulnerable = true;
        }
        else if (gameType == GameType.Spectator)
        {
            MayFly = true;
            Instabuild = false;
            Invulnerable = true;
            Flying = true;
        }
        else
        {
            MayFly = false;
            Instabuild = false;
            Invulnerable = false;
            Flying = false;
        }
        MayBuild = !gameType.IsBlockPlacingRestricted;
    }

    //WriteTo writes into the player NBT's abilities sub-tag; field names align with the codec of vanilla Abilities.Packed
    //Vanilla marks these fields optionalAlwaysPresent and always writes them
    public void WriteTo(CompoundTag parent)
    {
        var tag = new CompoundTag();
        tag.PutBoolean("invulnerable", Invulnerable);
        tag.PutBoolean("flying", Flying);
        tag.PutBoolean("mayfly", MayFly);
        tag.PutBoolean("instabuild", Instabuild);
        tag.PutBoolean("mayBuild", MayBuild);
        tag.PutFloat("flySpeed", FlyingSpeed);
        tag.PutFloat("walkSpeed", WalkingSpeed);
        parent.Put("abilities", tag);
    }

    //ReadFrom reads back from player NBT; missing fields keep the current value
    public void ReadFrom(CompoundTag parent)
    {
        if (parent.GetCompound("abilities") is not { } tag) return;
        Invulnerable = tag.GetBooleanOr("invulnerable", Invulnerable);
        Flying = tag.GetBooleanOr("flying", Flying);
        MayFly = tag.GetBooleanOr("mayfly", MayFly);
        Instabuild = tag.GetBooleanOr("instabuild", Instabuild);
        MayBuild = tag.GetBooleanOr("mayBuild", MayBuild);
        if (tag.Contains("flySpeed")) FlyingSpeed = tag.GetFloatValue("flySpeed");
        if (tag.Contains("walkSpeed")) WalkingSpeed = tag.GetFloatValue("walkSpeed");
    }
}
