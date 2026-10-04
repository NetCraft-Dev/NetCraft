# NetCraft.Util

> **完成度**：80% · **阶段**：M1+M6 进行中 · **依赖**：无（被所有上层引用）· **计划链位置**：Layer 1 工具层，对应原版 util/ (685 文件) 净约 650 文件

Layer 1 子库，通用工具集。承载跨子库的辅助类型与函数，包括统一日志系统、线程调度、崩溃报告、随机数、性能采样、命令解析 packrat 框架。

对应原版 `net.minecraft.util` + `net.minecraft.util.profiling` + `net.minecraft.util.random` + brigadier packrat 部分。被所有需要日志/随机/集合工具的子库依赖。

## 实现

### 日志系统（Logging 子目录）

- 统一日志系统（Log），支持控制台彩色输出与文件持久化
- 日志级别过滤（Debug/Info/Warning/Error/Critical/None），控制台与文件独立配置
- 日志源栈管理，PushSource/using 块跟踪调用上下文
- CallerFilePath/LineNumber/MemberName 自动捕获调用位置
- 文件日志按时间戳命名，支持保留期自动清理
- 警告/错误计数 + 最后消息记录 + OnLogOutput 事件
- 跨平台：纯托管代码，无 P/Invoke（.NET 10 在 Windows 默认支持 ANSI VT）

### 崩溃报告系统（根目录）

- `CrashReport` 崩溃报告收集异常与上下文分类对应原版 net.minecraft.CrashReport
- `CrashReportCategory` 上下文分类含 SetDetail/FillInStackTrace/GetDetails 对齐原版输出格式
- `ReportedException` 包装 CrashReport 的异常，Title 作为 Message，Report.Exception 作为 InnerException

### 动态位集

- `BitSet` 动态位集对应原版 java.util.BitSet，基于 long[] 按位存储，支持 Set/Get/Clear/IsEmpty
- 供 IOWorker blending_data 扫描时构建 region 级旧区块位图

### 数学与通用工具（根目录）

- `Mth` 数学工具集对应原版 net.minecraft.util.Mth，含 clamp/lerp/wrap/invLerp 等
- `Util` 通用工具集合对应原版 net.minecraft.util.Util，仅内联 Memoize 纯函数其他依赖 DFU 类型的方法放到 DataFixUtils
- `ConsoleAnsiArtist` 控制台 ANSI 转义艺术工具
- `FileUtil` 文件工具，CreateDirectoriesSafe 幂等创建目录
- `ExceptionCollector` 异常收集器，收集多个异常后统一抛出

### 线程调度框架（thread 子目录）

- `IExecutor` 执行器抽象接口对应原版 java.util.concurrent.Executor
- `DefaultThreadPoolExecutor` 默认实现复用 .NET 全局 ThreadPool 对应原版 Util.ioPool
- `StrictQueue<T>` 严格队列接口 + `QueueStrictQueue`（ConcurrentQueue 包装）+ `FixedPriorityQueue`（按优先级分桶）
- `RunnableWithPriority` record 含 priority + task 对应原版 StrictQueue.RunnableWithPriority
- `AbstractConsecutiveExecutor<T>` 抽象基类状态机 SLEEPING/RUNNING/CLOSED 用 CAS 切换保证单线程串行
- `ConsecutiveExecutor` 简单 FIFO 顺序执行器
- `PriorityConsecutiveExecutor` 优先级调度含 `ScheduleWithResult<T>` 返回 Task 由 TaskCompletionSource 完成

### 集合工具（Collection 子目录）

- `CollectionUtil` 集合工具对应原版 Util 中纯集合方法 Make/FindNext/MapValues/CopyAndAdd/Join 等
- `EnumCollections` Enum 集合工具对应原版 makeEnumMap/allOfEnumExcept
- `FixedSizeUtil` 固定大小校验工具对应原版 Util.fixedSize
- `IndexLookup` 索引查找工具小列表线性大列表 Dictionary 索引
- `Memoize` 记忆化工具对应原版 Util.memoize
- `OptionalUtil` Optional 工具对应原版 Util.ifElse
- `PredicateUtil` Predicate 工具 allOf/anyOf 变长参数合并
- `RandomCollections` 随机集合工具对应原版 Util 中随机相关集合方法
- `SingleKeyCache<K,V>` 单键缓存对应原版 SingleKeyCache
- `TaskSequence` Task 序列工具对应原版 Util.sequence/sequenceFailFast

### 随机数系统（Random 子目录）

- `RandomSource` 随机数源接口对应原版 net.minecraft.util.RandomSource
- `RandomSupport` 随机数工厂支持
- `Xoroshiro128PlusPlus` xoroshiro128++ 算法实现
- `XoroshiroRandomSource` RandomSource 的 xoroshiro 实现
- `MarsagliaPolarGaussian` 高斯分布采样
- `PositionalRandomFactory` 位置确定性随机工厂
- `Weighted`/`WeightedList`/`WeightedRandom` 权重随机集合

