using NetCraft.Game.Commands;
using NetCraft.Network.Protocol.Common;
using NetCraft.Network.Protocol.Cookie;
using NetCraft.Network.Protocol.Login;
using NetCraft.Network.Protocol.Ping;
using NetCraft.Game.Server;
using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Items;
using NetCraft.Game.World.Level;
using NetCraft.Game.World.Level.Block;
using NetCraft.Logging;
using NetCraft.Network;
using NetCraft.Network.Chat;
using NetCraft.Network.Component;
using NetCraft.Network.Protocol;
using NetCraft.Primitives;
using NetCraft.Storage;
using NetCraft.Util;
//别名避免 NetCraft.Registry 里的 Entity/Block 等 stub 与业务类型撞名
using SoundEvents = NetCraft.Registry.SoundEvents;
using SoundSource = NetCraft.Registry.SoundSource;
using BlockState = NetCraft.Registry.State.BlockState;

namespace NetCraft.Game.Network.Protocol.Game;

//ServerGamePacketListenerImpl 服务端 play 阶段监听器实现
//对应原版 ServerGamePacketListenerImpl
//核心 HandleChat/HandleMovePlayer/HandleClientCommand/HandleAcceptTeleportation 等注册包 Log.Debug
//其余继承自 ServerCommonPacketListener/ServerCookiePacketListener/ServerPingPacketListener 的方法暂空实现
//Protocol 显式返回 Play 因继承链默认是 Configuration
public sealed class ServerGamePacketListenerImpl : ServerGamePacketListener, TickablePacketListener
{
    private readonly Connection _connection;
    private readonly GameProfile _profile;
    //_ackBlockChangesUpTo 待回执的方块变更序号 -1 表示无 对应原版 ackBlockChangesUpTo
    private int _ackBlockChangesUpTo = -1;
    //_tickCount 监听器自身刻计数 对应原版 ServerGamePacketListenerImpl.tickCount
    private int _tickCount;
    //_awaitingTeleport 等待客户端确认的传送 id -1 表示无 对应原版 awaitingTeleport
    private int _awaitingTeleport = -1;
    //_awaitingPositionFromClient 等待确认的目标位置 对应原版 awaitingPositionFromClient
    //非空期间客户端上报的坐标一律按传送前的在途包处理 位置不予采纳
    private Vec3? _awaitingPositionFromClient;
    //_awaitingTeleportTime 上次发送位置包的刻号 超过 20 刻未确认重发 对应原版 awaitingTeleportTime
    private int _awaitingTeleportTime;
    //_isDestroyingBlock 是否正在挖掘 对应原版 isDestroyingBlock
    private bool _isDestroyingBlock;
    //_destroyPos 当前挖掘目标 对应原版 destroyPos
    private BlockPos _destroyPos = BlockPos.Zero;
    //_destroyProgressStart 本次挖掘的起始刻号 进度按它与当前刻号的差值算 对应原版 destroyProgressStart
    private int _destroyProgressStart;
    //_lastSentDestroyState 上次下发的裂纹阶段 0-10 阶段不变不重发 对应原版 lastSentState
    private int _lastSentDestroyState = -1;
    //_hasDelayedDestroy 客户端已松手但服务端估算进度还没到时挂起的收尾破坏 对应原版 hasDelayedDestroy
    private bool _hasDelayedDestroy;
    private BlockPos _delayedDestroyPos = BlockPos.Zero;
    private int _delayedDestroyTickStart;

    public ServerGamePacketListenerImpl(Connection connection, GameProfile profile)
    {
        _connection = connection;
        _profile = profile;
    }

    //显式返回 Play 因 ServerCookiePacketListener 默认 Protocol 是 Configuration
    ConnectionProtocol PacketListener.Protocol => ConnectionProtocol.Play;

    //TickListener 每 tick 把累积的方块变更序号回执客户端 对应原版 ServerGamePacketListenerImpl.tick
    //客户端靠它结束本地预测 缺回执时预测的方块会一直留在客户端直到区块重新加载
    public void TickListener()
    {
        _tickCount++;
        //传送迟迟没被确认就原地重发 对应原版 updateAwaitingTeleport
        if (_awaitingPositionFromClient is not null && _tickCount - _awaitingTeleportTime > AwaitingTeleportTimeoutTicks)
            ResendTeleport();
        TickDestroyProgress();
        if (_ackBlockChangesUpTo < 0) return;
        _connection.Send(new ClientboundBlockChangedAckPacket(_ackBlockChangesUpTo));
        _ackBlockChangesUpTo = -1;
    }

    //AwaitingTeleportTimeoutTicks 传送重发间隔 对应原版 20 刻
    private const int AwaitingTeleportTimeoutTicks = 20;

    //Teleport 服务端发起传送 对应原版 ServerGamePacketListenerImpl.teleport
    //target/yaw/pitch 是换算好的绝对量 先落到玩家状态并记为等待确认的目标
    //包内下发相对分量 客户端按 relatives 叠加自身当前值复原绝对位置
    //等待期间客户端上报的旧坐标不再采纳 在途包不会把玩家打回传送前的位置
    public void Teleport(Vec3 target, float yaw, float pitch,
        double relX, double relY, double relZ, float relYaw, float relPitch, int relatives)
    {
        var player = Player;
        if (player is null) return;
        player.Position = target;
        player.Yaw = yaw;
        player.Pitch = pitch;
        _awaitingPositionFromClient = target;
        _awaitingTeleport = NextTeleportId();
        SendPositionPacket(relX, relY, relZ, relYaw, relPitch, relatives);
    }

