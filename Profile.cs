using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;

namespace FSync
{
    internal class IEDConfigDefaults
    {
        public const string NAME = "New IED";
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

        public IEDConfig() { 
            
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
        }

        public IEDConfig(string name, string ip, string username, string password, int port, string logsFolder, Dictionary<string, bool> logEnabledFolders, Dictionary<string, bool> logEnabledExtensions) { 
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

    internal class Profile
    {
        public const string PROFILEEXTENSION = ".alfp";
        private string folderPath => ConfigFolder.extend(ConfigFolder.PROFILES);
        private string profileName;

        private bool hasUnsavedChanges = false;
        public bool HasUnsavedChanges => this.hasUnsavedChanges;

        public string Name => this.profileName;
        public string FileName => this.profileName + PROFILEEXTENSION;
        public string FilePath => Path.Combine(this.folderPath, this.profileName + PROFILEEXTENSION);

        private readonly List<IEDConfig> ieds = new List<IEDConfig>();

        public List<IEDConfig> IEDs { get
            {
                this.hasUnsavedChanges = true;
                return this.ieds;
            } }

        public Profile(string profileName) { 
            if (profileName.EndsWith(PROFILEEXTENSION))
            {
                profileName = profileName.Substring(0, profileName.Length - PROFILEEXTENSION.Length);
            }

            this.profileName = profileName;

            if (File.Exists(this.FilePath))
            {
                try
                {
                    this.load();
                } catch(IOException)
                {
                    this.resetConfigAndSaveBackup();
                }
                
            } else
            {
                this.save();
            }
        }

        public void load()
        {
            Globals.logs.log("Loading profile " + this.profileName + " from disk");
            XDocument profileXml;
            
            try
            {
                profileXml = XDocument.Load(this.FilePath);
            } catch (Exception)
            {
                Globals.logs.log(String.Format("Error while reading profile {0}", this.profileName));
                throw new IOException("Error reading file");
            }

            foreach (XElement XIed in profileXml.Root.Elements())
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
            XDocument XConfig = new XDocument(new XElement("ieds"));

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

                XConfig.Root.Add(XIed);
            }

            try
            {
                XConfig.Save(this.FilePath);
                this.hasUnsavedChanges = false;
                Globals.logs.log("Saved profile " + this.profileName);
            } catch (Exception e)
            {
                Globals.logs.log(String.Format("Error while saving profile {0} {1}", this.profileName, e.ToString()));
                MessageBox.Show("Error while saving profile", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Dictionary<string,bool> decodeDict(string encodedDict)
        {
            Dictionary<string,bool> output = new Dictionary<string, bool>();

            if (encodedDict.Contains(","))
            {
                foreach (string pair in encodedDict.Split(','))
                {
                    Globals.logs.log("Decoding pair " + pair);
                    string[] splittedPair = pair.Split(':');
                    //output.Add(splittedPair[0], bool.Parse(splittedPair[1]));
                }
            }

            return output;
        }

        private string encodeDict(Dictionary<string, bool> dict)
        {
            string output = String.Empty;

            foreach (KeyValuePair<string,bool> pair in dict)
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

            MessageBox.Show("The profile '" + this.profileName + "' file was not readable, a backup was saved and the configuration was reset. To restore it, fix the errors inside the '" + path + "' file, close the software and rename the file to '" + this.FileName + "'.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            this.save();
        }

        public bool delete()
        {
            Globals.logs.log("Deleting profile " + this.profileName);
            try
            {
                File.Delete(this.FilePath);
            } catch(Exception)
            {
                Globals.logs.log("Error deleting profile");
                return false;
            }
            
            Globals.logs.log("Profile deleted");
            return true;
        }
    }
}
