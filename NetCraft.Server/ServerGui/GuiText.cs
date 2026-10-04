using NetCraft.Game;

namespace NetCraft.Server.Gui;

//GuiText 面板 axaml 里的静态文案
//xaml 只能绑属性 这里把文案键转成静态属性供 {x:Static} 取
//面板在启动时构建一次 语言码来自 server.properties 的 nc-language 运行期不变 取一次就够
public static class GuiText
{
    public static string ChunkTerrain => Loc.Get("netcraft.gui.chunk.terrain");

    public static string ChunkPlayers => Loc.Get("netcraft.gui.chunk.players");

    public static string ChunkGotoSection => Loc.Get("netcraft.gui.chunk.goto_section");

    public static string ChunkGoto => Loc.Get("netcraft.gui.chunk.goto");

    public static string ChunkStrong => Loc.Get("netcraft.gui.chunk.strong");

    public static string ChunkWeak => Loc.Get("netcraft.gui.chunk.weak");

    public static string ChunkUnloaded => Loc.Get("netcraft.gui.chunk.unloaded");

    public static string ChunkWorldCenter => Loc.Get("netcraft.gui.chunk.world_center");

    public static string LogSearchWatermark => Loc.Get("netcraft.gui.log.search_watermark");
}
