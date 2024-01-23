// Copyright 2015 gRPC authors.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//	 http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#define GUICS

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Unix.Terminal; 

/// <summary>
/// Represents a dynamically loaded unmanaged library in a (partially) platform independent manner.
/// First, the native library is loaded using dlopen (on Unix systems) or using LoadLibrary (on Windows).
/// dlsym or GetProcAddress are then used to obtain symbol addresses. <c>Marshal.GetDelegateForFunctionPointer</c>
/// transforms the addresses into delegates to native methods.
/// See http://stackoverflow.com/questions/13461989/p-invoke-to-dynamically-loaded-library-on-mono.
/// </summary>
class UnmanagedLibrary {
	const string UnityEngineApplicationClassName = "UnityEngine.Application, UnityEngine";
	const string XamarinAndroidObjectClassName = "Java.Lang.Object, Mono.Android";
	const string XamarinIOSObjectClassName = "Foundation.NSObject, Xamarin.iOS";
	static readonly bool IsWindows;
	static readonly bool IsLinux;
	static readonly bool Is64Bit;
#if GUICS
	static readonly bool IsMono;
#else
		static bool IsMono, IsUnity, IsXamarinIOS, IsXamarinAndroid, IsXamarin;
#endif
	static bool IsNetCore;

	public static bool IsMacOSPlatform { get; }

	[DllImport ("libc")]
	extern static int uname (IntPtr buf);

	static string GetUname ()
	{
		var buffer = Marshal.AllocHGlobal (8192);
		try {
			if (uname (buffer) == 0) {
				return Marshal.PtrToStringAnsi (buffer);
			}
			return string.Empty;
		} catch {
			return string.Empty;
		} finally {
			if (buffer != IntPtr.Zero) {
				Marshal.FreeHGlobal (buffer);
			}
		}
	}

	static UnmanagedLibrary ()
	{
		var platform = Environment.OSVersion.Platform;

		IsMacOSPlatform = platform == PlatformID.Unix && GetUname () == "Darwin";
		IsLinux = platform == PlatformID.Unix && !IsMacOSPlatform;
		IsWindows = platform == PlatformID.Win32NT || platform == PlatformID.Win32S || platform == PlatformID.Win32Windows;
		Is64Bit = Marshal.SizeOf (typeof (IntPtr)) == 8;
		IsMono = Type.GetType ("Mono.Runtime") != null;
		if (!IsMono) {
			IsNetCore = Type.GetType ("System.MathF") != null;
		}
#if GUICS
		//IsUnity = IsXamarinIOS = IsXamarinAndroid = IsXamarin = false;
#else
			IsUnity = Type.GetType (UnityEngineApplicationClassName) != null;
			IsXamarinIOS = Type.GetType (XamarinIOSObjectClassName) != null;
			IsXamarinAndroid = Type.GetType (XamarinAndroidObjectClassName) != null;
			IsXamarin = IsXamarinIOS || IsXamarinAndroid;
#endif

	}

	// flags for dlopen
	const int RTLD_LAZY = 1;
	const int RTLD_GLOBAL = 8;

	public readonly string LibraryPath;

	public IntPtr NativeLibraryHandle { get; }

	//
	// if isFullPath is set to true, the provided array of libraries are full paths
	// and are tested for the file existing, otherwise the file is merely the name
	// of the shared library that we pass to dlopen
	//
	public UnmanagedLibrary (string [] libraryPathAlternatives, bool isFullPath)
	{
		if (isFullPath) {
			LibraryPath = FirstValidLibraryPath (libraryPathAlternatives);
			NativeLibraryHandle = PlatformSpecificLoadLibrary (LibraryPath);
		} else {
			foreach (var lib in libraryPathAlternatives) {
				NativeLibraryHandle = PlatformSpecificLoadLibrary (lib);
				if (NativeLibraryHandle != IntPtr.Zero) {
					LibraryPath = lib;
					break;
				}
			}
		}

		if (NativeLibraryHandle == IntPtr.Zero) {
			throw new IOException ($"Error loading native library \"{string.Join (", ", libraryPathAlternatives)}\"");
		}
	}

