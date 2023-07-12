using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace FSync
{
    internal class Logs
    {
        private Thread countChecker = null;

        private string twoChars(int component)
        {
            string conv = component.ToString();

            if (conv.Length == 2) return conv;

            return "0" + conv;
        }
        private string filePath
        {
            get
            {
                if (this.folderPath == null)
                {
                    return null;
                }

                return Path.Combine(folderPath, string.Format("{0}{1}{2}.log", DateTime.Now.Year, twoChars(DateTime.Now.Month), twoChars(DateTime.Now.Day), twoChars(DateTime.Now.Hour), twoChars(DateTime.Now.Minute), twoChars(DateTime.Now.Second)));
            }
        }
        private string folderPath = null;
        public readonly List<TextBox> outputs;

        public Logs(List<TextBox> outputs)
        {

            foreach (TextBox output in outputs)
            {
                output.IsReadOnly = true;
            }

            this.outputs = outputs;
        }

        public void log(List<string> lines)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                lines[i] = string.Format("[{0}] {1} \n", DateTime.Now.ToString(), lines[i]);
            }

            string joinedText = string.Join("\n", lines);

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                foreach (TextBox output in outputs)
                {
                    output.AppendText(joinedText);

                    if (output.LineCount > 200)
                    {
                        // Remove the lines needed so the textbox will have 200 lines

                        int linesToRemove = output.LineCount - 200;

                        for (int i = 0; i < linesToRemove; i++)
                        {
                            output.Text = output.Text.Substring(output.Text.IndexOf('\n') + 1);
                        }
                    }
                }
            }), System.Windows.Threading.DispatcherPriority.Input);

            if (this.filePath == null)
            {
                return;
            }

            using (StreamWriter logStream = new StreamWriter(this.filePath, true))
            {
                logStream.Write(joinedText);
            }
        }

        public void log(string message)
        {
            string finalMsg = string.Format("[{0}] {1} \n", DateTime.Now.ToString(), message);

            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (TextBox output in outputs)
                {
                    output.AppendText(finalMsg);
                    if (output.LineCount > 200)
                    {
                        output.Text = output.Text.Substring(output.Text.IndexOf('\n') + 1);
                    }
                }
            }, System.Windows.Threading.DispatcherPriority.Background);

            if (this.filePath == null)
            {
                return;
            }

            using (StreamWriter logStream = new StreamWriter(this.filePath, true))
            {
                logStream.Write(finalMsg);
            }

        }

        public void setFolder(string folderPath)
        {
            if (this.folderPath != null) throw new InvalidOperationException("folderPath already defined");

            this.folderPath = folderPath; // Path.Combine(folderPath, string.Format("{0}-{1}-{2} {3}-{4}-{5}.log", DateTime.Now.Day, DateTime.Now.Month, DateTime.Now.Year, DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second));
            Directory.CreateDirectory(folderPath);

            if (this.countChecker == null)
            {
                this.countChecker = new Thread(() =>
                {
                    while (Globals.config == null) Thread.Sleep(1000); // Wait for config to be loaded
                    while (true)
                    {
                        int filesKept = ((Globals.config != null) ? Globals.config.config.logFilesKept : 20);
                        
                        if (filesKept > 0)
                        {
                            string[] files = Directory.GetFiles(folderPath, "*.log");
                            if (files.Length > filesKept)
                            {
                                Array.Sort(files);

                                for (int i = 0; i < files.Length - filesKept; i++)
                                {
                                    File.Delete(files[i]);
                                }
                            }
                        }

                        Thread.Sleep(1000 * 60 * 60 * 1); // 1 hour
                    }
                });

                this.countChecker.IsBackground = true;
                this.countChecker.Start();
            }

            //this.log(String.Format("Log file set: {0}", filePath));
        }
    }
}
