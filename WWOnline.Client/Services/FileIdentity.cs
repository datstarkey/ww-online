using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace WWOnline.Services;

/// <summary>
/// Are two paths the same file on disk, whatever they're called? Catches a patched folder that is
/// the original under another spelling (a junction, subst drive, UNC share or hard link). Read only:
/// it opens both files for their volume serial number and file index, and never writes anything.
/// </summary>
public static class FileIdentity
{
    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out ByHandleFileInformation info);

    /// <summary>(volume serial, file index) of an existing file, or null (missing, unreadable, not Windows).</summary>
    public static (uint Volume, ulong Index)? TryGet(string path)
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(path)) return null;
        try
        {
            using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (!GetFileInformationByHandle(handle, out var info)) return null;
            return (info.VolumeSerialNumber, ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>True only when both files exist and are provably the same file.</summary>
    public static bool SameFile(string a, string b) => TryGet(a) is { } x && TryGet(b) is { } y && x == y;
}
