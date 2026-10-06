using NetCraft;
using NetCraft.Game.Gui;
using NetCraft.Gpu;
using NetCraft.Logging;

namespace NetCraft.Game.Gui.Screens;

//LanguageScreen language selection, maps to vanilla net.minecraft.client.gui.screens.LanguageSelectScreen
//Lists all languages declared by resource packs; clicking one writes the config and reruns the reload chain so the language table changes immediately
//Vanilla uses a scrollable list; here a fixed row count plus a right-side scrollbar is used since the control system has no scroll list yet
public sealed class LanguageScreen : Screen
{
    private const int RowHeight = 20;
    private const int RowGap = 2;

    //_entries language code and display name, sorted by language code
    private readonly List<(string Code, string Name)> _entries = new();
    //_rows current visible row controls; _visibleCodes maps one-to-one and records the language code each row carries
    private readonly List<GuiButton> _rows = new();
    private readonly List<string> _visibleCodes = new();
    private int _topIndex;
    private GuiSlider? _scroll;

    public override string Title => "Language";

    public override void Init()
    {
        var skin = RegisterButtonSprites();
        var cx = GuiWidth / 2;
        var listTop = 44;
        //Row count adapts to the window height so buttons do not overflow onto the Done button in small windows
        var rowCount = Math.Clamp((GuiHeight - 100) / (RowHeight + RowGap), 4, 14);
        var listWidth = Math.Max(120, GuiWidth - 100);

        BuildEntries();

        AddWidget(new GuiLabel("Language") { X = cx - 100, Y = 16, Width = 200, Height = 20 });

        for (int i = 0; i < rowCount; i++)
        {
            var slot = i;
            var button = AddWidget(new GuiButton(string.Empty)
            {
                X = 20,
                Y = listTop + i * (RowHeight + RowGap),
                Width = listWidth,
                Height = RowHeight
            });
            ApplyButtonSpriteSkin(button, skin);
            button.Click += (_, _) => SelectSlot(slot);
            _rows.Add(button);
            _visibleCodes.Add(string.Empty);
        }

        var maxTop = Math.Max(0, _entries.Count - rowCount);
        _scroll = AddWidget(new GuiSlider(0, maxTop, 0)
        {
            X = 20 + listWidth + 8,
            Y = listTop,
            Width = 20,
            Height = rowCount * (RowHeight + RowGap) - RowGap
        });
        _scroll.ValueChanged += (_, _) =>
        {
            _topIndex = (int)_scroll.Value;
            RefreshRows();
        };
        //Scrolling is only needed when entries do not fit on one screen
        _scroll.Enabled = maxTop > 0;

        var done = AddWidget(new GuiButton("Done")
        {
            X = cx - 100,
            Y = GuiHeight - 30,
            Width = 200,
            Height = 20
        });
        ApplyButtonSpriteSkin(done, skin);
        done.Click += (_, _) => Manager.PopScreen();

        RefreshRows();
    }

    //BuildEntries fetches the languages declared by resource packs; the display name falls back to the language code when missing
    private void BuildEntries()
    {
        _entries.Clear();
        var available = Minecraft.Language?.AvailableLanguages;
        if (available is not null)
        {
            foreach (var pair in available)
            {
                var name = string.IsNullOrEmpty(pair.Value.Name) ? pair.Key : pair.Value.Name;
                _entries.Add((pair.Key, name));
            }
        }
        _entries.Sort((a, b) => string.CompareOrdinal(a.Code, b.Code));
    }

    //RefreshRows refreshes the languages in the scroll window onto the buttons, marking the current language
    private void RefreshRows()
    {
        var current = Minecraft.Language?.LanguageCode ?? string.Empty;
        for (int i = 0; i < _rows.Count; i++)
        {
            var index = _topIndex + i;
            if (index >= _entries.Count)
            {
                _visibleCodes[i] = string.Empty;
                _rows[i].Text = string.Empty;
                _rows[i].Enabled = false;
                continue;
            }
            var (code, name) = _entries[index];
            _visibleCodes[i] = code;
            _rows[i].Text = code == current ? "> " + name : name;
            _rows[i].Enabled = true;
        }
    }

    //SelectSlot clicks a visible row; empty rows are ignored, and selecting the current language just pops back
    private void SelectSlot(int slot)
    {
        if (slot < 0 || slot >= _visibleCodes.Count) return;
        var code = _visibleCodes[slot];
        if (code.Length == 0) return;
        if (string.Equals(code, Minecraft.Language?.LanguageCode, StringComparison.Ordinal))
        {
            Manager.PopScreen();
            return;
        }
        Apply(code);
    }

    //Apply writes the language code, saves it back to options.txt and reruns the reload chain so the language table takes effect immediately
    //The reload chain also reruns Tags and recipes, matching vanilla's behavior of switching language via reloadResourcePacks
    private void Apply(string code)
    {
        var language = Minecraft.Language;
        if (language is null) return;
        language.LanguageCode = code;
        Minecraft.Config.Language = code;
        Minecraft.Config.Save(AppPaths.OptionsPath);
        Minecraft.Resources?.Reload();
        Log.Info($"Language switched to {code}");
        Manager.PopScreen();
    }
}
