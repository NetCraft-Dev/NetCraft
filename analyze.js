// 全面结构化分析 ETL/ETLX 文件
// 用法：EtlJS.exe analyze.js trace.etlx

var TARGET_PROCESS = 'NetCraft.Loader';
var TOP_N = 15;

function line() { log('---------------------------------------------------------------------'); }
function title(t) { log(''); line(); log('  ' + t); line(); }
function sub(t) { log(''); log('  >> ' + t); }
function fmtMs(v) { return v.toFixed(2) + 'ms'; }
function fmtSec(v) { return (v / 1000).toFixed(2) + 's'; }
function fmtPct(v) { return v.toFixed(2) + '%'; }
function pad(s, n) { return String(s).padStart(n); }
function padEnd(s, n) { return String(s).padEnd(n); }

log('ETL/ETLX 分析报告');
log('文件:     ' + etl.path);
if (etl.sourcePath !== etl.path) log('源文件:   ' + etl.sourcePath);
log('格式:     ' + (etl.isEtlx ? 'ETLX（已转换缓存）' : 'ETL'));
log('目标进程: ' + (TARGET_PROCESS || '全部'));
log('内存上限: ' + etl.memoryLimitMb + 'MB (' + etl.memorySource + ')');
log('生成:     ' + new Date().toISOString());

// 1. 概览
title('1. 全局概览');
log('  时长:      ' + fmtSec(etl.durationMs));
log('  事件总数:  ' + etl.eventCount);
log('  进程数:    ' + etl.processCount);
log('  线程数:    ' + etl.threadCount);
log('  模块数:    ' + etl.moduleCount);

// 2. 进程
title('2. 进程列表（按事件数）');
var procs = getProcesses().sort(function(a, b) { return b.eventCount - a.eventCount; });
log('  ' + padEnd('PID', 8) + padEnd('进程名', 32) + pad('事件', 12) + pad('线程', 6) + pad('时长', 10));
line();
for (var i = 0; i < Math.min(20, procs.length); i++) {
    var p = procs[i];
    log('  ' + padEnd(p.pid, 8) + padEnd(p.name || '(未知)', 32) +
        pad(p.eventCount, 12) + pad(p.threadCount, 6) + pad(fmtSec(p.durationMs), 10));
}
if (procs.length > 20) log('  ... 还有 ' + (procs.length - 20) + ' 个进程');

// 3. 模块
title('3. 模块文件');
var mods = getModules();
log('  模块总数: ' + mods.length);

// 4. JIT 编译
title('4. JIT 编译分析（' + (TARGET_PROCESS || '全部') + '）');
var jit = getJitEvents(TARGET_PROCESS);
log('  JIT 事件总数: ' + jit.length);

var byNs = {};
for (var j of jit) byNs[j.ns || '(无命名空间)'] = (byNs[j.ns || '(无命名空间)'] || 0) + 1;
sub('按命名空间 Top ' + TOP_N);
var nsSorted = Object.entries(byNs).sort(function(a, b) { return b[1] - a[1]; });
for (var i = 0; i < Math.min(TOP_N, nsSorted.length); i++)
    log('  ' + pad(nsSorted[i][1], 6) + '  ' + nsSorted[i][0]);
log('  ... 共 ' + nsSorted.length + ' 个命名空间');

var byMethod = {};
for (var j of jit) {
    if (!j.method) continue;
    var k = (j.ns ? j.ns + '.' : '') + j.method;
    byMethod[k] = (byMethod[k] || 0) + 1;
}
var methodSorted = Object.entries(byMethod).sort(function(a, b) { return b[1] - a[1]; });
sub('被 JIT 最频繁的方法 Top ' + TOP_N);
for (var i = 0; i < Math.min(TOP_N, methodSorted.length); i++)
    log('  ' + pad(methodSorted[i][1], 6) + '  ' + methodSorted[i][0]);

