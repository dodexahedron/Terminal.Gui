namespace Terminal.Gui.ConsoleDrivers.Windows.Interop;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;
using static System.Runtime.InteropServices.Marshal;

/// <content>
///     Contains extern methods from "KERNEL32.dll".
/// </content>
internal static partial class PInvoke
{
    [LibraryImport ("kernel32", EntryPoint = "SetConsoleMode")]
    public static partial bool SetConsoleModeImpl (nint hConsoleHandle, CONSOLE_MODE dwMode);

    /// <summary>Closes an open object handle.</summary>
    /// <param name="hObject">A valid handle to an open object.</param>
    /// <returns>
    ///     <para>
    ///         If the function succeeds, the return value is nonzero.<br/>
    ///         If the function fails, the return value is zero. To get extended error information, call <see cref="GetLastError"/>.
    ///     </para>
    /// </returns>
    /// <remarks>
    ///     If the application is running under a debugger, the function will throw an exception if it receives either a handle value
    ///     that is not valid  or a pseudo-handle value. This can happen if you close a handle twice.
    /// </remarks>
    [SupportedOSPlatform ("windows5.0")]
    internal static BOOL CloseHandle (SafeFileHandle hObject)
    {
        SetLastSystemError (0);
        BOOL result = CloseHandleImpl (hObject);
        SetLastPInvokeError (GetLastSystemError ());

        return result;
    }

    ///// <inheritdoc cref="GetConsoleMode(HANDLE,CONSOLE_MODE*)"/>
    //internal static unsafe bool GetConsoleMode (SafeHandle? hConsoleHandle, in CONSOLE_MODE lpMode)
    //{
    //    ArgumentNullException.ThrowIfNull (hConsoleHandle, nameof (hConsoleHandle));
    //    ArgumentOutOfRangeException.ThrowIfZero ((uint)lpMode, nameof (lpMode));

    //    bool hConsoleHandleRefAdded = false;

    //    try
    //    {
    //        fixed (CONSOLE_MODE* lpModeLocal = &lpMode)
    //        {
    //            hConsoleHandle.DangerousAddRef (ref hConsoleHandleRefAdded);
    //            HANDLE hConsoleHandleLocal = hConsoleHandle.DangerousGetHandle ();

    //            return GetConsoleMode (hConsoleHandleLocal, lpModeLocal);
    //        }
    //    }
    //    finally
    //    {
    //        if (hConsoleHandleRefAdded)
    //        {
    //            hConsoleHandle.DangerousRelease ();
    //        }
    //    }
    //}

    /// <summary>Retrieves the current input mode of a console's input buffer or the current output mode of a console screen buffer.</summary>
    /// <param name="hConsoleHandle">
    ///     A handle to the console input buffer or the console screen buffer. The handle must have the GENERIC_READ access right.
    ///     For more information, see [Console Buffer Security
    ///     and Access Rights](console-buffer-security-and-access-rights.md).
    /// </param>
    /// <param name="lpMode">
    ///     <para>
    ///         An out reference that receives the current mode of the specified buffer.
    ///     </para>
    ///     <para><see href="https://learn.microsoft.com/windows/console/getconsolemode#parameters">Read more on Microsoft Learn</see>.</para>
    /// </param>
    /// <returns>
    ///     If the function succeeds, the return value is true. If the function fails, the return value is false. To get extended
    ///     error information, call GetLastError.
    /// </returns>
    /// <remarks>
    ///     <para>
    ///         To change a console's I/O modes, call <see cref="SetConsoleMode(SafeHandle,CONSOLE_MODE)"/>
    ///         method.
    ///     </para>
    ///     <para><see href="https://learn.microsoft.com/windows/console/getconsolemode#">Read more on Microsoft Learn</see>.</para>
    /// </remarks>
    [SkipLocalsInit]
    internal static unsafe bool GetConsoleMode (SafeFileHandle hConsoleHandle, out CONSOLE_MODE lpMode)
    {
        // This is basically a cleaner version of what LibraryImport would have generated.
        // The DllImport 
        Unsafe.SkipInit (out lpMode);
        SafeHandleMarshaller<SafeFileHandle>.ManagedToUnmanagedIn consoleHandleNativeMarshaller = new ();

        int getConsoleModeReturnValueRaw;

        try
        {
            consoleHandleNativeMarshaller.FromManaged (hConsoleHandle);

            fixed (CONSOLE_MODE* consoleModeValuePointer = &lpMode)
            {
                nint nativeHandle = consoleHandleNativeMarshaller.ToUnmanaged ();

                if (GetConsoleMode (nativeHandle, consoleModeValuePointer) == 0)
                {
                    return true;
                }
            }
        }
        finally
        {
            consoleHandleNativeMarshaller.Free ();
        }

        SetLastPInvokeError (GetLastSystemError ());

        return false;
    }

