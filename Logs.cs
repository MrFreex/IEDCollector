using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace IEDCollector
{
    internal enum LogLevel
    {
        Basic,
        Detailed,
        Debug
    }
    internal class Logs
    {
        private static Dictionary<LogLevel, string> levelsTexts = new Dictionary<LogLevel, string>()
        {
            { LogLevel.Basic, "BASIC" },
            { LogLevel.Detailed, "DETAILED" },
            { LogLevel.Debug, "DEBUG" },
        };

        private Thread countChecker = null;

        public LogLevel logLevel => Globals.config != null ? Globals.config.config.logLevel : LogLevel.Debug;

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
        public readonly ContextMenu context;

        public void setLogLevel(object sender, RoutedEventArgs e)
        {
            
            RadioButton senderCast = (RadioButton)sender;
            if (senderCast.Tag == null) return;

            LogLevel logLevel = (LogLevel)senderCast.Tag;

            if (Globals.config.config.logLevel != logLevel)
            {
                Globals.config.config.logLevel = logLevel;
                Globals.config.save();
            }

        }

        public void updateLogLevelSelectors()
        {
            MenuItem levelSelector = (MenuItem)this.context.Items[this.context.Items.Count - 1];

            foreach (MenuItem sel in levelSelector.Items)
            {
                RadioButton r = (RadioButton)sel.Icon;
                LogLevel rep = (LogLevel)r.Tag;
                if (rep == Globals.config.config.logLevel)
                {
                    r.IsChecked = true;
                    break;
                }
            }
        }

        public Logs(List<TextBox> outputs)
        {
            ContextMenu actions = new ContextMenu();

            MenuItem openLogFile = new MenuItem()
            {
                Header = "Open log file"
            };



            MenuItem logLevels = new MenuItem()
            {
                Header = "Logging level",
                Items =
                {
                    new MenuItem() { Header = "Basic", Icon =  new RadioButton() {IsChecked = true, HorizontalAlignment = HorizontalAlignment.Center, GroupName ="LogLevel", Tag = LogLevel.Basic }},
                    new MenuItem() { Header = "Detailed", Icon =  new RadioButton() {IsChecked = false, HorizontalAlignment = HorizontalAlignment.Center, GroupName ="LogLevel", Tag = LogLevel.Detailed }},
                    new MenuItem() { Header = "Debug", Icon =  new RadioButton() {IsChecked = false, HorizontalAlignment = HorizontalAlignment.Center, GroupName ="LogLevel", Tag = LogLevel.Debug }},
                }
            };

            foreach (MenuItem item in logLevels.Items)
            {
                ((RadioButton)item.Icon).Checked += setLogLevel;
            }

            openLogFile.Click += (object sender, RoutedEventArgs e) => Process.Start(this.filePath);

            actions.Items.Add(openLogFile);
            actions.Items.Add(logLevels);

            this.context = actions;

            foreach (TextBox output in outputs)
            {
                output.IsReadOnly = true;
                output.ContextMenu = actions;
            }

            this.outputs = outputs;
        }

        /*

        public void log(List<string> lines) => log(lines, LogLevel.Basic);

        public void log(List<string> lines, LogLevel level)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                lines[i] = genLogMessage(lines[i], level);
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

        */

        public string genLogMessage(string message, LogLevel level) => string.Format("|{2}| [{0}] {1} \n", DateTime.Now.ToString(), message, levelsTexts[level]);

        public void log(string message) => log(message, LogLevel.Basic);

        public void log(string message, LogLevel level)
        {
            Debug.WriteLine(String.Format("[LOG] {0}", message));

            if (level > this.logLevel) return;

            string finalMsg = genLogMessage(message, level);

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