if (jit.length > 0) {
    sub('JIT 时间线（每 5 秒桶）');
    var bucketMs = 5000;
    var buckets = {};
    for (var j of jit) {
        var b = Math.floor(j.timeMs / bucketMs);
        buckets[b] = (buckets[b] || 0) + 1;
    }
    var keys = Object.keys(buckets).sort(function(a, b) { return a - b; });
    var maxCount = 0;
    for (var k of keys) if (buckets[k] > maxCount) maxCount = buckets[k];
    for (var k of keys) {
        var t = (k * bucketMs / 1000).toFixed(0);
        var barLen = Math.round(buckets[k] / maxCount * 50);
        log('  ' + pad(t + 's', 8) + ' ' + pad(buckets[k], 6) + ' ' + new Array(barLen + 1).join('#'));
    }
}

// 5. GC
title('5. GC 分析（' + (TARGET_PROCESS || '全部') + '）');
var gc = getGcEvents(TARGET_PROCESS);
log('  GC 事件总数: ' + gc.length);

var byGcName = {};
for (var g of gc) byGcName[g.eventName] = (byGcName[g.eventName] || 0) + 1;
sub('按事件名');
var gcNameSorted = Object.entries(byGcName).sort(function(a, b) { return b[1] - a[1]; });
for (var i = 0; i < Math.min(TOP_N, gcNameSorted.length); i++)
    log('  ' + pad(gcNameSorted[i][1], 6) + '  ' + gcNameSorted[i][0]);

// GC/Stop 才有暂停时长；GC/Start 只有代际和原因
var gcStarts = gc.filter(function(g) { return g.eventName.indexOf('GC/Start') >= 0; });
var gcStops = gc.filter(function(g) { return g.eventName.indexOf('GC/Stop') >= 0; });

if (gcStarts.length > 0) {
    sub('GC 启动事件（共 ' + gcStarts.length + ' 次）');
    var byGen = { '0': 0, '1': 0, '2': 0, '未知': 0 };
    var byReason = {};
    for (var g of gcStarts) {
        var gen = g.generation;
        if (gen === '0' || gen === '1' || gen === '2') byGen[gen]++;
        else byGen['未知']++;
        var r = g.reason || '(未知)';
        byReason[r] = (byReason[r] || 0) + 1;
    }
    log('  代际分布:  Gen0=' + byGen['0'] + '  Gen1=' + byGen['1'] +
        '  Gen2=' + byGen['2'] + '  未知=' + byGen['未知']);
    sub('  触发原因分布');
    var reasonSorted = Object.entries(byReason).sort(function(a, b) { return b[1] - a[1]; });
    for (var i = 0; i < reasonSorted.length; i++)
        log('    ' + pad(reasonSorted[i][1], 6) + '  ' + reasonSorted[i][0]);
}

if (gcStops.length > 0) {
    var totalPause = 0;
    var withPause = 0;
    for (var g of gcStops) {
        var p = parseFloat(g.pauseMs);
        if (!isNaN(p) && p > 0) { totalPause += p; withPause++; }
    }
    sub('GC 暂停统计（来自 GC/Stop）');
    log('  事件数:     ' + gcStops.length);
    log('  有效暂停:   ' + withPause);
    log('  总暂停:     ' + fmtMs(totalPause));
    if (withPause > 0)
        log('  平均每次:   ' + fmtMs(totalPause / withPause));
    if (withPause === 0) {
        log('  (无暂停时长数据)');
        log('  诊断: 用 dumpEvents(\'DotNETRuntime\', \'GC/Stop\', 1) 查看实际字段');
    }
}

// 6. 异常
title('6. 异常事件（' + (TARGET_PROCESS || '全部') + '）');
var exc = getExceptionEvents(TARGET_PROCESS);
log('  异常事件总数: ' + exc.length);
if (exc.length > 0) {
    var byType = {};
    for (var e of exc) {
        var t = e.exceptionType || '(未知)';
        byType[t] = (byType[t] || 0) + 1;
    }
    var typeSorted = Object.entries(byType).sort(function(a, b) { return b[1] - a[1]; });
    sub('异常类型 Top ' + TOP_N);
    for (var i = 0; i < Math.min(TOP_N, typeSorted.length); i++)
        log('  ' + pad(typeSorted[i][1], 6) + '  ' + typeSorted[i][0]);
    if (typeSorted.length > 0 && typeSorted[0][0] === '(未知)')
        log('  诊断: 用 dumpEvents(\'DotNETRuntime\', \'Exception\', 1) 查看实际字段');
}