### 性能采样系统（Profiling 子目录）

对应原版 net.minecraft.util.profiling 完整移植：

- `Profiler` 性能采样器接口
- `ActiveProfiler`/`InactiveProfiler` 启用/禁用实现
- `ContinuousProfiler` 持续采样
- `ProfileCollector` 采样数据收集
- `ProfileResults`/`FilledProfileResults`/`EmptyProfileResults` 采样结果
- `ProfilerFiller`/`ProfilerPathEntry`/`ResultField`/`Zone` 采样辅助
- `SingleTickProfiler` 单 tick 采样
- `ProfilingUtil` 采样工具

#### Metrics 子目录（性能指标）

- `MetricCategory` 指标分类枚举
- `MetricSampler` 指标采样器
- `MetricsRegistry` 指标注册表
- `MetricsSamplerProvider` 指标采样器提供者
- `ProfilerMeasured` 已测量指标

#### Metrics/Profiling 子目录（指标记录器）

- `MetricsRecorder` 指标记录器接口
- `ActiveMetricsRecorder`/`InactiveMetricsRecorder` 启用/禁用实现
- `ProfilerSamplerAdapter` 采样器适配器

#### Metrics/Storage 子目录（指标存储）

- `MetricsPersister` 指标持久化
- `RecordedDeviation` 偏差记录

### Packrat 解析框架（Parsing/Packrat 子目录）

对应原版 brigadier 私有 packrat parser 框架：

- `Atom`/`Term`/`Control` 文法原子
- `Rule`/`NamedRule` 规则定义
- `ParseState`/`CachedParseState` 解析状态
- `Scope`/`Dictionary` 作用域与字典
- `ErrorCollector`/`ErrorEntry`/`DelayedException` 错误收集
- `SuggestionSupplier` 建议提供者

#### Parsing/Packrat/Commands 子目录

命令字符串读取器私有组件，被 SnbtGrammar.cs + TagParser.cs 使用：

- `CommandStringReader`/`CommandSyntaxException`/`Grammar` 命令字符串读取与异常
- `GreedyPatternParseRule`/`GreedyPredicateParseRule`/`NumberRunParseRule`/`UnquotedStringParseRule` 解析规则
- `StringReaderParserState`/`StringReaderTerms` 解析状态与原子

与 NetCraft.Commands 互不依赖，命名空间隔离（NetCraft.Util.Parsing.Packrat.Commands vs NetCraft.Commands.Exceptions）。

## 文件清单（82 个 cs）

| 区域 | 数量 | 说明 |
|------|------|------|
| 根目录 | 9 | BitSet/CrashReport/CrashReportCategory/ReportedException/ExceptionCollector/FileUtil/Mth/Util/ConsoleAnsiArtist |
| Logging | 1 | Log 静态类 |
| thread | 6 | IExecutor/DefaultThreadPoolExecutor/StrictQueue/AbstractConsecutiveExecutor/ConsecutiveExecutor/PriorityConsecutiveExecutor |
| Collection | 10 | CollectionUtil/EnumCollections/FixedSizeUtil/IndexLookup/Memoize/OptionalUtil/PredicateUtil/RandomCollections/SingleKeyCache/TaskSequence |
| Random | 9 | RandomSource/RandomSupport/Xoroshiro128PlusPlus/XoroshiroRandomSource/MarsagliaPolarGaussian/PositionalRandomFactory/Weighted/WeightedList/WeightedRandom |
| Profiling | 14 | Profiler/ActiveProfiler/InactiveProfiler/ContinuousProfiler/ProfileCollector/ProfileResults/FilledProfileResults/EmptyProfileResults/ProfilerFiller/ProfilerPathEntry/ResultField/SingleTickProfiler/ProfilingUtil/Zone |
| Profiling/Metrics | 5 | MetricCategory/MetricSampler/MetricsRegistry/MetricsSamplerProvider/ProfilerMeasured |
| Profiling/Metrics/Profiling | 4 | MetricsRecorder/ActiveMetricsRecorder/InactiveMetricsRecorder/ProfilerSamplerAdapter |
| Profiling/Metrics/Storage | 2 | MetricsPersister/RecordedDeviation |
| Parsing/Packrat | 13 | Atom/Term/Control/Rule/NamedRule/ParseState/CachedParseState/Scope/Dictionary/ErrorCollector/ErrorEntry/DelayedException/SuggestionSupplier |
| Parsing/Packrat/Commands | 9 | CommandStringReader/CommandSyntaxException/Grammar/GreedyPatternParseRule/GreedyPredicateParseRule/NumberRunParseRule/StringReaderParserState/StringReaderTerms/UnquotedStringParseRule |

## 测试

- RandomTests 测试伪随机数生成器
- ProfilingTests 测试性能采样器
- CollectionTests 测试集合工具 + LRU 缓存
- 详见 NetCraft.Test/Overview

## 规划

- `ExtraCodecs` Codec 扩展（依赖 DFU）
- `SystemReport` 系统信息报告（CrashReport 系统信息部分待补）
