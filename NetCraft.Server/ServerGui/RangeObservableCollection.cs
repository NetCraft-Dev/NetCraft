using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace NetCraft.Server.Gui;

//RangeObservableCollection 能批量加入的 ObservableCollection
//ObservableCollection 每加一项就发一次通知 列表控件要为每一项跑一遍容器逻辑
//批量加完发一次就够了 但那条通知必须是增量的 Add/Remove 不能是 Reset
public sealed class RangeObservableCollection<T> : ObservableCollection<T>
{
    private bool _batching;

    //AddRange 逐项加入 每项发一条增量 Add 通知
    //原先批量加完只发一条 Reset 虚拟化列表收到 Reset 会把容器全部丢掉重建
    //日志区每帧都要插一批 于是每帧重建一次 滚动条的偏移与长度跟着重算 看着就是上下高频乱跳
    //单线程下逐项 Add 的通知顺序与索引天然对得上 追加到末尾生成容器的开销也只是 O(1)
    public void AddRange(IEnumerable<T> items)
    {
        foreach (var item in items) Add(item);
    }

    //RemoveRange 从指定下标起逐项移除 同样走增量通知
    //日志面板到上限后每帧都要从头部裁 一笔一笔删的通知远少于一次 Reset 带来的整体重建
    public void RemoveRange(int index, int count)
    {
        for (var i = 0; i < count; i++) RemoveAt(index);
    }

    //ReplaceAll 整体换一批 只发一条 Reset
    //留给首次铺满与过滤重排这类一次性动作 它们本来就换了整份内容 重建一次是应该的
    //常规的每帧追加必须走 AddRange 否则又回到每帧重建
    public void ReplaceAll(IEnumerable<T> items)
    {
        _batching = true;
        try
        {
            Clear();
            foreach (var item in items) Add(item);
        }
        finally
        {
            _batching = false;
        }
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (_batching) return;
        base.OnCollectionChanged(e);
    }
}
