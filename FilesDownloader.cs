using IEC61850.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace FSync
{
    internal class FilesDownloader
    {
        private Dictionary<string, List<byte[]>> files;
        private IedConnection conn;

        public delegate void ThreadOverHandler(List<string> filePaths);
        public FilesDownloader(string directory, ThreadOverHandler cb, IedConnection conn)
        {
            this.conn = conn;

            List<FileDirectoryEntry> files = conn.GetFileDirectory(directory);

            this.files = new Dictionary<string, List<byte[]>>();

            Thread downloadProc = new Thread(() => run(files, cb));

            downloadProc.Start();
        }

        private delegate void Run(List<FileDirectoryEntry> files);

        private void run(List<FileDirectoryEntry> files, ThreadOverHandler cb)
        {
            foreach (FileDirectoryEntry file in files)
            {
                if (Path.GetExtension(file.GetFileName()) != String.Empty)
                {
                    try
                    {
                        conn.GetFile(file.GetFileName(), new IedConnection.GetFileHandler(fileChunk), file);
                    }
                    catch (IedConnectionException e)
                    {
                        Debug.WriteLine(e.Message);
                        Debug.WriteLine("Ignoring File " + file.GetFileName());
                    }
                }
            }

            List<string> fileNames = new List<string>();

            foreach (KeyValuePair<string, List<byte[]>> entry in this.files)
            {
                fileNames.Add(entry.Key);
                byte[] fileBytes = sumByteArrays(entry.Value);
                try { Directory.CreateDirectory("files"); } catch { } // Purposefully do nothing: Directory Exists

                Directory.CreateDirectory(Path.Combine("files", Path.GetDirectoryName(entry.Key)));
                try
                {
                    File.WriteAllBytes(Path.Combine("files", entry.Key), fileBytes);
                }
                catch (UnauthorizedAccessException)
                {
                    Debug.WriteLine("Skipped file " + entry.Key);
                }
                catch (Exception e) { Debug.WriteLine(Path.Combine("files", entry.Key) + "\t" + e.Message); }
            }

            cb(fileNames);
        }

        private bool fileChunk(object fileData, byte[] chunk)
        {
            if (fileData is FileDirectoryEntry)
            {
                FileDirectoryEntry fd = (FileDirectoryEntry)fileData;
                List<byte[]> dataUntilNow;
                this.files.TryGetValue(fd.GetFileName(), out dataUntilNow);
                if (!this.files.ContainsKey(fd.GetFileName()))
                {
                    Debug.WriteLine("Downloading File " + fd.GetFileName());
                }
                dataUntilNow = dataUntilNow ?? new List<byte[]>();
                dataUntilNow.Add(chunk);

                this.files[fd.GetFileName()] = dataUntilNow;

                return true;
            }

            return false;
        }

        private static byte[] sumByteArrays(List<byte[]> bs)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                foreach (byte[] chunk in bs)
                {
                    ms.Write(chunk, 0, chunk.Length);
                }

                return ms.ToArray();
            }
        }
    }
}
