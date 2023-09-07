using IEC61850.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace IEDCollector
{
    internal enum RunType
    {
        SINGLE,
        POLLING
    }

    internal enum ExecutionResult
    {
        SKIPPED,
        SUCCESS,
        PARTIAL,
        FAILED
    }

    internal class Runner
    {
        public delegate void IedFinishedCallback(IED subject, ExecutionResult status);

        private ProgressBar progress;
        private ListBox queue;
        private TextBlock status;
        private IedFinishedCallback onSingleExecutionOver;
        private readonly RunType runType;
        private List<IEDConfig> subjects;
        private readonly OnThreadOver threadOverCallback;
        private TextBlock fileName;
        private ProgressBar fileProgress;

        public RunType RunType { get => runType; }

        private Thread worker;
        public Thread Worker { get => worker; }

        private int ProgressPerIed { get => 100 / this.subjects.Count; }

        public delegate void OnThreadOver();
        /// <summary>
        /// Creates a new Runner object
        /// </summary>
        /// <param name="callback">The delegate to call when the runner completes its job (completely)</param>
        /// <param name="runType">The type of execution (RunType.SINGLE or RunType.POLLING)</param>
        /// <param name="subjects">The IEDs treated</param>
        /// <param name="progress">The "total progress" progress bar</param>
        /// <param name="fileProgress">The "file progress" progress bar</param>
        /// <param name="fileName">The textblock to put the file name into</param>
        /// <param name="queue">The actions remaining listbox (used: actionsbox)</param>
        /// <param name="status">The textblock to write the current action in</param>
        /// <param name="onSingleExecutionOver">The delegate called when an IED finishes processing</param>
        public Runner(OnThreadOver callback, RunType runType, List<IEDConfig> subjects, ProgressBar progress, ProgressBar fileProgress, TextBlock fileName, ListBox queue, TextBlock status, IedFinishedCallback onSingleExecutionOver)
        {
            this.runType = runType;
            this.progress = progress;
            this.queue = queue;
            this.status = status;
            this.onSingleExecutionOver = onSingleExecutionOver;
            this.subjects = subjects;
            this.threadOverCallback = callback;
            this.fileProgress = fileProgress;
            this.fileName = fileName;

        }

        // Updates the file progress bar
        private void updateFileProgress(double progress)
        {
            if (this.fileProgress != null)
            {
                Debug.WriteLine(progress);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (progress == -1)
                    {
                        this.fileProgress.IsIndeterminate = true;
                    }
                    else
                    {
                        this.fileProgress.IsIndeterminate = false;
                        this.fileProgress.Value = progress * 100;
                    }
                }, System.Windows.Threading.DispatcherPriority.Background);

            }
        }

        // Executes the fetch
        private void Execution()
        {


            foreach (IEDConfig subject in this.subjects)
            {
                if (!this.IsRunning) break;



                ListBoxAsQueue realQueue = new ListBoxAsQueue(this.queue);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    realQueue.addLast(new ListBoxItem() { Content = Properties.Resources.connect_to_ied });
                    realQueue.addLast(new ListBoxItem() { Content = Properties.Resources.fetch_directories });
                });

                if (!this.IsRunning) break;

                ExecutionResult success = ExecutionResult.SUCCESS;

                using (IED ied = new IED(subject))
                {

                    Application.Current.Dispatcher.Invoke(() =>
                    {

                        this.status.Text = String.Format(Properties.Resources.transferring_from_x, ied.ToString());
                        Globals.logs.log("Transferring from " + ied.ToString());
                    });
                    if (subject.includedInCollection)
                    {


                        if (!this.IsRunning) break;

                        IedClientError connectionResult = ied.connect();

                        if (connectionResult == IedClientError.IED_ERROR_OK)
                        {
                            if (!this.IsRunning) break;

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                realQueue.removeFirst();
                            });

                            if (!this.IsRunning) break;

                            List<EditableFileDirectoryEntry> tree;
                            try
                            {

                                tree = ied.ReadFileTree("");

                                string[] tryAlternatives =
                                { "/", "\\" };
                                int i = 0;

                                while (tree.Count == 0 && i < tryAlternatives.Length)
                                {
                                    tree = ied.ReadFileTree(tryAlternatives[i]);
                                    i++;
                                }

                                if (!this.IsRunning) break;

                                foreach (EditableFileDirectoryEntry entry in tree)
                                {
                                    Globals.logs.log("Found Tree file: " + entry.fileName, LogLevel.Debug);
                                }


                                double addProgress = (this.ProgressPerIed + 0.0) / (tree.Count + 0.0);

                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    realQueue.removeFirst();
                                });

                                if (!this.IsRunning) break;

                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    foreach (EditableFileDirectoryEntry entry in tree)
                                    {
                                        realQueue.addLast(new ListBoxItem() { Content = String.Format(Properties.Resources.ied_download_x, entry.GetFileName(), ied.ToString()) });
                                    }
                                });

                                if (!this.IsRunning) break;

                                foreach (EditableFileDirectoryEntry entry in tree)
                                {
                                    if (!this.IsRunning) break;

                                    updateFileProgress(0.0);

                                    try
                                    {
                                        string fixEntry = entry.fileName.Replace("/", "\\");

                                        if (fixEntry.StartsWith("\\") || fixEntry.StartsWith("/"))
                                        {
                                            fixEntry = fixEntry.Substring(1);
                                        }

                                        Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            this.fileName.Text = entry.fileName;
                                        }, System.Windows.Threading.DispatcherPriority.Background);

                                        DownloadedFileState state;
                                        string dest = Path.Combine(Globals.currentProfile.Settings.RootFolder, ied.config.logsFolder, fixEntry);
                                        try
                                        {
                                            state = ied.DownloadFile(entry, dest, new FileProgressMonitor(this.updateFileProgress), false);
                                        }
                                        catch (IOException e)
                                        {
                                            Globals.logs.log("Execution stopped due to insufficient disk space. IED Collector is stopped." + e.Message, LogLevel.Basic);
                                            MessageBox.Show(Properties.Resources.not_enough_disk_space, Properties.Resources.error, MessageBoxButton.OK, MessageBoxImage.Error);
                                            Application.Current.Shutdown();
                                            return;
                                        }


                                        string log = String.Empty;
                                        LogLevel level = LogLevel.Detailed;

                                        if (state == DownloadedFileState.SKIPPED_NEWER)
                                        {
                                            log = String.Format("[SKIP] [{1}] File '{0}' is older or equal than stored file", entry.GetFileName(), ied.ToString());
                                        }
                                        else if (state == DownloadedFileState.SKIPPED_DIRECTORY)
                                        {
                                            log = String.Format("[SKIP] [{1}] File '{0}' is a directory", entry.GetFileName(), ied.ToString());
                                        }
                                        else if (state == DownloadedFileState.DOWNLOADED)
                                        {
                                            log = String.Format("[{1}] File '{0}' downloaded to '{2}'", entry.GetFileName(), ied.ToString(), dest);
                                            level = LogLevel.Basic;
                                        }
                                        else if (state == DownloadedFileState.SKIPPED_FILTER)
                                        {
                                            log = String.Format("[SKIP] [{1}] File '{0}' skipped due to filter (folder or file extension)", entry.GetFileName(), ied.ToString());
                                        }
                                        else if (state == DownloadedFileState.SKIPPED_FREEMODE)
                                        {
                                            log = String.Format("[FREEMODE] Would have downloaded file '{0}', but the software is in free mode.", entry.GetFileName());
                                            level = LogLevel.Basic;
                                        }

                                        //Application.Current.Dispatcher.Invoke(() =>
                                        //{
                                        Globals.logs.log(log, level);
                                        //});
                                    }
                                    catch (Exception e)
                                    {
                                        success = ExecutionResult.PARTIAL;
                                        Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            Globals.logs.log(e.ToString(), LogLevel.Basic);
                                            Globals.logs.log(String.Format("Failed to download file '{0}' from IED '{1}'", entry.GetFileName(), ied.ToString()));
                                        });
                                    }

                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        realQueue.removeFirst();
                                        this.progress.Value += addProgress;
                                    });
                                }
                            }
                            catch (Exception)
                            {
                                success = ExecutionResult.FAILED;
                                Application.Current.Dispatcher.Invoke(() => { Globals.logs.log("Failed to fetch directories from IED " + ied.ToString()); realQueue.removeFirst(); });
                            }


                        }
                        else
                        {
                            success = ExecutionResult.FAILED;
                            Globals.logs.log(String.Format("[WARNING] IED '{0}' is unreachable, skipping. Error code: {1}", ied.ToString(), connectionResult.ToString()));
                        }
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            this.progress.Value += this.ProgressPerIed;
                        }, System.Windows.Threading.DispatcherPriority.Background);
                        success = ExecutionResult.SKIPPED;
                        Globals.logs.log("[SKIP] " + ied.ToString() + " because it is unchecked", LogLevel.Detailed);
                    }

                    ied.Dispose();

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Globals.logs.log("Finished processing IED " + ied.ToString());
                        this.queue.Items.Clear();
                        this.onSingleExecutionOver(ied, success);
                    }, System.Windows.Threading.DispatcherPriority.Background);
                }

            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                this.progress.IsIndeterminate = false;
                this.progress.Value = 0;
                this.queue.Items.Clear();
                this.status.Text = Properties.Resources.idling;
                this.fileName.Text = Properties.Resources.no_file_being_processed;
            });

            updateFileProgress(0.0);
        }

        private bool running = false;
        private bool idling = false;

        public bool IsRunning { get => running; }
        public bool IsIdling { get => idling; }

        // Stops the runner
        public void Dispose()
        {
            running = false;
        }

        /// <summary>
        /// Forcefully stops the worker thread
        /// </summary>
        public void Abort()
        {
            if (this.worker != null && this.worker.IsAlive) this.worker.Abort();
        }

        /// <summary>
        /// Starts the runner
        /// </summary>
        public void Start()
        {
            if (this.worker != null && this.worker.IsAlive)
            {
                return;
            }

            this.progress.Value = 0;
            this.queue.Items.Clear();
            running = true;
            idling = false;
            if (this.runType == RunType.SINGLE)
            {
                this.worker = new Thread(() =>
                {
                    this.Execution();
                    running = false;
                    Application.Current.Dispatcher.Invoke(this.threadOverCallback);
                });

                Globals.logs.log("Starting single execution");
            }
            else
            {
                int cyclePeriod = Globals.currentProfile.Settings.PollingInterval;
                this.worker = new Thread(() =>
                {
                    while (this.IsRunning)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            this.progress.IsIndeterminate = false;
                            this.progress.Value = 0;
                            this.queue.Items.Clear();
                        });
                        this.Execution();

                        string waitingString = String.Format(Properties.Resources.waiting_x_minutes, cyclePeriod.ToString().PadLeft(2, '0'), "00");
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            this.status.Text = waitingString;
                            this.progress.IsIndeterminate = true;
                        });

                        Globals.logs.log(waitingString);

                        idling = true;
                        int waited = 0;
                        int toWait = cyclePeriod * 60 * 1000;

                        DateTime finished = DateTime.Now.AddMilliseconds(toWait);
                        while (DateTime.Now.Ticks < finished.Ticks && this.IsRunning) // ToDo : change this with user setting value
                        {
                            Thread.Sleep(100);
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                int secondsMissing = (int)((finished.Subtract(DateTime.Now).TotalMilliseconds / 1000));
                                int mins = (int)Math.Floor((secondsMissing + 0.0) / 60.0);
                                int secs = secondsMissing % 60;
                                waitingString = String.Format(Properties.Resources.waiting_x_minutes, mins.ToString().PadLeft(2, '0'), secs.ToString().PadLeft(2, '0'));
                                this.status.Text = waitingString;
                            }, System.Windows.Threading.DispatcherPriority.Background);
                            waited += 100;
                        }
                        idling = false;
                    }



                    Application.Current.Dispatcher.Invoke(() =>
                     {
                         this.status.Text = Properties.Resources.idling;
                     });

                    Application.Current.Dispatcher.Invoke(this.threadOverCallback);
                });

                Globals.logs.log("Starting polling execution");
            }

            this.worker.Start();
        }
    }
}
