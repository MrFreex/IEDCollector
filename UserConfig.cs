using System;
using System.IO;
using System.Xml.Linq;

namespace IEDCollector
{


    // log files kept (number or all [-1]), start with windows, resume polling on startup, log level
    internal class FSyncConfiguration
    {
        public int logFilesKept;
        public LogLevel logLevel;
        public bool startWithWindows;
        public bool minimizeToTray;
        public bool resumePollingOnStartup;
        public int fetchAttempts;

        public const string LOGFILESKEPT = "logFilesKept";
        public const string LOGLEVEL = "logLevel";
        public const string STARTWITHWINDOWS = "startWithWindows";
        public const string RESUMEPOLLINGONSTARTUP = "resumePollingOnStartup";
        public const string MINIMIZETOTRAY = "minimizeToTray";
        public const string FETCHATTEMPTS = "fetchAttempts";
    }

    internal class FSyncPreferences // Unused, but kept for future use
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

        /// <summary>
        /// Initializes a new UserConfig object, representing the user specific configuration.
        /// </summary>
        /// <param name="folderPath">The user data path where the file will be located</param>
        /// <param name="callback">A delegate called when the initialization is over</param>
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

        /// <summary>
        /// Saves the configuration to file
        /// </summary>
        public void save()
        {
            FSyncConfiguration config = this.config != null ? this.config : new FSyncConfiguration()
            {
                logFilesKept = 20,
                startWithWindows = false,
                resumePollingOnStartup = false,
                minimizeToTray = true,
                logLevel = LogLevel.Basic,
                fetchAttempts = 3
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

            XDocument Xconfig = new XDocument(new XElement("root", new XElement("configuration", new XElement(FSyncConfiguration.LOGFILESKEPT, config.logFilesKept), new XElement(FSyncConfiguration.LOGLEVEL, ((int)config.logLevel)), new XElement(FSyncConfiguration.FETCHATTEMPTS, config.fetchAttempts), new XElement(FSyncConfiguration.STARTWITHWINDOWS, config.startWithWindows), new XElement(FSyncConfiguration.RESUMEPOLLINGONSTARTUP, config.resumePollingOnStartup), new XElement(FSyncConfiguration.MINIMIZETOTRAY, config.minimizeToTray)), new XElement("preferences"/*, new XElement(FSyncPreferences.LANGUAGE, (int)preferences.language)*/)));

            try
            {
                Xconfig.Save(this.filePath);

            }
            catch { }
        }

        private string getXmlValue<T>(XElement el, string key, T defaultValue)
        {
            return el.Element(key) != null && el.Element(key).Value != null ? el.Element(key).Value : defaultValue.ToString();
        }

        /// <summary>
        /// Loads the configuration from the file
        /// </summary>
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
                    logFilesKept = int.Parse(getXmlValue<int>(configurationNode, FSyncConfiguration.LOGFILESKEPT, -1)), // int.Parse(doesXMLHaveKey(configurationNode.Element(FSyncConfiguration.LOGFILESKEPT).Value),
                    logLevel = (LogLevel)int.Parse(getXmlValue(configurationNode, FSyncConfiguration.LOGLEVEL, 0)),
                    startWithWindows = bool.Parse(getXmlValue(configurationNode, FSyncConfiguration.STARTWITHWINDOWS, false)),
                    resumePollingOnStartup = bool.Parse(getXmlValue(configurationNode, FSyncConfiguration.RESUMEPOLLINGONSTARTUP, false)),
                    minimizeToTray = bool.Parse(getXmlValue(configurationNode, FSyncConfiguration.MINIMIZETOTRAY, true)),
                    fetchAttempts = int.Parse(getXmlValue<int>(configurationNode, FSyncConfiguration.FETCHATTEMPTS, 3))
                };
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
