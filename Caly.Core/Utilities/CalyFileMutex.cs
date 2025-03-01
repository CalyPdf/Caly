using System;
using System.IO;

namespace Caly.Core.Utilities
{
    // Known issue with named System.Threading.Mutex on Linux/MacOS with AOT
    // See https://github.com/dotnet/runtime/issues/110348
    // https://www.martyndavis.com/?p=440

    // Solution:
    // https://mvolo.com/fun-with-file-locking/

    /// <summary>
    /// File mutex implementation to ensure cross-platform compatibility.
    /// <para>
    /// Known issue with named System.Threading.Mutex on Linux/MacOS with AOT.
    /// See <see href="https://github.com/dotnet/runtime/issues/110348"/> and
    /// <see href="https://www.martyndavis.com/?p=440"/>
    /// </para>
    /// </summary>
    public sealed class CalyFileMutex
    {
        private static readonly string _lockFileName = Path.Combine(Path.GetTempPath(), "caly.lock");

        private FileStream? _lockFile;

        /// <summary>
        /// Same signature as default System.Threading.Mutex.
        /// </summary>
        /// <param name="initiallyOwned">Not in use.</param>
        /// <param name="name">Not in use.</param>
        public CalyFileMutex(bool initiallyOwned, string? name)
        {
            // Same signature as default System.Threading.Mutex
        }

        public bool WaitOne(TimeSpan timeout, bool b)
        {
            // Same signature as default System.Threading.Mutex

            try
            {
                // Force FileMode.CreateNew - if the file already exists, should throw (done for Linux)
                _lockFile = new FileStream(_lockFileName, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Delete);
            }
            catch (IOException)
            {
                // File already in use
                return false;
            }

            return true;
        }

        public void ReleaseMutex()
        {
            try
            {
                File.Delete(_lockFileName);
            }
            catch (Exception e)
            {
                Debug.WriteExceptionToFile(e);
            }

            if (_lockFile is null)
            {
                throw new NullReferenceException("Cannot release file mutex, because the file is null.");
            }
            
            _lockFile.Dispose();
            _lockFile = null;
        }

        public static bool ForceReleaseMutex()
        {
            try
            {
                File.Delete(_lockFileName);
                return true;
            }
            catch (Exception e)
            {
                Debug.WriteExceptionToFile(e);
            }

            return false;
        }
    }
}
