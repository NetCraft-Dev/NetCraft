namespace NetCraft.Registry;

//SoundEvents 内置音效常量对应原版 net.minecraft.sounds.SoundEvents
//值即原版音效 id 服务端发内联 holder 时直接用这些常量
public static class SoundEvents
{
    //PlayerAttackStrong 玩家重攻击命中
    public static readonly SoundEvent PlayerAttackStrong = Create("entity.player.attack.strong");

    //PlayerAttackWeak 玩家轻攻击命中
    public static readonly SoundEvent PlayerAttackWeak = Create("entity.player.attack.weak");

    //PlayerAttackCrit 玩家暴击命中
    public static readonly SoundEvent PlayerAttackCrit = Create("entity.player.attack.crit");

    //PlayerAttackKnockback 玩家击退攻击命中
    public static readonly SoundEvent PlayerAttackKnockback = Create("entity.player.attack.knockback");

    //PlayerAttackSweep 玩家横扫攻击
    public static readonly SoundEvent PlayerAttackSweep = Create("entity.player.attack.sweep");

    //PlayerAttackNodamage 玩家攻击未造成伤害
    public static readonly SoundEvent PlayerAttackNodamage = Create("entity.player.attack.nodamage");

    //PlayerHurt 玩家受伤
    public static readonly SoundEvent PlayerHurt = Create("entity.player.hurt");

    //PlayerHurtDrowning 玩家溺水受伤
    public static readonly SoundEvent PlayerHurtDrowning = Create("entity.player.hurt_drowning");

    //PlayerDeath 玩家死亡
    public static readonly SoundEvent PlayerDeath = Create("entity.player.death");

    //PlayerLevelup 玩家升级
    public static readonly SoundEvent PlayerLevelup = Create("entity.player.levelup");

    //PlayerBurp 玩家进食完成
    public static readonly SoundEvent PlayerBurp = Create("entity.player.burp");

    //ItemPickup 物品被拾取
    public static readonly SoundEvent ItemPickup = Create("entity.item.pickup");

    //ItemBreak 物品损坏
    public static readonly SoundEvent ItemBreak = Create("entity.item.break");

    //GenericHurt 通用受伤
    public static readonly SoundEvent GenericHurt = Create("entity.generic.hurt");

    //GenericDeath 通用死亡
    public static readonly SoundEvent GenericDeath = Create("entity.generic.death");

    //PistonExtend 活塞伸出
    public static readonly SoundEvent PistonExtend = Create("block.piston.extend");

    //PistonContract 活塞收回
    public static readonly SoundEvent PistonContract = Create("block.piston.contract");

    //LeverClick 拉杆扳动 通电与断电音高不同
    public static readonly SoundEvent LeverClick = Create("block.lever.click");

    //音符盒 二十种基础乐器加六种生物头模仿 最后一首是自定义头兜底
    public static readonly SoundEvent NoteBlockHarp = Create("block.note_block.harp");
    public static readonly SoundEvent NoteBlockBasedrum = Create("block.note_block.basedrum");
    public static readonly SoundEvent NoteBlockSnare = Create("block.note_block.snare");
    public static readonly SoundEvent NoteBlockHat = Create("block.note_block.hat");
    public static readonly SoundEvent NoteBlockBass = Create("block.note_block.bass");
    public static readonly SoundEvent NoteBlockFlute = Create("block.note_block.flute");
    public static readonly SoundEvent NoteBlockBell = Create("block.note_block.bell");
    public static readonly SoundEvent NoteBlockGuitar = Create("block.note_block.guitar");
    public static readonly SoundEvent NoteBlockChime = Create("block.note_block.chime");
    public static readonly SoundEvent NoteBlockXylophone = Create("block.note_block.xylophone");
    public static readonly SoundEvent NoteBlockIronXylophone = Create("block.note_block.iron_xylophone");
    public static readonly SoundEvent NoteBlockCowBell = Create("block.note_block.cow_bell");
    public static readonly SoundEvent NoteBlockDidgeridoo = Create("block.note_block.didgeridoo");
    public static readonly SoundEvent NoteBlockBit = Create("block.note_block.bit");
    public static readonly SoundEvent NoteBlockBanjo = Create("block.note_block.banjo");
    public static readonly SoundEvent NoteBlockPling = Create("block.note_block.pling");
    public static readonly SoundEvent NoteBlockTrumpet = Create("block.note_block.trumpet");
    public static readonly SoundEvent NoteBlockTrumpetExposed = Create("block.note_block.trumpet_exposed");
    public static readonly SoundEvent NoteBlockTrumpetOxidized = Create("block.note_block.trumpet_oxidized");
    public static readonly SoundEvent NoteBlockTrumpetWeathered = Create("block.note_block.trumpet_weathered");
    public static readonly SoundEvent NoteBlockImitateZombie = Create("block.note_block.imitate.zombie");
    public static readonly SoundEvent NoteBlockImitateSkeleton = Create("block.note_block.imitate.skeleton");
    public static readonly SoundEvent NoteBlockImitateCreeper = Create("block.note_block.imitate.creeper");
    public static readonly SoundEvent NoteBlockImitateEnderDragon = Create("block.note_block.imitate.ender_dragon");
    public static readonly SoundEvent NoteBlockImitateWitherSkeleton = Create("block.note_block.imitate.wither_skeleton");
    public static readonly SoundEvent NoteBlockImitatePiglin = Create("block.note_block.imitate.piglin");

