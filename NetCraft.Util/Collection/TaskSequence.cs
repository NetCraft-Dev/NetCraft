namespace NetCraft.Util.Collection;

//Task序列工具对应原版net.minecraft.util.Util.sequence/sequenceFailFast/sequenceFailFastAndCancel
//Java CompletableFuture→C# Task
public static class TaskSequence
{
    //sequence合并多个Task为单一Task返回值列表对应原版Util.sequence
    //空列表返回已完成空列表单元素列表返回单元素Task
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

    //sequenceFailFast任一Task失败立即抛对应原版Util.sequenceFailFast
    //C#用Task.WhenAll语义等同任一异常立刻返回失败
    public static async Task<IReadOnlyList<V>> SequenceFailFast<V>(IReadOnlyList<Task<V>> futures)
    {
        if (futures.Count == 0)
            return Array.Empty<V>();
        var results = await Task.WhenAll(futures).ConfigureAwait(false);
        return results;
    }

    //sequenceFailFastAndCancel任一Task失败立即取消其他对应原版Util.sequenceFailFastAndCancel
    //C#用CancellationTokenSource取消其他Task
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
