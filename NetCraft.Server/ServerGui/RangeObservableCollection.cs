using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace NetCraft.Server.Gui;

//RangeObservableCollection, an ObservableCollection that supports bulk adds
//ObservableCollection raises a notification per add, forcing the list control to run its container logic for every item
//One notification after a bulk add is enough, but that notification must be an incremental Add/Remove, not a Reset
public sealed class RangeObservableCollection<T> : ObservableCollection<T>
{
    private bool _batching;

    //AddRange adds items one by one, each emitting an incremental Add notification
    //Previously a bulk add emitted a single Reset, and a virtualized list drops and rebuilds all containers on Reset
    //The log area inserts a batch every frame, so it rebuilt every frame and the scrollbar offset and length were recomputed along with it, which looked like high-frequency jumping up and down
    //On a single thread the order and indices of per-item Add notifications line up naturally, and appending at the end costs only O(1) to create a container
    public void AddRange(IEnumerable<T> items)
    {
        foreach (var item in items) Add(item);
    }

    //RemoveRange removes items one by one from the given index, also using incremental notifications
    //Once the log panel hits its cap it trims from the head every frame, and per-item removals are far cheaper than the full rebuild a Reset triggers
    public void RemoveRange(int index, int count)
    {
        for (var i = 0; i < count; i++) RemoveAt(index);
    }

    //ReplaceAll swaps the whole content, emitting a single Reset
    //Reserved for one-off actions like the initial fill and filter reordering, they replace the entire content anyway so one rebuild is expected
    //Regular per-frame appends must go through AddRange, otherwise it falls back to rebuilding every frame
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
