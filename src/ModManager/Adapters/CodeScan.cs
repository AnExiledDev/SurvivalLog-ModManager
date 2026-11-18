using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using ExiledToolkit;
using ExiledToolkit.Kit;
using HarmonyLib;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;
using ModManager.Core;

namespace ModManager.Adapters;

/// <summary>
/// Compares the game's compiled code in memory with GameAssembly.dll on disk, then sorts what differs into Harmony
/// patches, changes mods reported through Exiled's Toolkit, and the rest. The compare runs on its own thread, as it
/// reads about 70 MB. Finding which methods Harmony patches, and the names of methods, uses the game's runtime, so that
/// runs on the main thread, a little each frame.
/// </summary>
internal sealed class CodeScan
{
    private const string Module = "GameAssembly.dll";
    private const int ChunkBytes = 1 << 20;

    /// <summary>A relocation that a chunk's end would cut is read whole, so a chunk can run this much past its size.</summary>
    private const int ChunkSlack = 16;

    /// <summary>Far more than every mod's hooks and byte changes together. Past it the file can't be the one the game loaded.</summary>
    private const int MostChangedBytes = 64 * 1024;

    private const int MostRuns = 5000;

    private const uint Committed = 0x1000;
    private const uint ReadableProtections = 0x02 | 0x04 | 0x08 | 0x20 | 0x40 | 0x80;
    private const uint GuardPage = 0x100;

    private static readonly long SliceTicks = Stopwatch.Frequency * 2 / 1000;

    private readonly Action<string> _log;
    private readonly Func<bool> _isSliceSpent;

    /// <summary>Method names found so far, by where the method starts. Code does not move while the game runs.</summary>
    private readonly Dictionary<uint, string> _names = new();

    /// <summary>Method starts a whole walk of the game's classes found no name for.</summary>
    private readonly HashSet<uint> _nameless = new();

    /// <summary>Where each method a mod reported starts, by "Type.Method", once a whole walk has looked for it.</summary>
    private readonly Dictionary<string, IReadOnlyList<uint>> _startsByName = new(StringComparer.Ordinal);

    private ScanResult? _finished;
    private int _isScanning;

    /// <summary>How many methods Harmony patched, and how many hooks and byte changes mods reported, at the last compare.</summary>
    private (int Patched, int Hooks, int Bytes)? _lastMark;
    private Naming? _naming;
    private long _sliceEndsAt;

    public CodeScan(Action<string> log)
    {
        _log = log;
        _isSliceSpent = () => Stopwatch.GetTimestamp() >= _sliceEndsAt;
    }

    /// <summary>
    /// Starts a compare, unless one is still running or being sorted. <see cref="Continue"/> carries it on. Unless
    /// <paramref name="isAsked"/>, it only starts when the patches or reports differ from the last compare's: mods
    /// change code as they load or as they patch, and either one moves those counts.
    /// </summary>
    /// <returns>Whether a compare is running.</returns>
    public bool Start(ClaimSet claims, bool isAsked)
    {
        if (_naming != null || Volatile.Read(ref _isScanning) != 0) return true;

        var mark = (Harmony.GetAllPatchedMethods().Count(), claims.Hooks.Count, claims.Bytes.Count);

        if (!isAsked && _lastMark == mark) return false;

        if (Interlocked.CompareExchange(ref _isScanning, 1, 0) != 0) return true;

        var thread = new Thread(Scan) { IsBackground = true, Name = "Mod Manager code check", Priority = System.Threading.ThreadPriority.BelowNormal };

        _lastMark = mark;
        thread.Start();

        return true;
    }

    /// <summary>
    /// Sorts a finished compare, a slice each frame. What it found once it is done, else null. The Changers are left
    /// for the caller, who knows the mods.
    /// </summary>
    /// <param name="claims">What mods reported, read when the compare finishes.</param>
    public CodeCheck? Continue(Func<ClaimSet> claims)
    {
        if (_naming is { } naming) return Advance(naming);

        switch (Interlocked.Exchange(ref _finished, null))
        {
            case ScanResult.Failed failed:
                _log(failed.Note);

                return new CodeCheck.NoScan(failed.Reason);
            case ScanResult.Found found:
                _log(found.Note);

                return Sort(found, claims());
            default:
                return null;
        }
    }

