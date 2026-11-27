using System.Runtime.InteropServices;

namespace GroveGames.Database.Storage;

internal static class DiskSync
{
    private const int FullSyncCommand = 51;
    private const int ReadOnly = 0;

    private static bool s_fullSyncUnavailable;
    private static bool s_directorySyncUnavailable;

    public static void Flush(FileStream stream)
    {
        stream.Flush(false);

        // IL2CPP's SafeFileHandle wraps a pointer to its own handle struct, not a descriptor, so the file is opened by path.
        if (!s_fullSyncUnavailable && TryFullSync(stream.Name))
        {
            return;
        }

        stream.Flush(true);
    }

    public static void FlushDirectory(string directory)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        if (!s_fullSyncUnavailable && TryFlushAppleDirectory(directory))
        {
            return;
        }

        if (!s_directorySyncUnavailable)
        {
            TryFlushPosixDirectory(directory);
        }
    }

    private static bool TryFullSync(string path)
    {
        try
        {
            var descriptor = AppleOpen(path, ReadOnly);

            if (descriptor < 0)
            {
                return false;
            }

            try
            {
                return AppleFileControl(descriptor, FullSyncCommand, 0) != -1;
            }
            finally
            {
                _ = AppleClose(descriptor);
            }
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            s_fullSyncUnavailable = true;
            return false;
        }
    }

    private static bool TryFlushAppleDirectory(string directory)
    {
        try
        {
            var descriptor = AppleOpen(directory, ReadOnly);

            if (descriptor < 0)
            {
                throw new IOException($"Could not open '{directory}' to sync it.");
            }

            try
            {
                if (AppleFileControl(descriptor, FullSyncCommand, 0) == -1 && AppleSync(descriptor) == -1)
                {
                    throw new IOException($"Could not sync '{directory}'.");
                }
            }
            finally
            {
                _ = AppleClose(descriptor);
            }

            return true;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            s_fullSyncUnavailable = true;
            return false;
        }
    }

    private static void TryFlushPosixDirectory(string directory)
    {
        try
        {
            var descriptor = PosixOpen(directory, ReadOnly);

            if (descriptor < 0)
            {
                throw new IOException($"Could not open '{directory}' to sync it.");
            }

            try
            {
                if (PosixSync(descriptor) == -1)
                {
                    throw new IOException($"Could not sync '{directory}'.");
                }
            }
            finally
            {
                _ = PosixClose(descriptor);
            }
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            s_directorySyncUnavailable = true;
        }
    }

    [DllImport("libSystem.dylib", EntryPoint = "open", SetLastError = true)]
    private static extern int AppleOpen([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);

    [DllImport("libSystem.dylib", EntryPoint = "fcntl", SetLastError = true)]
    private static extern int AppleFileControl(int descriptor, int command, int argument);

    [DllImport("libSystem.dylib", EntryPoint = "fsync", SetLastError = true)]
    private static extern int AppleSync(int descriptor);

    [DllImport("libSystem.dylib", EntryPoint = "close", SetLastError = true)]
    private static extern int AppleClose(int descriptor);

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int PosixOpen([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);

    [DllImport("libc", EntryPoint = "fsync", SetLastError = true)]
    private static extern int PosixSync(int descriptor);

    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    private static extern int PosixClose(int descriptor);
}
