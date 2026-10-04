using System.Diagnostics;
using NetCraft.Game.Client.Level;
using NetCraft.Game.Gui;
using NetCraft.Game.Gui.Hud;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.World.Entity;
using NetCraft.Gpu;
using NetCraft.Primitives;

namespace NetCraft.Game.Gui.Screens;

//GameScreen 游戏 HUD 对应原版 Hud.java
//用 widgets sprites 纹理渲染十字准星 Hotbar 选中框 心 饥饿条
//P1 心走 HeartRenderer 完整算法（6 类型×8 变体+低血抖动+再生上跳+吸收心+闪烁覆盖）
//F3 Debug 多行文本读玩家真实状态 数字键 1-9 切换选中槽
public sealed class GameScreen : Screen
{
    private GuiImage? _crosshair;
    private GuiImage? _hotbar;
    private GuiImage? _hotbarSel;
    private readonly List<GuiImage> _foods = new();
    private readonly List<GuiLabel> _debugLines = new();
    //P1 HeartRenderer 心渲染器对标原版 Hud.extractHearts 完整算法
    private readonly HeartRenderer _heartRenderer = new();

    private int _selectedSlot;
    private bool _showDebug;
    private long _lastFpsTimestamp = Stopwatch.GetTimestamp();
    private long _lastFpsFrame;
    private int _fps;

    //饥饿 sprite identifier swapchain 重建后由 GuiSpriteManager 重新懒加载无需手动重取
    private const string FoodFullSprite = "minecraft:textures/gui/sprites/hud/food_full";
    private const string FoodHalfSprite = "minecraft:textures/gui/sprites/hud/food_half";
    private const string FoodEmptySprite = "minecraft:textures/gui/sprites/hud/food_empty";
    //Hotbar 布局参数供选中框跟随槽位用
    private int _hotbarX, _hotbarY;

    public override string Title => "游戏 HUD";

    public override void Init()
    {
        var cx = GuiWidth / 2;
        var cy = GuiHeight / 2;
        //P0 HUD 元素用 SpriteIdentifier 由 GuiSpriteManager 懒加载 PNG+.mcmeta
        //swapchain 重建后无需重取 textureId GuiSpriteManager 内部缓存命中

        //十字准星 15x15 居中 crosshair.png 16x16 留 1px 边距
        _crosshair = AddWidget(new GuiImage
        {
            SpriteIdentifier = "minecraft:textures/gui/sprites/hud/crosshair",
            X = cx - 7,
            Y = cy - 7,
            Width = 15,
            Height = 15
        });

        //Hotbar 182x22 底部居中 9 格 20x20 加左右各 1px 边框
        var hotbarW = 182;
        var hotbarH = 22;
        _hotbarX = cx - hotbarW / 2;
        _hotbarY = GuiHeight - hotbarH - 2;
        _hotbar = AddWidget(new GuiImage
        {
            SpriteIdentifier = "minecraft:textures/gui/sprites/hud/hotbar",
            X = _hotbarX,
            Y = _hotbarY,
            Width = hotbarW,
            Height = hotbarH
        });
        //选中框 24x24 跟随当前槽位比槽位 20x20 大 2px 每边
        _hotbarSel = AddWidget(new GuiImage
        {
            SpriteIdentifier = "minecraft:textures/gui/sprites/hud/hotbar_selection",
            Width = 24,
            Height = 24
        });
        UpdateSelectorPosition();

        //心走 RenderForeground HeartRenderer 完整算法每帧直接画不创建 GuiImage
        var heartW = 9;
        var heartsY = _hotbarY - heartW - 2;
        //饥饿条 10 鸡腿 Hotbar 上方右对齐每鸡腿 2 点饥饿
        var foodsX = _hotbarX + hotbarW - 10 * heartW;
        for (var i = 0; i < 10; i++)
        {
            _foods.Add(AddWidget(new GuiImage
            {
                X = foodsX + i * heartW,
                Y = heartsY,
                Width = heartW,
                Height = heartW
            }));
        }
        UpdateFoods();

        //F3 Debug 多行左上角默认隐藏 前7行业务状态 后6行 GPU 性能指标
        for (var i = 0; i < 13; i++)
        {
            _debugLines.Add(AddWidget(new GuiLabel
            {
                X = 4,
                Y = 4 + i * 12,
                Width = 360,
                Height = 12,
                ForegroundColor = GuiColor.White,
                Visible = false
            }));
        }
        //P15 注册 cube 物品到 ItemItemAtlas 供 Hotbar 物品图标渲染 PoC 用 CubeModel 程序化几何
        //RegisterItem 幂等 resize 重 Init 重复调返回 existing 不重复注册
        Minecraft.GpuApp?.ItemAtlas?.RegisterItem("cube", 1.0f);
    }