    private CodeCheck? Sort(ScanResult.Found found, ClaimSet claims)
    {
        try
        {
            var harmony = HarmonySpans(found);
            var unexplained = CodeReading.Explain(found.Scanned, found.Runs, harmony, rva => FunctionOf(found, rva)).Unreported.Select(code => code.Function);
            var naming = new Naming(found, claims, harmony, ClaimTypesToFind(claims), unexplained.Where(rva => !_names.ContainsKey(rva) && !_nameless.Contains(rva)));

            if (naming.IsComplete) return Finish(naming);

            naming.Job = new SlicedJob(Walk(naming));
            _naming = naming;

            return Advance(naming);
        }
        catch (Exception e)
        {
            _log("could not sort what the check of the game's code found: " + e);

            return new CodeCheck.NoScan(NoScanReason.Unreadable);
        }
    }

    private CodeCheck? Advance(Naming naming)
    {
        try
        {
            _sliceEndsAt = Stopwatch.GetTimestamp() + SliceTicks;

            if (naming.Job is { } job && !job.Advance(_isSliceSpent)) return null;
        }
        catch (Exception e)
        {
            naming.IsCutShort = true;
            _log("could not name every changed game method: " + e.Message);
        }

        _naming = null;

        return Finish(naming);
    }

    private CodeCheck Finish(Naming naming)
    {
        foreach (var (rva, name) in naming.Names) _names[rva] = name;

        if (naming.Job != null && !naming.IsCutShort) KeepMisses(naming);

        var found = naming.Found;

        // A walk cut short still found some starts; they hold for this reading, though they are not kept.
        var spans = naming.Harmony.Concat(CodeSpan.OfClaims(naming.Claims, method => naming.Starts.TryGetValue(method, out var starts) ? starts : StartsOf(method))).ToList();
        var reading = CodeReading.Explain(found.Scanned, found.Runs, spans, rva => FunctionOf(found, rva)).Named(rva => _names.TryGetValue(rva, out var name) ? name : null);

        return new CodeCheck.Done(reading, Array.Empty<string>());
    }

    /// <summary>Remembers what a whole walk did not find, so the next check does not walk for it again.</summary>
    private void KeepMisses(Naming naming)
    {
        foreach (var (type, methods) in naming.ClaimTypes)
        {
            foreach (var method in methods)
            {
                var name = type + "." + method;

                _startsByName[name] = naming.Starts.TryGetValue(name, out var starts) ? starts : Array.Empty<uint>();
            }
        }

        foreach (var function in naming.Functions) _nameless.Add(function);
    }

    private IReadOnlyList<uint> StartsOf(string method) => _startsByName.TryGetValue(method, out var starts) ? starts : Array.Empty<uint>();

    /// <summary>The type names, and their methods, of the reported hooks and byte changes not yet looked for.</summary>
    private Dictionary<string, HashSet<string>> ClaimTypesToFind(ClaimSet claims)
    {
        var types = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var methods = claims.Hooks.Select(hook => hook.Method).Concat(claims.Bytes.Select(bytes => bytes.Method));

        foreach (var name in methods.Where(name => !_startsByName.ContainsKey(name)))
        {
            var dot = name.LastIndexOf('.');

            if (dot <= 0 || dot == name.Length - 1) continue;

            var type = name.Substring(0, dot);

            if (!types.TryGetValue(type, out var names)) types[type] = names = new HashSet<string>(StringComparer.Ordinal);

            names.Add(name.Substring(dot + 1));
        }

        return types;
    }

    /// <summary>The start of every game method Harmony patches, where its hook is written.</summary>
    private static List<CodeSpan> HarmonySpans(ScanResult.Found found)
    {
        var spans = new List<CodeSpan>();

        foreach (var method in Harmony.GetAllPatchedMethods())
        {
            if (CodeStartOf(method) is { } start && found.RvaOf(start) is { } rva) spans.Add(CodeSpan.Hook(rva, CodeSource.Harmony));
        }

        return spans;
    }