	/// <summary>
	/// Loads symbol in a platform specific way.
	/// </summary>
	/// <param name="symbolName"></param>
	/// <returns></returns>
	public IntPtr LoadSymbol (string symbolName)
	{
		if (IsWindows) {
			// See http://stackoverflow.com/questions/10473310 for background on this.
			if (Is64Bit) {
				return Windows.GetProcAddress (NativeLibraryHandle, symbolName);
			}
			// Yes, we could potentially predict the size... but it's a lot simpler to just try
			// all the candidates. Most functions have a suffix of @0, @4 or @8 so we won't be trying
			// many options - and if it takes a little bit longer to fail if we've really got the wrong
			// library, that's not a big problem. This is only called once per function in the native library.
			symbolName = "_" + symbolName + "@";
			for (var stackSize = 0; stackSize < 128; stackSize += 4) {
				var candidate = Windows.GetProcAddress (NativeLibraryHandle, symbolName + stackSize);
				if (candidate != IntPtr.Zero) {
					return candidate;
				}
			}
			// Fail.
			return IntPtr.Zero;
		}
		if (IsLinux) {
			if (IsMono) {
				return Mono.dlsym (NativeLibraryHandle, symbolName);
			}
			if (IsNetCore) {
				return CoreCLR.dlsym (NativeLibraryHandle, symbolName);
			}
			return Linux.dlsym (NativeLibraryHandle, symbolName);
		}
		if (IsMacOSPlatform) {
			return MacOSX.dlsym (NativeLibraryHandle, symbolName);
		}
		throw new InvalidOperationException ("Unsupported platform.");
	}

	public T GetNativeMethodDelegate<T> (string methodName)
	where T : class
	{
		var ptr = LoadSymbol (methodName);
		if (ptr == IntPtr.Zero) {
			throw new MissingMethodException (string.Format ("The native method \"{0}\" does not exist", methodName));
		}
		return Marshal.GetDelegateForFunctionPointer<T> (ptr); // non-generic version is obsolete
	}

	/// <summary>
	/// Loads library in a platform specific way.
	/// </summary>
	static IntPtr PlatformSpecificLoadLibrary (string libraryPath)
	{
		if (IsWindows) {
			return Windows.LoadLibrary (libraryPath);
		}
		if (IsLinux) {
			if (IsMono) {
				return Mono.dlopen (libraryPath, RTLD_GLOBAL + RTLD_LAZY);
			}
			if (IsNetCore) {
				try {
					return CoreCLR.dlopen (libraryPath, RTLD_GLOBAL + RTLD_LAZY);
				} catch (Exception) {

					IsNetCore = false;
				}
			}
			return Linux.dlopen (libraryPath, RTLD_GLOBAL + RTLD_LAZY);
		}
		if (IsMacOSPlatform) {
			return MacOSX.dlopen (libraryPath, RTLD_GLOBAL + RTLD_LAZY);
		}
		throw new InvalidOperationException ("Unsupported platform.");
	}

	static string FirstValidLibraryPath (string [] libraryPathAlternatives)
	{
		foreach (var path in libraryPathAlternatives) {
			if (File.Exists (path)) {
				return path;
			}
		}
		throw new FileNotFoundException (
			String.Format ("Error loading native library. Not found in any of the possible locations: {0}",
				string.Join (",", libraryPathAlternatives)));
	}

	static class Windows {
		[DllImport ("kernel32.dll")]
		internal extern static IntPtr LoadLibrary (string filename);

		[DllImport ("kernel32.dll")]
		internal extern static IntPtr GetProcAddress (IntPtr hModule, string procName);
	}

	static class Linux {
		[DllImport ("libdl.so")]
		internal extern static IntPtr dlopen (string filename, int flags);

		[DllImport ("libdl.so")]
		internal extern static IntPtr dlsym (IntPtr handle, string symbol);
	}

	static class MacOSX {
		[DllImport ("libSystem.dylib")]
		internal extern static IntPtr dlopen (string filename, int flags);

		[DllImport ("libSystem.dylib")]
		internal extern static IntPtr dlsym (IntPtr handle, string symbol);
	}

	/// <summary>
	/// On Linux systems, using using dlopen and dlsym results in
	/// DllNotFoundException("libdl.so not found") if libc6-dev
	/// is not installed. As a workaround, we load symbols for
	/// dlopen and dlsym from the current process as on Linux
	/// Mono sure is linked against these symbols.
	/// </summary>
	static class Mono {
		[DllImport ("__Internal")]
		internal extern static IntPtr dlopen (string filename, int flags);

		[DllImport ("__Internal")]
		internal extern static IntPtr dlsym (IntPtr handle, string symbol);
	}

	/// <summary>
	/// Similarly as for Mono on Linux, we load symbols for
	/// dlopen and dlsym from the "libcoreclr.so",
	/// to avoid the dependency on libc-dev Linux.
	/// </summary>
	static class CoreCLR {
		// Custom resolver to support true single-file apps
		// (those which run directly from bundle; in-memory).
		//	 -1 on Unix means self-referencing binary (libcoreclr.so)
		//	 0 means fallback to CoreCLR's internal resolution
		// Note: meaning of -1 stay the same even for non-single-file form factors.
		static CoreCLR () => NativeLibrary.SetDllImportResolver (typeof (CoreCLR).Assembly,
			(libraryName, assembly, searchPath) =>
				libraryName == "libcoreclr.so" ? -1 : IntPtr.Zero);

		[DllImport ("libcoreclr.so")]
		internal extern static IntPtr dlopen (string filename, int flags);

		[DllImport ("libcoreclr.so")]
		internal extern static IntPtr dlsym (IntPtr handle, string symbol);
	}
}