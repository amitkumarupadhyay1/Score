using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace Score
{
    internal static class NativeSqliteLoader
    {
        private static IntPtr nativeModule;

        public static void EnsureLoaded()
        {
            if (nativeModule != IntPtr.Zero) return;
            var architectureFolder = Environment.Is64BitProcess ? "x64" : "x86";
            var libraryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, architectureFolder, "e_sqlite3.dll");
            if (!File.Exists(libraryPath))
                throw new FileNotFoundException("The native SQLite engine is missing for this Windows architecture.", libraryPath);

            nativeModule = LoadLibrary(libraryPath);
            if (nativeModule == IntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not load the native SQLite engine at " + libraryPath + ".");
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string fileName);
    }
}
