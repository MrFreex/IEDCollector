using IEC61850.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace IEDCollector
{
    // Used to allow to execute a block of code using the IED object and then tell the rest of the threads it is available
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

    // Describes a transfer's state
    public enum DownloadedFileState
    {
        DOWNLOADED, SKIPPED_DIRECTORY, SKIPPED_FILTER, SKIPPED_NEWER, SKIPPED_FREEMODE
    }

    // Same as FileDirectoryEntry in libiec61850, but with writable fields and a proper Equals method
    internal class EditableFileDirectoryEntry
    {
        public string fileName;

        public uint fileSize;

        public ulong lastModified;

        public EditableFileDirectoryEntry(FileDirectoryEntry fd)
        {
            fileName = fd.GetFileName();
            fileSize = fd.GetFileSize();
            lastModified = fd.GetLastModified();
        }

        public string GetFileName()
        {
            return fileName;
        }

        public uint GetFileSize()
        {
            return fileSize;
        }

        public ulong GetLastModified()
        {
            return lastModified;
        }

        public override bool Equals(object obj)
        {
            if (obj is EditableFileDirectoryEntry)
            {
                EditableFileDirectoryEntry other = (EditableFileDirectoryEntry)obj;
                return other.GetFileName().Equals(this.GetFileName());
            }
            else
            {
                return false;
            }
        }
    }

    // Comparer to allow a dictionary to verify its keys are equal with the proper .Equals method
    internal class FileDirectoryEntryComparer : IEqualityComparer<EditableFileDirectoryEntry>
    {
        public bool Equals(EditableFileDirectoryEntry x, EditableFileDirectoryEntry y)
        {
            return x.Equals(y);
        }

        public int GetHashCode(EditableFileDirectoryEntry obj)
        {
            return obj.GetFileName().GetHashCode();
        }
    }

    // Represents a connection to an IED with the needed methods
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

        /// <summary>
        /// Connects to the IED handling the IEDConnection exceptions
        /// </summary>
        /// <returns>True: success, False: otherwise</returns>
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

        /// <summary>
        /// Performs the name's cross check on the device
        /// </summary>
        /// <returns>True: CrossCheck Ok, False: CrossCheck differs</returns>
        public bool CrossCheckName()
        {
            if (this.connection == null) throw new InvalidOperationException("Not connected");
            List<string> devices;

            Globals.logs.log(String.Format("Performing name cross-check for '{0}'...", this.ToString()), LogLevel.Detailed);

            using (ConnStateHandler handler = new ConnStateHandler(this))
            {
                Globals.logs.log("Reading server directory [IEC61850_READ_NAMELIST_DOMAIN]");
                devices = this.Connection.GetServerDirectory();
            }

            foreach (string device in devices)
            {
                if (!device.StartsWith(this.config.name))
                {
                    Globals.logs.log(String.Format("Cross check failed for '{0}': '{1}' is not the device's name", this.ToString(), this.config.name));
                    return false;
                }
            }

            Globals.logs.log(String.Format("Cross check OK for '{0}'", this.ToString()), LogLevel.Detailed);
            return true;
        }

        /// <summary>
        ///  Retrieves all present extensions in the files list
        /// </summary>
        /// <param name="dir">The IED tree previously retrieved</param>
        /// <returns>A dictionary with the extensions as keys and the values as true, comfortable to init the checkboxes</returns>
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

        /// <summary>
        ///  Retrieves all present folders in the files list
        /// </summary>
        /// <param name="dir">The IED tree previously retrieved</param>
        /// <returns>A dictionary with the folders as keys and the values as true, comfortable to init the checkboxes</returns>
        public Dictionary<string, bool> GetAllUsedFolders(List<string> dir)
        {
            Dictionary<string, bool> folders = new Dictionary<string, bool>();

            foreach (string entry in dir)
            {
                string folderPath = Path.GetDirectoryName(entry).Replace("\\", "/");

                /*
                Globals.logs.log("File: " + entry);
                if (!folders.ContainsKey(folderPath))
                    Globals.logs.log(String.Format("[DEBUG] Adding folder '{0}'", folderPath));
                */
                folders[folderPath] = true;
            }

            return folders;
        }

        /// <summary>
        ///  Retrieves all present extensions in the files list. The file list is retrieved automatically.
        /// </summary>
        /// <returns>A dictionary with the extensions as keys and the values as true, comfortable to init the checkboxes</returns>
        public Dictionary<string, bool> GetAllUsedExtensions()
        {
            return GetAllUsedExtensions(this.ReadFileTree());
        }

        /// <summary>
        /// Reads all the file entries in the IED
        /// </summary>
        /// <param name="root">the folder to start from</param>
        /// <param name="a">dummy parameter</param>
        /// <returns>A Dictionary with the Entry as key, and a bool value to distinguish folders (false) from files (true)</returns>
        public Dictionary<EditableFileDirectoryEntry, bool> ReadFileTree(string root, bool a)
        {
            List<FileDirectoryEntry> files;



            Globals.logs.log(String.Format("Reading file list for IED '[{0}] {1}' root: '{2}'", this.config.name, this.config.ip, root), LogLevel.Basic);

            using (ConnStateHandler handler = new ConnStateHandler(this))
            {
                try
                {
                    files = this.Connection.GetFileDirectory(root);
                }
                catch (IedConnectionException e)
                {
                    Globals.logs.log(String.Format("{1} Error: {0}", e.ToString(), root), LogLevel.Debug);
                    files = new List<FileDirectoryEntry>();
                    //throw;
                }
            }

            Dictionary<EditableFileDirectoryEntry, bool> completeTree = new Dictionary<EditableFileDirectoryEntry, bool>(new FileDirectoryEntryComparer());

            foreach (FileDirectoryEntry file in files)
            {
                EditableFileDirectoryEntry efd = new EditableFileDirectoryEntry(file);

                efd.fileName = Path.Combine(root, efd.fileName);
                completeTree[efd] = Path.HasExtension(file.GetFileName());
            }

            Dictionary<EditableFileDirectoryEntry, bool> newTree = new Dictionary<EditableFileDirectoryEntry, bool>(completeTree, new FileDirectoryEntryComparer());

            foreach (KeyValuePair<EditableFileDirectoryEntry, bool> entry in completeTree)
            {
                if (!entry.Value)
                {
                    Dictionary<EditableFileDirectoryEntry, bool> temp = ReadFileTree(entry.Key.GetFileName(), false);

                    foreach (KeyValuePair<EditableFileDirectoryEntry, bool> subEntry in temp)
                    {
                        newTree[subEntry.Key] = subEntry.Value;
                    }
                }
            }



            return newTree;
        }

        /// <summary>
        /// Reads the IED file tree
        /// </summary>
        /// <param name="root">The path to start from (remote)</param>
        /// <returns>The list of entries found</returns>
        public List<EditableFileDirectoryEntry> ReadFileTree(string root)
        {
            return new List<EditableFileDirectoryEntry>(ReadFileTree(root, true).Keys);
        }

        /// <summary>
        /// Reads the IED file tree
        /// </summary>
        /// <returns>The list of strings representing the files/folders paths</returns>
        public List<string> ReadFileTree()
        {
            List<string> converted = new List<string>();

            foreach (EditableFileDirectoryEntry entry in this.ReadFileTree(""))
            {
                converted.Add(entry.GetFileName());
            }

            return converted;
        }

        /// <summary>
        /// Forcefully closes the connection
        /// </summary>
        public void CloseNow()
        {
            if (this.connection == null) return;
            this.connection.Abort();
            this.connection.Dispose();
            this.connection = null;
        }

        /// <summary>
        /// Disposes the connection when it's not busy
        /// </summary>
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
        /*
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
        */

        /// <summary>
        /// Downloads a file from the IED
        /// </summary>
        /// <param name="path">The remote file path</param>
        /// <param name="destination">The local destination</param>
        /// <param name="monitor">The FileProgressMonitor object that handles the progress update</param>
        /// <param name="overwrite">Whether to overwrite (true) or not (false) any homonym local file</param>
        /// <returns>A DownloadedFileState enum to represent the download state</returns>
        public DownloadedFileState DownloadFile(EditableFileDirectoryEntry path, string destination, FileProgressMonitor monitor, bool overwrite)
        {
            if (!overwrite && File.Exists(destination) && (ulong)File.GetLastWriteTime(destination).Ticks >= (path.GetLastModified())) return DownloadedFileState.SKIPPED_NEWER;
            if (!Path.HasExtension(path.GetFileName())) return DownloadedFileState.SKIPPED_DIRECTORY; // Tried downloading a directory

            bool filterPassed = false;

            if (!this.config.logEnabledExtensions.TryGetValue(Path.GetExtension(path.GetFileName()), out filterPassed) || !filterPassed)
            {
                return DownloadedFileState.SKIPPED_FILTER;
            }

            filterPassed = false;

            string dir = Path.GetDirectoryName(path.GetFileName());

            string[] dirAlternatives =
            {
                dir, dir.TrimStart('\\'), dir.Replace("\\", "/"), dir.Replace("\\", "/").TrimStart('/')
            };

            foreach (string alternative in dirAlternatives)
            {
                bool value = false;
                bool has = this.config.logEnabledFolders.TryGetValue(alternative, out value);

                if (has)
                {
                    if (value)
                    {
                        filterPassed = true;
                    }

                    break;
                }
            }

            if (!filterPassed)
            {
                return DownloadedFileState.SKIPPED_FILTER;
            }

            try {
                Directory.CreateDirectory(Path.GetDirectoryName(destination));

                if (Globals.IsFreeMode)
                {
                    File.WriteAllText(destination, "FREE MODE!");
                    return DownloadedFileState.SKIPPED_FREEMODE;
                }

                using (ConnStateHandler handler = new ConnStateHandler(this))
                {
                    double size = path.GetFileSize();


                    using (FileStream writer = new FileStream(destination, FileMode.OpenOrCreate))
                    {

                        this.Connection.GetFile(path.GetFileName(), (object parameter, byte[] data) =>
                        {
                            writer.Write(data, 0, data.Length);
                            if (size > 0)
                            {
                                monitor.Progress += (sizeof(byte) * data.Length) / size;
                            }
                            else
                            {
                                monitor.IsIndeterminate = true;
                            }

                            return true;
                        }, null);
                    }
                }
            } catch (IOException e) {
                throw e;
            }

            return DownloadedFileState.DOWNLOADED;
        }
    }
}
