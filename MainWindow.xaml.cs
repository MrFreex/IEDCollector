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
