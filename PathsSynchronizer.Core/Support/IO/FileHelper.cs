using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PathsSynchronizer.Core.Support.IO
{
    public static class FileHelper
    {
        private const int _StreamWriterDefaultBufferSize = 1024;
        private const int _FileStreamDefaultBufferSize = 4096;

        private static bool HasNetworkDrive(string path)
        {
            try
            {
                return new DriveInfo(path).DriveType == DriveType.Network;
            }
            catch (ArgumentNullException)
            {
                throw;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool IsUncPath(string path)
        {
            try
            {
                return new Uri(path).IsUnc;
            }
            catch (ArgumentNullException)
            {
                throw;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static async Task InternalWriteAllTextAsync(string path, string contents, Encoding encoding = null, bool append = false)
        {
            _ = path ?? throw new ArgumentNullException(nameof(path));

            using (StreamWriter sw = new StreamWriter(path, append, encoding ?? Encoding.Default, _StreamWriterDefaultBufferSize))
            {
                await sw.WriteAsync(contents).ConfigureAwait(false);
            }
        }

        private static async Task InternalWriteAllLinesAsync(string path, IEnumerable<string> lines, Encoding encoding = null, bool append = false)
        {
            _ = path ?? throw new ArgumentNullException(nameof(path));

            using (StreamWriter sw = new StreamWriter(path, append, encoding ?? Encoding.Default, _StreamWriterDefaultBufferSize))
            {
                foreach (string line in lines)
                {
                    await sw.WriteLineAsync(line).ConfigureAwait(false);
                }
            }
        }

        private static async Task InternalWriteAllBytesAsync(string path, byte[] bytes)
        {
            _ = path ?? throw new ArgumentNullException(nameof(path));

            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, _FileStreamDefaultBufferSize, true))
            {
                await fs.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            }
        }

        private static async Task InternalCopyToAsync(string sourceFilePath, string destFilePath, FileOptions? sourceFileOptions = null, bool overwrite = false)
        {
            _ = sourceFilePath ?? throw new ArgumentNullException(nameof(sourceFilePath));
            _ = destFilePath ?? throw new ArgumentNullException(nameof(destFilePath));

            var sourceStreamFileOpt = (sourceFileOptions ?? FileOptions.SequentialScan) | FileOptions.Asynchronous;

            using (FileStream sourceStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, _FileStreamDefaultBufferSize, sourceStreamFileOpt))
            using (FileStream destinationStream = new FileStream(destFilePath, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None, _FileStreamDefaultBufferSize, true))
            {
                await sourceStream.CopyToAsync(destinationStream, _FileStreamDefaultBufferSize).ConfigureAwait(false);
            }
        }

        private static async Task<byte[]> InternalReadAllBytesAsync(string path)
        {
            _ = path ?? throw new ArgumentNullException(nameof(path));

            byte[] bytes;
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, _FileStreamDefaultBufferSize, true))
            {
                int index = 0;
                long fileLength = fs.Length;
                if (fileLength > int.MaxValue)
                {
                    throw new IOException("The file is too long. This operation is currently limited to supporting files less than 2 gigabytes in size.");
                }

                int count = (int)fileLength;
                bytes = new byte[count];
                while (count > 0)
                {
                    int n = await fs.ReadAsync(bytes, index, count).ConfigureAwait(false);
                    if (n == 0) throw new EndOfStreamException("Failed to read past end of stream");
                    index += n;
                    count -= n;
                }
            }

            return bytes;
        }

        private static async Task<string[]> InternalReadAllLinesAsync(string path, Encoding encoding = null)
        {
            _ = path ?? throw new ArgumentNullException(nameof(path));

            string line;
            IList<string> lines = new List<string>();

            using (StreamReader sr = new StreamReader(path, encoding ?? Encoding.Default))
            {
                while ((line = await sr.ReadLineAsync().ConfigureAwait(false)) != null)
                {
                    lines.Add(line);
                }
            }

            return lines.ToArray();
        }

        private static async Task<string> InternalReadAllTextAsync(string path, Encoding encoding = null)
        {
            _ = path ?? throw new ArgumentNullException(nameof(path));

            using (StreamReader sr = new StreamReader(path, encoding ?? Encoding.Default, true, _StreamWriterDefaultBufferSize))
            {
                return await sr.ReadToEndAsync().ConfigureAwait(false);
            }
        }

        public static async Task WriteAllBytesAsync(string path, byte[] bytes)
        {
            await InternalWriteAllBytesAsync(path, bytes).ConfigureAwait(false);
        }

        public static async Task WriteAllLinesAsync(string path, IEnumerable<string> contents)
        {
            await InternalWriteAllLinesAsync(path, contents).ConfigureAwait(false);
        }

        public static async Task WriteAllLinesAsync(string path, IEnumerable<string> contents, Encoding encoding)
        {
            await InternalWriteAllLinesAsync(path, contents, encoding).ConfigureAwait(false);
        }

        public static async Task WriteAllLinesAsync(string path, string[] contents)
        {
            await InternalWriteAllLinesAsync(path, contents).ConfigureAwait(false);
        }

        public static async Task WriteAllLinesAsync(string path, string[] contents, Encoding encoding)
        {
            await InternalWriteAllLinesAsync(path, contents, encoding).ConfigureAwait(false);
        }

        public static async Task WriteAllTextAsync(string path, string contents)
        {
            await InternalWriteAllTextAsync(path, contents).ConfigureAwait(false);
        }

        public static async Task WriteAllTextAsync(string path, string contents, Encoding encoding)
        {
            await InternalWriteAllTextAsync(path, contents, encoding).ConfigureAwait(false);
        }

        public static async Task AppendAllLinesAsync(string path, IEnumerable<string> contents)
        {
            await InternalWriteAllLinesAsync(path, contents, append: true).ConfigureAwait(false);
        }

        public static async Task AppendAllLinesAsync(string path, IEnumerable<string> contents, Encoding encoding)
        {
            await InternalWriteAllLinesAsync(path, contents, encoding, true).ConfigureAwait(false);
        }

        public static async Task AppendAllTextAsync(string path, string contents, Encoding encoding)
        {
            await InternalWriteAllTextAsync(path, contents, encoding, true).ConfigureAwait(false);
        }

        public static async Task AppendAllTextAsync(string path, string contents)
        {
            await InternalWriteAllTextAsync(path, contents, append: true).ConfigureAwait(false);
        }

        public static async Task DeleteAsync(string filePath)
        {
            _ = filePath ?? throw new ArgumentNullException(nameof(filePath));

            if (!File.Exists(filePath))
            {
                return;
            }

            using (FileStream stream = new FileStream(filePath, FileMode.Truncate, FileAccess.ReadWrite, FileShare.Delete, 1, FileOptions.DeleteOnClose | FileOptions.Asynchronous))
            {
                await stream.FlushAsync().ConfigureAwait(false);
            }
        }

        public static async Task MoveAsync(string sourceFilePath, string destFilePath)
        {
            _ = sourceFilePath ?? throw new ArgumentNullException(nameof(sourceFilePath));
            _ = destFilePath ?? throw new ArgumentNullException(nameof(destFilePath));

            if (IsUncPath(sourceFilePath) || HasNetworkDrive(sourceFilePath) || IsUncPath(destFilePath) || HasNetworkDrive(destFilePath))
            {
                await InternalCopyToAsync(sourceFilePath, destFilePath, FileOptions.Asynchronous | FileOptions.DeleteOnClose).ConfigureAwait(false);
                return;
            }

            FileInfo sourceFileInfo = new FileInfo(sourceFilePath);
            string sourceDrive = Path.GetPathRoot(sourceFileInfo.FullName);

            FileInfo destFileInfo = new FileInfo(destFilePath);
            string destDrive = Path.GetPathRoot(destFileInfo.FullName);

            if (sourceDrive == destDrive)
            {
                File.Move(sourceFilePath, destFilePath);
                return;
            }

            await Task.Run(() => File.Move(sourceFilePath, destFilePath)).ConfigureAwait(false);
        }

        public static async Task CopyAsync(string sourceFilePath, string destFilePath)
        {
            await CopyAsync(sourceFilePath, destFilePath, false).ConfigureAwait(false);
        }

        public static async Task CopyAsync(string sourceFilePath, string destFilePath, bool overwrite)
        {
            _ = sourceFilePath ?? throw new ArgumentNullException(nameof(sourceFilePath));
            _ = destFilePath ?? throw new ArgumentNullException(nameof(destFilePath));

            if (IsUncPath(sourceFilePath) || HasNetworkDrive(sourceFilePath) || IsUncPath(destFilePath) || HasNetworkDrive(destFilePath))
            {
                await InternalCopyToAsync(sourceFilePath, destFilePath, FileOptions.Asynchronous | FileOptions.SequentialScan, overwrite).ConfigureAwait(false);
                return;
            }

            FileInfo sourceFileInfo = new FileInfo(sourceFilePath);
            string sourceDrive = Path.GetPathRoot(sourceFileInfo.FullName);

            FileInfo destFileInfo = new FileInfo(destFilePath);
            string destDrive = Path.GetPathRoot(destFileInfo.FullName);

            if (sourceDrive == destDrive)
            {
                File.Copy(sourceFilePath, destFilePath, overwrite);
                return;
            }

            await Task.Run(() => File.Copy(sourceFilePath, destFilePath, overwrite)).ConfigureAwait(false);
        }

        public static async Task<byte[]> ReadAllBytesAsync(string path)
        {
            return await InternalReadAllBytesAsync(path).ConfigureAwait(false);
        }

        public static async Task<string[]> ReadAllLinesAsync(string path, Encoding encoding)
        {
            return await InternalReadAllLinesAsync(path, encoding).ConfigureAwait(false);
        }

        public static async Task<string[]> ReadAllLinesAsync(string path)
        {
            return await InternalReadAllLinesAsync(path).ConfigureAwait(false);
        }

        public static async Task<string> ReadAllTextAsync(string path)
        {
            return await InternalReadAllTextAsync(path).ConfigureAwait(false);
        }

        public static async Task<string> ReadAllTextAsync(string path, Encoding encoding)
        {
            return await InternalReadAllTextAsync(path, encoding).ConfigureAwait(false);
        }
    }
}
