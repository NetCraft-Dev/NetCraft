using NetCraft.Game.World.Level;
using NetCraft.Nbt;

namespace NetCraft.Game.Server;

//Abilities 玩家能力状态对应原版 net.minecraft.world.entity.player.Abilities
//创造可飞行可秒建 旁观无敌且强制飞行 生存冒险一律收回 对应原版 GameType.updatePlayerAbilities
//mayfly 是模式给的许可 flying 是玩家自己按出来的状态 两者不是一回事
//创造切模式时不动 flying 存档里也得带着它 否则重进就掉下来
public sealed class Abilities
{
    //Invulnerable 无敌 创造与旁观为真 对应原版 abilities.invulnerable
    public bool Invulnerable { get; set; }

    //Flying 正在飞行 由客户端 player_abilities 包上报 对应原版 abilities.flying
    public bool Flying { get; set; }

    //MayFly 允许飞行 创造与旁观为真 对应原版 abilities.mayfly
    public bool MayFly { get; set; }

    //Instabuild 秒建 仅创造为真 对应原版 abilities.instabuild
    public bool Instabuild { get; set; }

    //MayBuild 允许改动方块 冒险与旁观为假 对应原版 abilities.mayBuild
    public bool MayBuild { get; set; } = true;

    //FlyingSpeed/WalkingSpeed 原版默认值 本作未接入速度修饰符
    public float FlyingSpeed { get; set; } = 0.05f;
    public float WalkingSpeed { get; set; } = 0.1f;

    //ApplyGameType 按游戏模式推导能力 对应原版 GameType.updatePlayerAbilities
    //创造分支不动 Flying 玩家自己按出来的飞行状态要留着 其余分支一律收回
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

    //WriteTo 写进玩家 NBT 的 abilities 子标签 字段名对齐原版 Abilities.Packed 的 codec
    //原版这些字段是 optionalAlwaysPresent 一律写出
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

    //ReadFrom 从玩家 NBT 读回 缺字段保持当前值
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
