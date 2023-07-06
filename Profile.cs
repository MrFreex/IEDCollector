using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;

namespace FSync
{
    internal class IEDConfig
    {
        public string name;
        public string ip;
        public string username;
        public string password;
        public int port;
        public string logsFolder;
        public List<string> logEnabledFolders;
        public List<string> logEnabledExtensions;

        public IEDConfig() { 
            
        }

        public IEDConfig(string name, string ip, string username, string password, int port, string logsFolder, List<string> logEnabledFolders, List<string> logEnabledExtensions) { 
            this.name = name;
            this.ip = ip;
            this.username = username;
            this.password = password;
            this.port = port;
            this.logsFolder = logsFolder;
            this.logEnabledFolders = logEnabledFolders;
            this.logEnabledExtensions = logEnabledExtensions;
        }
    }

    internal class Profile
    {
        private string folderPath => ConfigFolder.extend(ConfigFolder.PROFILES);
        private string profileName;

        private bool hasUnsavedChanges = false;
        public bool HasUnsavedChanges => this.hasUnsavedChanges;

        public string Name => this.profileName;
        public string FileName => this.profileName + ".xml";
        public string FilePath => Path.Combine(this.folderPath, this.profileName + ".xml");

        private readonly List<IEDConfig> ieds = new List<IEDConfig>();

        public List<IEDConfig> IEDs { get
            {
                this.hasUnsavedChanges = true;
                return this.ieds;
            } }

        public Profile(string profileName) { 
            if (profileName.EndsWith(".xml"))
            {
                profileName = profileName.Substring(0, profileName.Length - 4);
            }

            this.profileName = profileName;

            if (File.Exists(this.FilePath))
            {
                try
                {
                    this.load();
                } catch(IOException loadException)
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
            } catch (Exception ex)
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
                iEDConfig.logEnabledFolders = XIed.Attribute("logEnabledFolders").Value.Split(',').ToList();
                iEDConfig.logEnabledExtensions = XIed.Attribute("logEnabledExtensions").Value.Split(',').ToList();
            }

            Globals.logs.log("Loaded profile " + this.profileName + " from disk");
        }

        public void rename(string newName)
        {
            if (File.Exists(this.FilePath))
            {
                File.Move(this.FilePath, Path.Combine(this.folderPath, newName + ".xml"));
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
                XIed.Add(new XAttribute("logEnabledFolders", String.Join(",", iEDConfig.logEnabledFolders)));
                XIed.Add(new XAttribute("logEnabledExtensions", String.Join(",", iEDConfig.logEnabledExtensions)));

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
    }
}