    //UiButtonClick 自定义头颅没有专属音效时用它 对应原版 CUSTOM_HEAD 的音效
    public static readonly SoundEvent UiButtonClick = Create("ui.button.click");

    //活板门开合 木质按材质分六组 对应原版 BlockSetType 各档的 trapdoorOpen/trapdoorClose
    public static readonly SoundEvent WoodenTrapdoorOpen = Create("block.wooden_trapdoor.open");
    public static readonly SoundEvent WoodenTrapdoorClose = Create("block.wooden_trapdoor.close");
    public static readonly SoundEvent IronTrapdoorOpen = Create("block.iron_trapdoor.open");
    public static readonly SoundEvent IronTrapdoorClose = Create("block.iron_trapdoor.close");
    public static readonly SoundEvent CopperTrapdoorOpen = Create("block.copper_trapdoor.open");
    public static readonly SoundEvent CopperTrapdoorClose = Create("block.copper_trapdoor.close");
    public static readonly SoundEvent CherryWoodTrapdoorOpen = Create("block.cherry_wood_trapdoor.open");
    public static readonly SoundEvent CherryWoodTrapdoorClose = Create("block.cherry_wood_trapdoor.close");
    public static readonly SoundEvent BambooWoodTrapdoorOpen = Create("block.bamboo_wood_trapdoor.open");
    public static readonly SoundEvent BambooWoodTrapdoorClose = Create("block.bamboo_wood_trapdoor.close");
    public static readonly SoundEvent NetherWoodTrapdoorOpen = Create("block.nether_wood_trapdoor.open");
    public static readonly SoundEvent NetherWoodTrapdoorClose = Create("block.nether_wood_trapdoor.close");

    //栅栏门开合 默认木料一组 樱桃 竹 下界木各自一组 对应原版 WoodType 的 fenceGateOpen/fenceGateClose
    public static readonly SoundEvent FenceGateOpen = Create("block.fence_gate.open");
    public static readonly SoundEvent FenceGateClose = Create("block.fence_gate.close");
    public static readonly SoundEvent CherryWoodFenceGateOpen = Create("block.cherry_wood_fence_gate.open");
    public static readonly SoundEvent CherryWoodFenceGateClose = Create("block.cherry_wood_fence_gate.close");
    public static readonly SoundEvent BambooWoodFenceGateOpen = Create("block.bamboo_wood_fence_gate.open");
    public static readonly SoundEvent BambooWoodFenceGateClose = Create("block.bamboo_wood_fence_gate.close");
    public static readonly SoundEvent NetherWoodFenceGateOpen = Create("block.nether_wood_fence_gate.open");
    public static readonly SoundEvent NetherWoodFenceGateClose = Create("block.nether_wood_fence_gate.close");

    //门开合 材质分档同活板门 对应原版 BlockSetType 各档的 doorOpen/doorClose
    public static readonly SoundEvent WoodenDoorOpen = Create("block.wooden_door.open");
    public static readonly SoundEvent WoodenDoorClose = Create("block.wooden_door.close");
    public static readonly SoundEvent IronDoorOpen = Create("block.iron_door.open");
    public static readonly SoundEvent IronDoorClose = Create("block.iron_door.close");
    public static readonly SoundEvent CopperDoorOpen = Create("block.copper_door.open");
    public static readonly SoundEvent CopperDoorClose = Create("block.copper_door.close");
    public static readonly SoundEvent CherryWoodDoorOpen = Create("block.cherry_wood_door.open");
    public static readonly SoundEvent CherryWoodDoorClose = Create("block.cherry_wood_door.close");
    public static readonly SoundEvent BambooWoodDoorOpen = Create("block.bamboo_wood_door.open");
    public static readonly SoundEvent BambooWoodDoorClose = Create("block.bamboo_wood_door.close");
    public static readonly SoundEvent NetherWoodDoorOpen = Create("block.nether_wood_door.open");
    public static readonly SoundEvent NetherWoodDoorClose = Create("block.nether_wood_door.close");

    //Create 按原版音效路径建默认命名空间下的事件
    private static SoundEvent Create(string path) => new(Identifier.WithDefaultNamespace(path));
}
