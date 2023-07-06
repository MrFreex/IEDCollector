using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using IEC61850.Client;
using Ookii.Dialogs.Wpf;
using System.IO;
using System.Threading;

namespace FSync
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 

    static class Globals
    {
        public const string FOLDERSNAME = "AutoFetcher";
        public static OnProfileChange profileChangeHandler = null;

        public delegate void OnProfileChange(bool renameOnly);

        public static Profile currentProfile
        {
            get { return currProfile; }
            set
            {
                currProfile = value;
                if (profileChangeHandler != null)
                {
                    profileChangeHandler(false);
                }
            }
        }

        private static Profile currProfile;

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
            initializeFolders(Globals.globalConfiguration.Folder);
            Globals.logs.setFolder(ConfigFolder.extend(ConfigFolder.LOGS));

            Globals.profileChangeHandler = (bool renameOnly) =>
            {
                this.currentProfileName.Text = Globals.currentProfile.Name;
                if (renameOnly)
                {
                    return;
                }
                iedSelector.Items.Clear();
                foreach (IEDConfig ied in Globals.currentProfile.IEDs)
                {
                    iedSelector.Items.Add(ied.name);
                }

                if (iedSelector.Items.Count > 0)
                {
                    iedSelector.IsEnabled = true;
                    iedSelector.SelectedIndex = 0;
                }
                else
                {
                    iedSelector.Items.Add(new ListBoxItem().Content = "No IEDs configured");
                    iedSelector.IsEnabled = false;
                }
            };

            var placeholder = new TreeViewItem();
            placeholder.Header = "No IED Configured";
            iedTree.Items.Add(placeholder);

            Progress.Value = 0;
            ProgressLabel.Text = "Idling";

            actionsbox.Items.Add(new ListBoxItem().Content = "No actions queued");

            // Load Configurations list

            if (iedSelector.Items.Count == 0 || iedSelector.SelectedIndex == -1)
            {
                iedSettings.Visibility = Visibility.Hidden;
            }
        }

        public static bool receiveF(object param, byte[] data)
        {
            if (param is FileData)
            {
                FileData fd = (FileData)param;
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

        // Configuration view

        private void addIed(object sender, RoutedEventArgs e)
        {

        }

        private void deleteIed(object sender, RoutedEventArgs e)
        {

        }

        private void saveIeds(object sender, RoutedEventArgs e)
        {

        }

        private void loadProfile(object sender, RoutedEventArgs e)
        {
            Ookii.Dialogs.Wpf.VistaOpenFileDialog dialog = new Ookii.Dialogs.Wpf.VistaOpenFileDialog();
            dialog.CheckFileExists = true;
            dialog.CheckPathExists = true;

            dialog.Title = "Select a profile to load";
            dialog.DefaultExt = ".xml";
            dialog.Filter = "XML Files (*.xml)|*.xml";
            Debug.WriteLine(ConfigFolder.extend(ConfigFolder.PROFILES));
            dialog.InitialDirectory = ConfigFolder.extend(ConfigFolder.PROFILES);
            dialog.ValidateNames = true;
            Nullable<bool> profileSelected = dialog.ShowDialog();

            if (profileSelected != null && (bool)profileSelected)
            {
                Globals.currentProfile = new Profile(Path.GetFileName(dialog.FileName));
                
            }
        }

        private void iedSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (iedSelector.SelectedIndex == -1 && iedSettings.Visibility.Equals(Visibility.Visible))
            {
                iedSettings.Visibility = Visibility.Hidden;
            }
            else if (iedSelector.SelectedIndex >= 0 && iedSettings.Visibility.Equals(Visibility.Hidden))
            {
                iedSettings.Visibility = Visibility.Visible;
            }
        }

        private void createProfile(object sender, RoutedEventArgs e)
        {
            if (Globals.currentProfile != null && Globals.currentProfile.HasUnsavedChanges)
            {
                TaskDialog dialog = new TaskDialog();

                dialog.WindowTitle = "Confirm profile creation";
                dialog.Content = "Are you sure you want to create a new profile? All unsaved changes will be lost.";
                dialog.MainInstruction = "Confirm profile creation";
                dialog.MainIcon = TaskDialogIcon.Warning;
                TaskDialogButton autoSave = new TaskDialogButton("Save");
                autoSave.ButtonType = ButtonType.Custom;
                dialog.Buttons.Add(autoSave);
                dialog.Buttons.Add(new TaskDialogButton(Ookii.Dialogs.Wpf.ButtonType.Yes));
                dialog.Buttons.Add(new TaskDialogButton(Ookii.Dialogs.Wpf.ButtonType.No));

                TaskDialogButton result = dialog.ShowDialog();
                if (result != null)
                {
                    if (result.Equals(autoSave))
                    {
                        Globals.currentProfile.save();
                    } else if (result.ButtonType == ButtonType.No)
                    {
                        return;
                    }
                }
            }

            Globals.logs.log("Creating new profile");

            NewProfileDialog profileDialog = new NewProfileDialog();
            Nullable<bool> res = profileDialog.ShowDialog();
           
            if ((bool)res)
            {
                Globals.currentProfile = new Profile(profileDialog.profileName);
            }
        }

        private void scrollToBottom(object sender, TextChangedEventArgs e)
        {
            Logs.ScrollToEnd();
            //this.Logs.ScrollToLine(this.Logs.LineCount - 1);
        }

        private void renameProfile(object sender, RoutedEventArgs e)
        {
            NewProfileDialog newProfileDialog = new NewProfileDialog();
            newProfileDialog.Title = "Rename profile";
            newProfileDialog.ProfileName.Text = Globals.currentProfile.Name;
            newProfileDialog.ok.Content = "Rename";

            Nullable<bool> result = newProfileDialog.ShowDialog();

            if ((bool)result)
            {
                Globals.currentProfile.rename(newProfileDialog.profileName);
                if (Globals.profileChangeHandler != null)
                {
                    Globals.profileChangeHandler(true);
                }
            }
        }
    }
}
