using System.Diagnostics;
using NetCraft.Game.Client.Level;
using NetCraft.Game.Gui;
using NetCraft.Game.Gui.Hud;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.World.Entity;
using NetCraft.Gpu;
using NetCraft.Primitives;

namespace NetCraft.Game.Gui.Screens;

//GameScreen game HUD, maps to vanilla Hud.java
//Renders the crosshair, Hotbar selection box, hearts, and hunger bar with widgets sprites textures
//P1 hearts use HeartRenderer's full algorithm (6 types × 8 variants + low-health jitter + regeneration bounce + absorption hearts + blink overlay)
//F3 Debug multi-line text reads the player's real state; number keys 1-9 switch the selected slot
public sealed class GameScreen : Screen
{
    private GuiImage? _crosshair;
    private GuiImage? _hotbar;
    private GuiImage? _hotbarSel;
    private readonly List<GuiImage> _foods = new();
    private readonly List<GuiLabel> _debugLines = new();
    //P1 HeartRenderer heart renderer, maps to vanilla Hud.extractHearts' full algorithm
    private readonly HeartRenderer _heartRenderer = new();

    private int _selectedSlot;
    private bool _showDebug;
    private long _lastFpsTimestamp = Stopwatch.GetTimestamp();
    private long _lastFpsFrame;
    private int _fps;

    //Hunger sprite identifier; after a swapchain rebuild GuiSpriteManager lazily reloads it, no manual re-fetch needed
    private const string FoodFullSprite = "minecraft:textures/gui/sprites/hud/food_full";
    private const string FoodHalfSprite = "minecraft:textures/gui/sprites/hud/food_half";
    private const string FoodEmptySprite = "minecraft:textures/gui/sprites/hud/food_empty";
    //Hotbar layout parameters, used so the selection box follows the slot
    private int _hotbarX, _hotbarY;

    public override string Title => "Game HUD";

    public override void Init()
    {
        var cx = GuiWidth / 2;
        var cy = GuiHeight / 2;
        //P0 HUD elements use SpriteIdentifier; GuiSpriteManager lazily loads the PNG+.mcmeta
        //After a swapchain rebuild there is no need to re-fetch textureId; GuiSpriteManager hits its internal cache

        //Crosshair 15x15 centered; crosshair.png is 16x16, leaving a 1px margin
        _crosshair = AddWidget(new GuiImage
        {
            SpriteIdentifier = "minecraft:textures/gui/sprites/hud/crosshair",
            X = cx - 7,
            Y = cy - 7,
            Width = 15,
            Height = 15
        });

        //Hotbar 182x22 centered at the bottom; 9 slots 20x20 plus 1px border on each side
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
        //Selection box 24x24 follows the current slot; 2px larger on each side than the 20x20 slot
        _hotbarSel = AddWidget(new GuiImage
        {
            SpriteIdentifier = "minecraft:textures/gui/sprites/hud/hotbar_selection",
            Width = 24,
            Height = 24
        });
        UpdateSelectorPosition();

        //Hearts go through RenderForeground's full HeartRenderer algorithm, drawn directly each frame without creating a GuiImage
        var heartW = 9;
        var heartsY = _hotbarY - heartW - 2;
        //Hunger bar, 10 drumsticks right-aligned above the Hotbar, 2 hunger points per drumstick
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

        //F3 Debug multi-line at the top-left, hidden by default; the first 7 lines are game state, the last 6 are GPU performance metrics
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
        //P15 register the cube item into ItemItemAtlas for Hotbar item icon rendering; the PoC uses CubeModel procedural geometry
        //RegisterItem is idempotent; on resize re-Init a repeated call returns the existing entry without re-registering
        Minecraft.GpuApp?.ItemAtlas?.RegisterItem("cube", 1.0f);
    }