    /// <summary>
    /// Where the game's code for an interop method starts, from the method's IL2CPP info. Harmony leaves that info as it
    /// was: it hooks the code there and points a copy of the info at the original.
    /// </summary>
    private static unsafe IntPtr? CodeStartOf(MethodBase method)
    {
        try
        {
            if (Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(method) is not { } field || field.GetValue(null) is not IntPtr info || info == IntPtr.Zero) return null;

            return UnityVersionHandler.Wrap((Il2CppMethodInfo*)info).MethodPointer;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static unsafe uint? FunctionOf(ScanResult.Found found, uint rva)
    {
        var size = found.Image.SizeOfImage;
        var start = found.Base;

        return FunctionStarts.Of((at, length) => length >= 0 && (ulong)at + (ulong)length <= size ? new ReadOnlySpan<byte>((byte*)start + at, length) : ReadOnlySpan<byte>.Empty, found.Image.Functions, rva);
    }

    /// <summary>
    /// Every class of every loaded assembly, the game's first. A class whose name a report gives has its methods looked
    /// up, which sets them up if the game has not yet. Any other class is only read when the game has set up its methods,
    /// as code a mod changes has been looked up by that mod.
    /// </summary>
    private static IEnumerable<bool> Walk(Naming naming)
    {
        foreach (var image in Images())
        {
            var count = IL2CPP.il2cpp_image_get_class_count(image);

            for (uint index = 0; index < count; index++)
            {
                if (naming.IsComplete) yield break;

                Visit(IL2CPP.il2cpp_image_get_class(image, index), naming);

                yield return true;
            }
        }
    }

    private static unsafe List<IntPtr> Images()
    {
        uint count = 0;
        var assemblies = IL2CPP.il2cpp_domain_get_assemblies(IL2CPP.il2cpp_domain_get(), ref count);
        var images = new List<(int Rank, IntPtr Image)>();

        for (uint index = 0; index < count; index++)
        {
            var image = IL2CPP.il2cpp_assembly_get_image(assemblies[index]);

            if (image != IntPtr.Zero) images.Add((RankOf(IL2CPP.il2cpp_image_get_name_(image)), image));
        }

        return images.OrderBy(image => image.Rank).Select(image => image.Image).ToList();
    }

    /// <summary>HotUpdate holds the game's code, Assembly-CSharp a little of it.</summary>
    private static int RankOf(string? image) => image switch
    {
        "HotUpdate.dll" => 0,
        "Assembly-CSharp.dll" => 1,
        _ => 2,
    };

    private static unsafe void Visit(IntPtr klass, Naming naming)
    {
        if (klass == IntPtr.Zero) return;

        if (naming.ClaimTypes.Count > 0 && IL2CPP.il2cpp_class_get_name_(klass) is { } type && naming.ClaimTypes.TryGetValue(type, out var claimed)) FindClaimed(klass, type, claimed, naming);

        if (naming.Functions.Count == 0) return;

        var wrapped = UnityVersionHandler.Wrap((Il2CppClass*)klass);
        var methods = wrapped.Methods;

        if (methods == null) return;

        for (var index = 0; index < wrapped.MethodCount; index++)
        {
            var method = methods[index];

            if (method == null || naming.Found.RvaOf(UnityVersionHandler.Wrap(method).MethodPointer) is not { } rva || !naming.Functions.Remove(rva)) continue;

            naming.Names[rva] = (IL2CPP.il2cpp_class_get_name_(klass) ?? "?") + "." + (IL2CPP.il2cpp_method_get_name_((IntPtr)method) ?? "?");
        }
    }

    private static unsafe void FindClaimed(IntPtr klass, string type, HashSet<string> claimed, Naming naming)
    {
        var iterator = IntPtr.Zero;
        IntPtr method;

        while ((method = IL2CPP.il2cpp_class_get_methods(klass, ref iterator)) != IntPtr.Zero)
        {
            if (IL2CPP.il2cpp_method_get_name_(method) is not { } name || !claimed.Contains(name)) continue;

            if (naming.Found.RvaOf(UnityVersionHandler.Wrap((Il2CppMethodInfo*)method).MethodPointer) is not { } rva) continue;

            var key = type + "." + name;

            if (!naming.Starts.TryGetValue(key, out var starts)) naming.Starts[key] = starts = new List<uint>();

            starts.Add(rva);
        }
    }

    /// <summary>The compare, on its own thread. It never throws, and hands what it found to the main thread.</summary>
    private void Scan()
    {
        ScanResult result;

        try
        {
            result = Compare();
        }
        catch (Exception e)
        {
            result = new ScanResult.Failed(NoScanReason.Unreadable, "could not compare the game's code with GameAssembly.dll: " + e);
        }

        Volatile.Write(ref _finished, result);
        Volatile.Write(ref _isScanning, 0);
    }

    private static unsafe ScanResult Compare()
    {
        var watch = Stopwatch.StartNew();
        var module = GetModuleHandleW(Module);

        if (module == IntPtr.Zero) return new ScanResult.Failed(NoScanReason.Unreadable, "found no GameAssembly.dll loaded, so the game's code is not checked");

        var path = PathOf(module);

        if (path == null || !File.Exists(path)) return new ScanResult.Failed(NoScanReason.Unreadable, "could not find GameAssembly.dll on disk, so the game's code is not checked");

        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var loaded = CodeImage.Read(new ReadOnlySpan<byte>((void*)module, CodeImage.HeaderBytes));
        var image = CodeImage.Read(ReadAt(file, 0, CodeImage.HeaderBytes));

        if (loaded == null || image == null) return new ScanResult.Failed(NoScanReason.Unreadable, "could not read the headers of GameAssembly.dll, so the game's code is not checked");

        if (loaded.TimeStamp != image.TimeStamp) return new ScanResult.Failed(NoScanReason.FileChanged, "GameAssembly.dll on disk is from another build than the one the game loaded, so the game's code is not checked");

        if (image.ManagedCode is not { } code) return new ScanResult.Failed(NoScanReason.Unreadable, "found no " + CodeImage.ManagedSection + " part in GameAssembly.dll, so the game's code is not checked");

        if (RelocationsIn(file, image, code) is not { } relocations) return new ScanResult.Failed(NoScanReason.Unreadable, "could not read the relocations of GameAssembly.dll, so the game's code is not checked");

        var delta = (long)module - (long)image.ImageBase;
        var codeEnd = code.Rva + code.Compared;
        var buffer = new byte[ChunkBytes + ChunkSlack];
        var runs = new List<CodeRun>();
        var changed = 0;
        long scanned = 0;

        for (var rva = code.Rva; rva < codeEnd;)
        {
            var end = Math.Min(codeEnd, Relocations.SafeEnd(Math.Min(codeEnd, rva + ChunkBytes), relocations));
            var expected = buffer.AsSpan(0, (int)(end - rva));

            ReadInto(file, code.FileOffset + (rva - code.Rva), expected);
            Relocations.Apply(expected, rva, relocations, delta);

            foreach (var (from, length) in ReadablePieces(module, rva, end))
            {
                changed += CodeDiff.Add(expected.Slice((int)(from - rva), length), new ReadOnlySpan<byte>((byte*)module + from, length), from, runs);
                scanned += length;
            }

            if (changed > MostChangedBytes || runs.Count > MostRuns)
            {
                return new ScanResult.Failed(NoScanReason.TooMany, "found over " + MostChangedBytes / 1024 + " KB of the game's code changed, far more than mods change, so GameAssembly.dll on disk can't be the one the game loaded");
            }

            rva = end;
        }

        var note = "compared " + scanned / (1024 * 1024) + " MB of the game's code with GameAssembly.dll in " + watch.ElapsedMilliseconds + " ms: " + changed + " bytes differ in " + runs.Count + " places";

        return new ScanResult.Found(module, image, code, scanned, runs, note);
    }

    private static IReadOnlyList<Relocation>? RelocationsIn(FileStream file, CodeImage image, ImageSection code)
    {
        if (image.Relocations.Size == 0) return Array.Empty<Relocation>();

        if (image.FileOffsetOf(image.Relocations) is not { } at) return null;

        return Relocations.In(ReadAt(file, at, (int)image.Relocations.Size), code.Rva, code.Rva + code.Compared);
    }

    /// <summary>The parts of [rva, end) Windows lets this process read. Code is all readable unless something changed that.</summary>
    private static unsafe IEnumerable<(uint From, int Length)> ReadablePieces(IntPtr module, uint rva, uint end)
    {
        var pieces = new List<(uint From, int Length)>();

        for (var at = rva; at < end;)
        {
            if (VirtualQuery((IntPtr)((byte*)module + at), out var info, (UIntPtr)Marshal.SizeOf<MemoryInfo>()) == UIntPtr.Zero) break;

            var regionEnd = (long)info.BaseAddress + (long)info.RegionSize - (long)module;
            var pieceEnd = (uint)Math.Min(end, Math.Max(at + 1, regionEnd));

            if (IsReadable(info)) pieces.Add((at, (int)(pieceEnd - at)));

            at = pieceEnd;
        }

        return pieces;
    }

    private static bool IsReadable(MemoryInfo info) => info.State == Committed && (info.Protect & ReadableProtections) != 0 && (info.Protect & GuardPage) == 0;

    private static byte[] ReadAt(FileStream file, long at, int length)
    {
        var bytes = new byte[length];

        ReadInto(file, at, bytes);

        return bytes;
    }

    private static void ReadInto(FileStream file, long at, Span<byte> bytes)
    {
        file.Position = at;

        for (var read = 0; read < bytes.Length;)
        {
            var got = file.Read(bytes.Slice(read));

            if (got <= 0) throw new EndOfStreamException("GameAssembly.dll ended early");

            read += got;
        }
    }

    private static string? PathOf(IntPtr module)
    {
        var path = new StringBuilder(1024);
        var length = GetModuleFileNameW(module, path, (uint)path.Capacity);

        return length == 0 || length >= path.Capacity ? null : path.ToString();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandleW(string name);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern uint GetModuleFileNameW(IntPtr module, StringBuilder path, uint size);

    [DllImport("kernel32.dll")]
    private static extern UIntPtr VirtualQuery(IntPtr address, out MemoryInfo info, UIntPtr length);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryInfo
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public ushort PartitionId;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    /// <summary>What the compare thread hands the main thread.</summary>
    private abstract record ScanResult(string Note)
    {
        public sealed record Failed(NoScanReason Reason, string Note) : ScanResult(Note);

        /// <param name="Base">Where Windows loaded GameAssembly.dll.</param>
        public sealed record Found(IntPtr Base, CodeImage Image, ImageSection Code, long Scanned, IReadOnlyList<CodeRun> Runs, string Note) : ScanResult(Note)
        {
            /// <summary>Where an address in the game's managed code is, from the module's start; null for one outside it.</summary>
            public uint? RvaOf(IntPtr address)
            {
                var rva = (long)address - (long)Base;

                return rva >= Code.Rva && rva < Code.Rva + Code.Compared ? (uint)rva : null;
            }
        }
    }

    /// <summary>A finished compare waiting on the names it needs: of the methods reports name, and of the changed methods nothing explains.</summary>
    private sealed class Naming
    {
        public Naming(ScanResult.Found found, ClaimSet claims, List<CodeSpan> harmony, Dictionary<string, HashSet<string>> claimTypes, IEnumerable<uint> functions)
        {
            Found = found;
            Claims = claims;
            Harmony = harmony;
            ClaimTypes = claimTypes;
            Functions = functions.ToHashSet();
        }

        public ScanResult.Found Found { get; }

        public ClaimSet Claims { get; }

        public List<CodeSpan> Harmony { get; }

        /// <summary>Type names to look for, with the methods of each that reports name.</summary>
        public Dictionary<string, HashSet<string>> ClaimTypes { get; }

        /// <summary>The changed methods still to name. Each leaves the set once named.</summary>
        public HashSet<uint> Functions { get; }

        /// <summary>The starts found so far for each "Type.Method" reports name.</summary>
        public Dictionary<string, List<uint>> Starts { get; } = new(StringComparer.Ordinal);

        public Dictionary<uint, string> Names { get; } = new();

        public SlicedJob? Job { get; set; }

        public bool IsCutShort { get; set; }

        /// <summary>A type could be found in more than one assembly, so a report's types keep the walk going to the end.</summary>
        public bool IsComplete => ClaimTypes.Count == 0 && Functions.Count == 0;
    }
}