    //internal static unsafe bool GetConsoleMode (HANDLE hConsoleHandle, ref CONSOLE_MODE lpMode)
    //{
    //    Marshal.SetLastSystemError (0);
    //    bool status = GetConsoleModeImpl (hConsoleHandle, lpMode);
    //    Marshal.SetLastPInvokeError (Marshal.GetLastSystemError ());

    //    return status;
    //}

    [LibraryImport ("kernel32", EntryPoint = "GetConsoleMode", SetLastError = true)]
    [return: MarshalAs (UnmanagedType.Bool)]
    internal static unsafe partial bool GetConsoleModeImpl (SafeFileHandle hConsoleHandle, out CONSOLE_MODE lpMode);

    /// <summary>Retrieves a handle to the specified standard device (standard input, standard output, or standard error).</summary>
    /// <param name="nStdHandle">
    ///     The standard device.<br/>
    ///     NOTE - The values for these constants are unsigned numbers, but are defined in the header files as a cast from a signed
    ///     number and take advantage of the C compiler rolling
    ///     them over to just under the maximum 32-bit value. When interfacing with these handles in a language that does not parse the
    ///     headers and is re-defining the constants, please be
    ///     aware of this constraint. As an example, `((DWORD)-10)` is actually the unsigned number `4294967286`.
    /// </param>
    /// <returns>
    ///     If the function succeeds, the return value is a handle to the specified device, or a redirected handle set by a previous call
    ///     to [**SetStdHandle**](setstdhandle.md). The
    ///     handle has **GENERIC\_READ** and **GENERIC\_WRITE** access rights, unless the application has used **SetStdHandle** to set a
    ///     standard handle with lesser access. > [!TIP] >
    ///     It is not required to dispose of this handle with [**CloseHandle**](/windows/win32/api/handleapi/nf-handleapi-closehandle)
    ///     when done. See [**Remarks**](#handle-disposal)
    ///     for more information. If the function fails, the return value is **INVALID\_HANDLE\_VALUE**. To get extended error
    ///     information, call
    ///     [**GetLastError**](/windows/win32/api/errhandlingapi/nf-errhandlingapi-getlasterror). If an application does not have
    ///     associated standard handles, such as a service
    ///     running on an interactive desktop, and has not redirected them, the return value is **NULL**.
    /// </returns>
    /// <remarks>
    ///     <para>
    ///         Handles returned by **GetStdHandle** can be used by applications that need to read from or write to the console.<br/>
    ///         When a console is created, the standard input handle is a handle to the console's input buffer, and the standard output
    ///         and standard error handles are handles of the
    ///         console's active screen buffer. These handles can be used by the
    ///         [**ReadFile**](/windows/win32/api/fileapi/nf-fileapi-readfile) and
    ///         [**WriteFile**](/windows/win32/api/fileapi/nf-fileapi-writefile) functions, or by any of the console functions that
    ///         access the console input buffer or a screen buffer (for
    ///         example, the [**ReadConsoleInput**](readconsoleinput.md), [**WriteConsole**](writeconsole.md), or
    ///         [**GetConsoleScreenBufferInfo**](getconsolescreenbufferinfo.md) functions). The standard handles of a process may be
    ///         redirected by a call to
    ///         [**SetStdHandle**](setstdhandle.md), in which case **GetStdHandle** returns the redirected handle. If the standard
    ///         handles have been redirected, you can specify the
    ///         `CONIN$` value in a call to the [**CreateFile**](/windows/win32/api/fileapi/nf-fileapi-createfilea) function to get a
    ///         handle to a console's input buffer. Similarly, you
    ///         can specify the `CONOUT$` value to get a handle to a console's active screen buffer. The standard handles of a process on
    ///         entry of the main method are dictated by the
    ///         configuration of the [**/SUBSYSTEM**](/cpp/build/reference/subsystem-specify-subsystem) flag passed to the linker when
    ///         the application was built. Specifying
    ///         **/SUBSYSTEM:CONSOLE** requests that the operating system fill the handles with a console session on startup, if the
    ///         parent didn't already fill the standard handle table
    ///         by inheritance. On the contrary, **/SUBSYSTEM:WINDOWS** implies that the application does not need a console and will
    ///         likely not be making use of the standard handles.
    ///         More information on handle inheritance can be found in the documentation for
    ///         [**STARTF\_USESTDHANDLES**](/windows/win32/api/processthreadsapi/ns-processthreadsapi-startupinfoa). Some applications
    ///         operate outside the boundaries of their declared
    ///         subsystem; for instance, a **/SUBSYSTEM:WINDOWS** application might check/use standard handles for logging or debugging
    ///         purposes but operate normally with a graphical user
    ///         interface. These applications will need to carefully probe the state of standard handles on startup and make use of
    ///         [**AttachConsole**](attachconsole.md),
    ///         [**AllocConsole**](allocconsole.md), and [**FreeConsole**](freeconsole.md) to add/remove a console if desired. Some
    ///         applications may also vary their behavior on the type
    ///         of inherited handle.
    ///     </para>
    /// </remarks>
    [SkipLocalsInit]
    [MustDisposeResource]
    internal static SafeFileHandle GetStdHandle (STD_HANDLE nStdHandle)
    {
        SafeFileHandle __retVal;

        // Setup - Perform required setup.
        SafeHandleMarshaller<SafeFileHandle>.ManagedToUnmanagedOut consoleHandleNativeMarshaller = new ();

        try
        {
            consoleHandleNativeMarshaller.FromUnmanaged (GetStdHandleP (nStdHandle));

            __retVal = consoleHandleNativeMarshaller.ToManaged ();
        }
        finally
        {
            // CleanupCalleeAllocated - Perform cleanup of callee allocated resources.
            consoleHandleNativeMarshaller.Free ();
        }

        return __retVal;

        // Local P/Invoke
    }