    //ResendTeleport 超时重发 对应原版 updateAwaitingTeleport 的重发分支
    //重发走绝对量 relatives 清空 客户端按自身当前朝向直接吸附到目标位置
    private void ResendTeleport()
    {
        var player = Player;
        if (player is null || _awaitingPositionFromClient is not { } target) return;
        _awaitingTeleport = NextTeleportId();
        SendPositionPacket(target.X, target.Y, target.Z, player.Yaw, player.Pitch, 0);
    }

    //NextTeleportId 传送 id 自增 溢出回到 0 对应原版 teleport 的取值
    private int NextTeleportId()
        => _awaitingTeleport == int.MaxValue ? 0 : _awaitingTeleport + 1;

    //SendPositionPacket 下发位置包并记录发送刻号
    private void SendPositionPacket(double x, double y, double z, float yRot, float xRot, int relatives)
    {
        _awaitingTeleportTime = _tickCount;
        _connection.Send(new ClientboundPlayerPositionPacket(x, y, z, yRot, xRot, relatives, _awaitingTeleport));
    }

    //AckBlockChanges 记录待回执的方块变更序号取最大值 对应原版 ackBlockChangesUpTo
    private void AckBlockChanges(int sequence)
    {
        if (sequence > _ackBlockChangesUpTo) _ackBlockChangesUpTo = sequence;
    }

    //Player 关联的玩家对象 由 DedicatedServer.TransitionToGame 注入
    //用于把客户端回包落到玩家状态上
    public ServerPlayer? Player { get; set; }

    //Players 玩家列表 由 DedicatedServer.TransitionToGame 注入
    //方块变更需要向在线玩家广播
    public PlayerList? Players { get; set; }

    //BlockEntities 方块实体集合 由 DedicatedServer.TransitionToGame 注入
    //方块被替换或破坏时要同步清理旧方块实体
    public BlockEntityManager? BlockEntities { get; set; }

    //Commands 命令管理器 由 DedicatedServer.TransitionToGame 注入
    public CommandManager? Commands { get; set; }

    //HandlePlayerAction 玩家动作 对应原版 handlePlayerAction
    //START 分支: 创造模式是瞬时破坏 客户端挖完不再发 STOP 必须在此处理
    //生存模式先算一次进度 够 1 的瞬时方块立即破坏 否则进入每刻推进的挖掘状态
    //ABORT/STOP 分支按原版清状态并收尾 破坏进度包由本类统一广播
    public void HandlePlayerAction(ServerboundPlayerActionPacket packet)
    {
        if (Player is null || Players is null) return;
        if (Player.Level is not PersistentServerLevel level) return;
        //原版对三种破坏动作都先记回执 由 TickListener 统一发出
        AckBlockChanges(packet.Sequence);
        switch (packet.Action)
        {
            case ServerboundPlayerActionPacket.ActionType.StartDestroyBlock:
                StartDestroyBlock(level, packet.Pos);
                return;
            case ServerboundPlayerActionPacket.ActionType.StopDestroyBlock:
                StopDestroyBlock(level, packet.Pos);
                return;
            case ServerboundPlayerActionPacket.ActionType.AbortDestroyBlock:
                AbortDestroyBlock(level, packet.Pos);
                return;
            case ServerboundPlayerActionPacket.ActionType.DropItem:
            {
                var drop = Player.Drop(false);
                Log.Debug($"Drop item single profile={_profile.Name} drop={drop?.EntityId}");
                return;
            }
            case ServerboundPlayerActionPacket.ActionType.DropAllItems:
            {
                var drop = Player.Drop(true);
                Log.Debug($"Drop item stack profile={_profile.Name} drop={drop?.EntityId}");
                return;
            }
        }
    }

    //StartDestroyBlock 开始破坏方块 对应原版 handleBlockBreakAction 的 START 分支
    //创造模式瞬时破坏 生存模式把目标记入挖掘状态由 TickDestroyProgress 每刻推进
    //瞬时方块(进度一次就够 1) 直接破坏不发裂纹包
    private void StartDestroyBlock(PersistentServerLevel level, BlockPos pos)
    {
        var player = Player!;
        //原版在开始挖的这一刻先给方块一次 attack 回调 音符盒靠它左键试听
        if (level.GetBlockState(pos) is { Owner: BlockBehaviour behaviour } attacked)
            behaviour.OnAttack(level, player, pos, attacked);
        if (player.GameType == GameType.Creative)
        {
            var broken = ServerBlockUpdates.BreakBlock(level, Players!, player, pos);
            Log.Debug($"Break block (creative) {pos} result={broken} profile={_profile.Name}");
            return;
        }
        _destroyProgressStart = _tickCount;
        var state = level.GetBlockState(pos);
        //空位置按原版进度 1 处理 不进挖掘状态
        var progress = 1f;
        if (state is not null && state.Value != Blocks.AIR.DefaultBlockState)
            progress = BlockBehaviour.GetDestroyProgress(state.Value);
        if (state is not null && state.Value != Blocks.AIR.DefaultBlockState && progress >= 1f)
        {
            DestroyBlockAndAck(pos);
            return;
        }
        //换了目标但上一个还没收尾 先把旧位置的裂纹清掉 免得留在别人屏幕上
        if (_isDestroyingBlock) BroadcastDestroyProgress(level, _destroyPos, -1);
        _isDestroyingBlock = true;
        _destroyPos = pos;
        var stage = (int)(progress * 10f);
        BroadcastDestroyProgress(level, pos, stage);
        _lastSentDestroyState = stage;
    }

