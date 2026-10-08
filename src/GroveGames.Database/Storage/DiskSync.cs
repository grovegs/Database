using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace GroveGames.Database.Storage;

internal static class DiskSync
{
    private const int FullSyncCommand = 51;

    private static bool s_fullSyncUnavailable;

    public static void Flush(FileStream stream)
    {
        stream.Flush(false);

        if (!s_fullSyncUnavailable && TryFullSync(stream.SafeFileHandle))
        {
            return;
        }

        stream.Flush(true);
    }

    private static bool TryFullSync(SafeFileHandle handle)
    {
        try
        {
            return FileControl(handle, FullSyncCommand, 0) != -1;
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
        {
            s_fullSyncUnavailable = true;
            return false;
        }
    }

    [DllImport("libSystem.dylib", EntryPoint = "fcntl", SetLastError = true)]
    private static extern int FileControl(SafeFileHandle handle, int command, int argument);
}
