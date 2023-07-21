using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace IEDCollector
{
    

    // Cycle period, log files kept (number or all [-1]), start with windows, data folder location?
    internal class FSyncConfiguration
    {
        public int logFilesKept;
        public LogLevel logLevel;
        public bool startWithWindows;
        public bool resumePollingOnStartup;

        public const string LOGFILESKEPT = "logFilesKept";
        public const string LOGLEVEL = "logLevel";
        public const string STARTWITHWINDOWS = "startWithWindows";
        public const string RESUMEPOLLINGONSTARTUP = "resumePollingOnStartup";
    }

    internal class FSyncPreferences
    {

    }

    internal class UserConfig
    {
        public const string CONFIGFILENAME = "options.xml";

        private string filePath;

        public FSyncConfiguration config;
        public FSyncPreferences preferences;

        public OnLoad onLoadCallback;

        public delegate void OnLoad(FSyncConfiguration config, FSyncPreferences preferences);

        public UserConfig(string folderPath, OnLoad callback)
        {
            this.filePath = Path.Combine(folderPath, CONFIGFILENAME);
            this.onLoadCallback = callback;

            if (!File.Exists(this.filePath))
            {
                
                save();
            }

            load();

            this.onLoadCallback(this.config, this.preferences);
        }

        public void save()
        {
            FSyncConfiguration config = this.config != null ? this.config : new FSyncConfiguration()
            {
                logFilesKept = 20,
                startWithWindows = false,
                resumePollingOnStartup = false,
                logLevel = LogLevel.Basic
            };
            /*
            FSyncPreferences preferences = this.preferences != null ? this.preferences : new FSyncPreferences()
            {
                language = Language.English
            };
            */
            using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true))
            {
                if (config.startWithWindows)
                {
                    key.SetValue("IEDCollector", System.Reflection.Assembly.GetExecutingAssembly().Location);
                }
                else
                {
                    key.SetValue("IEDCollector", "");
                }
            }

            XDocument Xconfig = new XDocument(new XElement("root", new XElement("configuration", new XElement(FSyncConfiguration.LOGFILESKEPT, config.logFilesKept), new XElement(FSyncConfiguration.LOGLEVEL, ((int)config.logLevel)), new XElement(FSyncConfiguration.STARTWITHWINDOWS, config.startWithWindows), new XElement(FSyncConfiguration.RESUMEPOLLINGONSTARTUP, config.resumePollingOnStartup)), new XElement("preferences"/*, new XElement(FSyncPreferences.LANGUAGE, (int)preferences.language)*/)));

            try
            {
                Xconfig.Save(this.filePath);
                
            }
            catch { }
        }

        public void load()
        {
            if (!File.Exists(this.filePath))
            {
                return;
            }

            try
            {
                XDocument Xconfig = XDocument.Load(this.filePath);

                XElement configurationNode = Xconfig.Root.Element("configuration");
                XElement preferencesNode = Xconfig.Root.Element("preferences");

                this.config = new FSyncConfiguration()
                {
                    logFilesKept = int.Parse(configurationNode.Element(FSyncConfiguration.LOGFILESKEPT).Value),
                    logLevel = (LogLevel)int.Parse(configurationNode.Element(FSyncConfiguration.LOGLEVEL).Value),
                    startWithWindows = bool.Parse(configurationNode.Element(FSyncConfiguration.STARTWITHWINDOWS).Value),
                    resumePollingOnStartup = bool.Parse(configurationNode.Element(FSyncConfiguration.RESUMEPOLLINGONSTARTUP).Value)
                };
                /*
                this.preferences = new FSyncPreferences()
                {
                    language = (Language)(int.Parse(preferencesNode.Element(FSyncPreferences.LANGUAGE).Value))
                };
                */
            }
            catch (Exception)
            {
                File.Copy(this.filePath, this.filePath + ".bak", true);
                save();
                load();
            }

            
        }
    }
}
