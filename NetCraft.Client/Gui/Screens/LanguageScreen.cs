using NetCraft;
using NetCraft.Game.Gui;
using NetCraft.Gpu;
using NetCraft.Logging;

namespace NetCraft.Game.Gui.Screens;

//LanguageScreen 语言选择对应原版 net.minecraft.client.gui.screens.LanguageSelectScreen
//列出资源包声明的全部语言 点选即写入配置并重跑重载链让语言表立刻换掉
//原版是可滚动列表 这里用固定行数加右侧滚动条 控件体系暂没有滚动列表
public sealed class LanguageScreen : Screen
{
    private const int RowHeight = 20;
    private const int RowGap = 2;

    //_entries 语言码与显示名 按语言码排序
    private readonly List<(string Code, string Name)> _entries = new();
    //_rows 当前可见行控件 _visibleCodes 与之一一对应记录该行承载的语言码
    private readonly List<GuiButton> _rows = new();
    private readonly List<string> _visibleCodes = new();
    private int _topIndex;
    private GuiSlider? _scroll;

    public override string Title => "语言";

    public override void Init()
    {
        var skin = RegisterButtonSprites();
        var cx = GuiWidth / 2;
        var listTop = 44;
        //行数按窗口高度自适应 免得小窗口下按钮溢出到完成按钮上
        var rowCount = Math.Clamp((GuiHeight - 100) / (RowHeight + RowGap), 4, 14);
        var listWidth = Math.Max(120, GuiWidth - 100);

        BuildEntries();

        AddWidget(new GuiLabel("语言") { X = cx - 100, Y = 16, Width = 200, Height = 20 });

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
        //条目放不下一屏才需要滚动
        _scroll.Enabled = maxTop > 0;

        var done = AddWidget(new GuiButton("完成")
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

    //BuildEntries 取资源包声明的可选语言 显示名缺省退回语言码
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

    //RefreshRows 把滚动窗口内的语言刷到按钮上 当前语言加标记
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

    //SelectSlot 点选可见行 空行忽略 选中的是当前语言就直接退回
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

    //Apply 写入语言码 存回 options.txt 并重跑重载链让语言表立刻生效
    //重载链会连同 Tags 配方一起重跑 与原版切语言走 reloadResourcePacks 的行为一致
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