    public override void Tick()
    {
        //FPS is computed once per second from the FrameCount delta
        var now = Stopwatch.GetTimestamp();
        var elapsed = now - _lastFpsTimestamp;
        if (elapsed >= Stopwatch.Frequency)
        {
            _fps = (int)((Minecraft.FrameCount - _lastFpsFrame) * Stopwatch.Frequency / Math.Max(elapsed, 1));
            _lastFpsFrame = Minecraft.FrameCount;
            _lastFpsTimestamp = now;
        }
        //The selection box follows the current slot; heart and hunger textures refresh by health/hunger value
        UpdateSelectorPosition();
        _heartRenderer.Tick(Minecraft.Player);
        UpdateFoods();
        //Debug text update and visibility toggle
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

    //UpdateSelectorPosition makes the selection box follow the current slot
    //Slot i's left edge = _hotbarX + 1 + i*20; the 24x24 selection box is centered over the slot with offset -1
    private void UpdateSelectorPosition()
    {
        if (_hotbarSel is null) return;
        _hotbarSel.X = _hotbarX + _selectedSlot * 20 - 1;
        _hotbarSel.Y = _hotbarY - 1;
    }

    //RenderForeground heart rendering uses HeartRenderer's full algorithm, drawn directly each frame without recording to cache
    //Maps to vanilla Hud.renderHeart's delegate calling GuiGraphics.blitSprite
    //xLeft heart container left edge; yLineBase bottom row Y; healthRowHeight row spacing, moving up for multiple heart rows
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

    //RenderHotbarItems renders the hotbar's 9 item icons, calling ItemItemAtlas.GetOrUpdate to get a SlotView
    //The first GetOrUpdate triggers DrawToSlot GPU rendering into AtlasTexture; once Ready it returns UV directly without re-submitting
    //Item icon 16x16 centered in the 20x20 slot with 2px offset, maps to vanilla Hud.renderItem
    //PoC cube items use CubeModel procedural geometry; the full version goes through ItemModelResolver
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

    //UpdateFoods updates each drumstick sprite to full/half/empty by FoodLevel
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

    //UpdateDebugText fills the F3 Debug multi-line text reading the player's real state
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
        _debugLines[3].Text = $"Chunk: {cx} / {cz}";
        _debugLines[4].Text = $"Facing: {FacingName(player.YRot)} ({player.YRot:F1})";
        _debugLines[5].Text = $"Health: {player.Health:F1}/{player.MaxHealth:F0} Hunger: {player.FoodLevel}/20";
        _debugLines[6].Text = $"Render distance: {Minecraft.Config.RenderDistance} chunks FOV: {Minecraft.Config.Fov}";
        var gpu = Minecraft.GpuApp;
        if (gpu is null) return;
        _debugLines[7].Text = $"drawcall: {gpu.DrawCallCount} mesh: {gpu.MeshCount} vertex: {gpu.VertexCount}";
        _debugLines[8].Text = $"submission: {gpu.SubmissionCpuMs:F2}ms render: {gpu.RenderCpuMs:F2}ms";
        _debugLines[9].Text = $"tick: {gpu.TickRate}tps";
        _debugLines[10].Text = $"pipeline: hit={gpu.PipelineHits} miss={gpu.PipelineMisses}";
    }

    //FacingName derives the eight-way direction name from YRot: 0=South 90=West 180=North 270=East
    private static string FacingName(float yRot)
    {
        var deg = ((int)yRot % 360 + 360) % 360;
        return deg switch
        {
            < 22 or >= 338 => "South (+Z)",
            < 67 => "Southwest",
            < 112 => "West (-X)",
            < 157 => "Northwest",
            < 202 => "North (-Z)",
            < 247 => "Northeast",
            < 292 => "East (+X)",
            _ => "Southeast"
        };
    }

    public override void OnF3Pressed() => _showDebug = !_showDebug;

    public override void OnHotbarSelect(int slot) => _selectedSlot = Math.Clamp(slot, 0, 8);

    //EyeHeight eye height, same value as the server's ServerPlayer.EyeHeight; the pick origin is raised to eye level by it
    private const double EyeHeight = 1.62;

    //_actionSequence block action packet sequence number; the server acknowledges block changes by it; the client only guarantees it increments
    private int _actionSequence;

    //OnKeyPressed HUD business keys: Q drops the held item, E opens the inventory screen
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

    //OnMouseDown the left button breaks the block the crosshair points at; a minimal implementation of vanilla left-click mining
    //No mining progress or local prediction; a single click sends START then STOP and the server decides whether it actually breaks
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

    //NextActionSequence increments the block action sequence number
    private int NextActionSequence() => ++_actionSequence;

    public override void OnClose() => Manager.PushScreen(new PauseScreen());
    public override bool IsPauseScreen() => false;
}
