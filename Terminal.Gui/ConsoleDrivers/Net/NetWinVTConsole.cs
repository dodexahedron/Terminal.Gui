namespace Terminal.Gui.ConsoleDrivers.Net;

using System.Runtime.InteropServices;
using Windows.Interop;
using Microsoft.Win32.SafeHandles;
using static Windows.Interop.PInvoke;
using static Windows.Interop.STD_HANDLE;
using static Windows.Interop.CONSOLE_MODE;

///// <summary>
///// Values of console IO handles from the win32 API, as DWORD (UInt32)
///// </summary>
//internal enum STD_HANDLE : uint
//{
//    STD_INPUT_HANDLE = 4294967286U,
//    STD_OUTPUT_HANDLE = 4294967285U,
//    STD_ERROR_HANDLE = 4294967284U,
//}

[MustDisposeResource]
internal sealed partial class NetWinVTConsole : IDisposable
{
    public NetWinVTConsole ()
    {
        if (GetStdHandle (STD_INPUT_HANDLE) is not { IsInvalid: not true } stdin)
        {
            throw new IOException ($"Failed to get input console mode. Invalid handle. Error code: {GetLastError ()}.");
        }

        _inputHandle = stdin;

        if (!GetConsoleMode (_inputHandle, out CONSOLE_MODE mode))
        {
            throw new IOException ($"Failed to get input console mode, error code: {GetLastError ()}.");
        }

        _originalInputConsoleMode = mode;

        if ((mode & ENABLE_VIRTUAL_TERMINAL_INPUT) == 0U)
        {
            mode |= ENABLE_VIRTUAL_TERMINAL_INPUT;

            if (!SetConsoleMode (_inputHandle, mode))
            {
                throw new IOException ($"Failed to set input console mode, error code: {GetLastError ()}.");
            }
        }

        if (GetStdHandle (STD_OUTPUT_HANDLE) is not { IsInvalid: not true } stdout)
        {
            throw new IOException ($"Failed to get output console mode. Invalid handle. Error code: {GetLastError ()}.");
        }

        _outputHandle = stdout;

        if (!GetConsoleMode (_outputHandle, out mode))
        {
            throw new IOException ($"Failed to get output console mode, error code: {GetLastError ()}.");
        }

        _originalOutputConsoleMode = mode;

        if ((mode & (ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN)) < DISABLE_NEWLINE_AUTO_RETURN)
        {
            mode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN;

            if (!SetConsoleMode (_outputHandle, mode))
            {
                throw new IOException ($"Failed to set output console mode, error code: {GetLastError ()}.");
            }
        }

        if (GetStdHandle (STD_ERROR_HANDLE) is not { IsInvalid: not true } stderr)
        {
            throw new IOException ($"Failed to get error console mode. Invalid handle. Error code: {GetLastError ()}.");
        }

        _errorHandle = stderr;

        if (!GetConsoleMode (_errorHandle, out mode))
        {
            throw new IOException ($"Failed to get error console mode, error code: {GetLastError ()}.");
        }

        _originalErrorConsoleMode = mode;

        if ((mode & DISABLE_NEWLINE_AUTO_RETURN) != DISABLE_NEWLINE_AUTO_RETURN)
        {
            mode |= DISABLE_NEWLINE_AUTO_RETURN;

            if (!SetConsoleMode (_errorHandle, mode))
            {
                throw new IOException ($"Failed to set error console mode, error code: {GetLastError ()}.");
            }
        }
    }

    private readonly SafeFileHandle _errorHandle;
    private readonly SafeFileHandle _inputHandle;
    private readonly SafeFileHandle _outputHandle;
    private readonly CONSOLE_MODE _originalErrorConsoleMode;
    private readonly CONSOLE_MODE _originalInputConsoleMode;
    private readonly CONSOLE_MODE _originalOutputConsoleMode;

    //[MustDisposeResource (false)]
    //[LibraryImport ("kernel32", SetLastError = true)]
    //private static partial SafeFileHandle GetStdHandle (nint nStdHandle);

    //[LibraryImport ("kernel32", SetLastError = true)]
    //[return: MarshalAs (UnmanagedType.Bool)]
    //private static partial bool SetConsoleMode (nint hConsoleHandle, uint dwMode);

    private volatile bool _disposed;

    /// <inheritdoc/>
    public void Dispose ()
    {
        if (!_disposed)
        {
            Dispose (true);
            _disposed = true;
            GC.SuppressFinalize (this);
        }
    }

    public void Cleanup ()
    {
        if (!SetConsoleMode (_inputHandle, _originalInputConsoleMode))
        {
            throw new IOException ($"Failed to restore input console mode, error code: {GetLastError ()}.");
        }

        if (!SetConsoleMode (_outputHandle, _originalOutputConsoleMode))
        {
            throw new IOException ($"Failed to restore output console mode, error code: {GetLastError ()}.");
        }

        if (!SetConsoleMode (_errorHandle, _originalErrorConsoleMode))
        {
            throw new IOException ($"Failed to restore error console mode, error code: {GetLastError ()}.");
        }
    }

    private void Dispose (bool disposing)
    {
        if (disposing)
        {
            _errorHandle.Dispose ();
            _inputHandle.Dispose ();
            _outputHandle.Dispose ();
        }
    }

    //[LibraryImport ("kernel32", SetLastError = true)]
    //[return: MarshalAs(UnmanagedType.Bool)]
    //private static partial bool GetConsoleMode (nint hConsoleHandle, out uint lpMode);

    [LibraryImport ("kernel32")]
    private static partial uint GetLastError ();

    /// <inheritdoc/>
    ~NetWinVTConsole () { Dispose (false); }
}