    //StopDestroyBlock 客户端松手 对应原版 handleBlockBreakAction 的 STOP 分支
    //目标不是正在挖的那个就忽略 服务端估算进度到 0.7 直接破坏
    //不到 0.7 挂起延迟破坏 由后续刻继续推完 对应原版 hasDelayedDestroy
    private void StopDestroyBlock(PersistentServerLevel level, BlockPos pos)
    {
        if (!_isDestroyingBlock || _destroyPos != pos) return;
        var state = level.GetBlockState(pos);
        if (state is null || state.Value == Blocks.AIR.DefaultBlockState) return;
        var ticksSpent = _tickCount - _destroyProgressStart;
        var progress = BlockBehaviour.GetDestroyProgress(state.Value) * (ticksSpent + 1);
        if (progress >= 0.7f)
        {
            _isDestroyingBlock = false;
            BroadcastDestroyProgress(level, pos, -1);
            DestroyBlockAndAck(pos);
            return;
        }
        if (_hasDelayedDestroy) return;
        _isDestroyingBlock = false;
        _hasDelayedDestroy = true;
        _delayedDestroyPos = pos;
        _delayedDestroyTickStart = _destroyProgressStart;
    }

    //AbortDestroyBlock 中断挖掘 对应原版 handleBlockBreakAction 的 ABORT 分支
    //挖到一半移开视线走这里 清掉自家裂纹 目标对不上时旧位置的也一并清掉
    private void AbortDestroyBlock(PersistentServerLevel level, BlockPos pos)
    {
        _isDestroyingBlock = false;
        if (_destroyPos != pos) BroadcastDestroyProgress(level, _destroyPos, -1);
        BroadcastDestroyProgress(level, pos, -1);
        _lastSentDestroyState = -1;
    }

    //TickDestroyProgress 每刻推进挖掘进度 对应原版 ServerPlayerGameMode.tick
    //延迟破坏分支: 客户端已松手 继续把服务端认定还欠的那份挖完
    //正常分支: 方块中途被换成空气就取消并清裂纹 否则累加进度并广播裂纹阶段
    private void TickDestroyProgress()
    {
        if (Player is null) return;
        if (Player.Level is not PersistentServerLevel level) return;
        if (_hasDelayedDestroy)
        {
            var delayedState = level.GetBlockState(_delayedDestroyPos);
            if (delayedState is null || delayedState.Value == Blocks.AIR.DefaultBlockState)
            {
                _hasDelayedDestroy = false;
                return;
            }
            if (IncrementDestroyProgress(level, delayedState.Value, _delayedDestroyPos, _delayedDestroyTickStart) >= 1f)
            {
                _hasDelayedDestroy = false;
                DestroyBlockAndAck(_delayedDestroyPos);
            }
            return;
        }
        if (!_isDestroyingBlock) return;
        var state = level.GetBlockState(_destroyPos);
        if (state is null || state.Value == Blocks.AIR.DefaultBlockState)
        {
            BroadcastDestroyProgress(level, _destroyPos, -1);
            _lastSentDestroyState = -1;
            _isDestroyingBlock = false;
            return;
        }
        IncrementDestroyProgress(level, state.Value, _destroyPos, _destroyProgressStart);
    }

    //IncrementDestroyProgress 累加破坏进度 返回累计值 对应原版 incrementDestroyProgress
    //进度按每刻增量乘已挖刻数 裂纹阶段取 0-10 的整数 阶段没变就不重发
    private float IncrementDestroyProgress(PersistentServerLevel level, BlockState state, BlockPos pos, int startTick)
    {
        var ticksSpent = _tickCount - startTick;
        var progress = BlockBehaviour.GetDestroyProgress(state) * (ticksSpent + 1);
        var stage = (int)(progress * 10f);
        if (stage != _lastSentDestroyState)
        {
            BroadcastDestroyProgress(level, pos, stage);
            _lastSentDestroyState = stage;
        }
        return progress;
    }

    //BroadcastDestroyProgress 广播方块破坏进度 对应原版 ServerLevel.destroyBlockProgress
    //破坏者本人不收 客户端对自己挖的方块有本地预测动画 收了会打架
    //只发给 32 格内的同维度玩家 再远看不见裂纹
    private void BroadcastDestroyProgress(PersistentServerLevel level, BlockPos pos, int progress)
    {
        if (Players is null || Player is null) return;
        var packet = new ClientboundBlockDestructionPacket(Player.EntityId, pos, progress);
        foreach (var other in Players.Players)
        {
            if (ReferenceEquals(other, Player) || other.Level != level) continue;
            var dx = pos.X - other.Position.X;
            var dy = pos.Y - other.Position.Y;
            var dz = pos.Z - other.Position.Z;
            if (dx * dx + dy * dy + dz * dz >= 1024.0) continue;
            other.Connection.Send(packet);
        }
    }

