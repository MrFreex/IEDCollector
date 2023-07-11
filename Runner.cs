using IEC61850.Client;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace FSync
{
    internal enum RunType
    {
        SINGLE,
        POLLING
    }

    internal class Runner
    {
        public delegate void IedFinishedCallback(IED subject);

        private ProgressBar progress;
        private ListBox queue;
        private TextBlock status;
        private IedFinishedCallback onSingleExecutionOver;
        private readonly RunType runType;
        private List<IEDConfig> subjects;
        private readonly OnThreadOver threadOverCallback;

        private Thread worker;
        public Thread Worker {  get => worker; }

        private int ProgressPerIed { get => 100 / this.subjects.Count; }

        public delegate void OnThreadOver();

        public Runner(OnThreadOver callback, RunType runType, List<IEDConfig> subjects, ProgressBar progress, ListBox queue, TextBlock status, IedFinishedCallback onSingleExecutionOver)
        {
            this.runType = runType;
            this.progress = progress;
            this.queue = queue;
            this.status = status;
            this.onSingleExecutionOver = onSingleExecutionOver;
            this.subjects = subjects;
            this.threadOverCallback = callback;
        }

        private void Execution()
        {
            foreach (IEDConfig subject in this.subjects)
            {
                
                ListBoxAsQueue realQueue = new ListBoxAsQueue(this.queue);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    realQueue.addLast(new ListBoxItem() { Content = "Connect to IED" });
                    realQueue.addLast(new ListBoxItem() { Content = "Fetch directories" });
                });

                
                
                using (IED ied = new IED(subject))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        this.status.Text = "Transferring from " + ied.ToString();
                        Globals.logs.log("Transferring from " + ied.ToString());
                    }); 


                    
                    if (ied.connect())
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            realQueue.removeFirst();
                        });
                        
                        List<FileDirectoryEntry> tree;
                        try
                        {
                            
                            tree = ied.ReadFileTree("/");
                            double addProgress = (this.ProgressPerIed + 0.0) / (tree.Count + 0.0);

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                realQueue.removeFirst();
                            });

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                foreach (FileDirectoryEntry entry in tree)
                                {
                                    realQueue.addLast(new ListBoxItem() { Content = String.Format("[{1}] Download file '{0}'", entry.GetFileName(), ied.ToString()) });
                                }
                            });
                            

                            foreach (FileDirectoryEntry entry in tree)
                            {
                                try
                                {
                                    DownloadedFileState state = ied.DownloadFile(entry, Path.Combine(ied.config.logsFolder, entry.GetFileName()), false);

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
                                    } else if (state == DownloadedFileState.SKIPPED_FILTER)
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
                        } catch(Exception)
                        {
                            Application.Current.Dispatcher.Invoke(() => { Globals.logs.log("Failed to fetch directories from IED " + ied.ToString()); realQueue.removeFirst(); });
                        }


                    } else
                    {
                        Globals.logs.log(String.Format("[WARNING] IED '{0}' is unreachable, skipping.", ied.ToString()));
                    }

                    Application.Current.Dispatcher.Invoke(() => {
                        Globals.logs.log("Finished processing IED " + ied.ToString());
                        this.queue.Items.Clear();
                    });

                    this.onSingleExecutionOver(ied);
                }
                
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                this.progress.IsIndeterminate = false;
                this.progress.Value = 0;
                this.queue.Items.Clear();
                this.status.Text = "Idling";
            });
            
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
                this.worker = new Thread(() =>
                {
                    while (running)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            this.progress.IsIndeterminate = false;
                            this.progress.Value = 0;
                            this.queue.Items.Clear();
                        }); 
                        this.Execution();
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            this.status.Text = "Waiting";
                            this.progress.IsIndeterminate = true;
                        });

                        idling = true;
                        int waited = 0;
                        while (waited<10000 && running) // ToDo : change this with user setting value
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
