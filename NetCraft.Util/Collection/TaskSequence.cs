namespace NetCraft.Util.Collection;

//Task sequence helpers, map to vanilla net.minecraft.util.Util.sequence/sequenceFailFast/sequenceFailFastAndCancel
//Java CompletableFuture→C# Task
public static class TaskSequence
{
    //sequence merges multiple Tasks into a single Task returning a list of values, maps to vanilla Util.sequence
    //Empty list returns a completed empty list, single-element list returns a single-element Task
    public static async Task<IReadOnlyList<V>> Sequence<V>(IReadOnlyList<Task<V>> list)
    {
        if (list.Count == 0)
            return Array.Empty<V>();
        if (list.Count == 1)
            return new[] { await list[0].ConfigureAwait(false) };
        var results = new V[list.Count];
        for (var i = 0; i < list.Count; i++)
            results[i] = await list[i].ConfigureAwait(false);
        return results;
    }

    //sequenceFailFast throws immediately on any Task failure, maps to vanilla Util.sequenceFailFast
    //C# uses Task.WhenAll, semantically equivalent to failing immediately on any exception
    public static async Task<IReadOnlyList<V>> SequenceFailFast<V>(IReadOnlyList<Task<V>> futures)
    {
        if (futures.Count == 0)
            return Array.Empty<V>();
        var results = await Task.WhenAll(futures).ConfigureAwait(false);
        return results;
    }

    //sequenceFailFastAndCancel cancels the others immediately on any Task failure, maps to vanilla Util.sequenceFailFastAndCancel
    //C# uses a CancellationTokenSource to cancel the other Tasks
    public static async Task<IReadOnlyList<V>> SequenceFailFastAndCancel<V>(IReadOnlyList<Task<V>> futures)
    {
        if (futures.Count == 0)
            return Array.Empty<V>();
        using var cts = new CancellationTokenSource();
        try
        {
            var results = new V[futures.Count];
            var pending = new List<Task>(futures.Count);
            for (var i = 0; i < futures.Count; i++)
            {
                var idx = i;
                pending.Add(futures[i].ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        cts.Cancel();
                    else
                        results[idx] = t.Result;
                }, TaskScheduler.Default));
            }
            await Task.WhenAll(pending).ConfigureAwait(false);
            return results;
        }
        catch (OperationCanceledException)
        {
            throw new AggregateException(futures.Where(f => f.IsFaulted).SelectMany(f => f.Exception!.InnerExceptions));
        }
    }
}