    //DestroyBlockAndAck 破坏方块并把失败结果同步回客户端 对应原版 destroyAndAck
    //破坏没生效(比如方块已被换掉)就把服务端真实状态回发 结束客户端那边的本地预测
    private void DestroyBlockAndAck(BlockPos pos)
    {
        var player = Player;
        if (player is null || Players is null) return;
        if (player.Level is not PersistentServerLevel level) return;
        if (ServerBlockUpdates.BreakBlock(level, Players, player, pos)) return;
        var state = level.GetBlockState(pos);
        if (state is not null) player.Connection.Send(new ClientboundBlockUpdatePacket(pos, state.Value.Id));
    }

    //HandleUseItemOn 玩家对区块使用 先交给方块自身行为 未处理再走手持方块放置
    public void HandleUseItemOn(ServerboundUseItemOnPacket packet)
    {
        if (Player is null || Players is null) return;
        if (Player.Level is not PersistentServerLevel level) return;
        //原版在进入放置流程前先记回执 无论成功与否都要结束客户端预测
        AckBlockChanges(packet.Sequence);
        var pos = packet.BlockHit.BlockPos;
        var face = packet.BlockHit.Direction;
        //建筑高度越界只回动作栏提示不落地 对应原版 handleUseItemOn 里的 maxY/minY 检查
        //上限取 319 下限取 -64 与客户端 F3 看到的高度一致
        var maxY = level.MaxBuildHeight - 1;
        var minY = level.MinBuildHeight;
        if (pos.Y > maxY)
        {
            Player.SendBuildLimitMessage(true, maxY);
            return;
        }
        if (pos.Y < minY)
        {
            Player.SendBuildLimitMessage(false, minY);
            return;
        }
        //原版顺序: 方块自身行为 -> 物品 useOn -> 方块物品放置
        //潜行且手里拿着东西时跳过方块自身行为 对应原版 suppressUsingBlock
        //不然潜行按按钮开箱子照样会触发 原版这时只走物品使用与放置
        var haveSomethingInOurHands = !Player.Inventory.GetSelectedItem().IsEmpty()
            || !Player.Inventory.GetItem(PlayerInventory.OffhandSlot).IsEmpty();
        var suppressUsingBlock = Player.IsSneaking && haveSomethingInOurHands;
        var handled = !suppressUsingBlock && ServerBlockUpdates.UseOn(level, Player, pos, face);
        if (!handled) handled = ServerBlockUpdates.UseItemOn(level, Player, pos, face, packet.Hand);
        if (!handled)
        {
            var hit = packet.BlockHit.Location;
            handled = ServerBlockUpdates.PlaceHeldBlock(level, Players, Player, pos, face, packet.Hand,
                new NetCraft.Primitives.Vec3(hit.X - pos.X, hit.Y - pos.Y, hit.Z - pos.Z));
            //贴面方块的朝向全由这一格的状态决定 客户端显示与它不符就说明问题在状态编码或下发的包上
            var placePos = pos.Offset(face);
            if (handled && level.GetBlockState(placePos) is { } placed)
                Log.Debug($"Place result {placed.Owner.Id}[{placed.Id}] pos={placePos} face={face} yaw={Player.Yaw} properties={string.Join(",", placed.GetValues().Select(pv => $"{pv.Property.Name}={pv.Value}"))}");
        }
        //放置没成又贴着上下界就是高度不够 对应原版 wasBlockPlacementAttempt 之后的两支提示
        if (!handled && face == Direction.Up && pos.Y >= maxY) Player.SendBuildLimitMessage(true, maxY);
        else if (!handled && face == Direction.Down && pos.Y <= minY) Player.SendBuildLimitMessage(false, minY);
        Log.Debug($"Use block {pos} handled={handled} profile={_profile.Name}");
    }

    //HandleChat 玩家聊天广播 对应原版 handleChat
    //未接入签名链 走 system_chat 广播 chat.type.text 显示效果与原版玩家聊天一致
    public void HandleChat(ServerboundChatPacket packet)
    {
        if (Player is null || Players is null) return;
        var message = packet.Message;
        if (string.IsNullOrEmpty(message)) return;
        //原版 writeUtf(message,256) 超出长度客户端编码阶段就该失败 这里再挡一次
        if (message.Length > ServerboundChatPacket.MaxMessageLength) return;
        var name = Player.Profile.Name;
        Log.Info($"Chat {name}: {message}");
        Players.BroadcastSystemMessage(CreateChatMessage(name, message), false);
    }

    //CreateChatMessage 构造玩家聊天组件 对应原版 ChatType.bind(CHAT,player) 的 chat.type.text 装饰
    public static Component CreateChatMessage(string senderName, string message)
        => Component.Translatable("chat.type.text", Component.Literal(senderName), Component.Literal(message));

    //HandleMovePlayer 玩家移动包 同步坐标朝向到 ServerPlayer
    //玩家坐标是客户端权威 服务端只回填 跨块触发的视野更新由 ServerPlayer.Tick 检测
    //接触地面也要回填 实体追踪按它决定是否补发 onGround 变化包
    //传送等待确认期间只接受朝向 客户端上报的坐标还是传送前的在途值
    //采纳它会把玩家打回旧坐标 实体追踪随即把旧坐标广播出去 观察者那边的模型就停在旧位置
    public void HandleMovePlayer(ServerboundMovePlayerPacket packet)
    {
        var player = Player;
        if (player is null) return;
        if (_awaitingPositionFromClient is not null)
        {
            if (packet.HasRot)
            {
                player.Yaw = packet.YRot;
                player.Pitch = packet.XRot;
            }
            return;
        }
        if (packet.HasPos) player.Position = new Vec3(packet.X, packet.Y, packet.Z);
        if (packet.HasRot)
        {
            player.Yaw = packet.YRot;
            player.Pitch = packet.XRot;
        }
        player.OnGround = packet.OnGround;
    }

