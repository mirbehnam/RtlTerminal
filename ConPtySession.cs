using Microsoft.Win32.SafeHandles;

using System;

using System.ComponentModel;

using System.IO;

using System.Runtime.InteropServices;

using System.Text;



namespace RtlTerminal;



public sealed class ConPtySession : IDisposable

{

    private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;

    private const int PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;



    private IntPtr _pseudoConsole;

    private IntPtr _attributeList;

    private SafeFileHandle? _inputWriterHandle;

    private SafeFileHandle? _outputReaderHandle;

    private FileStream? _inputWriter;

    private FileStream? _outputReader;

    private PROCESS_INFORMATION _processInfo;

    private bool _disposed;



    public ConPtySession(short columns, short rows)

    {

        CreatePipe(out var inputReadSide, out var inputWriteSide, IntPtr.Zero, 0);

        CreatePipe(out var outputReadSide, out var outputWriteSide, IntPtr.Zero, 0);



        _inputWriterHandle = new SafeFileHandle(inputWriteSide, ownsHandle: true);

        _outputReaderHandle = new SafeFileHandle(outputReadSide, ownsHandle: true);



        var hr = CreatePseudoConsole(new COORD(columns, rows), inputReadSide, outputWriteSide, 0, out _pseudoConsole);



        CloseHandle(inputReadSide);

        CloseHandle(outputWriteSide);



        if (hr != 0)

            Marshal.ThrowExceptionForHR(hr);



        _inputWriter = new FileStream(_inputWriterHandle, FileAccess.Write, 4096, isAsync: false);

        _outputReader = new FileStream(_outputReaderHandle, FileAccess.Read, 4096, isAsync: false);

    }



    public void Start(string commandLine, string? workingDirectory = null)

    {

        var startupInfo = new STARTUPINFOEX();

        startupInfo.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();



        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref varSize);

        _attributeList = Marshal.AllocHGlobal(varSize);



        if (!InitializeProcThreadAttributeList(_attributeList, 1, 0, ref varSize))

            throw new Win32Exception(Marshal.GetLastWin32Error());



        if (!UpdateProcThreadAttribute(

                _attributeList,

                0,

                (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,

                _pseudoConsole,

                (IntPtr)IntPtr.Size,

                IntPtr.Zero,

                IntPtr.Zero))

        {

            throw new Win32Exception(Marshal.GetLastWin32Error());

        }



        startupInfo.lpAttributeList = _attributeList;



        var command = new StringBuilder(commandLine);



        var success = CreateProcess(

            null,

            command,

            IntPtr.Zero,

            IntPtr.Zero,

            false,

            EXTENDED_STARTUPINFO_PRESENT,

            IntPtr.Zero,

            workingDirectory,

            ref startupInfo,

            out _processInfo);



        if (!success)

            throw new Win32Exception(Marshal.GetLastWin32Error());

    }



    public int Read(byte[] buffer)

    {

        if (_outputReader is null)

            return 0;



        return _outputReader.Read(buffer, 0, buffer.Length);

    }



    public void Write(string text)

    {

        if (_inputWriter is null)

            return;



        var bytes = Encoding.UTF8.GetBytes(text);

        _inputWriter.Write(bytes, 0, bytes.Length);

        _inputWriter.Flush();

    }



    public void Resize(short columns, short rows)

    {

        if (_pseudoConsole != IntPtr.Zero)

            ResizePseudoConsole(_pseudoConsole, new COORD(columns, rows));

    }



    public void Dispose()

    {

        if (_disposed)

            return;



        _disposed = true;



        _inputWriter?.Dispose();

        _outputReader?.Dispose();



        if (_processInfo.hThread != IntPtr.Zero)

            CloseHandle(_processInfo.hThread);



        if (_processInfo.hProcess != IntPtr.Zero)

            CloseHandle(_processInfo.hProcess);



        if (_attributeList != IntPtr.Zero)

        {

            DeleteProcThreadAttributeList(_attributeList);

            Marshal.FreeHGlobal(_attributeList);

        }



        if (_pseudoConsole != IntPtr.Zero)

            ClosePseudoConsole(_pseudoConsole);

    }



    private static IntPtr varSize = IntPtr.Zero;



    [DllImport("kernel32.dll", SetLastError = true)]

    private static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, IntPtr lpPipeAttributes, uint nSize);



    [DllImport("kernel32.dll", SetLastError = true)]

    private static extern bool CloseHandle(IntPtr hObject);



    [DllImport("kernel32.dll", SetLastError = true)]

    private static extern int CreatePseudoConsole(COORD size, IntPtr hInput, IntPtr hOutput, uint dwFlags, out IntPtr phPC);



    [DllImport("kernel32.dll", SetLastError = true)]

    private static extern int ResizePseudoConsole(IntPtr hPC, COORD size);



    [DllImport("kernel32.dll", SetLastError = true)]

    private static extern void ClosePseudoConsole(IntPtr hPC);



    [DllImport("kernel32.dll", SetLastError = true)]

    private static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);



    [DllImport("kernel32.dll", SetLastError = true)]

    private static extern bool UpdateProcThreadAttribute(

        IntPtr lpAttributeList,

        uint dwFlags,

        IntPtr attribute,

        IntPtr lpValue,

        IntPtr cbSize,

        IntPtr lpPreviousValue,

        IntPtr lpReturnSize);



    [DllImport("kernel32.dll", SetLastError = true)]

    private static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);



    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]

    private static extern bool CreateProcess(

        string? lpApplicationName,

        StringBuilder lpCommandLine,

        IntPtr lpProcessAttributes,

        IntPtr lpThreadAttributes,

        bool bInheritHandles,

        uint dwCreationFlags,

        IntPtr lpEnvironment,

        string? lpCurrentDirectory,

        ref STARTUPINFOEX lpStartupInfo,

        out PROCESS_INFORMATION lpProcessInformation);



    [StructLayout(LayoutKind.Sequential)]

    private readonly struct COORD

    {

        public readonly short X;

        public readonly short Y;



        public COORD(short x, short y)

        {

            X = x;

            Y = y;

        }

    }



    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]

    private struct STARTUPINFO

    {

        public int cb;

        public string? lpReserved;

        public string? lpDesktop;

        public string? lpTitle;

        public int dwX;

        public int dwY;

        public int dwXSize;

        public int dwYSize;

        public int dwXCountChars;

        public int dwYCountChars;

        public int dwFillAttribute;

        public int dwFlags;

        public short wShowWindow;

        public short cbReserved2;

        public IntPtr lpReserved2;

        public IntPtr hStdInput;

        public IntPtr hStdOutput;

        public IntPtr hStdError;

    }



    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]

    private struct STARTUPINFOEX

    {

        public STARTUPINFO StartupInfo;

        public IntPtr lpAttributeList;

    }



    [StructLayout(LayoutKind.Sequential)]

    private struct PROCESS_INFORMATION

    {

        public IntPtr hProcess;

        public IntPtr hThread;

        public int dwProcessId;

        public int dwThreadId;

    }

}
