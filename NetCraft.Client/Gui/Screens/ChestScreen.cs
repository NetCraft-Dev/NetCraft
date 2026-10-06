using NetCraft.Game.Client.Inventory;
using NetCraft.Game.Gui;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Items;
using NetCraft.Gpu;
using NetCraft.Network.Chat;
using NetCraft.Network.Inventory;

namespace NetCraft.Game.Gui.Screens;

//ChestScreen chest-style container screen, maps to vanilla ContainerScreen's 9x3 layout
//Slot numbers and coordinates match the server's ChestMenu: container 0-26, inventory 27-53, hotbar 54-62
public sealed class ChestScreen : Screen
{
    //PanelWidth vanilla chest panel width 176
    private const int PanelWidth = 176;

    //SlotSize slot edge length 18, including a 1-pixel border
    private const int SlotSize = 18;

    private readonly Component _title;
    private readonly int _panelHeight;
    private readonly IReadOnlyList<(int Index, int X, int Y)> _slots;
    private int _panelX;
    private int _panelY;

    public ChestScreen(Component title, int rows)
    {
        _title = title;
        _panelHeight = 112 + rows * 18;
        _slots = BuildSlotLayout(rows);
    }

    //RowsFor menu type to container row count; the server only sends the six tiers 9x1 to 9x6
    public static int RowsFor(MenuType kind)
        => ReferenceEquals(kind, MenuTypes.GENERIC_9X1) ? 1
            : ReferenceEquals(kind, MenuTypes.GENERIC_9X2) ? 2
            : ReferenceEquals(kind, MenuTypes.GENERIC_9X3) ? 3
            : ReferenceEquals(kind, MenuTypes.GENERIC_9X4) ? 4
            : ReferenceEquals(kind, MenuTypes.GENERIC_9X5) ? 5
            : 6;

    //IsChestKind whether it is a chest-style screen; crafting-table-style screens have a different slot layout and are not implemented, so the chest panel must not stand in for them
    public static bool IsChestKind(MenuType kind)
        => ReferenceEquals(kind, MenuTypes.GENERIC_9X1)
            || ReferenceEquals(kind, MenuTypes.GENERIC_9X2)
            || ReferenceEquals(kind, MenuTypes.GENERIC_9X3)
            || ReferenceEquals(kind, MenuTypes.GENERIC_9X4)
            || ReferenceEquals(kind, MenuTypes.GENERIC_9X5)
            || ReferenceEquals(kind, MenuTypes.GENERIC_9X6);

    public override string Title => _title.GetString();

    public override void Init()
    {
        _panelX = (GuiWidth - PanelWidth) / 2;
        _panelY = (GuiHeight - _panelHeight) / 2;
    }

    //BuildSlotLayout slot layout fully aligned with the server's ChestMenu; the slot number is the slotNum sent to the server
    private static IReadOnlyList<(int Index, int X, int Y)> BuildSlotLayout(int rows)
    {
        var slots = new List<(int, int, int)>();
        //The container grid starts at (8,18) inside the panel
        for (var i = 0; i < rows * 9; i++)
            slots.Add((i, 8 + i % 9 * 18, 18 + i / 9 * 18));
        //Leave 13 pixels between the player inventory top and the chest grid bottom; the hotbar is 58 pixels further down
        var inventoryTop = 18 + rows * 18 + 13;
        var containerSlots = rows * 9;
        for (var i = 0; i < PlayerInventory.MainSlots; i++)
            slots.Add((containerSlots + i, 8 + i % 9 * 18, inventoryTop + i / 9 * 18));
        for (var i = 0; i < PlayerInventory.HotbarSlots; i++)
            slots.Add((containerSlots + PlayerInventory.MainSlots + i, 8 + i * 18, inventoryTop + 58));
        return slots;
    }

    //Inventory client inventory mirror, filled by server container packets
    private ClientInventory? Inventory
        => (Minecraft.Connection?.Listener as ClientGamePacketListenerImpl)?.Inventory;

