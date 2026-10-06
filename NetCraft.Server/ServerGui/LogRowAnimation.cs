using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Styling;

namespace NetCraft.Server.Gui;

//LogRowAnimation, entrance animation for log rows, shared by the main page and the log page
//Snapping the whole row in is too harsh, but animating hundreds of backfilled history rows at once would slow down window opening
internal static class LogRowAnimation
{
    //Appear, entrance animation for a new log row, fades in and shifts slightly upward
    private static readonly Animation Appear = new()
    {
        Duration = TimeSpan.FromMilliseconds(150),
        Easing = new CubicEaseOut(),
        //FillMode does not keep the end value by default, once the animation finishes it removes its own assignment layer
        //The property falls back to the local value written before the animation, the local fade-in start is 0, so the whole row flashes back to transparent
        //Forward keeps the last frame when the animation ends, removing that one-frame flicker
        FillMode = FillMode.Forward,
        Children =
        {
            new KeyFrame
            {
                Cue = new Cue(0d),
                Setters =
                {
                    new Setter(Visual.OpacityProperty, 0d),
                    new Setter(TranslateTransform.YProperty, 6d),
                },
            },
            new KeyFrame
            {
                Cue = new Cue(1d),
                Setters =
                {
                    new Setter(Visual.OpacityProperty, 1d),
                    new Setter(TranslateTransform.YProperty, 0d),
                },
            },
        },
    };

    //Play runs the entrance animation once, the caller just fires it and does not wait
    public static void Play(Visual target) => _ = RunAsync(target);

    //RunAsync writes the end value locally after the animation completes
    //FillAfter does not guarantee keeping the end value when the animation is cancelled, so it is set once more here, otherwise the row stays at Opacity=0 and never shows
    private static async Task RunAsync(Visual target)
    {
        //A row pushed out of the visible area mid-animation cancels the animation, an unhandled cancellation crash would reach the SynchronizationContext and kill the process
        try { await Appear.RunAsync(target); }
        catch (OperationCanceledException) { }
        target.Opacity = 1;
        if (target.RenderTransform is TranslateTransform transform) transform.Y = 0;
    }
}
