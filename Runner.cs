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

        private void Execution()
        {


            foreach (IEDConfig subject in this.subjects)
            {
                if (!this.IsRunning) break;



                ListBoxAsQueue realQueue = new ListBoxAsQueue(this.queue);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    realQueue.addLast(new ListBoxItem() { Content = "Connect to IED" });
                    realQueue.addLast(new ListBoxItem() { Content = "Fetch directories" });
                });

                if (!this.IsRunning) break;

                ExecutionResult success = ExecutionResult.SUCCESS;

                using (IED ied = new IED(subject))
                {

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        this.status.Text = "Transferring from " + ied.ToString();
                        Globals.logs.log("Transferring from " + ied.ToString());
                    });
                    if (subject.includedInCollection)
                    {


                        if (!this.IsRunning) break;



                        if (ied.connect())
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
                                /*
                                foreach (EditableFileDirectoryEntry entry in tree)
                                {
                                    Globals.logs.log("[DEBUG] Tree file: " + entry.fileName);
                                }
                                */

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
                                        realQueue.addLast(new ListBoxItem() { Content = String.Format("[{1}] Download file '{0}'", entry.GetFileName(), ied.ToString()) });
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

                                        DownloadedFileState state = ied.DownloadFile(entry, Path.Combine(Globals.currentProfile.Settings.RootFolder, ied.config.logsFolder, fixEntry), new FileProgressMonitor(this.updateFileProgress), false);

                                        string log = String.Empty;

                                        if (state == DownloadedFileState.SKIPPED_NEWER)
                                        {
                                            log = String.Format("[SKIP] [{1}] File '{0}' is newer or equal locally", entry.GetFileName(), ied.ToString());
                                        }
                                        else if (state == DownloadedFileState.SKIPPED_DIRECTORY)
                                        {
                                            log = String.Format("[SKIP] [{1}] File '{0}' is a directory", entry.GetFileName(), ied.ToString());
                                        }
                                        else if (state == DownloadedFileState.DOWNLOADED)
                                        {
                                            log = String.Format("[{1}] File '{0}' downloaded", entry.GetFileName(), ied.ToString());
                                        }
                                        else if (state == DownloadedFileState.SKIPPED_FILTER)
                                        {
                                            log = String.Format("[SKIP] [{1}] File '{0}' skipped due to filter (folder or file extension)", entry.GetFileName(), ied.ToString());
                                        }

                                        Application.Current.Dispatcher.Invoke(() =>
                                        {
                                            Globals.logs.log(log);
                                        });
                                    }
                                    catch (Exception)
                                    {
                                        success = ExecutionResult.PARTIAL;
                                        Application.Current.Dispatcher.Invoke(() =>
                                        {
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
                            Globals.logs.log(String.Format("[WARNING] IED '{0}' is unreachable, skipping.", ied.ToString()));
                        }
                    }
                    else
                    {
                        success = ExecutionResult.SKIPPED;
                        Globals.logs.log("[SKIP] " + ied.ToString() + " because it is unchecked");
                    }



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
                this.status.Text = "Idling";
                this.fileName.Text = "No file being processed";
            });

            updateFileProgress(0.0);
        }

        private bool running = false;
        private bool idling = false;

        public bool IsRunning { get => running; }
        public bool IsIdling { get => idling; }

        public void Dispose()
        {
            running = false;
        }

        public void Abort()
        {
            if (this.worker != null && this.worker.IsAlive) this.worker.Abort();
        }

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

                        string waitingString = String.Format("Waiting {0} minute{1}", cyclePeriod, cyclePeriod != 1 ? "s" : "");
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            this.status.Text = waitingString;
                            this.progress.IsIndeterminate = true;
                        });

                        Globals.logs.log(waitingString);

                        idling = true;
                        int waited = 0;

                        while (waited < (cyclePeriod * 60 * 1000) && this.IsRunning) // ToDo : change this with user setting value
                        {
                            Thread.Sleep(100);
                            waited += 100;
                        }
                        idling = false;
                    }



                    Application.Current.Dispatcher.Invoke(() =>
                     {
                         this.status.Text = "Idling";
                     });

                    Application.Current.Dispatcher.Invoke(this.threadOverCallback);
                });

                Globals.logs.log("Starting polling execution");
            }

            this.worker.Start();
        }
    }
}