    //HandleClientCommand 客户端命令包如请求 respawn Log.Debug
    public void HandleClientCommand(ServerboundClientCommandPacket packet)
    {
        Log.Debug($"HandleClientCommand profile={_profile.Name}");
    }

    //HandleAcceptTeleportPacket 客户端确认传送 对应原版 handleAcceptTeleportation
    //id 匹配才把玩家吸附到等待的目标位置并解除等待 之后上报的位置重新被采纳
    //id 不符说明确认包与当前这轮传送不同轮 保持等待交给 TickListener 重发
    public void HandleAcceptTeleportPacket(ServerboundAcceptTeleportationPacket packet)
    {
        var player = Player;
        if (player is null || _awaitingPositionFromClient is not { } target) return;
        if (packet.Id != _awaitingTeleport)
        {
            //不符时玩家一直停在等待状态 期间每次移动上报的位置都被丢弃
            //每 20 刻又被重发的位置包按绝对量拽回目标 表现就是传送后一动就被拉走
            //这条告警是该现象的唯一入口 一条都不出现说明确认包压根没到服务端
            Log.Warning($"Teleport ack id mismatch received={packet.Id} waiting={_awaitingTeleport} profile={_profile.Name}");
            return;
        }
        player.Position = target;
        _awaitingPositionFromClient = null;
        _awaitingTeleport = -1;
    }

    //HandleAnimate 挥手动画 对应原版 handleAnimate 的广播分支
    //发 animate 给其他玩家 自己的挥手动画客户端本地已播放不再回发
    public void HandleAnimate(ServerboundSwingPacket packet)
    {
        var player = Player;
        if (player is null || Players is null) return;
        Players.BroadcastAllExcept(player,
            new ClientboundAnimatePacket(player.EntityId, AnimateAction(packet.Hand)));
    }

    //AnimateAction 挥手动作编号 对应原版 ClientboundAnimatePacket 的主手/副手常量
    private static int AnimateAction(InteractionHand hand)
        => hand == InteractionHand.OffHand ? ClientboundAnimatePacket.SwingOffHand : ClientboundAnimatePacket.SwingMainHand;

    //以下 54 个 ServerGamePacketListener 自有方法本轮未注册对应 PacketType 不会解码到空实现

    //HandleChatCommand 玩家执行斜杠命令 交给命令管理器解析执行
    public void HandleChatCommand(ServerboundChatCommandPacket packet)
        => ExecuteChatCommand(packet.Command);

    //HandleSignedChatCommand 带签名命令不验签 与无签名命令走同一条执行路径
    public void HandleSignedChatCommand(ServerboundChatCommandSignedPacket packet)
        => ExecuteChatCommand(packet.Command);

    //ExecuteChatCommand 聊天命令统一入口
    private void ExecuteChatCommand(string command)
    {
        var player = Player;
        if (player is null || Commands is null) return;
        Log.Debug($"HandleChatCommand profile={_profile.Name} command={command}");
        Commands.Execute(player, command);
    }

    public void HandleChatAck(ServerboundChatAckPacket packet) { }
    //HandleContainerButtonClick 容器按钮点击 交给当前菜单处理
    //切石机选配方这类不走槽位号的交互走它 对应原版 handleContainerButtonClick 的 clickMenuButton
    public void HandleContainerButtonClick(ServerboundContainerButtonClickPacket packet)
    {
        var player = Player;
        if (player?.ContainerMenu is not { } menu) return;
        if (packet.ContainerId != menu.ContainerId) return;
        menu.ClickMenuButton(player, packet.ButtonId);
    }

    //HandleContainerClick 容器点击 交给玩家当前菜单处理
    //原版在此比对 stateId 与 carriedItem 哈希防作弊 本作只按完整栈处理不做校验
    public void HandleContainerClick(ServerboundContainerClickPacket packet)
    {
        var player = Player;
        if (player?.ContainerMenu is not { } menu)
        {
            Log.Warning($"[Container] click packet arrived but the player has no menu profile={_profile.Name} slot={packet.SlotNum}");
            return;
        }
        if (packet.ContainerId != menu.ContainerId)
        {
            Log.Warning($"[Container] click packet container id mismatch packet={packet.ContainerId} server={menu.ContainerId} profile={_profile.Name}");
            return;
        }
        //容器点击是低频操作 打 Info 便于对照客户端行为排查 槽位与点击类型要和客户端按 Q 时对得上
        Log.Info($"[Container] click slot={packet.SlotNum} button={packet.ButtonNum} type={packet.Input} profile={_profile.Name}");
        menu.Clicked(packet.SlotNum, packet.ButtonNum, packet.Input, player);
    }

    public void HandlePlaceRecipe(ServerboundPlaceRecipePacket packet) { }