// 7. 程序集加载
title('7. 程序集加载（' + (TARGET_PROCESS || '全部') + '）');
var asm = getAssemblyLoads(TARGET_PROCESS);
log('  程序集事件总数: ' + asm.length);
if (asm.length > 0) {
    var byAsm = {};
    for (var a of asm) {
        var k = a.assemblyName || a.eventName || '(未知)';
        byAsm[k] = (byAsm[k] || 0) + 1;
    }
    var asmSorted = Object.entries(byAsm).sort(function(a, b) { return b[1] - a[1]; });
    sub('Top ' + TOP_N);
    for (var i = 0; i < Math.min(TOP_N, asmSorted.length); i++)
        log('  ' + pad(asmSorted[i][1], 6) + '  ' + asmSorted[i][0]);
}

// 8. 线程
title('8. 线程与线程池（' + (TARGET_PROCESS || '全部') + '）');
var th = getThreadPoolEvents(TARGET_PROCESS);
log('  线程事件总数: ' + th.length);
if (th.length > 0) {
    var byThName = {};
    for (var t of th) byThName[t.eventName] = (byThName[t.eventName] || 0) + 1;
    var thSorted = Object.entries(byThName).sort(function(a, b) { return b[1] - a[1]; });
    for (var i = 0; i < Math.min(TOP_N, thSorted.length); i++)
        log('  ' + pad(thSorted[i][1], 6) + '  ' + thSorted[i][0]);
}

// 9. 锁竞争
title('9. 锁竞争（' + (TARGET_PROCESS || '全部') + '）');
var cnt = getContentionEvents(TARGET_PROCESS);
log('  竞争事件总数: ' + cnt.length);
if (cnt.length > 0) {
    var totalDur = 0;
    var withDur = 0;
    for (var c of cnt) {
        var d = parseFloat(c.durationMs);
        if (!isNaN(d) && d > 0) { totalDur += d; withDur++; }
    }
    log('  总竞争时长:   ' + fmtMs(totalDur));
    log('  有效样本:     ' + withDur + ' / ' + cnt.length);
    if (withDur > 0)
        log('  平均每次:     ' + fmtMs(totalDur / withDur));
    if (withDur === 0)
        log('  诊断: 用 dumpEvents(\'DotNETRuntime\', \'Contention/Stop\', 1) 查看实际字段');
}

// 10. CPU 采样热点
title('10. CPU 采样热点（' + (TARGET_PROCESS || '全部') + '）');
var cpu = getCpuSamples(TARGET_PROCESS, TOP_N * 2);
if (cpu.length === 0) {
    log('  (无 CPU 采样数据)');
    log('  提示: ETL 采集时需启用 KernelEvents:Profile');
} else {
    log('  ' + pad('占比', 8) + pad('采样数', 8) + '方法');
    line();
    for (var c of cpu) {
        var m = c.method;
        if (m.length > 80) m = '...' + m.slice(-77);
        log('  ' + pad(fmtPct(c.percent), 8) + pad(c.samples, 8) + m);
    }
}

// 11. 调用栈样本
title('11. 调用栈样本');
var stacks = getCallStack(5, 15);
log('  样本数: ' + stacks.length);
for (var i = 0; i < stacks.length; i++) {
    var s = stacks[i];
    sub('#' + (i + 1) + '  ' + s.eventName + '  @ ' + fmtMs(s.timeMs) + '  (' + (s.processName || '?') + ')');
    for (var j = 0; j < s.frames.length; j++) {
        var f = s.frames[j];
        if (f.length > 90) f = '...' + f.slice(-87);
        log('    ' + pad(j, 3) + '  ' + f);
    }
}

// 12. Provider 分布
title('12. Provider 分布（' + (TARGET_PROCESS || '全部进程') + '）');
var provNames = [
    'DotNETRuntime', 'DotNETRuntimePrivate',
    'Windows Kernel', 'KernelTraceControl',
    'Kernel-Power', 'Kernel-EventTracing', 'Kernel-File',
    'Kernel-Processor-Power', 'Kernel-Process'
];
var totalProv = 0;
for (var p of provNames) {
    var c = countEvents(p, TARGET_PROCESS);
    totalProv += c;
    if (c > 0) log('  ' + pad(c, 10) + '  ' + p);
}
log('  ' + pad(totalProv, 10) + '  (以上合计)');

line();
log('分析完成');
line();