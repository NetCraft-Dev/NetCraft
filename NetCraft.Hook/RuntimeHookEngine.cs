using System.Reflection;
using NetCraft.Hook.Runtime;

namespace NetCraft.Hook;

public enum PatchMode
{
    ILRewrite,
    RuntimePatch,
    //RuntimeInject 同样是改写 只是不落在加载前 而是借原生层把新方法体在运行时提交给 CLR
    //与 ILRewrite 共用全部锚点参数 要改已经加载甚至已经跑起来的代码时用这个
    RuntimeInject,
}

public sealed class RuntimeHookEngine : IDisposable
{
    private readonly RuntimePatcher _patcher = new();
    private readonly Dictionary<string, RuntimePatch> _patchMap = new();
    private readonly Dictionary<string, Delegate?> _trampolines = new();
    private bool _disposed;

    public IReadOnlyList<RuntimePatch> ActivePatches => _patcher.Patches;

    public RuntimePatch Patch(MethodInfo original, MethodInfo replacement)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var key = GetKey(original);
        if (_patchMap.ContainsKey(key))
            throw new InvalidOperationException($"Method {key} is already patched. Unpatch it first.");

        var patch = _patcher.PatchMethod(original, replacement);
        _patchMap[key] = patch;
        _trampolines[key] = null;
        return patch;
    }

    public RuntimePatch Patch(Type originalType, string methodName, Type replacementType, string replacementMethodName)
    {
        var original = FindMethod(originalType, methodName)
            ?? throw new ArgumentException($"Method {originalType.Name}::{methodName} not found");
        var replacement = FindMethod(replacementType, replacementMethodName)
            ?? throw new ArgumentException($"Method {replacementType.Name}::{replacementMethodName} not found");

        return Patch(original, replacement);
    }

    public RuntimePatch Patch(string originalTypeFullName, string methodName, Type replacementType, string replacementMethodName)
    {
        var originalType = FindType(originalTypeFullName)
            ?? throw new ArgumentException($"Type {originalTypeFullName} not found in any loaded assembly");
        return Patch(originalType, methodName, replacementType, replacementMethodName);
    }

    public bool Unpatch(MethodInfo original)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var key = GetKey(original);
        if (!_patchMap.TryGetValue(key, out var patch))
            return false;

        var result = _patcher.Unpatch(patch);
        if (result)
        {
            _patchMap.Remove(key);
            _trampolines.Remove(key);
        }
        return result;
    }

    public bool Unpatch(string originalTypeFullName, string methodName)
    {
        var key = $"{originalTypeFullName}::{methodName}";
        if (!_patchMap.TryGetValue(key, out var patch))
            return false;

        var result = _patcher.Unpatch(patch);
        if (result)
        {
            _patchMap.Remove(key);
            _trampolines.Remove(key);
        }
        return result;
    }

    public void UnpatchAll()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _patcher.UnpatchAll();
        _patchMap.Clear();
        _trampolines.Clear();
    }

    public TDelegate? GetTrampoline<TDelegate>(MethodInfo original) where TDelegate : Delegate
    {
        var key = GetKey(original);
        if (!_patchMap.TryGetValue(key, out var patch))
            return null;

        if (!_trampolines.TryGetValue(key, out var trampoline) || trampoline == null)
        {
            trampoline = TrampolineFactory.CreateTrampoline(original, patch.OriginalBytes, patch.OriginalEntry);
            _trampolines[key] = trampoline;
        }

        return (TDelegate?)trampoline;
    }

    public bool IsPatched(MethodInfo original) => _patchMap.ContainsKey(GetKey(original));

    public bool IsPatched(string originalTypeFullName, string methodName) => _patchMap.ContainsKey($"{originalTypeFullName}::{methodName}");

    public void Dispose()
    {
        if (_disposed) return;
        UnpatchAll();
        _disposed = true;
    }

    private static string GetKey(MethodInfo method) => $"{method.DeclaringType?.FullName}::{method.Name}";

    private static MethodInfo? FindMethod(Type type, string methodName)
    {
        return type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);
    }

    private static Type? FindType(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var type = asm.GetType(fullName);
                if (type != null) return type;
            }
            catch
            {
            }
        }
        return null;
    }
}
