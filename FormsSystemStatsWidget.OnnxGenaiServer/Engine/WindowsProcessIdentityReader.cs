using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FormsSystemStatsWidget.OnnxGenaiServer.Engine;

internal sealed record WindowsProcessIdentity(
    int ProcessId,
    int ParentProcessId,
    DateTimeOffset ProcessStartTimeUtc,
    string ExecutablePath,
    string CommandLine);

internal static class WindowsProcessIdentityReader
{
    private const uint ProcessQueryInformation = 0x0400;
    private const uint ProcessVmRead = 0x0010;
    private const int PebProcessParametersOffset64 = 0x20;
    private const int ProcessParametersCommandLineOffset64 = 0x70;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(
        IntPtr process,
        IntPtr baseAddress,
        IntPtr buffer,
        nuint size,
        out nuint bytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool IsWow64Process(IntPtr process, out bool wow64Process);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        IntPtr process,
        int processInformationClass,
        out ProcessBasicInformation processInformation,
        int processInformationLength,
        out int returnLength);

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern IntPtr CommandLineToArgvW(string commandLine, out int argumentCount);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);

    public static bool TryRead(Process process, out WindowsProcessIdentity? identity)
    {
        identity = null;
        if (!OperatingSystem.IsWindows())
            return false;

        IntPtr handle = IntPtr.Zero;
        try
        {
            handle = OpenProcess(ProcessQueryInformation | ProcessVmRead, false, (uint)process.Id);
            if (handle == IntPtr.Zero || !IsWow64Process(handle, out var isWow64))
                return false;

            if (!Environment.Is64BitProcess || isWow64)
                return false;

            var status = NtQueryInformationProcess(
                handle,
                0,
                out var basicInformation,
                Marshal.SizeOf<ProcessBasicInformation>(),
                out _);
            if (status != 0 || basicInformation.PebBaseAddress == IntPtr.Zero)
                return false;

            var processParametersAddress = ReadPointer(
                handle,
                IntPtr.Add(basicInformation.PebBaseAddress, PebProcessParametersOffset64));
            if (processParametersAddress == IntPtr.Zero)
                return false;

            var commandLineAddress = IntPtr.Add(
                processParametersAddress,
                ProcessParametersCommandLineOffset64);
            var unicodeString = ReadStructure<UnicodeString>(handle, commandLineAddress);
            if (unicodeString.Buffer == IntPtr.Zero || unicodeString.Length == 0)
                return false;

            var commandLineBuffer = Marshal.AllocHGlobal(unicodeString.Length);
            try
            {
                if (!ReadProcessMemory(
                    handle,
                    unicodeString.Buffer,
                    commandLineBuffer,
                    unicodeString.Length,
                    out var bytesRead)
                    || bytesRead != unicodeString.Length)
                {
                    return false;
                }

                var commandLine = Marshal.PtrToStringUni(commandLineBuffer, unicodeString.Length / 2);
                var executablePath = process.MainModule?.FileName;
                if (string.IsNullOrWhiteSpace(commandLine) || string.IsNullOrWhiteSpace(executablePath))
                    return false;

                identity = new WindowsProcessIdentity(
                    process.Id,
                    basicInformation.InheritedFromUniqueProcessId.ToInt32(),
                    new DateTimeOffset(process.StartTime.ToUniversalTime()),
                    Path.GetFullPath(executablePath),
                    commandLine);
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(commandLineBuffer);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException
            or System.ComponentModel.Win32Exception
            or UnauthorizedAccessException
            or ArgumentException)
        {
            return false;
        }
        finally
        {
            if (handle != IntPtr.Zero)
                CloseHandle(handle);
        }
    }

    public static bool TrySplitCommandLine(string commandLine, out string[] arguments)
    {
        arguments = [];
        var values = CommandLineToArgvW(commandLine, out var count);
        if (values == IntPtr.Zero)
            return false;

        try
        {
            arguments = new string[count];
            for (var index = 0; index < count; index++)
            {
                var value = Marshal.ReadIntPtr(values, index * IntPtr.Size);
                arguments[index] = Marshal.PtrToStringUni(value) ?? string.Empty;
            }
            return true;
        }
        finally
        {
            LocalFree(values);
        }
    }

    private static IntPtr ReadPointer(IntPtr process, IntPtr address)
    {
        var buffer = Marshal.AllocHGlobal(IntPtr.Size);
        try
        {
            if (!ReadProcessMemory(process, address, buffer, (nuint)IntPtr.Size, out var bytesRead)
                || bytesRead != (nuint)IntPtr.Size)
            {
                return IntPtr.Zero;
            }
            return Marshal.ReadIntPtr(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static T ReadStructure<T>(IntPtr process, IntPtr address) where T : struct
    {
        var size = Marshal.SizeOf<T>();
        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (!ReadProcessMemory(process, address, buffer, (nuint)size, out var bytesRead)
                || bytesRead != (nuint)size)
            {
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
            return Marshal.PtrToStructure<T>(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        public IntPtr Reserved1;
        public IntPtr PebBaseAddress;
        public IntPtr Reserved2First;
        public IntPtr Reserved2Second;
        public IntPtr UniqueProcessId;
        public IntPtr InheritedFromUniqueProcessId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }
}