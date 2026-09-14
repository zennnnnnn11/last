using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace last.Core.Connection.Native;

public sealed partial class WindowsProcessCommandLine : IProcessCommandLineReader
{
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const int ProcessCommandLineInformation = 60;

    private const int StatusSuccess = 0;
    private const int StatusBufferOverflow = unchecked((int)0x80000005);
    private const int StatusBufferTooSmall = unchecked((int)0xC0000023);
    private const int StatusInfoLengthMismatch = unchecked((int)0xC0000004);

    public static WindowsProcessCommandLine Instance { get; } = new();

    public string? GetCommandLine(int pid)
    {
        if (pid <= 0)
            return null;

        using var processHandle = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (processHandle.IsInvalid)
            return null;

        var buffer = IntPtr.Zero;
        try
        {
            var status = NtQueryInformationProcess(
                processHandle,
                ProcessCommandLineInformation,
                IntPtr.Zero,
                0,
                out var requiredLength);

            if (status != StatusSuccess &&
                status != StatusBufferOverflow &&
                status != StatusBufferTooSmall &&
                status != StatusInfoLengthMismatch)
                return null;

            if (requiredLength <= 0)
                return null;

            buffer = Marshal.AllocHGlobal(requiredLength);

            status = NtQueryInformationProcess(
                processHandle,
                ProcessCommandLineInformation,
                buffer,
                requiredLength,
                out _);

            if (status < 0)
                return null;

            var unicodeString = Marshal.PtrToStructure<UnicodeString>(buffer);
            if (unicodeString.Buffer == IntPtr.Zero || unicodeString.Length == 0)
                return null;

            var bufferStart = buffer;
            var bufferEnd = bufferStart + requiredLength;
            var stringStart = unicodeString.Buffer;
            var stringEnd = stringStart + unicodeString.Length;

            if (stringStart < bufferStart || stringEnd > bufferEnd || stringStart > stringEnd)
                return null;

            return Marshal.PtrToStringUni(unicodeString.Buffer, unicodeString.Length / sizeof(char));
        }
        catch
        {
            return null;
        }
        finally
        {
            if (buffer != IntPtr.Zero)
                Marshal.FreeHGlobal(buffer);
        }
    }

    public unsafe string? GetProcessExecutablePath(int pid)
    {
        if (pid <= 0)
            return null;

        using var processHandle = OpenProcess(ProcessQueryLimitedInformation, false, pid);
        if (processHandle.IsInvalid)
            return null;

        var buffer = stackalloc char[1024];
        var size = 1024u;
        if (QueryFullProcessImageNameW(processHandle, 0, buffer, ref size) && size > 0)
            return new string(buffer, 0, (int)size);

        return null;
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    public static partial SafeProcessHandle OpenProcess(uint dwDesiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool QueryFullProcessImageNameW(
        SafeProcessHandle hProcess,
        uint dwFlags,
        char* lpExeName,
        ref uint lpdwSize);

    [LibraryImport("ntdll.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int NtQueryInformationProcess(
        SafeProcessHandle processHandle,
        int processInformationClass,
        IntPtr processInformation,
        int processInformationLength,
        out int returnLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }
}