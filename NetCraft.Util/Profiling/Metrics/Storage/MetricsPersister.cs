using System.Globalization;
using System.Text;
using NetCraft.Logging;
using NetCraft.Util.Profiling.Metrics;

namespace NetCraft.Util.Profiling.Metrics.Storage;

//Metrics persistence, maps to vanilla net.minecraft.util.profiling.metrics.storage.MetricsPersister
//Writes MetricSampler results to CSV, deviations to profiler results, returns the working directory
public sealed class MetricsPersister
{
    public const string MetricsDirName = "metrics";
    public const string DeviationsDirName = "deviations";
    public const string ProfilingResultFilename = "profiling.txt";
    public static readonly string ProfilingResultsDir = Path.Combine("debug", "profiling");

    private readonly string _rootFolderName;

    public MetricsPersister(string rootFolderName) => _rootFolderName = rootFolderName;

    public string SaveReports(ISet<MetricSampler> samplers, IReadOnlyDictionary<MetricSampler, List<RecordedDeviation>> deviationsBySampler, ProfileResults profilerResults)
    {
        try
        {
            Directory.CreateDirectory(ProfilingResultsDir);
            var tempDir = Path.Combine(Path.GetTempPath(), "minecraft-profiling-" + ProfilingUtil.GetFilenameFormattedDateTime());
            Directory.CreateDirectory(tempDir);
            var workingDir = Path.Combine(tempDir, _rootFolderName);
            var metricsDir = Path.Combine(workingDir, MetricsDirName);
            SaveMetrics(samplers, metricsDir);
            if (deviationsBySampler.Count > 0)
            {
                SaveDeviations(deviationsBySampler, Path.Combine(workingDir, DeviationsDirName));
            }
            SaveProfilingTaskExecutionResult(profilerResults, workingDir);
            return tempDir;
        }
        catch (Exception e)
        {
            Log.Error($"Could not save metrics reports: {e}");
            throw;
        }
    }

    private void SaveMetrics(ISet<MetricSampler> samplers, string dir)
    {
        if (samplers.Count == 0)
            throw new ArgumentException("Expected at least one sampler to persist");

        var samplersByCategory = new Dictionary<MetricCategory, List<MetricSampler>>();
        foreach (var sampler in samplers)
        {
            if (!samplersByCategory.TryGetValue(sampler.Category, out var list))
            {
                list = new List<MetricSampler>();
                samplersByCategory[sampler.Category] = list;
            }
            list.Add(sampler);
        }
        foreach (var (category, samplersInCategory) in samplersByCategory)
        {
            SaveCategory(category, samplersInCategory, dir);
        }
    }

    private void SaveCategory(MetricCategory category, List<MetricSampler> samplers, string dir)
    {
        var file = Path.Combine(dir, SanitizeName(category.GetDescription()) + ".csv");
        Directory.CreateDirectory(dir);
        try
        {
            using var writer = new StreamWriter(file, false, Encoding.UTF8);
            writer.Write("@tick");
            foreach (var sampler in samplers) writer.Write("," + sampler.Name);
            writer.WriteLine();
            var results = samplers.Select(s => s.Result()).ToList();
            var firstTick = results.Min(r => r.FirstTick);
            var lastTick = results.Max(r => r.LastTick);
            for (var tick = firstTick; tick <= lastTick; tick++)
            {
                var row = new StringBuilder(tick.ToString(CultureInfo.InvariantCulture));
                foreach (var result in results)
                {
                    row.Append(',').Append(result.ValueAtTick(tick).ToString(CultureInfo.InvariantCulture));
                }
                writer.WriteLine(row.ToString());
            }
            Log.Debug($"Flushed metrics to {file}");
        }
        catch (Exception e)
        {
            Log.Error($"Could not save profiler results to {file}: {e}");
        }
    }

    private void SaveDeviations(IReadOnlyDictionary<MetricSampler, List<RecordedDeviation>> deviationsBySampler, string directory)
    {
        const string format = "yyyy-MM-dd_HH.mm.ss.SSS";
        foreach (var (sampler, deviations) in deviationsBySampler)
        {
            foreach (var deviation in deviations)
            {
                var timestamp = deviation.Timestamp.ToString(format, CultureInfo.InvariantCulture);
                var deviationLogFile = Path.Combine(directory, SanitizeName(sampler.Name), $"{deviation.Tick}@{timestamp}.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(deviationLogFile)!);
                deviation.ProfilerResultAtTick.SaveResults(deviationLogFile);
            }
        }
    }

    private void SaveProfilingTaskExecutionResult(ProfileResults results, string directory)
    {
        Directory.CreateDirectory(directory);
        results.SaveResults(Path.Combine(directory, ProfilingResultFilename));
    }

    //Filename sanitizing, maps to vanilla Util.sanitizeName; keeps letters, digits, underscore and minus, replaces the rest with underscore
    private static string SanitizeName(string name)
    {
        var builder = new StringBuilder();
        foreach (var c in name)
        {
            if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == '.')
                builder.Append(c);
            else
                builder.Append('_');
        }
        return builder.ToString();
    }
}
