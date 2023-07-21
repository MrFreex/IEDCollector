using IEDCollector.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Xml.Linq;

namespace IEDCollector
{
    internal class IEDConfigDefaults
    {
        public static readonly string NAME = Properties.Resources.new_ied;
        public const string IP = "127.0.0.1";
        public const int PORT = 102;
        public const string USERNAME = "";
        public const string PASSWORD = "";
        public const string LOGSFOLDER = "";
        public static readonly Dictionary<string, bool> LOGENABLEDFOLDERS = new Dictionary<string, bool>();
        public static readonly Dictionary<string, bool> LOGENABLEDEXTENSIONS = new Dictionary<string, bool>();
    }

    internal class IEDConfig
    {
        public string name = string.Copy(IEDConfigDefaults.NAME);
        public string ip = string.Copy(IEDConfigDefaults.IP);
        public string username;
        public string password;
        public int port;
        public string logsFolder;
        public Dictionary<string, bool> logEnabledFolders;
        public Dictionary<string, bool> logEnabledExtensions;
        public bool includedInCollection = true;
        public string protocol = "IEC61850";

        public IEDConfig()
        {

        }

        public IEDConfig(IEDConfig toClone)
        {
            this.name = toClone.name;
            this.ip = toClone.ip;
            this.username = toClone.username;
            this.password = toClone.password;
            this.port = toClone.port;
            this.logsFolder = toClone.logsFolder;
            this.logEnabledFolders = new Dictionary<string, bool>(toClone.logEnabledFolders);
            this.logEnabledExtensions = new Dictionary<string, bool>(toClone.logEnabledExtensions);
            this.protocol = toClone.protocol;
        }

        public IEDConfig(string name, string ip, string username, string password, int port, string logsFolder, Dictionary<string, bool> logEnabledFolders, Dictionary<string, bool> logEnabledExtensions)
        {
            this.name = name;
            this.ip = ip;
            this.username = username;
            this.password = password;
            this.port = port;
            this.logsFolder = logsFolder;
            this.logEnabledFolders = logEnabledFolders;
            this.logEnabledExtensions = logEnabledExtensions;

        }

        public override string ToString()
        {
            return String.Format("IEDConfig: {0} {1} {2} {3} {4} {5} {6} {7}", this.name, this.ip, this.username, this.password, this.port, this.logsFolder, this.logEnabledFolders, this.logEnabledExtensions);
        }
    }

    internal class ProfileSettings
    {
        private string rootFolder;
        public string RootFolder
        {
            get => this.rootFolder; set => this.rootFolder = value;
        }

        private int pollingInterval;
        public int PollingInterval
        {
            get => this.pollingInterval; set => this.pollingInterval = value;
        }
    }

    internal class Profile
    {
        public const string PROFILEEXTENSION = ".iedcprofile";
        private string folderPath => ConfigFolder.extend(ConfigFolder.PROFILES);
        private string profileName;

        private bool hasUnsavedChanges = false;
        public bool HasUnsavedChanges => this.hasUnsavedChanges;

        public string Name => this.profileName;
        public string FileName => this.profileName + PROFILEEXTENSION;
        public string FilePath => Path.Combine(this.folderPath, this.profileName + PROFILEEXTENSION);

        private readonly List<IEDConfig> ieds = new List<IEDConfig>();
        private readonly ProfileSettings settings;

        private readonly List<IedsChangedHandler> iedsChangedHandlers = new List<IedsChangedHandler>();

        public IedsChangedHandler IedsChanged
        {
            set
            {
                value();
                this.iedsChangedHandlers.Add(value);
            }
        }

        public ProfileSettings Settings
        {
            get
            {
                return this.settings;
            }
        }

        public List<IEDConfig> IEDs
        {
            get
            {
                this.hasUnsavedChanges = true;
                return this.ieds;
            }
        }

        public Profile(string profileName)
        {
            if (profileName.EndsWith(PROFILEEXTENSION))
            {
                profileName = profileName.Substring(0, profileName.Length - PROFILEEXTENSION.Length);
            }

            this.profileName = profileName;



            this.settings = new ProfileSettings()
            {
                PollingInterval = 10,
                RootFolder = Path.Combine(ConfigFolder.extend(ConfigFolder.IEDLOGSROOT), this.Name)
            };

            if (File.Exists(this.FilePath))
            {
                try
                {
                    this.load();
                }
                catch (IOException)
                {
                    this.resetConfigAndSaveBackup();
                }

            }
            else
            {
                this.save();
            }
        }

        public delegate void IedsChangedHandler();

