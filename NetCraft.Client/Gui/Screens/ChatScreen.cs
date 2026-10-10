using System.Text;
using NetCraft.Client.Gui;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
using NetCraft.Client.Render;
using NetCraft.Client.Render.Model;
using NetCraft.Client.Render.Texture;
using NetCraft.Client.Render.Texture.Atlas;
using NetCraft.Client.Render.Item;
using NetCraft.Client.Render.Entity;
using NetCraft.Client.Render.Entity.State;
using NetCraft.Client.Render.State.Gui;
using NetCraft.Client.Gui.Render;
using NetCraft.Client.Gui.Render.State;
using NetCraft.Client.Gui.Render.Pip;
using NetCraft.Client.Gui.Navigation;
using NetCraft.Client.Gui.Layouts;
using NetCraft.Client.Gui.Font;
using NetCraft.Client.Gui.Font.Providers;
using NetCraft.Client.Gui.Font.Glyphs;
using NetCraft.Client.Model;
using NetCraft.Client.Model.Geom;
using NetCraft.Client.Resources.Metadata.Gui;

namespace NetCraft.Client.Gui.Screens;

//ChatScreen maps to vanilla ChatScreen
//Input box + message history; Enter sends but networking is not wired up yet, only local accumulation
public sealed class ChatScreen : Screen
{
    private GuiTextBox? _inputBox;
    private GuiLabel? _historyLabel;
    private readonly List<string> _history = new();

    public override string Title => "Chat";

    public override void Init()
    {
        var inputY = GuiHeight - 30;
        _inputBox = AddWidget(new GuiTextBox { X = 4, Y = inputY, Width = GuiWidth - 8, Height = 20 });
        _historyLabel = AddWidget(new GuiLabel { X = 4, Y = inputY - 100, Width = GuiWidth - 8, Height = 90, ForegroundColor = GuiColor.White });
        Window.FocusedControl = _inputBox;
    }

    public override void Tick()
    {
        if (_historyLabel is null || _history.Count == 0) return;
        var sb = new StringBuilder();
        foreach (var msg in _history.Skip(Math.Max(0, _history.Count - 5)))
            sb.AppendLine(msg);
        _historyLabel.Text = sb.ToString();
    }

    //Send adds the message to history and clears the input box; networking not wired up yet
    public void Send(string message)
    {
        _history.Add(message);
        if (_inputBox is not null) _inputBox.Text = string.Empty;
    }

    public override void OnClose() => Manager.SetScreen(new GameScreen());
    public override bool IsPauseScreen() => false;
}
