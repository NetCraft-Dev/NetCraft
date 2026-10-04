using NetCraft.Util.Profiling;
using NetCraft.Util.Profiling.Metrics.Storage;

namespace NetCraft.Util.Profiling.Metrics.Profiling;

//活跃指标记录器对应原版net.minecraft.util.profiling.metrics.profiling.ActiveMetricsRecorder
//按tick调度MetricSampler超阈值时记录偏差到RecordedDeviation
//达到截止时间或killSwitch触发后持久化结果
public sealed class ActiveMetricsRecorder : MetricsRecorder
{
    public const int ProfilingMaxDurationSeconds = 10;

    private static Action<string>? _globalOnReportFinished;

    private readonly ContinuousProfiler _taskProfiler;
    private readonly Action<Action> _ioExecutor;
    private readonly MetricsPersister _metricsPersister;
    private readonly Action<ProfileResults> _onProfilingEnd;
    private readonly Action<string> _onReportFinished;
    private readonly MetricsSamplerProvider _metricsSamplerProvider;
    private readonly Func<long> _wallTimeSource;
    private readonly long _deadlineNano;
    private int _currentTick;
    private ProfileCollector _singleTickProfiler;
    private volatile bool _killSwitch;
    private readonly Dictionary<MetricSampler, List<RecordedDeviation>> _deviationsBySampler = new();
    private ISet<MetricSampler> _thisTickSamplers = new HashSet<MetricSampler>();

    private ActiveMetricsRecorder(MetricsSamplerProvider metricsSamplerProvider, Func<long> timeSource,
        Action<Action> ioExecutor, MetricsPersister metricsPersister,
        Action<ProfileResults> onProfilingEnd, Action<string> onReportFinished)
    {
        _metricsSamplerProvider = metricsSamplerProvider;
        _wallTimeSource = timeSource;
        _taskProfiler = new ContinuousProfiler(timeSource, () => _currentTick, () => false);
        _ioExecutor = ioExecutor;
        _metricsPersister = metricsPersister;
        _onProfilingEnd = onProfilingEnd;
        _onReportFinished = _globalOnReportFinished is null
            ? onReportFinished
            : path => { onReportFinished(path); _globalOnReportFinished?.Invoke(path); };
        _deadlineNano = timeSource() + ProfilingMaxDurationSeconds * 1_000_000_000L;
        _singleTickProfiler = new ActiveProfiler(_wallTimeSource, () => _currentTick, () => true);
        _taskProfiler.Enable();
    }

    public static ActiveMetricsRecorder CreateStarted(MetricsSamplerProvider metricsSamplerProvider, Func<long> timeSource,
        Action<Action> ioExecutor, MetricsPersister metricsPersister,
        Action<ProfileResults> onProfilingEnd, Action<string> onReportFinished)
        => new(metricsSamplerProvider, timeSource, ioExecutor, metricsPersister, onProfilingEnd, onReportFinished);

    public void End()
    {
        lock (this)
        {
            if (!IsRecording()) return;
            _killSwitch = true;
        }
    }

    public void Cancel()
    {
        lock (this)
        {
            if (!IsRecording()) return;
            _singleTickProfiler = InactiveProfiler.Instance;
            _onProfilingEnd(EmptyProfileResults.Empty);
            Cleanup(_thisTickSamplers);
        }
    }

    public void StartTick()
    {
        VerifyStarted();
        _thisTickSamplers = _metricsSamplerProvider.Samplers(() => _singleTickProfiler);
        foreach (var sampler in _thisTickSamplers) sampler.OnStartTick();
        _currentTick++;
    }

    public void SampleDuringExtract() => Sample(MetricSampler.SamplingPhase.Extract);

    public void EndTick()
    {
        Sample(MetricSampler.SamplingPhase.EndTick);
        if (_killSwitch || _wallTimeSource() > _deadlineNano)
        {
            _killSwitch = false;
            var results = _taskProfiler.GetResults();
            _singleTickProfiler = InactiveProfiler.Instance;
            _onProfilingEnd(results);
            ScheduleSaveResults(results);
            return;
        }
        _singleTickProfiler = new ActiveProfiler(_wallTimeSource, () => _currentTick, () => true);
    }

    private void Sample(MetricSampler.SamplingPhase samplingPhase)
    {
        VerifyStarted();
        if (_currentTick == 0) return;
        foreach (var sampler in _thisTickSamplers)
        {
            if (sampler.Phase == samplingPhase)
            {
                sampler.OnEndTick(_currentTick);
                if (sampler.TriggersThreshold())
                {
                    var deviation = new RecordedDeviation(DateTimeOffset.Now, _currentTick, _singleTickProfiler.GetResults());
                    if (!_deviationsBySampler.TryGetValue(sampler, out var list))
                    {
                        list = new List<RecordedDeviation>();
                        _deviationsBySampler[sampler] = list;
                    }
                    list.Add(deviation);
                }
            }
        }
    }

    public bool IsRecording() => _taskProfiler.IsEnabled();

    public ProfilerFiller GetProfiler()
        => ProfilerFiller.Combine(_taskProfiler.GetFiller(), _singleTickProfiler);

    private void VerifyStarted()
    {
        if (!IsRecording()) throw new InvalidOperationException("Not started!");
    }

    private void ScheduleSaveResults(ProfileResults profilerResults)
    {
        var samplers = new HashSet<MetricSampler>(_thisTickSamplers);
        _ioExecutor(() =>
        {
            var path = _metricsPersister.SaveReports(samplers, _deviationsBySampler, profilerResults);
            Cleanup(samplers);
            _onReportFinished(path);
        });
    }

    private void Cleanup(ICollection<MetricSampler> samplers)
    {
        foreach (var sampler in samplers) sampler.OnFinished();
        _deviationsBySampler.Clear();
        _taskProfiler.Disable();
    }

    public static void RegisterGlobalCompletionCallback(Action<string> onFinished)
        => _globalOnReportFinished = onFinished;
}