    //HandleContainerClose 客户端关闭菜单对应原版 handleContainerClose 里的 player.doCloseContainer
    //只切回背包菜单 不回发 container_close 客户端已经自己关掉了界面
    public void HandleContainerClose(ServerboundContainerClosePacket packet) => Player?.DoCloseContainer();
    //HandleAttack 左键攻击实体 26.2 的 attack 包与挥手包分开
    //目标先在在线玩家里找再落到关卡实体 玩家不在实体管理器里故两条路都要查
    //未实现攻击冷却系数/暴击/横扫 对齐原版 Player.attack 的基础伤害
    public void HandleAttack(ServerboundAttackPacket packet)
    {
        var attacker = Player;
        if (attacker is null || Players is null) return;
        //打自己不发伤害 对应原版 isAttackable 里的 self 排除
        if (packet.EntityId == attacker.EntityId) return;
        //伤害取攻击者的 attack_damage 属性 对应原版 Player.attack 读 ATTACK_DAMAGE
        var damage = attacker.AttackDamage;
        var playerTarget = Players.GetPlayerByEntityId(packet.EntityId);
        if (playerTarget is not null)
        {
            var damaged = Players.HurtPlayer(playerTarget, attacker, damage);
            PlayAttackSound(attacker, damaged);
            Log.Debug($"Attack player target={playerTarget.Profile.Name} damage={damage} hit={damaged} profile={_profile.Name}");
            return;
        }
        if (attacker.Level is not PersistentServerLevel level) return;
        var entity = level.EntityManager.GetByEntityId(packet.EntityId);
        if (entity is null) return;
        //带上攻击者位置 目标会沿攻击者指向目标的方向被击退 对应原版 Player.attack 的 knockback
        var hurt = entity.Hurt(damage, attacker.Position);
        //生物受伤动画走实体事件 2 对应原版 LivingEntity.hurt 的 broadcastEntityEvent(2)
        if (hurt)
        {
            Players.BroadcastAll(new ClientboundEntityEventPacket(entity.EntityId, 2));
            //受伤声按生物自身位置播 本作没有实体音效表统一用通用受伤声
            ServerSounds.PlaySound(Players, SoundEvents.GenericHurt, SoundSource.Neutral,
                entity.Pos.X, entity.Pos.Y, entity.Pos.Z, 1f, 1f);
        }
        PlayAttackSound(attacker, hurt);
        Log.Debug($"Attack entity target={entity.Id} damage={damage} hit={hurt} health={entity.Health} profile={_profile.Name}");
    }

    //PlayAttackSound 挥击音效命中用重击声落空用轻击声 对应原版 Player.attack 收尾的播放分支
    private void PlayAttackSound(ServerPlayer attacker, bool hit)
    {
        if (Players is null) return;
        ServerSounds.PlaySound(Players, hit ? SoundEvents.PlayerAttackStrong : SoundEvents.PlayerAttackWeak,
            SoundSource.Players, attacker.Position.X, attacker.Position.Y, attacker.Position.Z, 1f, 1f);
    }
    //HandleInteract 右键实体 骑乘/交易/喂食等都走这个包
    //本作没有实体交互行为 按原版先播挥手动画再记日志 目标是否存在都照发
    public void HandleInteract(ServerboundInteractPacket packet)
    {
        var player = Player;
        if (player is null || Players is null) return;
        Players.BroadcastAllExcept(player,
            new ClientboundAnimatePacket(player.EntityId, AnimateAction(packet.Hand)));
        Log.Debug($"Interact entity target={packet.EntityId} hand={packet.Hand} secondary={packet.UsingSecondaryAction} profile={_profile.Name}");
    }
    public void HandleSpectatorAction(ServerboundSpectatorActionPacket packet) { }
    //HandlePlayerAbilities 客户端上报自己开始/停止飞行 对应原版 handlePlayerAbilities
    //没有 mayfly 许可时上报也当没飞 生存模式客户端改包飞不起来
    public void HandlePlayerAbilities(ServerboundPlayerAbilitiesPacket packet)
    {
        if (Player is not { } player) return;
        player.Abilities.Flying = packet.IsFlying && player.Abilities.MayFly;
    }
    //HandlePlayerCommand 玩家状态切换 本作只接疾跑起停 其余动作(骑乘跳跃/打开背包/鞘翅)无对应系统
    //状态变化由 EntityTracker 每 tick 检测并下发给其他玩家
    public void HandlePlayerCommand(ServerboundPlayerCommandPacket packet)
    {
        var player = Player;
        if (player is null) return;
        switch (packet.Action)
        {
            case PlayerCommandAction.StartSprinting:
                player.SetSprinting(true);
                return;
            case PlayerCommandAction.StopSprinting:
                player.SetSprinting(false);
                return;
        }
    }

    //HandlePlayerInput 玩家键盘输入 潜行状态由输入位驱动 对应原版 handlePlayerInput 设置 shift 键状态
    //疾跑不在这里处理 由 player_command 的起停动作管理 否则输入位会覆盖已开启的疾跑
    public void HandlePlayerInput(ServerboundPlayerInputPacket packet)
    {
        Player?.SetSneaking(packet.Input.Shift);
    }
    public void HandleSetCarriedItem(ServerboundSetCarriedItemPacket packet)
    {
        var player = Player;
        if (player is null) return;
        //槽位不落位会导致后续放置与使用一直读旧槽物品 与客户端实际手持不一致
        if (packet.Slot >= 0 && packet.Slot < PlayerInventory.HotbarSlots)
        {
            player.Inventory.SelectedSlot = packet.Slot;
            return;
        }
        Log.Warning($"Hotbar slot out of range {packet.Slot} profile={_profile.Name}");
        player.Disconnect("Invalid hotbar selection (Hacking?)");
    }