    public override void RenderBackground(IGuiRenderContext context)
    {
        context.DrawQuad(_panelX - 2, _panelY - 2, PanelWidth + 4, _panelHeight + 4, GuiColor.FromRgb(60, 60, 60));
        context.DrawQuad(_panelX, _panelY, PanelWidth, _panelHeight, GuiColor.FromRgb(198, 198, 198));
        foreach (var (_, x, y) in _slots)
            DrawSlotFrame(context, _panelX + x, _panelY + y);
        context.DrawText(_panelX + 8, _panelY + 6, _title.GetString(), GuiColor.FromRgb(64, 64, 64));
    }

    private static void DrawSlotFrame(IGuiRenderContext context, int x, int y)
    {
        context.DrawQuad(x, y, SlotSize, SlotSize, GuiColor.FromRgb(139, 139, 139));
        context.DrawQuad(x + 1, y + 1, SlotSize - 2, SlotSize - 2, GuiColor.FromRgb(55, 55, 55));
    }

    public override void RenderForeground(IGuiRenderContext context)
    {
        var gpu = Minecraft.GpuApp;
        var atlas = gpu?.ItemAtlas;
        var textureId = gpu?.ItemAtlasTextureId ?? 0;
        var inventory = Inventory;
        if (atlas is null || textureId == 0 || inventory is null) return;
        const int iconSize = 16;
        const int iconOffset = (SlotSize - iconSize) / 2;
        foreach (var (index, sx, sy) in _slots)
        {
            var stack = GetStack(inventory, index);
            if (stack.IsEmpty()) continue;
            var identity = stack.GetItem().Id.ToString();
            atlas.RegisterItem(identity);
            var slot = atlas.GetOrUpdate(identity, isAnimated: false);
            if (slot is null) continue;
            var x = _panelX + sx + iconOffset;
            var y = _panelY + sy + iconOffset;
            var srcX = (int)(slot.U0 * atlas.TextureSize);
            var srcY = (int)(slot.V0 * atlas.TextureSize);
            context.DrawImage(textureId, x, y, iconSize, iconSize,
                srcX, srcY, atlas.SlotTextureSize, atlas.SlotTextureSize, GuiColor.White);
            if (stack.GetCount() > 1)
                context.DrawText(x + 11, y + 9, stack.GetCount().ToString(), GuiColor.White);
        }
    }

    private static ItemStack GetStack(ClientInventory inventory, int slot)
        => slot < inventory.MenuSlots.Count ? inventory.MenuSlots[slot] : ItemStack.Empty;

    public override void OnMouseDown(GuiMouseButton button, int x, int y)
    {
        if (button is not (GuiMouseButton.Left or GuiMouseButton.Right)) return;
        var scale = Math.Max(1, Window.GuiScale);
        var slot = HitTest(x / scale, y / scale);
        if (slot < 0) return;
        SendClick(slot, button == GuiMouseButton.Right ? (byte)1 : (byte)0, ContainerInput.Pickup);
    }

    public override void OnKeyPressed(int key)
    {
        if (key != GameKeys.Q) return;
        var slot = HitTest(Manager.MouseX, Manager.MouseY);
        if (slot < 0) return;
        SendClick(slot, 0, ContainerInput.Throw);
    }

    private int HitTest(int x, int y)
    {
        foreach (var (index, sx, sy) in _slots)
        {
            var left = _panelX + sx;
            var top = _panelY + sy;
            if (x >= left && x < left + SlotSize && y >= top && y < top + SlotSize) return index;
        }
        return -1;
    }

    //SendClick sends the container click packet; the server decides by slot number and click type and sends back slot changes
    private void SendClick(int slot, byte button, ContainerInput input)
    {
        var connection = Minecraft.Connection;
        var inventory = Inventory;
        if (connection is null || inventory is null || inventory.ContainerId < 0) return;
        connection.Send(new ServerboundContainerClickPacket(
            inventory.ContainerId, inventory.StateId, (short)slot, button, input,
            new Dictionary<int, HashedStack>(), HashedStack.Empty));
    }

    public override void OnClose()
    {
        var connection = Minecraft.Connection;
        var inventory = Inventory;
        if (connection is not null && inventory is not null && inventory.ContainerId >= 0)
            connection.Send(new ServerboundContainerClosePacket(inventory.ContainerId));
        Manager.SetScreen(new GameScreen());
    }

    public override bool IsPauseScreen() => true;
}
