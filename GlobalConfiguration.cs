using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Xml.Linq;

namespace IEDCollector
{
    static class GlobalConfigurationCategories
    {
        public const string USERCONFIGFOLDERS = "userConfigFolders";
    }

    /// <summary>
    /// Handles the file containing the references to user specific configration folders.
    /// </summary>
    class GlobalConfiguration
    {
        private const string GLOBALCONFIGNAME = "config.xml";

        public string Folder
        {
            get
            {
                return this.getCurrentConfigFolder();
            }
        }

        private readonly string filePath;

        private Dictionary<string, Dictionary<string, string>> tree = new Dictionary<string, Dictionary<string, string>>()
        {
            { GlobalConfigurationCategories.USERCONFIGFOLDERS, new Dictionary<string, string>() { } }
        };

        public GlobalConfiguration()
        {
            string currentPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), Globals.FOLDERSNAME);

            Directory.CreateDirectory(currentPath); // No need to check if it exists, it will ignore it.
            this.filePath = Path.Combine(currentPath, GLOBALCONFIGNAME);

            if (File.Exists(filePath))
            {
                load();
                if (this.tree[GlobalConfigurationCategories.USERCONFIGFOLDERS].Count == 0 || !this.tree[GlobalConfigurationCategories.USERCONFIGFOLDERS].ContainsKey(Environment.UserName))
                {
                    addUsernameToFolders();
                    save();
                }
            }
            else
            {
                addUsernameToFolders();
                save();
            }


        }

        // Adds the current user to the file with the default path
        private void addUsernameToFolders() => this.tree[GlobalConfigurationCategories.USERCONFIGFOLDERS].Add(Environment.UserName, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Globals.FOLDERSNAME));

        /// <summary>
        /// Sets the new user specific config folder
        /// </summary>
        /// <param name="newFolder">The new configuration folder.</param>
        public void setConfigFolder(string newFolder)
        {
            this.tree[GlobalConfigurationCategories.USERCONFIGFOLDERS][string.Format("{0}", Environment.UserName)] = newFolder;
        }

        /// <summary>
        /// Saves the current global configuration to the file
        /// </summary>
        public void save()
        {
            XDocument globalConfiguration = new XDocument(new XElement("root"));

            foreach (KeyValuePair<string, Dictionary<string, string>> pair in this.tree)
            {
                Dictionary<string, string> configCategory = pair.Value;
                XElement category = new XElement(pair.Key);

                foreach (KeyValuePair<string, string> subElementsPair in configCategory)
                {
                    category.Add(new XElement(subElementsPair.Key, subElementsPair.Value));
                }

                globalConfiguration.Root.Add(category);
            }

            globalConfiguration.Save(filePath);
        }

        /// <summary>
        /// Loads the global configuration from the file
        /// </summary>
        private void load()
        {
            if (!File.Exists(filePath))
            {
                Globals.logs.log("Config file does not exist, throwing exception");
                throw new FileNotFoundException("The Config file does not exist");
            };

            XDocument config = null;

            try
            {
                config = XDocument.Load(filePath);
            }
            catch (Exception)
            {
                resetConfigAndSaveBackup();
            }

            if (config != null)
            {
                Globals.logs.log(String.Format("Loaded global config '{0}'", this.filePath));
                Globals.logs.log("Extracting global config properties");

                Dictionary<string, Dictionary<string, string>> futureTree = new Dictionary<string, Dictionary<string, string>>();

                foreach (KeyValuePair<string, Dictionary<string, string>> categories in this.tree)
                {

                    XElement category = config.Root.Element(categories.Key);
                    futureTree[categories.Key] = new Dictionary<string, string>();

                    foreach (XElement child in category.Elements())
                    {
                        futureTree[categories.Key][child.Name.ToString()] = child.Value;
                    }
                }

                this.tree = futureTree;
            }

        }

        /// <summary>
        /// Set a value in the config file
        /// </summary>
        /// <param name="category">The category to write into</param>
        /// <param name="key">The key to write</param>
        /// <param name="value">The value to write</param>
        public void set(string category, string key, string value)
        {
            if (!tree.ContainsKey(category)) throw new ArgumentException("The category does not exist");
            if (!tree[category].ContainsKey(key)) throw new ArgumentException("The key is not a valid config key");
            tree[category][key] = value;

            save();
        }

        /// <summary>
        /// Retrieves a file entry
        /// </summary>
        /// <param name="category">The value category</param>
        /// <param name="key">The value key</param>
        /// <returns>The wanted value</returns>
        public string get(string category, string key)
        {
            if (!tree.ContainsKey(category)) throw new ArgumentException("The category does not exist");
            if (!tree[category].ContainsKey(key)) throw new ArgumentException("The key is not a valid config key");

            return tree[category][key];
        }

        /// <summary>
        /// Gets the current configuration folder for the current user
        /// </summary>
        /// <returns>The current configuration folder for the current user</returns>
        public string getCurrentConfigFolder()
        {
            return tree[GlobalConfigurationCategories.USERCONFIGFOLDERS][Environment.UserName];
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
                    path = filePath.Replace(GLOBALCONFIGNAME, GLOBALCONFIGNAME + "-" + c + ".bck");
                    File.Copy(filePath, path);
                    ok = true;
                }
                catch (IOException) { c++; ok = false; }
            } while (!ok);
            
            MessageBox.Show(String.Format(Properties.Resources.messagebox_global_config_not_readable, path, GLOBALCONFIGNAME), Properties.Resources.error, MessageBoxButton.OK, MessageBoxImage.Error);

            save();
        }
    }
}