    //HandleSetCreativeModeSlot 创造背包槽位设置对应原版 handleSetCreativeModeSlot
    //slotNum<0 为丢出物品 本作暂无世界掉落实体 忽略丢弃分支
    //槽位校验 1-45 与数量上限对齐原版 设置后广播变更
    public void HandleSetCreativeModeSlot(ServerboundSetCreativeModeSlotPacket packet)
    {
        var player = Player;
        if (player?.ContainerMenu is not { } menu || player.GameType != GameType.Creative) return;
        var validSlot = packet.SlotNum >= 1 && packet.SlotNum < menu.Slots.Count;
        var validData = packet.Stack.IsEmpty()
            || packet.Stack.GetCount() <= packet.Stack.GetItem().GetDefaultMaxStackSize();
        if (validSlot && validData)
        {
            menu.GetSlot(packet.SlotNum).Set(packet.Stack);
            menu.BroadcastChanges();
        }
    }
    public void HandleSignUpdate(ServerboundSignUpdatePacket packet) { }

    //HandleUseItem 玩家对空气使用物品 对应原版 handleUseItem
    //取对应手物品 空栈忽略 朝向以客户端上报为准校正
    //投掷类物品在这里出手 其余物品使用行为待物品系统接入
    public void HandleUseItem(ServerboundUseItemPacket packet)
    {
        var player = Player;
        if (player is null) return;
        //对空气使用也带方块变更序号 同样要结束客户端预测
        AckBlockChanges(packet.Sequence);
        var stack = packet.Hand == InteractionHand.OffHand
            ? player.Inventory.GetItem(PlayerInventory.OffhandSlot)
            : player.Inventory.GetSelectedItem();
        if (stack.IsEmpty()) return;
        var yRot = Mth.WrapDegrees(packet.YRot);
        var xRot = Mth.WrapDegrees(packet.XRot);
        if (yRot != player.Yaw || xRot != player.Pitch)
        {
            player.Yaw = yRot;
            player.Pitch = xRot;
        }
        //雪球鸡蛋末影珍珠火焰弹这类直接出手 对应原版 SnowballItem.use 里的 spawnProjectileFromRotation
        if (player.Level is PersistentServerLevel level && stack.GetItem() is ProjectileItem projectileItem)
        {
            projectileItem.Use(level, player, stack);
            //创造模式投掷不消耗 对应原版 abilities.instabuild 分支
            if (player.GameType != GameType.Creative)
            {
                stack.SetCount(stack.GetCount() - 1);
                player.ContainerMenu?.SendAllDataToRemote();
            }
        }
        Log.Debug($"Use item {stack.GetItem().Id} hand={packet.Hand} sequence={packet.Sequence} profile={_profile.Name}");
    }
    public void HandleTeleportToEntityPacket(ServerboundTeleportToEntityPacket packet) { }
    public void HandlePaddleBoat(ServerboundPaddleBoatPacket packet) { }
    public void HandleMoveVehicle(ServerboundMoveVehiclePacket packet) { }
    public void HandleAcceptPlayerLoad(ServerboundPlayerLoadedPacket packet)
    {
        //客户端退出加载地形进入世界的信号
        Log.Info($"Client finished loading and entered the world profile={_profile.Name}");
    }
    public void HandleRecipeBookSeenRecipePacket(ServerboundRecipeBookSeenRecipePacket packet) { }
    public void HandleBundleItemSelectedPacket(ServerboundSelectBundleItemPacket packet) { }
    public void HandleRecipeBookChangeSettingsPacket(ServerboundRecipeBookChangeSettingsPacket packet) { }
    public void HandleSeenAdvancements(ServerboundSeenAdvancementsPacket packet) { }
    //HandleCustomCommandSuggestions 客户端按 tab 请求命令补全 服务端算完回建议包
    //原版同名方法 不实现这个客户端只剩本地补全(literal 与坐标) 实体/物品这类候选一个都不显示
    public void HandleCustomCommandSuggestions(ServerboundCommandSuggestionPacket packet)
    {
        var player = Player;
        if (player is null || Commands is null) return;
        var suggestions = Commands.GetCompletions(player, packet.Command);
        //排查 time 补全缺失用 只对 time 命令打 定位完就撤
        if (packet.Command.StartsWith("time", StringComparison.OrdinalIgnoreCase))
            Log.Info($"Command suggestions request id={packet.Id} command=\"{packet.Command}\" candidates={suggestions.List.Count} range=[{suggestions.Range.Start},{suggestions.Range.Length}]");
        var entries = new List<CommandSuggestionEntry>(suggestions.List.Count);
        foreach (var suggestion in suggestions.List)
            entries.Add(new CommandSuggestionEntry(suggestion.Text, suggestion.Tooltip as Component));
        player.Connection.Send(new ClientboundCommandSuggestionsPacket(
            packet.Id, suggestions.Range.Start, suggestions.Range.Length, entries));
    }
    public void HandleSetCommandBlock(ServerboundSetCommandBlockPacket packet) { }
    public void HandleSetCommandMinecart(ServerboundSetCommandMinecartPacket packet) { }
    //HandlePickItemFromBlock 中键选方块 对应原版 handlePickItemFromBlock 接 tryPickItem
    //IncludeData 附带方块实体数据的变体待方块实体组件序列化补齐后扩展
    public void HandlePickItemFromBlock(ServerboundPickItemFromBlockPacket packet)
    {
        var player = Player;
        if (player?.Level is not PersistentServerLevel level) return;
        var state = level.GetBlockState(packet.Pos);
        //未加载区块或空气等无 Owner 状态直接忽略
        if (state?.Owner is not { } block) return;
        //方块注册名查同名物品 绝大多数方块都有对应 BlockItem
        var holder = BuiltInRegistries.ITEM.Get(block.Id);
        if (holder is null) return;
        var inventory = player.Inventory;
        var stack = new ItemStack(holder, 1, DataComponentPatch.Empty);
        //原版 tryPickItem 顺序: 全背包找同物品 -> 热键栏里就切过去 主背包里就换出来 -> 都没有且创造模式才新给一个
        var matching = inventory.FindSlotMatchingItem(stack);
        if (matching != -1)
        {
            if (matching < PlayerInventory.HotbarSlots) inventory.SelectedSlot = matching;
            else inventory.PickSlot(matching);
        }
        else if (inventory.InfiniteMaterials)
        {
            inventory.AddAndPickItem(stack);
        }
        _connection.Send(new ClientboundSetHeldSlotPacket(inventory.SelectedSlot));
        player.ContainerMenu?.BroadcastChanges();
    }

