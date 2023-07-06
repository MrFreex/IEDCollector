using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using IEC61850.Client;
using Ookii.Dialogs.Wpf;
using System.IO;
using System.Xml.Linq;
using System.Runtime.CompilerServices;

namespace FSync
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    static class Globals
    {
        public const string FOLDERSNAME = "AutoFetcher";
        
        public static Logs logs;
        public static GlobalConfiguration globalConfiguration;
    }

    static class ConfigFolder {

        private static string folderPath;

        public static string Path { get { return folderPath; } set { setFolderPath(value); } }
        
        public const string LOGS = "logs";
        public const string LOCALES = "locales";
        public const string PROFILES = "profiles";

        public static string extend(string constant)
        {
            return System.IO.Path.Combine(folderPath, constant);
        } 

        private static void setFolderPath(string folderPath)
        {
            ConfigFolder.folderPath = folderPath;
        }
    }

    class FileData
    {
        private string fileName;

        public FileData(string fileName) { this.fileName = fileName; }

        public string getFileName() { return fileName; }
    }

    static class GlobalConfigurationCategories
    {
        public const string USERCONFIGFOLDERS = "userConfigFolders";
    }

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

        public GlobalConfiguration() {
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
            } else
            {
                addUsernameToFolders();
                save();
            }


        }

        private void addUsernameToFolders() => this.tree[GlobalConfigurationCategories.USERCONFIGFOLDERS].Add(Environment.UserName, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Globals.FOLDERSNAME));

        private void save()
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

        private void load()
        {
            if (!File.Exists(filePath)) {
                Globals.logs.log("Config file does not exist, throwing exception");
                throw new FileNotFoundException("The Config file does not exist");
            };

            XDocument config = null;

            try
            {
                config = XDocument.Load(filePath);
            } catch (Exception ex)
            {
                resetConfigAndSaveBackup();
            }

            if (config != null)
            {
                Globals.logs.log(String.Format("Loaded global config '{0}'", this.filePath));
                Globals.logs.log("Extracting global config properties");

                Dictionary<string, Dictionary<string, string>> futureTree = new Dictionary<string, Dictionary<string, string>>();

                foreach (KeyValuePair<string, Dictionary<string,string>> categories in this.tree)
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

        public void set(string category, string key, string value)
        {
            if (!tree.ContainsKey(category)) throw new ArgumentException("The category does not exist");
            if (!tree[category].ContainsKey(key)) throw new ArgumentException("The key is not a valid config key");
            tree[category][key] = value;

            save();
        }

        public string get(string category, string key) {
            if (!tree.ContainsKey(category)) throw new ArgumentException("The category does not exist");
            if (!tree[category].ContainsKey(key)) throw new ArgumentException("The key is not a valid config key");

            return tree[category][key];
        }

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

            MessageBox.Show("The config file was not readable, a backup was saved and the configuration was reset. To restore it, fix the errors inside the '" + path + "' file, close the software and rename the file to '" + GLOBALCONFIGNAME + "'.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);

            save();
        }
    }

    public partial class MainWindow : Window
    {
        


        private static readonly string[] NEEDEDFOLDERS =
        {
            ConfigFolder.LOGS,
            ConfigFolder.LOCALES,
            ConfigFolder.PROFILES
        };
        public MainWindow()
        {
            
            InitializeComponent();
            Globals.logs = new Logs(new List<TextBox>()
            {
                this.Logs
            });
            Globals.logs.log("Software started");
            Globals.globalConfiguration = new GlobalConfiguration();
            ConfigFolder.Path = Globals.globalConfiguration.getCurrentConfigFolder();
            Globals.logs.setFolder(ConfigFolder.extend(ConfigFolder.LOGS));

            
            initializeFolders(Globals.globalConfiguration.Folder);
        }

        public static bool receiveF(object param, byte[] data)
        {
            if (param is FileData)
            {
                FileData fd = (FileData)param;
                Debug.WriteLine(fd.getFileName());
            }
            return true;
        }

        private void initializeFolders(string path)
        {
            foreach (string folder in NEEDEDFOLDERS)
            {
                string combined = Path.Combine(path, folder);
                if (!Directory.Exists(combined))
                {
                    Directory.CreateDirectory(combined);
                }
            }
        }

        private void TestConnection()

        {   
            var connection = new IedConnection();
            connection.Connect("10.1.21.201", 102);

            List<string> devices = connection.GetServerDirectory();

            foreach (string device in devices)
            {
                Debug.WriteLine(device);
            }

            /*

            FilesDownloader f = new FilesDownloader("/", (List<string> files) => {
                Debug.WriteLine("Thread Over");
                connection.Abort();
            }, connection);
            */
            
        }

        private void iedLogFolderBrowse_Click(object sender, RoutedEventArgs e)
        {
            VistaFolderBrowserDialog dialog = new VistaFolderBrowserDialog();
            dialog.Description = "Select the folder where the logs will be saved";
            dialog.ShowNewFolderButton = true;
            Nullable<bool> result = dialog.ShowDialog();
            
            if (result == true)
            {
                this.iedLogFolderInput.Text = dialog.SelectedPath;
            }
        }

        private void openPreferences(object sender, RoutedEventArgs e)
        {
            Preferences preferences = new Preferences();
            preferences.Show();
        }

        private void openConfiguration(object sender, RoutedEventArgs e)
        {
            //TODO: Add logged in check

            Configuration configuration = new Configuration();
            configuration.Show();
        }

        private void openAbout(object sender, RoutedEventArgs e)
        {
            About about = new About();
            about.Show();
        }
    }
}
