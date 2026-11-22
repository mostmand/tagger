using System.Runtime.InteropServices;
using LibVLCSharp.Shared;

namespace Tagger.Infrastructure.Playback;

/// <summary>
/// Loads the bundled Linux libVLC runtime without a process-wide LD_LIBRARY_PATH.
/// Windows and macOS use the official VideoLAN NuGet packages and need no work
/// here beyond <see cref="Core.Initialize"/>.
/// </summary>
public static class VlcRuntimeBootstrap
{
    private const int RtldNow = 2;
    private const int RtldGlobal = 0x100;
    private static readonly object Sync = new();
    private static bool _resolverInstalled;

    /// <summary>
    /// Prepares the native loader for the bundled runtime. Returns false when a
    /// bundle is present but cannot be loaded; no bundle means "use the system
    /// installation", which is still allowed for development.
    /// </summary>
    public static bool TryPrepare(string? explicitDirectory = null)
    {
        if (!OperatingSystem.IsLinux())
        {
            return true;
        }

        var directory = FindBundle(explicitDirectory);
        if (directory is null)
        {
            return true;
        }

        var library = Path.Combine(directory, "lib", "libvlc.so");
        var plugins = Path.Combine(directory, "plugins");
        if (!File.Exists(library) || !Directory.Exists(plugins))
        {
            return false;
        }

        lock (Sync)
        {
            if (_resolverInstalled)
            {
                return true;
            }

            var handle = Native.dlopen(library, RtldNow | RtldGlobal);
            if (handle == IntPtr.Zero)
            {
                return false;
            }

            NativeLibrary.SetDllImportResolver(
                typeof(LibVLC).Assembly,
                (name, _, _) => name == "libvlc" ? handle : IntPtr.Zero);

            // .NET's managed environment setter is not visible to native getenv
            // on Linux, and libvlc reads VLC_PLUGIN_PATH through it.
            Native.setenv("VLC_PLUGIN_PATH", plugins, 1);
            _resolverInstalled = true;
            return true;
        }
    }

    /// <summary>
    /// Returns the runtime directory: explicit override, TAGGER_LIBVLC_PATH, or
    /// the copy shipped next to the app.
    /// </summary>
    public static string? FindBundle(string? explicitDirectory = null)
    {
        var candidates = new[]
        {
            explicitDirectory,
            Environment.GetEnvironmentVariable("TAGGER_LIBVLC_PATH"),
            Path.Combine(AppContext.BaseDirectory, "libvlc", "linux-x64")
        };

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate) && Directory.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    private static class Native
    {
        [DllImport("libdl.so.2", CharSet = CharSet.Ansi)]
        internal static extern IntPtr dlopen(string name, int flags);

        [DllImport("libc.so.6", CharSet = CharSet.Ansi)]
        internal static extern int setenv(string name, string value, int overwrite);
    }
}
