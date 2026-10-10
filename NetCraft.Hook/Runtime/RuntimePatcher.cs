using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace NetCraft.Hook.Runtime;

internal static class JitHelper
{
    public static IntPtr GetNativeCode(MethodInfo method)
    {
        RuntimeHelpers.PrepareMethod(method.MethodHandle);
        return ResolveRealEntryPoint(method.MethodHandle.GetFunctionPointer());
    }

    public static void ForceCompile(MethodInfo method)
    {
        RuntimeHelpers.PrepareMethod(method.MethodHandle);
    }

    private static IntPtr ResolveRealEntryPoint(IntPtr funcPtr)
    {
        try
        {
            if (PlatformInfo.IsX64)
                return ResolveX64EntryPoint(funcPtr);
            if (PlatformInfo.IsArm64)
                return ResolveArm64EntryPoint(funcPtr);
        }
        catch
        {
        }

        return funcPtr;
    }

    private static IntPtr ResolveX64EntryPoint(IntPtr funcPtr)
    {
        var bytes = new byte[16];
        Marshal.Copy(funcPtr, bytes, 0, 16);

        if (bytes[0] == 0xFF && bytes[1] == 0x25)
        {
            var offset = BitConverter.ToInt32(bytes, 2);
            var targetAddr = Marshal.ReadIntPtr(funcPtr + 6 + offset);
            if (targetAddr != IntPtr.Zero)
                return targetAddr;
        }

        if (bytes[0] == 0xE9)
        {
            var offset = BitConverter.ToInt32(bytes, 1);
            return funcPtr + 5 + offset;
        }

        if (bytes[0] == 0x48 && bytes[1] == 0xB8)
        {
            var addr = Marshal.ReadIntPtr(funcPtr + 2);
            if (addr != IntPtr.Zero)
                return addr;
        }

        return funcPtr;
    }

    private static IntPtr ResolveArm64EntryPoint(IntPtr funcPtr)
    {
        var bytes = new byte[8];
        Marshal.Copy(funcPtr, bytes, 0, 8);

        if ((bytes[3] & 0x9F) == 0x90 && (bytes[7] & 0xFF) == 0xD6)
        {
            var addr = Marshal.ReadIntPtr(funcPtr - 8);
            if (addr != IntPtr.Zero)
                return addr;
        }

        return funcPtr;
    }
}

internal sealed class RuntimePatcher
{
    private readonly List<RuntimePatch> _patches = new();

    public IReadOnlyList<RuntimePatch> Patches => _patches;

    public RuntimePatch PatchMethod(MethodInfo original, MethodInfo replacement)
    {
        JitHelper.ForceCompile(original);
        JitHelper.ForceCompile(replacement);

        var originalPtr = JitHelper.GetNativeCode(original);
        var replacementPtr = JitHelper.GetNativeCode(replacement);

        if (originalPtr == IntPtr.Zero || replacementPtr == IntPtr.Zero)
            throw new InvalidOperationException($"Cannot get native entry point. Original: {originalPtr}, Replacement: {replacementPtr}");

        var patchSize = PlatformInfo.PatchSize;
        var originalBytes = BackupBytes(originalPtr, patchSize);

        if (!NativeMemory.MakeWritable(originalPtr, patchSize))
            throw new InvalidOperationException("Failed to make original method memory writable");

        var jumpBytes = JumpWriter.BuildJump(originalPtr, replacementPtr);
        Marshal.Copy(jumpBytes, 0, originalPtr, jumpBytes.Length);

        var patch = new RuntimePatch(original, replacement, originalPtr, replacementPtr, originalBytes);
        _patches.Add(patch);
        return patch;
    }

    public bool Unpatch(RuntimePatch patch)
    {
        if (!_patches.Contains(patch))
            return false;

        RestoreBytes(patch.OriginalEntry, patch.OriginalBytes);
        _patches.Remove(patch);
        return true;
    }

    public void UnpatchAll()
    {
        foreach (var patch in _patches.ToList())
            RestoreBytes(patch.OriginalEntry, patch.OriginalBytes);
        _patches.Clear();
    }

    private static byte[] BackupBytes(IntPtr address, int count)
    {
        var bytes = new byte[count];
        Marshal.Copy(address, bytes, 0, count);
        return bytes;
    }

    private static void RestoreBytes(IntPtr address, byte[] bytes)
    {
        if (!NativeMemory.MakeWritable(address, bytes.Length))
            throw new InvalidOperationException("Failed to make memory writable for restore");
        Marshal.Copy(bytes, 0, address, bytes.Length);
    }
}

public sealed class RuntimePatch
{
    public MethodInfo Original { get; }
    public MethodInfo Replacement { get; }
    public IntPtr OriginalEntry { get; }
    public IntPtr ReplacementEntry { get; }
    public byte[] OriginalBytes { get; }

    public RuntimePatch(MethodInfo original, MethodInfo replacement, IntPtr originalEntry, IntPtr replacementEntry, byte[] originalBytes)
    {
        Original = original;
        Replacement = replacement;
        OriginalEntry = originalEntry;
        ReplacementEntry = replacementEntry;
        OriginalBytes = originalBytes;
    }

    public override string ToString() => $"[RuntimePatch] {Original.DeclaringType?.Name}::{Original.Name} → {Replacement.DeclaringType?.Name}::{Replacement.Name}";
}
