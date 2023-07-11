using IEC61850.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace FSync
{
    internal class ConnStateHandler : IDisposable
    {
        private IED ied;
        public ConnStateHandler(IED ied)
        {
            this.ied = ied;
            ied.isWorking = true;
        }

        public void Dispose()
        {
            ied.isWorking = false;
        }
    }

    public enum DownloadedFileState
    {
        DOWNLOADED, SKIPPED_DIRECTORY, SKIPPED_FILTER, SKIPPED_NEWER
    }

    internal class IED : IDisposable
    {
        public readonly IEDConfig config;

        private IedConnection connection = null;

        public IedConnection Connection
        {
            get => connection;
        }

        public bool isWorking = false;

        public bool IsConnected { get => connection != null; }

        public IED(IEDConfig config)
        {
            this.config = config;
        }

        public bool connect()
        {
            if (this.connection != null)
            {
                throw new InvalidOperationException("Already connected");
            }

            IedConnection connection = new IedConnection();
            try
            {
                connection.Connect(this.config.ip, this.config.port);
            }
            catch (IedConnectionException)
            {
                return false;
            }

            this.connection = connection;

            return true;
        }

        public bool CrossCheckName()
        {
            if (this.connection == null) throw new InvalidOperationException("Not connected");
            List<string> devices;

            Globals.logs.log(String.Format("Performing name cross-check for '{0}'...", this.ToString()));

            using (ConnStateHandler handler = new ConnStateHandler(this))
            {
                Globals.logs.log("Reading server directory [IEC61850_READ_NAMELIST_DOMAIN]");
                devices = this.Connection.GetServerDirectory();
            }

            foreach (string device in devices)
            {
                if (!device.StartsWith(this.config.name))
                {
                    Globals.logs.log(String.Format("Cross check failed for '{0}': '{1}' is not the device's name", this.ToString(), device));
                    return false;
                }
            }

            Globals.logs.log(String.Format("Cross check ok for '{0}'", this.ToString()));
            return true;
        }

        public Dictionary<string, bool> GetAllUsedExtensions(List<string> dir)
        {
            Dictionary<string, bool> extensions = new Dictionary<string, bool>();

            foreach (string entry in dir)
            {
                if (Path.HasExtension(entry))
                {
                    extensions[Path.GetExtension(entry)] = true;
                }
            }

            return extensions;
        }

        public Dictionary<string, bool> GetAllUsedFolders(List<string> dir)
        {
            Dictionary<string, bool> folders = new Dictionary<string, bool>();

            foreach (string entry in dir)
            {
                if (!Path.HasExtension(entry))
                {
                    folders[entry.Substring(0, entry.Length - 1)] = true;
                }
            }

            return folders;
        }

        public Dictionary<string, bool> GetAllUsedExtensions()
        {
            return GetAllUsedExtensions(this.ReadFileTree());
        }

        public List<FileDirectoryEntry> ReadFileTree(string root)
        {
            List<FileDirectoryEntry> files;
            


            Globals.logs.log(String.Format("Reading file tree for IED '[{0}] {1}' root: '{2}'", this.config.name, this.config.ip, root));

            using (ConnStateHandler handler = new ConnStateHandler(this))
            {
                try
                {
                    files = this.Connection.GetFileDirectory(root);
                }
                catch (IedConnectionException e)
                {
                    Globals.logs.log(String.Format("Error: {0}", e.ToString()));
                    throw;
                }
            }

            return files;
        }

        public List<string> ReadFileTree()
        {
            List<string> converted = new List<string>();
            
            foreach (FileDirectoryEntry entry in this.ReadFileTree(""))
            {
                converted.Add(entry.GetFileName());
            }

            return converted;
        }

        public void CloseNow()
        {
            if (this.connection == null) return;
            this.connection.Abort();
            this.connection.Dispose();
            this.connection = null;
        }

        public void Dispose()
        {
            if (this.connection == null) return;
            Thread disposeWhenReady = new Thread(() =>
            {
                while (this.isWorking)
                {
                    Thread.Sleep(100);
                }
                try
                {
                    this.connection.Abort();
                    this.connection.Dispose();
                }
                catch (IedConnectionException) { }

                this.connection = null;
            });


            disposeWhenReady.Start();
        }

        public override string ToString()
        {
            return String.Format("[{0}] {1}", this.config.name, this.config.ip);
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

        internal class FileData
        {
            public string path;
            public List<byte[]> data = new List<byte[]>();
        }

        public DownloadedFileState DownloadFile(FileDirectoryEntry path, string destination, bool overwrite)
        {
            if (!overwrite && File.Exists(destination) && (ulong)File.GetLastWriteTime(destination).Ticks >= (path.GetLastModified())) return DownloadedFileState.SKIPPED_NEWER;
            if (!Path.HasExtension(path.GetFileName())) return DownloadedFileState.SKIPPED_DIRECTORY; // Tried downloading a directory

            bool filterPassed = false;

            if (!this.config.logEnabledExtensions.TryGetValue(Path.GetExtension(path.GetFileName()), out filterPassed) || !filterPassed)
            {
                return DownloadedFileState.SKIPPED_FILTER;
            }

            filterPassed = false;

            foreach (KeyValuePair<string, bool> pair in this.config.logEnabledFolders)
            {
                Debug.WriteLine(String.Format("{0} {1}", Path.GetDirectoryName(path.GetFileName()), pair.Key));
            }
            

            if (!this.config.logEnabledFolders.TryGetValue(Path.GetDirectoryName(path.GetFileName()).Replace("\\", "/"), out filterPassed) || !filterPassed)
            {
                return DownloadedFileState.SKIPPED_FILTER;
            }

            using (ConnStateHandler handler = new ConnStateHandler(this))
            {
                FileData reference = new FileData() { path = path.GetFileName() };
                this.Connection.GetFile(path.GetFileName(), (object parameter, byte[] data) =>
                {
                    reference.data.Add(data);
                    return true;
                }, null);

                byte[] finalFileContent = sumByteArrays(reference.data);
                
                Directory.CreateDirectory(Path.GetDirectoryName(destination));

                File.WriteAllBytes(destination, finalFileContent);
            }

            return DownloadedFileState.DOWNLOADED;
        }
    }
}
