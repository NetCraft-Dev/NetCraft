using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Styling;

namespace NetCraft.Server.Gui;

//LogRowAnimation 日志行的入场动画 主页与日志页共用一份
//整行直接拍上去太生硬 一次性几百行的历史补看不做动画 那会把开窗拖慢
internal static class LogRowAnimation
{
    //Appear 新日志行的入场动画 淡入并微微上移
    private static readonly Animation Appear = new()
    {
        Duration = TimeSpan.FromMilliseconds(150),
        Easing = new CubicEaseOut(),
        //FillMode 默认不保留终值 动画一跑完就把自己那层赋值撤掉
        //属性回落到动画前写下的本地值 淡入的本地起点是 0 于是整行会闪回透明
        //Forward 让动画结束时留住最后一帧 这一帧的闪烁就没了
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

    //Play 播一遍入场动画 调用方只管发不用等
    public static void Play(Visual target) => _ = RunAsync(target);

    //RunAsync 动画跑完把终值落到本地
    //动画被取消时 FillAfter 不保证留住终值 这里补一次 否则该行会停在 Opacity=0 上再也看不见
    private static async Task RunAsync(Visual target)
    {
        //行在动画途中被挤出可见区会让动画取消 取消异常冲进 SynchronizationContext 会崩掉进程
        try { await Appear.RunAsync(target); }
        catch (OperationCanceledException) { }
        target.Opacity = 1;
        if (target.RenderTransform is TranslateTransform transform) transform.Y = 0;
    }
}