    public override void Tick()
    {
        //FPS 每秒统计一次基于 FrameCount 差值
        var now = Stopwatch.GetTimestamp();
        var elapsed = now - _lastFpsTimestamp;
        if (elapsed >= Stopwatch.Frequency)
        {
            _fps = (int)((Minecraft.FrameCount - _lastFpsFrame) * Stopwatch.Frequency / Math.Max(elapsed, 1));
            _lastFpsFrame = Minecraft.FrameCount;
            _lastFpsTimestamp = now;
        }
        //选中框跟随当前槽位心饥饿纹理按生命饥饿值刷新
        UpdateSelectorPosition();
        _heartRenderer.Tick(Minecraft.Player);
        UpdateFoods();
        //Debug 文本更新与可见性切换
        if (_showDebug)
        {
            UpdateDebugText();
            foreach (var line in _debugLines) line.Visible = true;
        }
        else
        {
            foreach (var line in _debugLines) line.Visible = false;
        }
    }

    //UpdateSelectorPosition 选中框跟随当前槽位
    //槽位 i 左边缘 = _hotbarX + 1 + i*20 选中框 24x24 居中盖在槽位上偏移 -1
    private void UpdateSelectorPosition()
    {
        if (_hotbarSel is null) return;
        _hotbarSel.X = _hotbarX + _selectedSlot * 20 - 1;
        _hotbarSel.Y = _hotbarY - 1;
    }

    //RenderForeground 心渲染走 HeartRenderer 完整算法每帧直接画不录 cache
    //对标原版 Hud.renderHeart 委托调 GuiGraphics.blitSprite
    //xLeft 心容器左边缘 yLineBase 最底行 Y healthRowHeight 行间距多行心时上移
    public override void RenderForeground(IGuiRenderContext context)
    {
        var player = Minecraft.Player;
        var heartW = 9;
        var xLeft = _hotbarX;
        var yLineBase = _hotbarY - heartW - 2;
        const int healthRowHeight = 10;
        _heartRenderer.ExtractHearts(player, xLeft, yLineBase, healthRowHeight,
            (identifier, xo, yo) => context.DrawSprite(identifier, xo, yo, heartW, heartW, GuiColor.White));
        RenderHotbarItems(context);
    }

    //RenderHotbarItems 渲染 hotbar 9 格物品图标调 ItemItemAtlas.GetOrUpdate 拿 SlotView
    //首次 GetOrUpdate 触发 DrawToSlot GPU 渲染到 AtlasTexture 后续 Ready 状态直接返回 UV 不重复 Submit
    //物品图标 16x16 居中在槽位 20x20 内偏移 2px 对标原版 Hud.renderItem
    //PoC cube 物品用 CubeModel 程序化几何完整版走 ItemModelResolver
    private void RenderHotbarItems(IGuiRenderContext context)
    {
        var gpu = Minecraft.GpuApp;
        var atlas = gpu?.ItemAtlas;
        var textureId = gpu?.ItemAtlasTextureId ?? 0;
        if (atlas is null || textureId == 0) return;
        var player = Minecraft.Player;
        const int iconSize = 16;
        const int slotSize = 20;
        const int iconOffset = (slotSize - iconSize) / 2;
        for (var i = 0; i < Inventory.HotbarSlots; i++)
        {
            var identity = player.Inventory.GetHotbarItem(i);
            if (identity is null) continue;
            var slot = atlas.GetOrUpdate(identity, isAnimated: false);
            if (slot is null) continue;
            var x = _hotbarX + 1 + i * slotSize + iconOffset;
            var y = _hotbarY + iconOffset;
            var srcX = (int)(slot.U0 * atlas.TextureSize);
            var srcY = (int)(slot.V0 * atlas.TextureSize);
            context.DrawImage(textureId, x, y, iconSize, iconSize,
                srcX, srcY, atlas.SlotTextureSize, atlas.SlotTextureSize, GuiColor.White);
        }
    }

    //UpdateFoods 按 FoodLevel 更新每个鸡腿 sprite full/half/empty
    private void UpdateFoods()
    {
        if (_foods.Count == 0) return;
        var food = Minecraft.Player.FoodLevel;
        for (var i = 0; i < _foods.Count; i++)
        {
            var point = food - i * 2;
            var sprite = point >= 2 ? FoodFullSprite : point == 1 ? FoodHalfSprite : FoodEmptySprite;
            _foods[i].SpriteIdentifier = sprite;
        }
    }

