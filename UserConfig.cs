using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace FSync
{
    internal class FSyncPreferences
    {

    }

    // Cycle period, log files kept (number or all [-1]), start with windows, data folder location?
    internal class FSyncConfiguration
    {
        public int cyclePeriod;
        public int logFilesKept;
        public bool startWithWindows;
        public bool resumePollingOnStartup;

        public const string CYCLEPERIOD = "cyclePeriod";
        public const string LOGFILESKEPT = "logFilesKept";
        public const string STARTWITHWINDOWS = "startWithWindows";
        public const string RESUMEPOLLINGONSTARTUP = "resumePollingOnStartup";
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

            if (File.Exists(this.filePath))
            {
                load();
            } else
            {
                save();
            }

            this.onLoadCallback(this.config, this.preferences);
        }

        public void save()
        {
            FSyncConfiguration config = this.config != null ? this.config : new FSyncConfiguration()
            {
                cyclePeriod = 1,
                logFilesKept = 20,
                startWithWindows = false,
                resumePollingOnStartup = false
            };

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

            XDocument Xconfig = new XDocument(new XElement("root", new XElement("configuration", new XElement(FSyncConfiguration.CYCLEPERIOD, config.cyclePeriod), new XElement(FSyncConfiguration.LOGFILESKEPT, config.logFilesKept), new XElement(FSyncConfiguration.STARTWITHWINDOWS, config.startWithWindows), new XElement(FSyncConfiguration.RESUMEPOLLINGONSTARTUP, config.resumePollingOnStartup)), new XElement("preferences")));
        
            try
            {
                Xconfig.Save(this.filePath);
            } catch { }
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

                this.config = new FSyncConfiguration()
                {
                    cyclePeriod = int.Parse(configurationNode.Element(FSyncConfiguration.CYCLEPERIOD).Value),
                    logFilesKept = int.Parse(configurationNode.Element(FSyncConfiguration.LOGFILESKEPT).Value),
                    startWithWindows = bool.Parse(configurationNode.Element(FSyncConfiguration.STARTWITHWINDOWS).Value),
                    resumePollingOnStartup = bool.Parse(configurationNode.Element(FSyncConfiguration.RESUMEPOLLINGONSTARTUP).Value)
                };
            } catch (Exception)
            {
                File.Copy(this.filePath, this.filePath + ".bak", true);
                save();
            }
        }
    }
}