        public void load()
        {
            Globals.logs.log("Loading profile " + this.profileName + " from disk");
            XDocument profileXml;

            try
            {
                profileXml = XDocument.Load(this.FilePath);
            }
            catch (Exception)
            {
                Globals.logs.log(String.Format("Error while reading profile {0}", this.profileName));
                throw new IOException("Error reading file");
            }

            XElement XSettings = profileXml.Root.Element("settings");

            this.settings.PollingInterval = int.Parse(XSettings.Attribute("pollingInterval").Value);
            this.settings.RootFolder = (XSettings.Attribute("rootFolder").Value);

            foreach (XElement XIed in profileXml.Root.Element("ieds").Elements())
            {
                IEDConfig iEDConfig = new IEDConfig();

                iEDConfig.name = XIed.Attribute("name").Value;
                iEDConfig.ip = XIed.Attribute("ip").Value;
                iEDConfig.username = XIed.Attribute("username").Value;
                iEDConfig.password = XIed.Attribute("password").Value;
                iEDConfig.port = int.Parse(XIed.Attribute("port").Value);
                iEDConfig.logsFolder = XIed.Attribute("logsFolder").Value;
                iEDConfig.logEnabledFolders = decodeDict(XIed.Attribute("logEnabledFolders").Value);
                iEDConfig.logEnabledExtensions = decodeDict(XIed.Attribute("logEnabledExtensions").Value);
                iEDConfig.includedInCollection = bool.Parse(XIed.Attribute("includedInCollection") != null ? XIed.Attribute("includedInCollection").Value : "true");
                iEDConfig.protocol = XIed.Attribute("protocol") != null ? XIed.Attribute("protocol").Value : "IEC61850";

                this.ieds.Add(iEDConfig);
            }

            Globals.logs.log("Loaded profile " + this.profileName + " from disk");
        }

        public void rename(string newName)
        {
            if (File.Exists(this.FilePath))
            {
                File.Move(this.FilePath, Path.Combine(this.folderPath, newName + PROFILEEXTENSION));
            }
            Globals.logs.log(String.Format("Renaming profile {0} into {1}", this.profileName, newName));
            this.profileName = newName;
            this.save();
            Globals.logs.log("Profile renamed");
        }

        public void save()
        {
            Globals.logs.log("Saving profile " + this.profileName);
            XDocument XConfig = new XDocument(new XElement("profile", new XElement("settings"), new XElement("ieds")));

            XElement XSettings = XConfig.Root.Element("settings");

            if (!Directory.Exists(this.settings.RootFolder))
            {
                Directory.CreateDirectory(this.settings.RootFolder);
            }

            XSettings.Add(new XAttribute("rootFolder", this.settings.RootFolder));
            XSettings.Add(new XAttribute("pollingInterval", this.settings.PollingInterval));

            XElement XIeds = XConfig.Root.Element("ieds");

            foreach (IEDConfig iEDConfig in this.ieds)
            {
                XElement XIed = new XElement("ied");

                XIed.Add(new XAttribute("name", iEDConfig.name));
                XIed.Add(new XAttribute("ip", iEDConfig.ip));
                XIed.Add(new XAttribute("username", iEDConfig.username));
                XIed.Add(new XAttribute("password", iEDConfig.password));
                XIed.Add(new XAttribute("port", iEDConfig.port));
                XIed.Add(new XAttribute("logsFolder", iEDConfig.logsFolder));
                XIed.Add(new XAttribute("logEnabledFolders", encodeDict(iEDConfig.logEnabledFolders)));
                XIed.Add(new XAttribute("logEnabledExtensions", encodeDict(iEDConfig.logEnabledExtensions)));
                XIed.Add(new XAttribute("includedInCollection", iEDConfig.includedInCollection.ToString()));
                XIed.Add(new XAttribute("protocol", iEDConfig.protocol));

                XIeds.Add(XIed);
            }

            try
            {
                XConfig.Save(this.FilePath);
                this.hasUnsavedChanges = false;
                Globals.logs.log("Saved profile " + this.profileName);
                foreach (IedsChangedHandler handler in this.iedsChangedHandlers)
                {
                    try { handler(); } catch (Exception) { }
                }
            }
            catch (Exception e)
            {
                Globals.logs.log(String.Format("Error while saving profile {0} {1}", this.profileName, e.ToString()));
                MessageBox.Show(Resources.messagebox_error_saving_profile, Resources.error, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Dictionary<string, bool> decodeDict(string encodedDict)
        {
            Dictionary<string, bool> output = new Dictionary<string, bool>();

            if (encodedDict.Contains(","))
            {
                foreach (string pair in encodedDict.Split(','))
                {
                    string[] splittedPair = pair.Split(':');
                    output.Add(splittedPair[0], bool.Parse(splittedPair[1]));
                }
            }
            else if (encodedDict.Length > 0)
            {
                string[] splittedPair = encodedDict.Split(':');
                output.Add(splittedPair[0], bool.Parse(splittedPair[1]));
            }

            return output;
        }

        private string encodeDict(Dictionary<string, bool> dict)
        {
            string output = String.Empty;

            foreach (KeyValuePair<string, bool> pair in dict)
            {
                output += pair.Key + ":" + pair.Value + ",";
            }

            output = output.Length > 0 ? output.Substring(0, output.Length - 1) : output; // Remove last ,

            return output;
        }

        private void resetConfigAndSaveBackup()
        {
            bool ok = true;
            string path = String.Empty;
            int c = 0;

            do
            {
                try
                {
                    path = this.FilePath.Replace(this.FileName, this.FileName + "-" + c + ".bck");
                    File.Copy(this.FilePath, path);
                    ok = true;
                }
                catch (IOException) { c++; ok = false; }
            } while (!ok);
            
            MessageBox.Show(Resources.messagebox_error_reading_profile_backup, Resources.error, MessageBoxButton.OK, MessageBoxImage.Error);

            this.save();
        }

        public bool delete()
        {
            Globals.logs.log("Deleting profile " + this.profileName);
            try
            {
                File.Delete(this.FilePath);
            }
            catch (Exception)
            {
                Globals.logs.log("Error deleting profile");
                return false;
            }

            Globals.logs.log("Profile deleted");
            return true;
        }
    }
}
