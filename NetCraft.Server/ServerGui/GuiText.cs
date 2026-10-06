using NetCraft.Game;

namespace NetCraft.Server.Gui;

//GuiText, static text used in the panel axaml
//XAML can only bind properties, so text keys become static properties fetched via {x:Static}
//Panels are built once at startup, the language code comes from nc-language in server.properties and never changes at runtime, one read is enough
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
