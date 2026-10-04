using System.Globalization;

namespace NetCraft.Util.Profiling.Metrics;

//指标采样器对应原版net.minecraft.util.profiling.metrics.MetricSampler
//按tick采集double值存入List+二进制流支持阈值告警
public class MetricSampler
{
    private readonly string _name;
    private readonly SamplingPhase _samplingPhase;
    private readonly MetricCategory _category;
    private readonly Func<double> _sampler;
    private readonly Action? _beforeTick;
    internal readonly ThresholdTest? _thresholdTest;
    private double _currentValue;
    private readonly List<double> _values = new();
    private readonly List<int> _ticks = new();
    private volatile bool _isRunning = true;

    //采样阶段对应原版SamplingPhase标识在extract还是endTick阶段采样
    public enum SamplingPhase
    {
        Extract,
        EndTick
    }

    //阈值测试接口对应原版ThresholdTest判断当前值是否触发告警
    public interface ThresholdTest
    {
        bool Test(double value);
    }

    protected MetricSampler(string name, SamplingPhase samplingPhase, MetricCategory category, Func<double> sampler, Action? beforeTick, ThresholdTest? thresholdTest)
    {
        _name = name;
        _samplingPhase = samplingPhase;
        _category = category;
        _sampler = sampler;
        _beforeTick = beforeTick;
        _thresholdTest = thresholdTest;
    }

    public static MetricSampler Create(string name, MetricCategory category, Func<double> sampler)
        => new(name, SamplingPhase.EndTick, category, sampler, null, null);

    public static MetricSampler CreateExtractSampler(string name, MetricCategory category, Func<double> sampler)
        => new(name, SamplingPhase.Extract, category, sampler, null, null);

    public static MetricSamplerBuilder<T> Builder<T>(string metricName, MetricCategory category, Func<T, double> sampler, T context)
        => new(metricName, category, sampler, context);

    public void OnStartTick()
    {
        if (!_isRunning) throw new InvalidOperationException("Not running");
        _beforeTick?.Invoke();
    }

    public void OnEndTick(int currentTick)
    {
        VerifyRunning();
        _currentValue = _sampler();
        _values.Add(_currentValue);
        _ticks.Add(currentTick);
    }

    public void OnFinished()
    {
        VerifyRunning();
        _isRunning = false;
    }

    private void VerifyRunning()
    {
        if (!_isRunning)
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Sampler for metric {0} not started!", _name));
    }

    public Func<double> Sampler => _sampler;
    public string Name => _name;
    public SamplingPhase Phase => _samplingPhase;
    public MetricCategory Category => _category;

    public SamplerResult Result()
    {
        var recording = new Dictionary<int, double>();
        var firstTick = int.MinValue;
        var lastTick = int.MinValue;
        for (var i = 0; i < _values.Count; i++)
        {
            var currentTick = _ticks[i];
            if (firstTick == int.MinValue) firstTick = currentTick;
            recording[currentTick] = _values[i];
            lastTick = currentTick;
        }
        return new SamplerResult(firstTick, lastTick, recording);
    }

    public bool TriggersThreshold()
        => _thresholdTest is not null && _thresholdTest.Test(_currentValue);

    public override bool Equals(object? obj)
        => obj is MetricSampler that && _name == that._name && _category == that._category;

    public override int GetHashCode() => _name.GetHashCode();

    //采样结果对应原版SamplerResult
    public sealed class SamplerResult
    {
        private readonly IReadOnlyDictionary<int, double> _recording;
        public int FirstTick { get; }
        public int LastTick { get; }

        public SamplerResult(int firstTick, int lastTick, IReadOnlyDictionary<int, double> recording)
        {
            FirstTick = firstTick;
            LastTick = lastTick;
            _recording = recording;
        }

        public double ValueAtTick(int tick)
            => _recording.TryGetValue(tick, out var v) ? v : 0.0d;
    }

    //百分比增幅阈值对应原版ValueIncreasedByPercentage
    //值相对前一次增长超过阈值百分比触发
    public sealed class ValueIncreasedByPercentage : ThresholdTest
    {
        private readonly float _percentageIncreaseThreshold;
        private double _previousValue = double.MinValue;

        public ValueIncreasedByPercentage(float percentageIncreaseThreshold)
            => _percentageIncreaseThreshold = percentageIncreaseThreshold;

        public bool Test(double value)
        {
            bool result;
            if (_previousValue == double.MinValue || value <= _previousValue)
            {
                result = false;
            }
            else
            {
                result = (value - _previousValue) / _previousValue >= _percentageIncreaseThreshold;
            }
            _previousValue = value;
            return result;
        }
    }

    //采样器builder对应原版MetricSamplerBuilder
    public sealed class MetricSamplerBuilder<T>
    {
        private readonly string _name;
        private readonly MetricCategory _category;
        private readonly Func<double> _sampler;
        private readonly T _context;
        private SamplingPhase _samplingPhase = SamplingPhase.EndTick;
        private Action? _beforeTick;
        private ThresholdTest? _thresholdTest;

        public MetricSamplerBuilder(string name, MetricCategory category, Func<T, double> sampler, T context)
        {
            _name = name;
            _category = category;
            _sampler = () => sampler(context);
            _context = context;
        }

        public MetricSamplerBuilder<T> WithBeforeTick(Action<T> beforeTick)
        {
            _beforeTick = () => beforeTick(_context);
            return this;
        }

        public MetricSamplerBuilder<T> WithThresholdAlert(ThresholdTest thresholdTest)
        {
            _thresholdTest = thresholdTest;
            return this;
        }

        public MetricSamplerBuilder<T> WithSamplingPhase(SamplingPhase samplingPhase)
        {
            _samplingPhase = samplingPhase;
            return this;
        }

        public MetricSampler Build()
            => new(_name, _samplingPhase, _category, _sampler, _beforeTick, _thresholdTest);
    }
}
