using NetCraft.Game.Client.Inventory;
using NetCraft.Game.Gui;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.World.Inventory;
using NetCraft.Game.World.Items;
using NetCraft.Gpu;

namespace NetCraft.Game.Gui.Screens;

//InventoryScreen 背包界面对应原版 InventoryScreen
//槽位编号与坐标跟服务端 InventoryMenu 一致 点击与 Q 丢弃发 container_click 由服务端裁定
//物品图标走 ItemItemAtlas 的 cube 占位模型 数量叠在槽位右下角
public sealed class InventoryScreen : Screen
{
    //PanelWidth/PanelHeight 原版背包面板尺寸 176x166
    private const int PanelWidth = 176;
    private const int PanelHeight = 166;

    //SlotSize 槽位边长 18 含 1 像素边框 与原版一致
    private const int SlotSize = 18;

    //_slots 槽位号到面板内坐标 顺序与服务端 InventoryMenu 的 AddSlot 次序一致
    private static readonly IReadOnlyList<(int Index, int X, int Y)> Slots = BuildSlotLayout();

    private int _panelX;
    private int _panelY;

    public override string Title => "背包";

    public override void Init()
    {
        _panelX = (GuiWidth - PanelWidth) / 2;
        _panelY = (GuiHeight - PanelHeight) / 2;
    }

    //BuildSlotLayout 槽位布局与服务端 InventoryMenu 完全对齐 槽号即发往服务端的 slotNum
    private static IReadOnlyList<(int Index, int X, int Y)> BuildSlotLayout()
    {
        var slots = new List<(int, int, int)> { (InventoryMenu.ResultSlotIndex, 154, 28) };
        //1-4 合成格 2x2
        for (var i = 0; i < 4; i++)
            slots.Add((InventoryMenu.CraftSlotStart + i, 98 + i % 2 * 18, 18 + i / 2 * 18));
        //5-8 护甲
        for (var i = 0; i < 4; i++)
            slots.Add((InventoryMenu.ArmorSlotStart + i, 8, 8 + i * 18));
        //9-35 主物品栏 3x9
        for (var i = 0; i < 27; i++)
            slots.Add((InventoryMenu.InvSlotStart + i, 8 + i % 9 * 18, 84 + i / 9 * 18));
        //36-44 快捷栏
        for (var i = 0; i < 9; i++)
            slots.Add((InventoryMenu.UseRowSlotStart + i, 8 + i * 18, 142));
        //45 副手
        slots.Add((InventoryMenu.ShieldSlotIndex, 77, 62));
        return slots;
    }

    //Inventory 客户端物品栏镜像 由服务端容器包填充
    private ClientInventory? Inventory
        => (Minecraft.Connection?.Listener as ClientGamePacketListenerImpl)?.Inventory;

    //RenderBackground 画面板底与全部槽位框
    public override void RenderBackground(IGuiRenderContext context)
    {
        context.DrawQuad(_panelX - 2, _panelY - 2, PanelWidth + 4, PanelHeight + 4, GuiColor.FromRgb(60, 60, 60));
        context.DrawQuad(_panelX, _panelY, PanelWidth, PanelHeight, GuiColor.FromRgb(198, 198, 198));
        foreach (var (_, x, y) in Slots)
            DrawSlotFrame(context, _panelX + x, _panelY + y);
        context.DrawText(_panelX + 8, _panelY + 6, "背包", GuiColor.FromRgb(64, 64, 64));
    }

    //DrawSlotFrame 单个槽位框 外框深色内底浅色
    private static void DrawSlotFrame(IGuiRenderContext context, int x, int y)
    {
        context.DrawQuad(x, y, SlotSize, SlotSize, GuiColor.FromRgb(139, 139, 139));
        context.DrawQuad(x + 1, y + 1, SlotSize - 2, SlotSize - 2, GuiColor.FromRgb(55, 55, 55));
    }

    //RenderForeground 画槽内物品图标与数量 图标走物品图集 cube 占位模型
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
            //RegisterItem 幂等 首次见到该物品时建占位模型
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

    //GetStack 取菜单槽位物品 内容包未到时返回空栈
    private static ItemStack GetStack(ClientInventory inventory, int slot)
        => slot < inventory.MenuSlots.Count ? inventory.MenuSlots[slot] : ItemStack.Empty;

    //OnMouseDown 左键取放右键取一半 槽号与点击类型照原版发给服务端
    public override void OnMouseDown(GuiMouseButton button, int x, int y)
    {
        if (button is not (GuiMouseButton.Left or GuiMouseButton.Right)) return;
        var scale = Math.Max(1, Window.GuiScale);
        var slot = HitTest(x / scale, y / scale);
        if (slot < 0) return;
        SendClick(slot, button == GuiMouseButton.Right ? (byte)1 : (byte)0, ContainerInput.Pickup);
    }

    //OnKeyPressed Q 丢弃悬停槽的物品 对应原版背包装口里按 Q 丢鼠标指着的槽
    //本作拿不到修饰键 Ctrl+Q 丢整叠暂未接 一次只丢一个
    public override void OnKeyPressed(int key)
    {
        if (key != GameKeys.Q) return;
        var slot = HitTest(Manager.MouseX, Manager.MouseY);
        if (slot < 0) return;
        SendClick(slot, 0, ContainerInput.Throw);
    }

    //HitTest 面板内坐标命中哪个槽位 未命中返回 -1
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

    //SendClick 发容器点击包 服务端按槽号与点击类型裁定结果并回发槽位变更
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
