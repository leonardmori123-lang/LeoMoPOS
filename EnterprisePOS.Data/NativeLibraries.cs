using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace EnterprisePOS.Data
{
    internal static class NativeLibraries
    {
        private static readonly object padlock = new object();
        private static bool loaded = false;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string fileName);

        public static void LoadOnce()
        {
            lock (padlock)
            {
                if (loaded)
                {
                    return;
                }

                string folderName;
                if (Environment.Is64BitProcess)
                {
                    folderName = "x64";
                }
                else
                {
                    folderName = "x86";
                }

                byte[] bytes = ReadEmbeddedLibrary("." + folderName + ".SQLite.Interop.dll");
                if (bytes == null)
                {
                    loaded = true;
                    return;
                }

                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string folder = Path.Combine(localAppData, "EnterprisePOS", "native", folderName);
                string target = Path.Combine(folder, "SQLite.Interop.dll");

                Directory.CreateDirectory(folder);
                CopyIfDifferent(bytes, target);

                IntPtr handle = LoadLibrary(target);
                if (handle == IntPtr.Zero)
                {
                    int code = Marshal.GetLastWin32Error();
                    throw new InvalidOperationException(
                        "The program's built-in database engine could not be started (Windows error " + code + ").");
                }

                loaded = true;
            }
        }

        private static byte[] ReadEmbeddedLibrary(string nameEnding)
        {
            Assembly assembly = typeof(NativeLibraries).Assembly;
            string[] names = assembly.GetManifestResourceNames();

            for (int i = 0; i < names.Length; i++)
            {
                if (names[i].EndsWith(nameEnding, StringComparison.OrdinalIgnoreCase))
                {
                    using (Stream stream = assembly.GetManifestResourceStream(names[i]))
                    using (MemoryStream memory = new MemoryStream())
                    {
                        stream.CopyTo(memory);
                        return memory.ToArray();
                    }
                }
            }

            return null;
        }

        private static void CopyIfDifferent(byte[] bytes, string target)
        {
            if (File.Exists(target))
            {
                FileInfo existing = new FileInfo(target);
                if (existing.Length == bytes.Length)
                {
                    return;
                }
            }

            string temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllBytes(temp, bytes);

            try
            {
                if (File.Exists(target))
                {
                    File.Delete(target);
                }
                File.Move(temp, target);
            }
            catch (IOException)
            {
                File.Delete(temp);
            }
            catch (UnauthorizedAccessException)
            {
                File.Delete(temp);
            }
        }
    }
}