# NetCraft.Client.Gui

The business-layer GUI of the client. It maps to the vanilla `net.minecraft.client.gui` package — the client's screens and in-game HUD — and sits on top of the generic UI framework provided by `NetCraft.Gpu`.

## Features

- Screen stack and lifecycle: a `Screen` base class with `Init`/`Removed`/`OnClose`/`Tick`/`RenderBackground`, plus `ScreenManager` for `SetScreen`/`PushScreen`/`PopScreen`, resize handling and raw key dispatch.
- Business screens: title, pause, options, inventory, chat, chest, language, and the in-game `GameScreen` HUD.
- In-game HUD rendering: hotbar, crosshair, and the health/hunger/armor/air/experience bars (`HeartRenderer`/`HeartType`).
- Game key bindings: `GameKeys` code constants (Esc, F3, number and keypad keys) bridged from the window's raw key events.
- Composes game-specific widgets from the generic controls supplied by `NetCraft.Gpu` instead of reimplementing them. Generic UI concerns — control bases, layouts, render context and fonts — stay in `NetCraft.Gpu`; all drawing goes through `IGuiRenderContext` (`DrawImage`/`DrawText`/`DrawQuad`, scissor and pose push/pop), matching vanilla `GuiGraphics` semantics, and GUI scale is an integer multiple derived from `GuiWindow.GuiScale`/`ScaledWidth`/`ScaledHeight`.