    /// <summary>Sets the input mode of a console's input buffer or the output mode of a console screen buffer.</summary>
    /// <param name="hConsoleHandle">
    ///     A handle to the console input buffer or a console screen buffer. The handle must have the **GENERIC\_READ** access right.
    /// </param>
    /// <param name="dwMode">The input or output mode to be set.</param>
    /// <returns>
    ///     <para>
    ///         If the function succeeds, the return value is nonzero.<br/>
    ///         If the function fails, the return value is zero.<br/>
    ///         To get extended error information, call see cref="GetLastError"/>.
    ///     </para>
    /// </returns>
    /// <remarks>
    ///     To determine the current mode of a console input buffer or a screen buffer, use the
    ///     <see cref="GetConsoleMode(SafeFileHandle,out CONSOLE_MODE)"/> method.
    /// </remarks>
    internal static bool SetConsoleMode (SafeFileHandle hConsoleHandle, CONSOLE_MODE dwMode)
    {
        ArgumentNullException.ThrowIfNull (hConsoleHandle, nameof (hConsoleHandle));
        var hConsoleHandleAddRef = false;

        try
        {
            hConsoleHandle.DangerousAddRef (ref hConsoleHandleAddRef);

            return SetConsoleModeImpl (hConsoleHandle.DangerousGetHandle (), dwMode);
        }
        finally
        {
            if (hConsoleHandleAddRef)
            {
                hConsoleHandle.DangerousRelease ();
            }
        }
    }

    [LibraryImport ("kernel32", EntryPoint = "CloseHandle")]
    private static partial BOOL CloseHandleImpl (SafeFileHandle hObject);

    [DllImport ("kernel32", EntryPoint = "GetConsoleMode", ExactSpelling = true)]
    internal static extern unsafe int GetConsoleMode (nint hConsoleHandle, CONSOLE_MODE* lpMode);

    [LibraryImport ("kernel32")]
    private static partial uint GetLastError ();

    [DllImport ("kernel32", EntryPoint = "GetStdHandle", ExactSpelling = true)]
    private static extern nint GetStdHandleP (STD_HANDLE nStdHandle);
}

file static class NativeExtensions
{
    [MustDisposeResource]
    internal static SafeHandle ToSafeFileHandle (this nint nativeHandle)
    {
        SafeHandleMarshaller<SafeFileHandle>.ManagedToUnmanagedOut marshaller = new ();
        marshaller.FromUnmanaged (nativeHandle);
        SafeFileHandle managedHandle = marshaller.ToManaged ();
        marshaller.Free ();
        return managedHandle;
    }

    [SkipLocalsInit]
    internal static CONSOLE_MODE GetConsoleMode (this nint consoleHandle)
    {
        CONSOLE_MODE consoleMode;
        Unsafe.AsPointer() (ref consoleMode);
        return PInvoke.GetConsoleMode (consoleHandle )
    }
}