    //HandlePickItemFromEntity 中键选实体 实体到掉落物映射补齐后扩展
    public void HandlePickItemFromEntity(ServerboundPickItemFromEntityPacket packet) { }
    public void HandleRenameItem(ServerboundRenameItemPacket packet) { }
    public void HandleSetBeaconPacket(ServerboundSetBeaconPacket packet) { }
    public void HandleSetGameRule(ServerboundSetGameRulePacket packet) { }
    public void HandleSetStructureBlock(ServerboundSetStructureBlockPacket packet) { }
    public void HandleSetTestBlock(ServerboundSetTestBlockPacket packet) { }
    public void HandleTestInstanceBlockAction(ServerboundTestInstanceBlockActionPacket packet) { }
    public void HandleSelectTrade(ServerboundSelectTradePacket packet) { }
    public void HandleEditBook(ServerboundEditBookPacket packet) { }
    public void HandleEntityTagQuery(ServerboundEntityTagQueryPacket packet) { }
    public void HandleContainerSlotStateChanged(ServerboundContainerSlotStateChangedPacket packet) { }
    public void HandleBlockEntityTagQuery(ServerboundBlockEntityTagQueryPacket packet) { }
    public void HandleSetJigsawBlock(ServerboundSetJigsawBlockPacket packet) { }
    public void HandleJigsawGenerate(ServerboundJigsawGeneratePacket packet) { }
    public void HandleChangeDifficulty(ServerboundChangeDifficultyPacket packet) { }
    //HandleChangeGameMode 客户端 F3+F4 切换游戏模式 需权限 2 级
    //客户端入口已按权限等级关闭 这里是防伪造的服务端校验
    //切换后只给操作者本人回执 对应原版 F3+F4 的 commands.gamemode.success.self 私人提示
    public void HandleChangeGameMode(ServerboundChangeGameModePacket packet)
    {
        if (Player is null || Players is null) return;
        if (!Player.HasPermissions(2)) return;
        if (Player.GameType == packet.Mode) return;
        Players.ChangeGameMode(Player, packet.Mode);
        Player.Connection.Send(new ClientboundSystemChatPacket(
            Component.Literal($"已将您的游戏模式设置为 {packet.Mode.Name}"), false));
    }
    public void HandleLockDifficulty(ServerboundLockDifficultyPacket packet) { }
    public void HandleChatSessionUpdate(ServerboundChatSessionUpdatePacket packet) { }
    public void HandleConfigurationAcknowledged(ServerboundConfigurationAcknowledgedPacket packet) { }
    public void HandleChunkBatchReceived(ServerboundChunkBatchReceivedPacket packet) { }
    public void HandleDebugSubscriptionRequest(ServerboundDebugSubscriptionRequestPacket packet) { }
    public void HandleClientTickEnd(ServerboundClientTickEndPacket packet) { }

    //以下继承自 ServerCommonPacketListener 的 6 个方法本轮空实现
    public void HandleClientInformation(ServerboundClientInformationPacket packet) { }
    public void HandleCustomPayload(ServerboundCustomPayloadPacket packet) { }
    public void HandleKeepAlive(ServerboundKeepAlivePacket packet)
    {
        //打印回包 id 与关联玩家 心跳异常时用于区分回包缺失与玩家未关联
        Log.Debug($"HandleKeepAlive received id={packet.Id} player={(Player is null ? "null" : Player.Profile.Name)}");
        Player?.HandleKeepAliveResponse(packet.Id);
    }
    public void HandlePong(ServerboundPongPacket packet) { }
    public void HandleResourcePack(ServerboundResourcePackPacket packet) { }
    public void HandleCustomClickAction(ServerboundCustomClickActionPacket packet) { }

    //继承自 ServerCookiePacketListener
    public void HandleCookieResponse(ServerboundCookieResponsePacket packet) { }

    //继承自 ServerPingPacketListener
    //客户端 PingDebugMonitor 周期性发 ping_request 回传同一时间戳供客户端算往返延迟
    //Play 的 pong 协议 id 与 Status 不同 由当前出站协议表按包类定 ID 无需在此指定
    public void HandlePingRequest(ServerboundPingRequestPacket packet)
        => Player?.Connection.Send(new ClientboundPongResponsePacket(packet.Time));

    public void OnDisconnect(string reason)
    {
        Log.Info($"play phase disconnect reason={reason} profile={_profile.Name}");
    }
}