    //UpdateDebugText 填充 F3 Debug 多行文本读真实玩家状态
    private void UpdateDebugText()
    {
        var player = Minecraft.Player;
        var px = player.Pos.X;
        var py = player.Pos.Y;
        var pz = player.Pos.Z;
        var cx = (long)Math.Floor(px / 16);
        var cz = (long)Math.Floor(pz / 16);
        _debugLines[0].Text = "NetCraft v0.1.0";
        _debugLines[1].Text = $"{_fps} fps";
        _debugLines[2].Text = $"XYZ: {px:F2} / {py:F2} / {pz:F2}";
        _debugLines[3].Text = $"区块: {cx} / {cz}";
        _debugLines[4].Text = $"面向: {FacingName(player.YRot)} ({player.YRot:F1})";
        _debugLines[5].Text = $"生命: {player.Health:F1}/{player.MaxHealth:F0} 饥饿: {player.FoodLevel}/20";
        _debugLines[6].Text = $"渲染距离: {Minecraft.Config.RenderDistance} chunks FOV: {Minecraft.Config.Fov}";
        var gpu = Minecraft.GpuApp;
        if (gpu is null) return;
        _debugLines[7].Text = $"drawcall: {gpu.DrawCallCount} mesh: {gpu.MeshCount} vertex: {gpu.VertexCount}";
        _debugLines[8].Text = $"submission: {gpu.SubmissionCpuMs:F2}ms render: {gpu.RenderCpuMs:F2}ms";
        _debugLines[9].Text = $"tick: {gpu.TickRate}tps";
        _debugLines[10].Text = $"pipeline: hit={gpu.PipelineHits} miss={gpu.PipelineMisses}";
    }

    //FacingName 由 YRot 推算八方位名 0=南 90=西 180=北 270=东
    private static string FacingName(float yRot)
    {
        var deg = ((int)yRot % 360 + 360) % 360;
        return deg switch
        {
            < 22 or >= 338 => "南(+Z)",
            < 67 => "西南",
            < 112 => "西(-X)",
            < 157 => "西北",
            < 202 => "北(-Z)",
            < 247 => "东北",
            < 292 => "东(+X)",
            _ => "东南"
        };
    }

    public override void OnF3Pressed() => _showDebug = !_showDebug;

    public override void OnHotbarSelect(int slot) => _selectedSlot = Math.Clamp(slot, 0, 8);

    //EyeHeight 眼睛高度 与服务端 ServerPlayer.EyeHeight 同值 拾取起点按它抬到眼部
    private const double EyeHeight = 1.62;

    //_actionSequence 方块动作包序列号 服务端按它回方块变更回执 客户端只保证递增
    private int _actionSequence;

    //OnKeyPressed HUD 业务键 Q 丢弃手持物品 E 打开背包界面
    public override void OnKeyPressed(int key)
    {
        if (key == GameKeys.E)
        {
            Minecraft.SetScreen(new InventoryScreen());
            return;
        }
        if (key != GameKeys.Q) return;
        var connection = Minecraft.Connection;
        if (connection is null) return;
        connection.Send(new ServerboundPlayerActionPacket(ServerboundPlayerActionPacket.ActionType.DropItem,
            BlockPos.Zero, NetCraft.Primitives.Direction.Up, NextActionSequence()));
    }

    //OnMouseDown 左键破坏准星指向的方块 对应原版左键挖掘的最小实现
    //没有挖掘进度与本地预测 一次点按连发 START 与 STOP 由服务端判定是否真的破坏
    public override void OnMouseDown(GuiMouseButton button, int x, int y)
    {
        if (button != GuiMouseButton.Left) return;
        var connection = Minecraft.Connection;
        if (connection is null) return;
        var player = Minecraft.Player;
        var eye = new Vec3(player.Pos.X, player.Pos.Y + EyeHeight, player.Pos.Z);
        if (!BlockRaycast.TryPick(Minecraft.Level, eye, player.YRot, player.XRot, out var pos, out var face)) return;
        connection.Send(new ServerboundPlayerActionPacket(ServerboundPlayerActionPacket.ActionType.StartDestroyBlock,
            pos, face, NextActionSequence()));
        connection.Send(new ServerboundPlayerActionPacket(ServerboundPlayerActionPacket.ActionType.StopDestroyBlock,
            pos, face, NextActionSequence()));
    }

    //NextActionSequence 方块动作序列号自增
    private int NextActionSequence() => ++_actionSequence;

    public override void OnClose() => Manager.PushScreen(new PauseScreen());
    public override bool IsPauseScreen() => false;
}
