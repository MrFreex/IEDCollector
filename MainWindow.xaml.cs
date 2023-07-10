using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using IEC61850.Client;
using Ookii.Dialogs.Wpf;
using System.IO;
using System.Threading;
using System.Net;
using System.Diagnostics.Eventing.Reader;

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
        public const string IEDLOGSROOT = "IEDlogs";

        public const string LASTPROFILEFILE = ".lastprofile";

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
        private bool isIedSaved
        {
            get
            {
                return iedSaved;
            }

            set
            {

               if (value)
               {
                   iedName.Text = iedNameInput.Text.TrimEnd('*');
               } else
               {
                   iedName.Text = iedNameInput.Text + "*";
               }
                
                iedSaved = value;
            }
        }
        private bool iedSaved = true;
        private bool isProfileSaved
        {
            get { return profileSaved; }
            set { 
                if (Globals.currentProfile != null)
                {
                    currentProfileName.Text = value ? Globals.currentProfile.Name.TrimEnd('*') : Globals.currentProfile.Name + "*";
                }
                profileSaved = value; 
            }
        }
        private bool profileSaved = true;
        private static readonly string[] NEEDEDFOLDERS =
        {
            ConfigFolder.LOGS,
            ConfigFolder.LOCALES,
            ConfigFolder.PROFILES,
            ConfigFolder.IEDLOGSROOT
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
                if (Globals.currentProfile == null)
                {
                    this.currentProfileName.Text = "No profile";
                    iedName.Visibility = Visibility.Hidden;
                    connectIedInConf.Visibility = Visibility.Hidden;
                    saveIED.Visibility = Visibility.Hidden;
                    iedSelector.IsEnabled = false;
                    iedSelector.Items.Clear();
                    ListBoxItem add = new ListBoxItem();
                    add.Content = "Load a profile to continue";
                    iedSelector.Items.Add(add);
                    iedSettings.Visibility = Visibility.Hidden;
                    toggleIedButtonsEnabled(false);

                    return;
                } else
                {
                    saveIED.Visibility = Visibility.Visible;
                    connectIedInConf.Visibility = Visibility.Visible;
                    iedName.Visibility = Visibility.Visible;
                    toggleIedButtonsEnabled(true);
                }

                this.currentProfileName.Text = Globals.currentProfile.Name;
                if (renameOnly)
                {
                    return;
                }
                iedSelector.Items.Clear();
                foreach (IEDConfig ied in Globals.currentProfile.IEDs)
                {
                    ListBoxItem add = new ListBoxItem();
                    add.Content = ied.name;
                    iedSelector.Items.Add(add);
                }

                if (iedSelector.Items.Count > 0)
                {
                    cloneIedBtn.IsEnabled = true;
                    deleteIedBtn.IsEnabled = true;
                    iedSelector.IsEnabled = true;
                    iedSelector.SelectedIndex = 0;
                }
                else
                {
                    cloneIedBtn.IsEnabled = false;
                    deleteIedBtn.IsEnabled = false;
                    ListBoxItem add = new ListBoxItem();
                    add.Content = "No IEDs configured";
                    iedSelector.Items.Add(add);
                    iedSelector.IsEnabled = false;
                }
            };

            var placeholder = new TreeViewItem();
            placeholder.Header = "No IED Configured";
            iedTree.Items.Add(placeholder);

            Progress.Value = 0;
            ProgressLabel.Text = "Idling";

            ListBoxItem toAdd = new ListBoxItem();
            toAdd.Content = "No IEDs configured";
            actionsbox.Items.Add(toAdd);

            // Load Configurations list

            if (iedSelector.Items.Count == 0 || iedSelector.SelectedIndex == -1)
            {
                iedSettings.Visibility = Visibility.Hidden;
            }

            if (File.Exists(ConfigFolder.extend(ConfigFolder.LASTPROFILEFILE)))
            {
                string profilePath = File.ReadAllText(ConfigFolder.extend(ConfigFolder.LASTPROFILEFILE));

                if (File.Exists(profilePath))
                {
                    string fileName = Path.GetFileName(profilePath);
                    Globals.currentProfile = new Profile(fileName.Substring(0, fileName.Length - Profile.PROFILEEXTENSION.Length));
                }
            }

            Globals.profileChangeHandler(false);
        }

        private void toggleIedButtonsEnabled(bool toggle)
        {
            newIedBtn.IsEnabled = toggle;
            cloneIedBtn.IsEnabled = toggle;
            deleteIedBtn.IsEnabled = toggle;
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
            if (Globals.currentProfile == null) return;

            if (!iedSelector.IsEnabled)
            {
                iedSelector.Items.Clear();
                iedSelector.IsEnabled = true;
            }

            isProfileSaved = false;
            isIedSaved = true;

            IEDConfig ied = new IEDConfig();
            ied.name = "NewIED";
            ied.port = 102;
            ied.ip = "127.0.0.1";
            ied.logsFolder = ConfigFolder.extend(ConfigFolder.IEDLOGSROOT);
            ied.logEnabledExtensions = new Dictionary<string, bool>();
            ied.logEnabledFolders = new Dictionary<string, bool>();

            Globals.currentProfile.IEDs.Add(ied);
            ListBoxItem toAdd = new ListBoxItem();
            toAdd.Content = ied.name;
            iedSelector.Items.Add(toAdd);
            iedSelector.SelectedIndex = iedSelector.Items.Count - 1;
        }

        private void deleteIed(object sender, RoutedEventArgs e)
        {
            if (Globals.currentProfile == null || iedSelector.SelectedIndex == -1)
            {
                return;
            }

            MessageBoxResult res = MessageBox.Show("Are you sure you want to delete this IED?", "Delete IED", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (res.Equals(MessageBoxResult.Yes))
            {
                int index = iedSelector.SelectedIndex;
                if (iedSelector.Items.Count > 1)
                {
                    if (index == 0)
                    {
                        iedSelector.SelectedIndex = 1;
                    } else
                    {
                        iedSelector.SelectedIndex -= 1;
                    }
                } else
                {
                    ListBoxItem add = new ListBoxItem();
                    add.Content = "No IEDs configured";
                    iedSelector.Items.Add(add);
                    iedSelector.IsEnabled = false;
                }

                Globals.currentProfile.IEDs.RemoveAt(index);
                iedSelector.Items.RemoveAt(index);
            }
        }

        private void loadProfile(object sender, RoutedEventArgs e)
        {
            Ookii.Dialogs.Wpf.VistaOpenFileDialog dialog = new Ookii.Dialogs.Wpf.VistaOpenFileDialog();
            dialog.CheckFileExists = true;
            dialog.CheckPathExists = true;

            dialog.Title = "Select a profile to load";
            dialog.DefaultExt = Profile.PROFILEEXTENSION;
            dialog.Filter = String.Format("Alf profiles (*{0})|*{0}", Profile.PROFILEEXTENSION);
            dialog.InitialDirectory = ConfigFolder.extend(ConfigFolder.PROFILES);
            dialog.ValidateNames = true;
            Nullable<bool> profileSelected = dialog.ShowDialog();

            if (profileSelected != null && (bool)profileSelected)
            {
                Globals.currentProfile = new Profile(Path.GetFileName(dialog.FileName));
                isProfileSaved = true;       
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

            isIedSaved = true;
            IEDConfig selectedIed = getSelectedIed();

            if (selectedIed == null)
            {
                iedName.Text = "Select a device to continue";
                connectIedInConf.IsEnabled = false;
                saveIED.IsEnabled = false;
                cloneIedBtn.IsEnabled = false;
                deleteIedBtn.IsEnabled = false;
                return;
            };

            saveIED.IsEnabled = true;
            cloneIedBtn.IsEnabled = true;
            deleteIedBtn.IsEnabled = true;
            connectIedInConf.IsEnabled = true;
            iedName.Text = selectedIed.name;
            iedNameInput.Text = selectedIed.name;
            iedIpInput.Text = selectedIed.ip;

            iedUsernameInput.Text = selectedIed.username;
            iedPasswordInput.Text = selectedIed.password;
            iedPortInput.Text = selectedIed.port.ToString();
            iedLogFolderInput.Text = selectedIed.logsFolder;

            iedLogExtensionsIncludedInput.Items.Clear();
            iedLogFoldersIncludedInput.Items.Clear();

            foreach (KeyValuePair<string,bool> extension in selectedIed.logEnabledExtensions)
            {
                CheckBox add = new CheckBox();
                add.Content = extension.Key;
                add.IsChecked = extension.Value;
                add.Tag = extension.Key;
                add.Checked += (object r, RoutedEventArgs args) => { isIedSaved = false; };
                iedLogExtensionsIncludedInput.Items.Add(add);
            }

            foreach (KeyValuePair<string, bool> folder in selectedIed.logEnabledFolders)
            {
                CheckBox add = new CheckBox();
                add.Content = folder.Key;
                add.IsChecked = folder.Value;
                add.Checked += (object r, RoutedEventArgs args) => { isIedSaved = false; };
                add.Tag = folder.Key;
                iedLogFoldersIncludedInput.Items.Add(add);
            }

            isIedSaved = true;
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

        private void cloneIed(object sender, RoutedEventArgs e)
        {
            // TODO
            IEDConfig selectedIED = getSelectedIed();

            if (selectedIED == null) return;
            IEDConfig cloned = new IEDConfig(selectedIED);
            Globals.currentProfile.IEDs.Add(cloned);
            ListBoxItem add = new ListBoxItem();
            add.Content = cloned.name;
            iedSelector.Items.Add(add);
            iedSelector.SelectedIndex = iedSelector.Items.Count - 1;
        }

        private IEDConfig getSelectedIed()
        {
            if (!iedSelector.IsEnabled) return null;
            if (Globals.currentProfile == null) return null;
            if (Globals.currentProfile.IEDs.Count <= iedSelector.SelectedIndex) return null;
            if (iedSelector.SelectedIndex < 0) return null;

            return Globals.currentProfile.IEDs[iedSelector.SelectedIndex];
        }

        private void saveIed(object sender, RoutedEventArgs e)
        {
            IEDConfig selectedIed = getSelectedIed();

            if (selectedIed == null) return;

            if (!checkPortInt())
            {
                MessageBox.Show("Invalid port", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                IPAddress.Parse(iedIpInput.Text);
            }
            catch
            {
                MessageBox.Show("Invalid IP address", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!validateLogsPath())
            {
                MessageBox.Show("Invalid logs path. The directory path is invalid or doesn't exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;            
            }

            ((ListBoxItem)(iedSelector.SelectedItem)).Content = iedNameInput.Text;
            selectedIed.name = iedNameInput.Text;
            selectedIed.ip = iedIpInput.Text;
            selectedIed.username = iedUsernameInput.Text;
            

            selectedIed.password = iedPasswordInput.Text;
            selectedIed.logsFolder = iedLogFolderInput.Text;
            
            //selectedIed.logEnabledExtensions.Clear();

            foreach (CheckBox extension in iedLogExtensionsIncludedInput.Items)
            {
                selectedIed.logEnabledExtensions[extension.Tag.ToString()] = (bool)extension.IsChecked;
            }

            selectedIed.logEnabledFolders.Clear();

            foreach (CheckBox folder in iedLogFoldersIncludedInput.Items)
            {
                selectedIed.logEnabledFolders[folder.Tag.ToString()] = (bool)folder.IsChecked;
            }

            isIedSaved = true;
            isProfileSaved = false;
        }

        private bool checkPortInt()
        {
            bool issue = false;
            try
            {
                int.Parse(iedPortInput.Text);
            }
            catch
            {
                issue = true;
            }

            if (!issue && int.Parse(iedPortInput.Text) > 65535)
            {
                issue = true;
            }

            return !issue;
        }

        private void checkPortInt(object sender, RoutedEventArgs e)
        {
            

            if (!checkPortInt())
            {
                MessageBox.Show("Invalid port number", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                iedPortInput.Text = "102";
            }
        }

        private void validateIedIP(object sender, RoutedEventArgs e)
        {
            try
            {
                IPAddress.Parse(iedIpInput.Text);
            } catch
            {
                MessageBox.Show("Invalid IP address", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                iedIpInput.Text = "127.0.0.1";
            }
            
        }

        private bool validateLogsPath()
        {
            try
            {
                if (Directory.Exists(iedLogFolderInput.Text))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void validateLogsPath(object sender, RoutedEventArgs e)
        {
           if (!validateLogsPath()) {
                MessageBox.Show("Invalid logs path", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                iedLogFolderInput.Text = ConfigFolder.extend(ConfigFolder.IEDLOGSROOT);
           }
            
        }

        private void updateNameLabel(object sender, TextChangedEventArgs e)
        {
            //iedFieldChanged(sender, e);
            isIedSaved = false;
        }

        private void iedFieldChanged(object sender, TextChangedEventArgs e)
        {
            isIedSaved = false;
        }

        private void saveIeds(object sender, RoutedEventArgs e)
        {
            isProfileSaved = true;
            Globals.currentProfile.save();
        }

        private void deleteProfile(object sender, RoutedEventArgs e)
        {
            if (Globals.currentProfile == null) return;

            MessageBoxResult res = MessageBox.Show("Do you really want to delete this profile? The action cannot be undone.", "Delete profile", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (res.Equals(MessageBoxResult.Yes))
            {
                if (Globals.currentProfile.delete())
                {
                    Globals.currentProfile = null;
                }
            }

        }

        private void handleClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (Globals.currentProfile == null)
            {
                if (File.Exists(ConfigFolder.extend(ConfigFolder.LASTPROFILEFILE)))
                {
                      File.Delete(ConfigFolder.extend(ConfigFolder.LASTPROFILEFILE));
                }
                return;
            }

            File.WriteAllText(ConfigFolder.extend(ConfigFolder.LASTPROFILEFILE), Globals.currentProfile.FilePath);

            if (!isProfileSaved || !isIedSaved)
            {
                MessageBoxResult res = MessageBox.Show("Do you want to save changes?", "Save changes", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                if (res.Equals(MessageBoxResult.Yes))
                {
                    saveIed(sender, null);
                    saveIeds(sender, null);
                }
                else if (res.Equals(MessageBoxResult.Cancel))
                {
                    e.Cancel = true;
                }   
            }
        }
    }
}
