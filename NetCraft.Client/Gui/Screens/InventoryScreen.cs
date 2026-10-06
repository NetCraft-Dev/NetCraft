using NetCraft.Game.Client.Inventory;
using NetCraft.Game.Gui;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Items;
using NetCraft.Gpu;

namespace NetCraft.Game.Gui.Screens;

//InventoryScreen inventory screen, maps to vanilla InventoryScreen
//Slot numbers and coordinates match the server's InventoryMenu; clicks and Q drops send container_click and the server decides
//Item icons use ItemItemAtlas's cube placeholder model; the count overlays the slot's bottom-right corner
public sealed class InventoryScreen : Screen
{
    //PanelWidth/PanelHeight vanilla inventory panel size 176x166
    private const int PanelWidth = 176;
    private const int PanelHeight = 166;

    //SlotSize slot edge length 18 including a 1-pixel border, matching vanilla
    private const int SlotSize = 18;

    //_slots slot number to panel coordinates; order matches the server InventoryMenu's AddSlot sequence
    private static readonly IReadOnlyList<(int Index, int X, int Y)> Slots = BuildSlotLayout();

    private int _panelX;
    private int _panelY;

    public override string Title => "Inventory";

    public override void Init()
    {
        _panelX = (GuiWidth - PanelWidth) / 2;
        _panelY = (GuiHeight - PanelHeight) / 2;
    }

    //BuildSlotLayout slot layout fully aligned with the server's InventoryMenu; the slot number is the slotNum sent to the server
    private static IReadOnlyList<(int Index, int X, int Y)> BuildSlotLayout()
    {
        var slots = new List<(int, int, int)> { (InventoryMenu.ResultSlotIndex, 154, 28) };
        //1-4 crafting grid 2x2
        for (var i = 0; i < 4; i++)
            slots.Add((InventoryMenu.CraftSlotStart + i, 98 + i % 2 * 18, 18 + i / 2 * 18));
        //5-8 armor
        for (var i = 0; i < 4; i++)
            slots.Add((InventoryMenu.ArmorSlotStart + i, 8, 8 + i * 18));
        //9-35 main inventory 3x9
        for (var i = 0; i < 27; i++)
            slots.Add((InventoryMenu.InvSlotStart + i, 8 + i % 9 * 18, 84 + i / 9 * 18));
        //36-44 hotbar
        for (var i = 0; i < 9; i++)
            slots.Add((InventoryMenu.UseRowSlotStart + i, 8 + i * 18, 142));
        //45 offhand
        slots.Add((InventoryMenu.ShieldSlotIndex, 77, 62));
        return slots;
    }

    //Inventory client inventory mirror, filled by server container packets
    private ClientInventory? Inventory
        => (Minecraft.Connection?.Listener as ClientGamePacketListenerImpl)?.Inventory;

    //RenderBackground draws the panel background and all slot frames
    public override void RenderBackground(IGuiRenderContext context)
    {
        context.DrawQuad(_panelX - 2, _panelY - 2, PanelWidth + 4, PanelHeight + 4, GuiColor.FromRgb(60, 60, 60));
        context.DrawQuad(_panelX, _panelY, PanelWidth, PanelHeight, GuiColor.FromRgb(198, 198, 198));
        foreach (var (_, x, y) in Slots)
            DrawSlotFrame(context, _panelX + x, _panelY + y);
        context.DrawText(_panelX + 8, _panelY + 6, "Inventory", GuiColor.FromRgb(64, 64, 64));
    }

    //DrawSlotFrame a single slot frame: dark outer border, light inner base
    private static void DrawSlotFrame(IGuiRenderContext context, int x, int y)
    {
        context.DrawQuad(x, y, SlotSize, SlotSize, GuiColor.FromRgb(139, 139, 139));
        context.DrawQuad(x + 1, y + 1, SlotSize - 2, SlotSize - 2, GuiColor.FromRgb(55, 55, 55));
    }

    //RenderForeground draws item icons and counts in slots; icons use the item atlas's cube placeholder model
    public override void RenderForeground(IGuiRenderContext context)
    {
        var gpu = Minecraft.GpuApp;
        var atlas = gpu?.ItemAtlas;
        var textureId = gpu?.ItemAtlasTextureId ?? 0;
        var inventory = Inventory;
        if (atlas is null || textureId == 0 || inventory is null) return;
        const int iconSize = 16;
        const int iconOffset = (SlotSize - iconSize) / 2;
        foreach (var (index, sx, sy) in Slots)
        {
            var stack = GetStack(inventory, index);
            if (stack.IsEmpty()) continue;
            var identity = stack.GetItem().Id.ToString();
            //RegisterItem is idempotent; builds the placeholder model the first time the item is seen
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

    //GetStack gets the menu slot item; returns an empty stack before the content packet arrives
    private static ItemStack GetStack(ClientInventory inventory, int slot)
        => slot < inventory.MenuSlots.Count ? inventory.MenuSlots[slot] : ItemStack.Empty;

    //OnMouseDown left click picks up/places, right click takes half; slot number and click type are sent to the server as in vanilla
    public override void OnMouseDown(GuiMouseButton button, int x, int y)
    {
        if (button is not (GuiMouseButton.Left or GuiMouseButton.Right)) return;
        var scale = Math.Max(1, Window.GuiScale);
        var slot = HitTest(x / scale, y / scale);
        if (slot < 0) return;
        SendClick(slot, button == GuiMouseButton.Right ? (byte)1 : (byte)0, ContainerInput.Pickup);
    }

    //OnKeyPressed Q drops the item in the hovered slot, maps to vanilla pressing Q in the inventory to drop the slot under the cursor
    //This build cannot get modifier keys; Ctrl+Q to drop a whole stack is not wired up, it drops one at a time
    public override void OnKeyPressed(int key)
    {
        if (key != GameKeys.Q) return;
        var slot = HitTest(Manager.MouseX, Manager.MouseY);
        if (slot < 0) return;
        SendClick(slot, 0, ContainerInput.Throw);
    }

    //HitTest which slot the panel coordinates hit; returns -1 when none
    private int HitTest(int x, int y)
    {
        foreach (var (index, sx, sy) in Slots)
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
